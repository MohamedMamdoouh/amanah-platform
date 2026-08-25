using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();

    public DbSet<OtpSmsOutboxMessage> OtpSmsOutboxMessages => Set<OtpSmsOutboxMessage>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

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
