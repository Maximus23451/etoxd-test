using Microsoft.EntityFrameworkCore;
namespace Eto;
public class PortalDb(DbContextOptions<PortalDb> options) : DbContext(options) {
    public DbSet<Person> People => Set<Person>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<Training> Trainings => Set<Training>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ScheduleImport> Imports => Set<ScheduleImport>();
    public DbSet<Idea> Ideas => Set<Idea>();
    protected override void OnModelCreating(ModelBuilder b) {
        b.Entity<Person>().HasIndex(x=>x.Talentx).IsUnique();
        b.Entity<AppRole>().HasIndex(x=>x.Name).IsUnique();
        b.Entity<Group>().HasIndex(x=>x.Name).IsUnique();
        b.Entity<Membership>().HasKey(x=>new{x.PersonId,x.GroupId});
        b.Entity<Membership>().HasOne<Person>().WithMany().HasForeignKey(x=>x.PersonId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Membership>().HasOne<Group>().WithMany().HasForeignKey(x=>x.GroupId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Person>().HasOne<AppRole>().WithMany().HasForeignKey(x=>x.RoleId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Training>().HasOne<Group>().WithMany().HasForeignKey(x=>x.GroupId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Attendance>().HasKey(x=>new{x.TrainingId,x.PersonId});
        b.Entity<Attendance>().HasOne<Training>().WithMany().HasForeignKey(x=>x.TrainingId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Attendance>().HasOne<Person>().WithMany().HasForeignKey(x=>x.PersonId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Review>().HasIndex(x=>new{x.TrainingId,x.PersonId}).IsUnique();
        b.Entity<Review>().HasOne<Training>().WithMany().HasForeignKey(x=>x.TrainingId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Review>().HasOne<Person>().WithMany().HasForeignKey(x=>x.PersonId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Activity>().HasIndex(x=>x.At);
        b.Entity<Activity>().HasIndex(x=>x.ActorId);
        b.Entity<Idea>().HasOne<Person>().WithMany().HasForeignKey(x=>x.PersonId).OnDelete(DeleteBehavior.Cascade);
    }
}
public class Person {
    public Guid Id {get;set;}=Guid.NewGuid();
    public string Name {get;set;}="";
    public string Talentx {get;set;}="";
    public string PasswordHash {get;set;}="";
    public string RoleId {get;set;}="player";
    public bool Active {get;set;}=true;
    public string Palette {get;set;}="classic";
    public string Mode {get;set;}="system";
}
public class AppRole {
    public string Id {get;set;}="";
    public string Name {get;set;}="";
    public string Base {get;set;}="player";
    public bool ManageMemberships {get;set;}
}
public class Group { public int Id {get;set;} public string Name {get;set;}=""; }
public class Membership { public Guid PersonId {get;set;} public int GroupId {get;set;} }
public class Training {
    public Guid Id {get;set;}=Guid.NewGuid(); public int GroupId {get;set;}
    public string Title {get;set;}=""; public string Location {get;set;}="";
    public string Coach {get;set;}=""; public DateTimeOffset Start {get;set;} public DateTimeOffset End {get;set;}
    public string Source {get;set;}="demo";
}
public class Attendance { public Guid TrainingId {get;set;} public Guid PersonId {get;set;} public DateTimeOffset In {get;set;} public DateTimeOffset? Out {get;set;} }
public class Review {
    public Guid Id {get;set;}=Guid.NewGuid(); public Guid TrainingId {get;set;} public Guid PersonId {get;set;}
    public double Mood {get;set;} public double Load {get;set;} public double Self {get;set;}
    public string Comment {get;set;}=""; public bool Anonymous {get;set;} public DateTimeOffset At {get;set;}=DateTimeOffset.UtcNow;
}
public class Activity {
    public long Id {get;set;} public DateTimeOffset At {get;set;}=DateTimeOffset.UtcNow;
    public Guid? ActorId {get;set;} public string Action {get;set;}=""; public string Target {get;set;}="";
    public string Data {get;set;}="{}"; public string RequestId {get;set;}=""; public string Source {get;set;}="server";
}
public class ScheduleImport {
    public Guid Id {get;set;}=Guid.NewGuid(); public string FileName {get;set;}="";
    public int Count {get;set;} public Guid ActorId {get;set;} public DateTimeOffset At {get;set;}=DateTimeOffset.UtcNow;
}
public class Idea { public Guid Id {get;set;}=Guid.NewGuid(); public Guid PersonId {get;set;} public string Text {get;set;}=""; public DateTimeOffset At {get;set;}=DateTimeOffset.UtcNow; }
public record LoginInput(string Talentx,string Password);
public record PersonInput(string Name,string Talentx,string Password,string RoleId,int[] GroupIds);
public record PersonUpdate(string RoleId,bool Active,int[]? GroupIds);
public record MembershipInput(int[] GroupIds,string Mode,int? FromGroupId);
public record RoleInput(string Name,string Base,bool ManageMemberships);
public record ReviewInput(double Mood,double Load,double Self,string? Comment,bool Anonymous);
public record SettingsInput(string Palette,string Mode);
public record IdeaInput(string Text);
public record EventInput(string Action,string? Target);
public class ApiFault(int status,string message) : Exception(message) { public int Status {get;}=status; }
