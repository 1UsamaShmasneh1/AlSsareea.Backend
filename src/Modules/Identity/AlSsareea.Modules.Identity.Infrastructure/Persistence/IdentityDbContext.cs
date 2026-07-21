using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Identity.Domain;
using AlSsareea.Modules.Identity.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<LoginSession> LoginSessions => Set<LoginSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordHistory> PasswordHistory => Set<PasswordHistory>();
    public DbSet<LoginHistory> LoginHistory => Set<LoginHistory>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<SecurityAuditRecord> SecurityAuditRecords => Set<SecurityAuditRecord>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceAppendOnlyHistory();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceAppendOnlyHistory();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(IdentityPersistenceConstants.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }

    private void EnforceAppendOnlyHistory()
    {
        bool changed = ChangeTracker.Entries()
            .Any(entry =>
                entry.Entity is global::AlSsareea.Modules.Identity.Domain.LoginHistory or global::AlSsareea.Modules.Identity.Domain.PasswordHistory or global::AlSsareea.Modules.Identity.Domain.SecurityAuditRecord &&
                entry.State is EntityState.Modified or EntityState.Deleted);
        if (changed)
        {
            throw new InvalidOperationException("Identity history records are append-only.");
        }
    }
}
