using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace Eto;
public class PortalService(PortalDb db,IHttpContextAccessor accessor) {
    public HttpContext Http => accessor.HttpContext!;
    public async Task<Person> Actor() {
        if (!Guid.TryParse(Http.User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)) throw new ApiFault(401,"Jelentkezz be a folytatáshoz.");
        return await db.People.FirstOrDefaultAsync(p=>p.Id==id && p.Active) ?? throw new ApiFault(401,"A munkameneted lejárt vagy a fiókod le van tiltva.");
    }
    public async Task<AppRole> Role(Person p) => await db.Roles.SingleAsync(r=>r.Id==p.RoleId);
    public static string[] Permissions(AppRole r) => new[]{r.Base is "admin" or "manager" ? "viewAll" : "", r.Base is "admin" or "manager" || r.ManageMemberships ? "manageMemberships" : "",r.Base is "admin" or "manager" ? "manageAccounts" : "",r.Base is "admin" or "manager" ? "importSchedule" : "",r.Base=="manager" ? "manageRoles" : "",r.Base=="manager" ? "viewLogs" : ""}.Where(x=>x!="").ToArray();
    public async Task<Person> Require(string permission) {
        var actor=await Actor(); if(!Permissions(await Role(actor)).Contains(permission)) throw new ApiFault(403,"Ehhez a művelethez nincs jogosultságod."); return actor;
    }
    public async Task<int[]> GroupIds(Guid id)=>await db.Memberships.Where(m=>m.PersonId==id).Select(m=>m.GroupId).ToArrayAsync();
    public async Task<bool> Visible(Person actor,Training t) {
        var role=await Role(actor);
        return Permissions(role).Contains("viewAll") || (await GroupIds(actor.Id)).Contains(t.GroupId) || await db.Attendances.AnyAsync(a=>a.PersonId==actor.Id && a.TrainingId==t.Id);
    }
    public async Task<Training> GetTraining(Guid id) => await db.Trainings.FindAsync(id) ?? throw new ApiFault(404,"Az edzés nem található.");
    public void Audit(string action,Guid? actor,string target="",object? data=null,string source="server") {
        db.Activities.Add(new Activity {ActorId=actor,Action=action,Target=target,Data=JsonSerializer.Serialize(data ?? new{}),RequestId=Http.TraceIdentifier,Source=source});
    }
    public async Task EnsureAssignable(Person actor,string roleId) {
        var role=await db.Roles.FindAsync(roleId) ?? throw new ApiFault(422,"Ismeretlen szerepkör.");
        if((await Role(actor)).Base!="manager" && role.Base is not ("player" or "coach")) throw new ApiFault(403,"Adminfiókot és rendszergazdát csak rendszergazda kezelhet.");
    }
    public async Task CheckGroups(int[] ids) {
        if(ids.Length==0 || ids.Length>20 || ids.Distinct().Count()!=ids.Length || await db.Groups.CountAsync(g=>ids.Contains(g.Id))!=ids.Length) throw new ApiFault(422,"Válassz legalább egy érvényes, különböző korosztályt.");
    }
    public async Task<object> Bootstrap() {
        var actor=await Actor();var role=await Role(actor);var permissions=Permissions(role);var memberships=await db.Memberships.AsNoTracking().ToListAsync();
        var myGroups=memberships.Where(m=>m.PersonId==actor.Id).Select(m=>m.GroupId).ToArray();
        var history=await db.Attendances.Where(a=>a.PersonId==actor.Id).Select(a=>a.TrainingId).ToArrayAsync();
        var trainings=await db.Trainings.Where(t=>permissions.Contains("viewAll") || myGroups.Contains(t.GroupId) || history.Contains(t.Id)).OrderBy(t=>t.Start).ToListAsync();
        var tids=trainings.Select(t=>t.Id).ToArray();var users=await db.People.AsNoTracking().ToListAsync();var roles=await db.Roles.AsNoTracking().ToListAsync();
        var staff=role.Base!="player";
        var relevantGroups=trainings.Select(t=>t.GroupId).ToArray();
        var historicalPeople=await db.Attendances.Where(a=>tids.Contains(a.TrainingId)).Select(a=>a.PersonId).Distinct().ToArrayAsync();
        var visiblePeople=permissions.Contains("manageAccounts") || permissions.Contains("manageMemberships") ? users : staff ? users.Where(u=>historicalPeople.Contains(u.Id) || memberships.Any(m=>m.PersonId==u.Id && relevantGroups.Contains(m.GroupId))) : users.Where(u=>u.Id==actor.Id || u.Active && memberships.Any(m=>m.PersonId==u.Id && myGroups.Contains(m.GroupId)));
        var reviews=await db.Reviews.Where(r=>tids.Contains(r.TrainingId) && (staff || r.PersonId==actor.Id)).OrderByDescending(r=>r.At).ToListAsync();
        return new {
            user=new{actor.Id,actor.Name,actor.Talentx,actor.RoleId,role.Base,RoleName=role.Name,GroupIds=myGroups,actor.Palette,actor.Mode}, permissions,
            groups=await db.Groups.OrderBy(g=>g.Name).ToListAsync(),
            users=visiblePeople.Select(u=>new{u.Id,u.Name,u.Talentx,u.RoleId,Base=roles.Single(r=>r.Id==u.RoleId).Base,RoleName=roles.Single(r=>r.Id==u.RoleId).Name,u.Active,GroupIds=memberships.Where(m=>m.PersonId==u.Id).Select(m=>m.GroupId)}),
            roles=permissions.Contains("manageAccounts") ? roles : [], trainings,
            attendance=await db.Attendances.Where(a=>tids.Contains(a.TrainingId) && (staff || a.PersonId==actor.Id)).ToListAsync(),
            reviews=reviews.Select(r=>new{r.Id,r.TrainingId,PersonId=r.Anonymous && r.PersonId!=actor.Id ? (Guid?)null : r.PersonId,r.Mood,r.Load,r.Self,r.Comment,r.Anonymous,r.At}),
            imports=await db.Imports.OrderByDescending(i=>i.At).Take(10).ToListAsync(),
            diagnostics=Http.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment() || Environment.GetEnvironmentVariable("DIAGNOSTICS")=="1"
        };
    }
    public static DateTimeOffset Deadline(Training t) {
        var zone=TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest"); var end=TimeZoneInfo.ConvertTime(t.End,zone);
        var at=end.Date.AddHours(22);var deadline=new DateTimeOffset(at,zone.GetUtcOffset(at)); return deadline-t.End<TimeSpan.FromHours(1)?t.End.AddHours(1):deadline;
    }
    public static bool PasswordOk(Person p,string password)=>new PasswordHasher<Person>().VerifyHashedPassword(p,p.PasswordHash,password)!=PasswordVerificationResult.Failed;
    public static void SetPassword(Person p,string password)=>p.PasswordHash=new PasswordHasher<Person>().HashPassword(p,password);
}
