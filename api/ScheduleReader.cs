using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using System.Globalization;
namespace Eto;
public static class ScheduleReader {
    static readonly TimeZoneInfo Zone=TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest");
    public static async Task<IResult> Import(HttpRequest request,PortalDb db,PortalService s) {
        var actor=await s.Require("importSchedule");
        if(!request.HasFormContentType)throw new ApiFault(422,"Válassz CSV vagy XLSX fájlt.");
        var form=await request.ReadFormAsync();var file=form.Files.GetFile("file");
        if(file is null || file.Length is <1 or >2_000_000)throw new ApiFault(422,"A fájl legfeljebb 2 MB lehet.");
        var extension=Path.GetExtension(file.FileName).ToLowerInvariant();
        if(extension is not (".xlsx" or ".csv"))throw new ApiFault(422,"Csak .xlsx és .csv fájl tölthető fel.");
        var rows=new List<string[]>();
        try {
            using var input=file.OpenReadStream();
            if(extension==".xlsx") {
                using var book=new XLWorkbook(input);var sheet=book.Worksheets.First();
                var used=sheet.RangeUsed();if(used is null)throw new ApiFault(422,"Üres munkalap.");
                if(used.RowCount()>1001 || used.ColumnCount()>20)throw new ApiFault(422,"Legfeljebb 1000 edzés és 20 oszlop tölthető fel.");
                foreach(var row in used.Rows())rows.Add(row.Cells(1,Math.Max(7,used.ColumnCount())).Select(cell=>cell.DataType==XLDataType.DateTime?cell.GetDateTime().ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture):cell.GetFormattedString(CultureInfo.InvariantCulture)).ToArray());
            } else {
                using var reader=new StreamReader(input);var text=await reader.ReadToEndAsync();
                using var parser=new TextFieldParser(new StringReader(text.TrimStart('\uFEFF'))){TextFieldType=FieldType.Delimited,HasFieldsEnclosedInQuotes=true};
                parser.SetDelimiters(text.Split('\n')[0].Contains(';')?";":",");
                while(!parser.EndOfData){rows.Add(parser.ReadFields() ?? []);if(rows.Count>1001)throw new ApiFault(422,"Legfeljebb 1000 edzés tölthető fel.");}
            }
        }catch(ApiFault){throw;}catch(Exception){throw new ApiFault(422,"A táblázat nem olvasható. Ellenőrizd a formátumot és az oszlopokat.");}
        if(rows.Count<2)throw new ApiFault(422,"A fejléc mellett legalább egy edzés szükséges.");
        var headers=rows[0].Select(x=>x.Trim().ToLowerInvariant()).ToArray();
        int Column(params string[] names)=>Array.FindIndex(headers,h=>names.Contains(h));
        int date=Column("dátum","date"),start=Column("kezdés","start"),end=Column("vége","end"),team=Column("csapat","korosztály","team"),title=Column("edzés","training"),location=Column("helyszín","location"),coach=Column("edző","coach");
        if(new[]{date,start,end,team,title,location}.Any(i=>i<0))throw new ApiFault(422,"Szükséges oszlopok: Dátum, Kezdés, Vége, Csapat, Edzés, Helyszín. Opcionális: Edző.");
        var groups=await db.Groups.ToListAsync();var people=await db.People.Where(p=>p.Active).ToListAsync();var coaches=await db.Roles.Where(r=>r.Base=="coach").Select(r=>r.Id).ToListAsync();var links=await db.Memberships.ToListAsync();
        var parsed=new List<Training>();var seen=new HashSet<string>();
        foreach(var pair in rows.Skip(1).Select((cells,i)=>(cells,line:i+2))) {
            var cells=pair.cells;string Cell(int index)=>index>=0 && index<cells.Length?cells[index].Trim():"";
            if(cells.All(string.IsNullOrWhiteSpace))continue;
            ApiFault Invalid(string reason)=>new(422,$"{pair.line}. sor: {reason} A feltöltés nem módosította a beosztást.");
            var rawDate=Cell(date).Split(' ')[0].TrimEnd('.');
            if(!DateOnly.TryParseExact(rawDate,new[]{"yyyy-MM-dd","yyyy.MM.dd","yyyy/MM/dd"},CultureInfo.InvariantCulture,DateTimeStyles.None,out var day))throw Invalid("érvénytelen dátum");
            TimeSpan? ParseTime(string raw) {raw=raw.Split(' ').Last();if(double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out var fraction) && fraction>=0 && fraction<1)return TimeSpan.FromDays(fraction);return TimeSpan.TryParse(raw,CultureInfo.InvariantCulture,out var ts) && ts>=TimeSpan.Zero && ts<TimeSpan.FromDays(1)?ts:null;}
            var from=ParseTime(Cell(start));var to=ParseTime(Cell(end));
            if(from is null || to is null || to<=from)throw Invalid("érvénytelen időpont vagy a vége nem későbbi a kezdésnél");
            var group=groups.FirstOrDefault(g=>g.Name.Equals(Cell(team),StringComparison.OrdinalIgnoreCase));if(group is null)throw Invalid("ismeretlen korosztály");
            if(string.IsNullOrWhiteSpace(Cell(title)) || Cell(title).Length>120 || string.IsNullOrWhiteSpace(Cell(location)) || Cell(location).Length>160)throw Invalid("adj meg edzésnevet és helyszínt");
            var coachName=Cell(coach);if(coachName=="")coachName=string.Join(", ",people.Where(p=>coaches.Contains(p.RoleId) && links.Any(m=>m.PersonId==p.Id && m.GroupId==group.Id)).Select(p=>p.Name));
            if(coachName.Length>160)throw Invalid("túl hosszú edzőnév");
            var fromDate=day.ToDateTime(TimeOnly.FromTimeSpan(from.Value));var toDate=day.ToDateTime(TimeOnly.FromTimeSpan(to.Value));
            if(Zone.IsInvalidTime(fromDate) || Zone.IsInvalidTime(toDate) || Zone.IsAmbiguousTime(fromDate) || Zone.IsAmbiguousTime(toDate))throw Invalid("óraátállítás miatti kétértelmű időpont");
            var t=new Training{GroupId=group.Id,Title=Cell(title),Location=Cell(location),Coach=coachName==""?"Nincs megadva":coachName,Start=new DateTimeOffset(fromDate,Zone.GetUtcOffset(fromDate)),End=new DateTimeOffset(toDate,Zone.GetUtcOffset(toDate)),Source="spreadsheet"};
            if(!seen.Add($"{t.GroupId}/{t.Start:O}"))throw Invalid("azonos korosztály és kezdés kétszer szerepel");parsed.Add(t);
        }
        if(parsed.Count==0)throw new ApiFault(422,"Nincs importálható edzés.");
        // Replace only untouched sessions on the supplied group/day pairs. Keep attendance history intact.
        var dates=parsed.Select(t=>(t.GroupId,Day:TimeZoneInfo.ConvertTime(t.Start,Zone).Date)).ToHashSet();
        var existing=await db.Trainings.ToListAsync();var touched=(await db.Attendances.Select(a=>a.TrainingId).ToListAsync()).Union(await db.Reviews.Select(r=>r.TrainingId).ToListAsync()).ToHashSet();
        var protectedTrainings=existing.Where(t=>touched.Contains(t.Id) && dates.Contains((t.GroupId,TimeZoneInfo.ConvertTime(t.Start,Zone).Date))).ToList();
        if(parsed.Any(t=>protectedTrainings.Any(p=>p.GroupId==t.GroupId && p.Start==t.Start)))throw new ApiFault(409,"A feltöltés egy már jelenléttel rendelkező edzést érint. A történet megőrzése miatt nem írható felül.");
        db.Trainings.RemoveRange(existing.Where(t=>!touched.Contains(t.Id) && dates.Contains((t.GroupId,TimeZoneInfo.ConvertTime(t.Start,Zone).Date))));db.Trainings.AddRange(parsed);
        var fileName=Path.GetFileName(file.FileName);if(fileName.Length>120)fileName=fileName[..120];
        db.Imports.Add(new ScheduleImport{FileName=fileName,Count=parsed.Count,ActorId=actor.Id});s.Audit("schedule.imported",actor.Id,"schedule",new{count=parsed.Count,format=extension});await db.SaveChangesAsync();return Results.Ok(new{count=parsed.Count,preserved=protectedTrainings.Count});
    }
}
