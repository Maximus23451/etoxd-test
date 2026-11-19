using Microsoft.EntityFrameworkCore;
namespace Eto;
public static class DemoSeed {
    public static async Task Run(PortalDb db) {
        if(await db.People.AnyAsync())return;
        db.Roles.AddRange(new AppRole{Id="player",Name="Játékos",Base="player"},new AppRole{Id="coach",Name="Edző",Base="coach"},new AppRole{Id="admin",Name="Admin",Base="admin",ManageMemberships=true},new AppRole{Id="manager",Name="Rendszergazda",Base="manager",ManageMemberships=true});
        var groups=new[]{new Group{Name="U15"},new Group{Name="U17"},new Group{Name="U19"}};db.Groups.AddRange(groups);await db.SaveChangesAsync();
        Person Person(string id,string name,string role,params string[] teams){var p=new Person{Talentx=id,Name=name,RoleId=role};PortalService.SetPassword(p,"eto1904");db.People.Add(p);foreach(var team in teams)db.Memberships.Add(new Membership{PersonId=p.Id,GroupId=groups.Single(g=>g.Name==team).Id});return p;}
        var bence=Person("TX-1001","Kiss Bence","player","U17");Person("TX-1002","Tóth Máté","player","U17");Person("TX-1003","Horváth Levente","player","U17");Person("TX-1101","Balogh Kristóf","player","U19");Person("TX-1501","Kovács Józsi","player","U15");
        Person("TX-2001","Nagy Péter","coach","U17");Person("TX-2002","Szabó Anna","coach","U19");Person("TX-2003","Varga Tamás","coach","U15");Person("TX-9001","Kovács Eszter","admin");Person("TX-0001","ETO Rendszergazda","manager");
        var zone=TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest");var today=TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,zone).Date;
        foreach(var group in groups) foreach(var offset in Enumerable.Range(-1,8)) {
            var start=today.AddDays(offset).AddHours(group.Name=="U15"?15:group.Name=="U17"?16:18);
            db.Trainings.Add(new Training{GroupId=group.Id,Title=offset%2==0?"Csapatedzés":"Technikai edzés",Location=group.Name=="U15"?"Műfüves pálya":"Füves pálya 1",Coach=group.Name=="U15"?"Varga Tamás":group.Name=="U17"?"Nagy Péter":"Szabó Anna",Start=new DateTimeOffset(start,zone.GetUtcOffset(start)),End=new DateTimeOffset(start.AddMinutes(90),zone.GetUtcOffset(start.AddMinutes(90)))});
        }
        var now=DateTimeOffset.UtcNow;
        var live=new Training{GroupId=groups.Single(g=>g.Name=="U17").Id,Title="Aktuális demó edzés",Location="Füves pálya 2",Coach="Nagy Péter",Start=now.AddMinutes(-10),End=now.AddMinutes(60)};
        var ended=new Training{GroupId=live.GroupId,Title="Visszajelzésre váró edzés",Location="Műfüves pálya",Coach=live.Coach,Start=now.AddMinutes(-100),End=now.AddMinutes(-10)};
        db.Trainings.AddRange(live,ended);db.Attendances.Add(new Attendance{PersonId=bence.Id,TrainingId=ended.Id,In=ended.Start,Out=ended.End});
        db.Activities.Add(new Activity{Action="system.demo_seed",Target="portal",Data="{}"});await db.SaveChangesAsync();
    }
}
