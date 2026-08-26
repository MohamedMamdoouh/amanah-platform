using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();

    public DbSet<OtpSmsOutboxMessage> OtpSmsOutboxMessages => Set<OtpSmsOutboxMessage>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<CategoryFieldDefinition> CategoryFieldDefinitions => Set<CategoryFieldDefinition>();

    public DbSet<Governorate> Governorates => Set<Governorate>();

    public DbSet<Report> Reports => Set<Report>();

    public DbSet<CategoryField> CategoryFields => Set<CategoryField>();

    public DbSet<ReportPhoto> ReportPhotos => Set<ReportPhoto>();

    public DbSet<Claim> Claims => Set<Claim>();

    public DbSet<Resolution> Resolutions => Set<Resolution>();

    public DbSet<ChatThread> ChatThreads => Set<ChatThread>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<AbuseReport> AbuseReports => Set<AbuseReport>();

    public DbSet<ModerationAction> ModerationActions => Set<ModerationAction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        AssignGuids();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AssignGuids();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void AssignGuids()
    {
        foreach (var entry in ChangeTracker.Entries<IEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.Id == Guid.Empty)
            {
                entry.Entity.Id = Guid.NewGuid();
            }
        }
    }
}
