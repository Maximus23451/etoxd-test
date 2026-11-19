using Eto;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
var builder=WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=3_000_000);
var runtime=Environment.GetEnvironmentVariable("APP_RUNTIME_DIR") ?? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath,"../.runtime"));
Directory.CreateDirectory(runtime);
builder.Configuration.AddJsonFile(Path.Combine(runtime,"config.json"),optional:true,reloadOnChange:false).AddEnvironmentVariables();
var connection=builder.Configuration.GetConnectionString("Portal") ?? throw new InvalidOperationException("Set ConnectionStrings__Portal or run npm run db:start.");
builder.Services.AddDbContext<PortalDb>(o=>o.UseSqlServer(connection,o=>o.EnableRetryOnFailure(3)));
builder.Services.AddHttpContextAccessor();builder.Services.AddScoped<PortalService>();
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(runtime,"keys")));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o=>{
    o.Cookie.Name="eto.session";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;
    o.ExpireTimeSpan=TimeSpan.FromHours(8);o.SlidingExpiration=false;
    o.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401;return c.Response.WriteAsJsonAsync(new{message="Jelentkezz be a folytatáshoz.",requestId=c.HttpContext.TraceIdentifier});};
    o.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403;return c.Response.WriteAsJsonAsync(new{message="Nincs jogosultságod.",requestId=c.HttpContext.TraceIdentifier});};
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options=>{options.RejectionStatusCode=429;options.AddPolicy("login",http=>RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString() ?? "local",_=>new FixedWindowRateLimiterOptions{PermitLimit=30,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
builder.Services.ConfigureHttpJsonOptions(o=>o.SerializerOptions.PropertyNamingPolicy=JsonNamingPolicy.CamelCase);
var app=builder.Build();
await using(var scope=app.Services.CreateAsyncScope()) {
    var db=scope.ServiceProvider.GetRequiredService<PortalDb>();
    if(args.Contains("--drop-test-db")) {
        var name=new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection).InitialCatalog;
        if(!name.StartsWith("EtoPortal_Test_",StringComparison.Ordinal))throw new InvalidOperationException("Refusing to delete a non-test database.");
        await db.Database.EnsureDeletedAsync();return;
    }
    await db.Database.EnsureCreatedAsync();
    if(app.Environment.IsDevelopment() || Environment.GetEnvironmentVariable("SEED_DEMO")=="1")await DemoSeed.Run(db);
}
app.Use(async(ctx,next)=>{
    ctx.Response.Headers["X-Content-Type-Options"]="nosniff";ctx.Response.Headers["Referrer-Policy"]="same-origin";
    if(ctx.Request.Path.StartsWithSegments("/api"))ctx.Response.Headers.CacheControl="no-store";
    try {
        if(ctx.Request.Method is not ("GET" or "HEAD" or "OPTIONS") && ctx.Request.Headers.TryGetValue("Origin",out var origin) && origin!=$"{ctx.Request.Scheme}://{ctx.Request.Host}")throw new ApiFault(403,"Idegen forrásból érkező kérés elutasítva.");
        await next();
    } catch(ApiFault e){ctx.Response.StatusCode=e.Status;await ctx.Response.WriteAsJsonAsync(new{message=e.Message,requestId=ctx.TraceIdentifier});}
    catch(BadHttpRequestException){ctx.Response.StatusCode=400;await ctx.Response.WriteAsJsonAsync(new{message="Hibás vagy túl nagy kérés.",requestId=ctx.TraceIdentifier});}
    catch(Exception e){app.Logger.LogError("API failure {Type} request {RequestId}",e.GetType().Name,ctx.TraceIdentifier);ctx.Response.StatusCode=500;await ctx.Response.WriteAsJsonAsync(new{message="Váratlan hiba történt. Próbáld újra.",requestId=ctx.TraceIdentifier});}
});
app.UseAuthentication();
app.Use(async(ctx,next)=>{
    var timer=Stopwatch.StartNew();
    try{await next();} catch(ApiFault e){ctx.Response.StatusCode=e.Status;throw;} catch {ctx.Response.StatusCode=500;throw;} finally {
        if(ctx.Request.Path.StartsWithSegments("/api") && !ctx.Request.Path.StartsWithSegments("/api/events") && !ctx.Request.Path.StartsWithSegments("/api/health")) {
            try {
                // Store route templates, never query strings, headers, form bodies or passwords.
                await using var scope=app.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<PortalDb>();
                Guid? actor=!ctx.Items.ContainsKey("anonymousReview") && Guid.TryParse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:null;
                db.Activities.Add(new Eto.Activity{ActorId=actor,Action="http.request",Target=(ctx.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "/api/unknown",RequestId=ctx.TraceIdentifier,Data=JsonSerializer.Serialize(new{method=ctx.Request.Method,status=ctx.Response.StatusCode,elapsedMs=timer.ElapsedMilliseconds})});await db.SaveChangesAsync();
            }catch(Exception e){app.Logger.LogError("Request audit unavailable: {Type}",e.GetType().Name);}
        }
    }
});
app.UseRateLimiter();app.UseAuthorization();
app.MapGet("/api/health",async(PortalDb db)=>await db.Database.CanConnectAsync()?Results.Ok(new{status="ok",stack="ASP.NET Core + SQL Server"}):Results.Json(new{status="unavailable"},statusCode:503));
app.MapPost("/api/login",async(LoginInput input,PortalDb db,PortalService s)=>{
    var key=(input.Talentx ?? "").Trim().ToUpperInvariant();
    var user=await db.People.SingleOrDefaultAsync(p=>p.Talentx==key);
    // Do a real password hash check even for unknown accounts.
    var candidate=user ?? new Person{PasswordHash=DummyPassword.Hash};
    var valid=PortalService.PasswordOk(candidate,input.Password ?? "");
    if(user is null || !valid || !user.Active){s.Audit("auth.failed",null,"login");await db.SaveChangesAsync();throw new ApiFault(401,"Hibás azonosító vagy jelszó, vagy a fiók nem aktív.");}
    await s.Http.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,user.Id.ToString())},CookieAuthenticationDefaults.AuthenticationScheme)));
    s.Audit("auth.login",user.Id);await db.SaveChangesAsync();return Results.Ok(new{ok=true});
}).RequireRateLimiting("login");
app.MapPost("/api/logout",async(PortalDb db,PortalService s)=>{var a=await s.Actor();await s.Http.SignOutAsync();s.Audit("auth.logout",a.Id);await db.SaveChangesAsync();return Results.Ok(new{ok=true});}).RequireAuthorization();
app.MapGet("/api/bootstrap",async(PortalService s)=>Results.Ok(await s.Bootstrap())).RequireAuthorization();
app.MapPost("/api/users",async(PersonInput input,PortalDb db,PortalService s)=>{
    var actor=await s.Require("manageAccounts");await s.EnsureAssignable(actor,input.RoleId);
    var name=(input.Name ?? "").Trim();var talentx=(input.Talentx ?? "").Trim().ToUpperInvariant();
    if(name.Length is <1 or >80 || !Regex.IsMatch(talentx,"^[A-Z0-9-]{3,40}$") || input.Password?.Length is not (>=8 and <=128))throw new ApiFault(422,"Név, egyedi azonosító és legalább 8 karakteres jelszó szükséges.");
    if(await db.People.AnyAsync(p=>p.Talentx==talentx))throw new ApiFault(409,"Ez az azonosító már használatban van.");
    var role=await db.Roles.SingleAsync(r=>r.Id==input.RoleId);var groups=input.GroupIds ?? [];
    if(role.Base is "player" or "coach")await s.CheckGroups(groups);else groups=[];
    var p=new Person{Name=name,Talentx=talentx,RoleId=role.Id};PortalService.SetPassword(p,input.Password!);db.People.Add(p);
    foreach(var g in groups)db.Memberships.Add(new Membership{PersonId=p.Id,GroupId=g});
    s.Audit("account.created",actor.Id,p.Id.ToString(),new{roleId=p.RoleId,groupIds=groups});await db.SaveChangesAsync();return Results.Created($"/api/users/{p.Id}",new{p.Id});
}).RequireAuthorization();
app.MapPut("/api/users/{id:guid}",async(Guid id,PersonUpdate input,PortalDb db,PortalService s)=>{
    var actor=await s.Require("manageAccounts");var target=await db.People.FindAsync(id) ?? throw new ApiFault(404,"A felhasználó nem található.");
    if(id==actor.Id)throw new ApiFault(403,"Saját jogosultságodat és hozzáférésedet itt nem módosíthatod.");
    await s.EnsureAssignable(actor,target.RoleId);await s.EnsureAssignable(actor,input.RoleId);
    var role=await db.Roles.SingleAsync(r=>r.Id==input.RoleId);
    var groupIds=input.GroupIds ?? await s.GroupIds(id);
    if(role.Base is "player" or "coach")await s.CheckGroups(groupIds);
    var before=new{target.RoleId,target.Active,groupIds=await s.GroupIds(id)};target.RoleId=input.RoleId;target.Active=input.Active;
    if(input.GroupIds is not null) {
        var links=await db.Memberships.Where(m=>m.PersonId==id).ToListAsync();
        db.Memberships.RemoveRange(links.Where(m=>!groupIds.Contains(m.GroupId)));
        foreach(var g in groupIds.Except(links.Select(m=>m.GroupId)))db.Memberships.Add(new Membership{PersonId=id,GroupId=g});
    }
    s.Audit("account.updated",actor.Id,id.ToString(),new{before,after=new{target.RoleId,target.Active}});await db.SaveChangesAsync();return Results.Ok(new{ok=true});
}).RequireAuthorization();
app.MapPost("/api/roles",async(RoleInput input,PortalDb db,PortalService s)=>{
    var actor=await s.Require("manageRoles");var name=(input.Name ?? "").Trim();
    if(name.Length is <1 or >60 || !new[]{"player","coach","admin"}.Contains(input.Base))throw new ApiFault(422,"Adj meg nevet és érvényes jogosultsági szintet.");
    if(await db.Roles.AnyAsync(r=>r.Name==name))throw new ApiFault(409,"Már létezik ilyen nevű szerepkör.");
    var role=new AppRole{Id="role-"+Guid.NewGuid(),Name=name,Base=input.Base,ManageMemberships=input.ManageMemberships};db.Roles.Add(role);
    s.Audit("role.created",actor.Id,role.Id,new{role.Base,role.ManageMemberships});await db.SaveChangesAsync();return Results.Created("/api/roles",role);
}).RequireAuthorization();
app.MapPut("/api/players/{id:guid}/memberships",async(Guid id,MembershipInput input,PortalDb db,PortalService s)=>{
    var actor=await s.Require("manageMemberships");var player=await db.People.FindAsync(id) ?? throw new ApiFault(404,"A játékos nem található.");
    if((await s.Role(player)).Base!="player")throw new ApiFault(422,"Csak játékos korosztálytagságát módosíthatod itt.");
    var before=await s.GroupIds(id);var requested=input.GroupIds ?? [];
    if(!new[]{"add","move","replace"}.Contains(input.Mode))throw new ApiFault(422,"Ismeretlen módosítási mód.");
    await s.CheckGroups(requested);
    int[] after;
    if(input.Mode=="add")after=before.Union(requested).ToArray();
    else if(input.Mode=="move") {
        if(input.FromGroupId is null || !before.Contains(input.FromGroupId.Value) || requested.Contains(input.FromGroupId.Value))throw new ApiFault(409,"Az áthelyezés kiinduló korosztálya érvénytelen. Frissítsd a névsort.");
        after=before.Where(g=>g!=input.FromGroupId).Union(requested).ToArray();
    } else after=requested;
    await s.CheckGroups(after);
    // Only the links change. Training, attendance and review history are retained.
    var links=await db.Memberships.Where(m=>m.PersonId==id).ToListAsync();
    db.Memberships.RemoveRange(links.Where(m=>!after.Contains(m.GroupId)));
    foreach(var g in after.Except(before))db.Memberships.Add(new Membership{PersonId=id,GroupId=g});
    s.Audit("membership."+input.Mode,actor.Id,id.ToString(),new{before,after});await db.SaveChangesAsync();return Results.Ok(new{groupIds=after});
}).RequireAuthorization();
app.MapPost("/api/trainings/{id:guid}/checkin",async(Guid id,PortalDb db,PortalService s)=>{
    var actor=await s.Actor();var t=await s.GetTraining(id);var now=DateTimeOffset.UtcNow;
    if((await s.Role(actor)).Base!="player" || !(await s.GroupIds(actor.Id)).Contains(t.GroupId))throw new ApiFault(403,"Ez nem a te csapatod edzése.");
    if(now<t.Start.AddMinutes(-15) || now>=t.End)throw new ApiFault(409,"A belépés még nem nyitott vagy az edzés már lezárult.");
    if(await db.Attendances.AnyAsync(a=>a.TrainingId==id && a.PersonId==actor.Id))throw new ApiFault(409,"Már rögzítetted a jelenléted.");
    db.Attendances.Add(new Attendance{PersonId=actor.Id,TrainingId=id,In=now});s.Audit("training.checkin",actor.Id,id.ToString());await db.SaveChangesAsync();return Results.Ok(new{ok=true});
}).RequireAuthorization();
app.MapPost("/api/trainings/{id:guid}/checkout",async(Guid id,PortalDb db,PortalService s)=>{
    var actor=await s.Actor();var rec=await db.Attendances.FindAsync(id,actor.Id) ?? throw new ApiFault(409,"Előbb lépj be az edzésre.");
    if(rec.Out is not null)throw new ApiFault(409,"Már kiléptél.");rec.Out=DateTimeOffset.UtcNow;s.Audit("training.checkout",actor.Id,id.ToString());await db.SaveChangesAsync();return Results.Ok(new{ok=true});
}).RequireAuthorization();
app.MapPost("/api/trainings/{id:guid}/reviews",async(Guid id,ReviewInput input,PortalDb db,PortalService s)=>{
    var actor=await s.Actor();var t=await s.GetTraining(id);var now=DateTimeOffset.UtcNow;
    if((await s.Role(actor)).Base!="player" || !await db.Attendances.AnyAsync(a=>a.TrainingId==id && a.PersonId==actor.Id))throw new ApiFault(403,"Csak a résztvevő játékos küldhet visszajelzést.");
    if(now<t.End || now>PortalService.Deadline(t))throw new ApiFault(409,"A visszajelzési időszak nem aktív.");
    if(new[]{input.Mood,input.Load,input.Self}.Any(v=>!double.IsFinite(v) || v<0 || v>4) || input.Comment?.Length>2000)throw new ApiFault(422,"A pontszám 0–4 között, a megjegyzés legfeljebb 2000 karakter lehet.");
    if(await db.Reviews.AnyAsync(r=>r.PersonId==actor.Id && r.TrainingId==id))throw new ApiFault(409,"Már elküldted a visszajelzést.");
    db.Reviews.Add(new Review{PersonId=actor.Id,TrainingId=id,Mood=input.Mood,Load=input.Load,Self=input.Self,Comment=input.Comment?.Trim() ?? "",Anonymous=input.Anonymous});
    if(input.Anonymous)s.Http.Items["anonymousReview"]=true;
    // Do not connect anonymous review submissions to an actor or training in the audit stream.
    s.Audit("review.submitted",input.Anonymous ? null : actor.Id,input.Anonymous ? "anonymous" : id.ToString(),new{input.Anonymous});await db.SaveChangesAsync();return Results.Ok(new{ok=true});
}).RequireAuthorization();
app.MapPost("/api/imports",ScheduleReader.Import).DisableAntiforgery().RequireAuthorization();
app.MapGet("/api/imports/template",()=>Results.File(System.Text.Encoding.UTF8.GetBytes("\uFEFFDátum;Kezdés;Vége;Csapat;Edzés;Helyszín;Edző\n2026-11-16;16:00;17:30;U17;Csapatedzés;Füves pálya 1;Nagy Péter\n"),"text/csv; charset=utf-8","heti-beosztas-minta.csv")).RequireAuthorization();
app.MapGet("/api/logs",async(string? action,long? before,PortalDb db,PortalService s)=>{
    await s.Require("viewLogs");var query=db.Activities.AsNoTracking().Where(a=>before==null || a.Id<before);
    if(!string.IsNullOrWhiteSpace(action))query=query.Where(a=>a.Action.Contains(action));
    var rows=await query.OrderByDescending(a=>a.Id).Take(100).ToListAsync();var names=await db.People.ToDictionaryAsync(p=>p.Id,p=>p.Name);
    return Results.Ok(rows.Select(a=>new{a.Id,a.At,a.Action,a.Target,a.Data,a.RequestId,a.Source,a.ActorId,Actor=a.ActorId is Guid id && names.TryGetValue(id,out var n)?n:"Rendszer / névtelen"}));
}).RequireAuthorization();
var clientEvents=new HashSet<string>{"nav.calendar","nav.players","nav.accounts","nav.roles","nav.logs","nav.lab","nav.appearance","calendar.previous","calendar.next","calendar.today","calendar.filter","training.open","dialog.close","action.failed","ui.error","theme.changed","membership.open","review.open","calendar.day"};
app.MapPost("/api/events",async(EventInput input,PortalDb db,PortalService s)=>{
    var actor=await s.Actor();if(!clientEvents.Contains(input.Action))throw new ApiFault(422,"Ismeretlen esemény.");
    var target=input.Target ?? "";if(target.Length>80 || !Regex.IsMatch(target,"^[a-zA-Z0-9-]*$"))throw new ApiFault(422,"Érvénytelen eseménycél.");
    s.Audit(input.Action,actor.Id,target,source:"client");await db.SaveChangesAsync();return Results.Ok(new{ok=true});
}).RequireAuthorization();
app.MapPut("/api/preferences",async(SettingsInput input,PortalDb db,PortalService s)=>{
    var actor=await s.Actor();if(!new[]{"classic","sand","slate","forest"}.Contains(input.Palette) || !new[]{"system","light","dark"}.Contains(input.Mode))throw new ApiFault(422,"Ismeretlen téma.");
    actor.Palette=input.Palette;actor.Mode=input.Mode;s.Audit("preferences.updated",actor.Id);await db.SaveChangesAsync();return Results.Ok(new{ok=true});
}).RequireAuthorization();
app.MapGet("/api/ideas",async(PortalDb db,PortalService s)=>{var a=await s.Actor();return Results.Ok(await db.Ideas.Where(i=>i.PersonId==a.Id).OrderByDescending(i=>i.At).ToListAsync());}).RequireAuthorization();
app.MapPost("/api/ideas",async(IdeaInput input,PortalDb db,PortalService s)=>{var a=await s.Actor();var text=(input.Text ?? "").Trim();if(text.Length is <1 or >500)throw new ApiFault(422,"Az ötlet 1–500 karakter lehet.");var idea=new Idea{PersonId=a.Id,Text=text};db.Ideas.Add(idea);s.Audit("idea.created",a.Id,idea.Id.ToString());await db.SaveChangesAsync();return Results.Ok(idea);}).RequireAuthorization();
app.MapDelete("/api/ideas/{id:guid}",async(Guid id,PortalDb db,PortalService s)=>{var a=await s.Actor();var idea=await db.Ideas.FindAsync(id) ?? throw new ApiFault(404,"Az ötlet nem található.");if(idea.PersonId!=a.Id)throw new ApiFault(403,"Csak saját ötleted törölheted.");db.Ideas.Remove(idea);s.Audit("idea.deleted",a.Id,id.ToString());await db.SaveChangesAsync();return Results.Ok(new{ok=true});}).RequireAuthorization();
app.MapPost("/api/diagnostics/{code:int}",async(int code,PortalDb db,PortalService s)=>{
    var a=await s.Require("viewLogs");if(!app.Environment.IsDevelopment() && Environment.GetEnvironmentVariable("DIAGNOSTICS")!="1")throw new ApiFault(404,"Az oldal nem található.");
    s.Audit("diagnostics.test",a.Id,code.ToString());await db.SaveChangesAsync();
    if(code==200){await Task.Delay(1800);return Results.Ok(new{ok=true});}
    if(code is 401 or 403 or 404 or 500)throw new ApiFault(code,"Tesztelt hibaállapot – az adatok nem változtak.");
    throw new ApiFault(422,"Ismeretlen teszt.");
}).RequireAuthorization();
app.MapFallback("/api/{**path}",()=>Results.NotFound(new{message="Az API végpont nem található."}));
var dist=Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath,"../dist"));
if(Directory.Exists(dist)){app.UseStaticFiles(new StaticFileOptions{FileProvider=new Microsoft.Extensions.FileProviders.PhysicalFileProvider(dist)});app.MapFallback(()=>Results.File(Path.Combine(dist,"index.html"),"text/html"));}
app.Run();
static class DummyPassword { public static readonly string Hash=new Microsoft.AspNetCore.Identity.PasswordHasher<Person>().HashPassword(new Person(),"unknown-account-dummy"); }
