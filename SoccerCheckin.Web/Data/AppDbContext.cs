using Microsoft.EntityFrameworkCore;
using SoccerCheckin.Web.Models;

namespace SoccerCheckin.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<Models.Program> Programs => Set<Models.Program>();
    public DbSet<ProgramUser> ProgramUsers => Set<ProgramUser>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<ProgramInvite> ProgramInvites => Set<ProgramInvite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserSession>()
            .HasIndex(u => u.MicrosoftEmail)
            .IsUnique()
            .HasFilter("\"MicrosoftEmail\" IS NOT NULL");

        modelBuilder.Entity<ProgramUser>()
            .HasIndex(pu => new { pu.ProgramId, pu.UserSessionId })
            .IsUnique();

        modelBuilder.Entity<ProgramUser>()
            .HasOne(pu => pu.Program)
            .WithMany(p => p.ProgramUsers)
            .HasForeignKey(pu => pu.ProgramId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProgramUser>()
            .HasOne(pu => pu.UserSession)
            .WithMany(u => u.ProgramUsers)
            .HasForeignKey(pu => pu.UserSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Event>()
            .HasOne(e => e.Program)
            .WithMany(p => p.Events)
            .HasForeignKey(e => e.ProgramId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Player>()
            .HasOne(p => p.Program)
            .WithMany(prog => prog.Players)
            .HasForeignKey(p => p.ProgramId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Player>()
            .HasOne(p => p.OwnerUserSession)
            .WithMany()
            .HasForeignKey(p => p.OwnerUserSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Attendance>()
            .HasIndex(a => new { a.EventId, a.PlayerId })
            .IsUnique();

        modelBuilder.Entity<Attendance>()
            .HasOne(a => a.Event)
            .WithMany(e => e.Attendances)
            .HasForeignKey(a => a.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Attendance>()
            .HasOne(a => a.Player)
            .WithMany(p => p.Attendances)
            .HasForeignKey(a => a.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProgramInvite>()
            .HasIndex(i => i.Token)
            .IsUnique();

        modelBuilder.Entity<ProgramInvite>()
            .HasOne(i => i.Program)
            .WithMany()
            .HasForeignKey(i => i.ProgramId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
