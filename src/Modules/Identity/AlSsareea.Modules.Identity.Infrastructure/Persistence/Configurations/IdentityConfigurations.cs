using AlSsareea.Modules.Identity.Domain;
using AlSsareea.Modules.Identity.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlSsareea.Modules.Identity.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    internal static PropertyBuilder<TId> IdentityId<TId>(this PropertyBuilder<TId> property, Func<TId, Guid> toGuid, Func<Guid, TId> fromGuid) where TId : struct =>
        property.HasConversion(value => toGuid(value), value => fromGuid(value)).HasColumnType("uuid").ValueGeneratedNever();
    internal static PropertyBuilder<DateTime> Utc(this PropertyBuilder<DateTime> property) => property.HasColumnType("timestamp with time zone");
    internal static PropertyBuilder<DateTime?> Utc(this PropertyBuilder<DateTime?> property) => property.HasColumnType("timestamp with time zone");
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users", IdentityPersistenceConstants.Schema, t =>
        {
            t.HasCheckConstraint("ck_users_contact_required", "normalized_email IS NOT NULL OR normalized_phone_number IS NOT NULL");
            t.HasCheckConstraint("ck_users_user_type", "user_type BETWEEN 1 AND 9");
            t.HasCheckConstraint("ck_users_status", "status BETWEEN 1 AND 6");
        });
        b.HasKey(x => x.Id).HasName("pk_users");
        b.Property(x => x.Id).IdentityId(x => x.Value, x => new UserId(x));
        b.Property(x => x.UserType).HasConversion<short>().HasColumnType("smallint");
        b.Property(x => x.Status).HasConversion<short>().HasColumnType("smallint");
        b.Property(x => x.Email).HasConversion(x => x.HasValue ? x.Value.Value : null, x => x == null ? null : new Email(x)).HasMaxLength(Email.MaximumLength);
        b.Property(x => x.NormalizedEmail).HasMaxLength(Email.MaximumLength);
        b.Property(x => x.PhoneNumber).HasConversion(x => x.HasValue ? x.Value.Value : null, x => x == null ? null : new PhoneNumber(x)).HasMaxLength(16);
        b.Property(x => x.NormalizedPhoneNumber).HasMaxLength(16);
        b.Property(x => x.PasswordHash).HasConversion(x => x.Value, x => new PasswordHash(x)).HasMaxLength(PasswordHash.MaximumLength).IsRequired();
        b.Property(x => x.SecurityStamp).HasColumnType("uuid");
        b.Property(x => x.ConcurrencyStamp).HasColumnType("uuid").IsConcurrencyToken();
        b.Property(x => x.LockoutEndUtc).Utc(); b.Property(x => x.LastPasswordChangedUtc).Utc(); b.Property(x => x.CreatedUtc).Utc(); b.Property(x => x.UpdatedUtc).Utc(); b.Property(x => x.DeletedUtc).Utc();
        b.Property(x => x.CreatedByUserId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new UserId(x.Value) : null).HasColumnType("uuid");
        b.Property(x => x.UpdatedByUserId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new UserId(x.Value) : null).HasColumnType("uuid");
        b.Property(x => x.DeletedByUserId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new UserId(x.Value) : null).HasColumnType("uuid");
        b.HasIndex(x => x.NormalizedEmail).IsUnique().HasDatabaseName("ux_users_normalized_email");
        b.HasIndex(x => x.NormalizedPhoneNumber).IsUnique().HasDatabaseName("ux_users_normalized_phone_number");
        b.HasIndex(x => x.Status).HasDatabaseName("ix_users_status"); b.HasIndex(x => x.UserType).HasDatabaseName("ix_users_user_type"); b.HasIndex(x => x.CreatedUtc).HasDatabaseName("ix_users_created_utc"); b.HasIndex(x => x.DeletedUtc).HasDatabaseName("ix_users_deleted_utc");
        b.HasQueryFilter(x => x.DeletedUtc == null);
        b.Ignore(x => x.DomainEvents);
        b.HasMany(x => x.Roles).WithOne().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_user_roles_users_user_id");
        b.HasMany(x => x.Devices).WithOne().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_devices_users_user_id");
        b.HasMany(x => x.Sessions).WithOne().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_login_sessions_users_user_id");
        b.HasMany(x => x.RefreshTokens).WithOne().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_refresh_tokens_users_user_id");
        b.HasMany(x => x.PasswordHistory).WithOne().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_password_history_users_user_id");
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_users_users_created_by_user_id");
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_users_users_updated_by_user_id");
        b.HasOne<User>().WithMany().HasForeignKey(x => x.DeletedByUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_users_users_deleted_by_user_id");
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("roles", IdentityPersistenceConstants.Schema); b.HasKey(x => x.Id).HasName("pk_roles"); b.Property(x => x.Id).IdentityId(x => x.Value, x => new RoleId(x));
        b.Property(x => x.Name).HasMaxLength(100).IsRequired(); b.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired(); b.Property(x => x.Description).HasMaxLength(500); b.Property(x => x.CreatedUtc).Utc(); b.Property(x => x.UpdatedUtc).Utc(); b.Property(x => x.ConcurrencyStamp).HasColumnType("uuid").IsConcurrencyToken();
        b.HasIndex(x => x.NormalizedName).IsUnique().HasDatabaseName("ux_roles_normalized_name"); b.HasIndex(x => x.IsActive).HasDatabaseName("ix_roles_is_active"); b.Ignore(x => x.DomainEvents);
        b.HasMany(x => x.Permissions).WithOne().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_role_permissions_roles_role_id");
    }
}

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("permissions", IdentityPersistenceConstants.Schema, t => t.HasCheckConstraint("ck_permissions_name_format", "normalized_name = lower(normalized_name) AND normalized_name ~ '^[a-z0-9]+(\\.[a-z0-9]+){2,}$'"));
        b.HasKey(x => x.Id).HasName("pk_permissions"); b.Property(x => x.Id).IdentityId(x => x.Value, x => new PermissionId(x));
        b.Property(x => x.Name).HasMaxLength(150).IsRequired(); b.Property(x => x.NormalizedName).HasMaxLength(150).IsRequired(); b.Property(x => x.DisplayName).HasMaxLength(150).IsRequired(); b.Property(x => x.Description).HasMaxLength(500); b.Property(x => x.Module).HasMaxLength(50).IsRequired(); b.Property(x => x.CreatedUtc).Utc(); b.Property(x => x.UpdatedUtc).Utc(); b.Property(x => x.ConcurrencyStamp).HasColumnType("uuid").IsConcurrencyToken();
        b.HasIndex(x => x.NormalizedName).IsUnique().HasDatabaseName("ux_permissions_normalized_name"); b.HasIndex(x => x.Module).HasDatabaseName("ix_permissions_module"); b.HasIndex(x => x.IsActive).HasDatabaseName("ix_permissions_is_active"); b.Ignore(x => x.DomainEvents);
    }
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("user_roles", IdentityPersistenceConstants.Schema); b.HasKey(x => new { x.UserId, x.RoleId }).HasName("pk_user_roles"); b.Property(x => x.UserId).IdentityId(x => x.Value, x => new UserId(x)); b.Property(x => x.RoleId).IdentityId(x => x.Value, x => new RoleId(x)); b.Property(x => x.AssignedByUserId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new UserId(x.Value) : null).HasColumnType("uuid"); b.Property(x => x.AssignedUtc).Utc(); b.HasIndex(x => x.RoleId).HasDatabaseName("ix_user_roles_role_id");
        b.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_user_roles_roles_role_id"); b.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedByUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_user_roles_users_assigned_by_user_id");
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("role_permissions", IdentityPersistenceConstants.Schema); b.HasKey(x => new { x.RoleId, x.PermissionId }).HasName("pk_role_permissions"); b.Property(x => x.RoleId).IdentityId(x => x.Value, x => new RoleId(x)); b.Property(x => x.PermissionId).IdentityId(x => x.Value, x => new PermissionId(x)); b.Property(x => x.AssignedByUserId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new UserId(x.Value) : null).HasColumnType("uuid"); b.Property(x => x.AssignedUtc).Utc(); b.HasIndex(x => x.PermissionId).HasDatabaseName("ix_role_permissions_permission_id");
        b.HasOne<Permission>().WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_role_permissions_permissions_permission_id"); b.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedByUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_role_permissions_users_assigned_by_user_id");
    }
}

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> b)
    {
        b.ToTable("devices", IdentityPersistenceConstants.Schema, t => t.HasCheckConstraint("ck_devices_platform", "platform BETWEEN 1 AND 6")); b.HasKey(x => x.Id).HasName("pk_devices"); b.Property(x => x.Id).IdentityId(x => x.Value, x => new DeviceId(x)); b.Property(x => x.UserId).IdentityId(x => x.Value, x => new UserId(x)); b.Property(x => x.DeviceIdentifier).HasConversion(x => x.Value, x => new DeviceIdentifier(x)).HasMaxLength(DeviceIdentifier.MaximumLength); b.Property(x => x.Platform).HasConversion<short>().HasColumnType("smallint"); b.Property(x => x.DeviceName).HasMaxLength(150); b.Property(x => x.AppVersion).HasMaxLength(50); b.Property(x => x.OperatingSystemVersion).HasMaxLength(100); b.Property(x => x.LastSeenUtc).Utc(); b.Property(x => x.CreatedUtc).Utc(); b.Property(x => x.UpdatedUtc).Utc(); b.Property(x => x.RevokedUtc).Utc(); b.Property(x => x.ConcurrencyStamp).HasColumnType("uuid").IsConcurrencyToken();
        b.HasIndex(x => new { x.UserId, x.DeviceIdentifier }).IsUnique().HasDatabaseName("ux_devices_user_id_device_identifier"); b.HasIndex(x => x.UserId).HasDatabaseName("ix_devices_user_id"); b.HasIndex(x => x.LastSeenUtc).HasDatabaseName("ix_devices_last_seen_utc"); b.HasIndex(x => x.IsRevoked).HasDatabaseName("ix_devices_is_revoked");
    }
}

internal sealed class LoginSessionConfiguration : IEntityTypeConfiguration<LoginSession>
{
    public void Configure(EntityTypeBuilder<LoginSession> b)
    {
        b.ToTable("login_sessions", IdentityPersistenceConstants.Schema, t =>
        {
            t.HasCheckConstraint("ck_login_sessions_state", "state BETWEEN 1 AND 4"); t.HasCheckConstraint("ck_login_sessions_expiry", "expires_utc > started_utc"); t.HasCheckConstraint("ck_login_sessions_activity", "last_activity_utc >= started_utc"); t.HasCheckConstraint("ck_login_sessions_end", "ended_utc IS NULL OR ended_utc >= started_utc");
        });
        b.HasKey(x => x.Id).HasName("pk_login_sessions"); b.Property(x => x.Id).IdentityId(x => x.Value, x => new LoginSessionId(x)); b.Property(x => x.UserId).IdentityId(x => x.Value, x => new UserId(x)); b.Property(x => x.DeviceId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new DeviceId(x.Value) : null).HasColumnType("uuid"); b.Property(x => x.RefreshTokenId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new RefreshTokenId(x.Value) : null).HasColumnType("uuid"); b.Property(x => x.State).HasConversion<short>().HasColumnType("smallint"); b.Property(x => x.EndReason).HasConversion<short?>().HasColumnType("smallint"); b.Property(x => x.StartedUtc).Utc(); b.Property(x => x.ExpiresUtc).Utc(); b.Property(x => x.LastActivityUtc).Utc(); b.Property(x => x.EndedUtc).Utc(); b.Property(x => x.CreatedUtc).Utc(); b.Property(x => x.UpdatedUtc).Utc(); b.Property(x => x.IpAddress).HasColumnType("inet"); b.Property(x => x.UserAgent).HasMaxLength(1024);
        b.HasIndex(x => x.UserId).HasDatabaseName("ix_login_sessions_user_id"); b.HasIndex(x => x.DeviceId).HasDatabaseName("ix_login_sessions_device_id"); b.HasIndex(x => x.RefreshTokenId).HasDatabaseName("ix_login_sessions_refresh_token_id"); b.HasIndex(x => x.State).HasDatabaseName("ix_login_sessions_state"); b.HasIndex(x => x.LastActivityUtc).HasDatabaseName("ix_login_sessions_last_activity_utc"); b.HasIndex(x => x.ExpiresUtc).HasDatabaseName("ix_login_sessions_expires_utc");
        b.HasOne<Device>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_login_sessions_devices_device_id");
        b.HasOne<RefreshToken>().WithMany().HasForeignKey(x => x.RefreshTokenId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_login_sessions_refresh_tokens_refresh_token_id");
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens", IdentityPersistenceConstants.Schema, t => { t.HasCheckConstraint("ck_refresh_tokens_expiry", "expires_utc > created_utc"); t.HasCheckConstraint("ck_refresh_tokens_not_self_replaced", "replaced_by_token_id IS NULL OR replaced_by_token_id <> id"); });
        b.HasKey(x => x.Id).HasName("pk_refresh_tokens"); b.Property(x => x.Id).IdentityId(x => x.Value, x => new RefreshTokenId(x)); b.Property(x => x.UserId).IdentityId(x => x.Value, x => new UserId(x)); b.Property(x => x.DeviceId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new DeviceId(x.Value) : null).HasColumnType("uuid"); b.Property(x => x.LoginSessionId).IdentityId(x => x.Value, x => new LoginSessionId(x)); b.Property(x => x.TokenHash).HasConversion(x => x.Value, x => new RefreshTokenHash(x)).HasMaxLength(64).IsFixedLength(); b.Property(x => x.SecurityStampSnapshot).HasColumnType("uuid"); b.Property(x => x.CreatedUtc).Utc(); b.Property(x => x.ExpiresUtc).Utc(); b.Property(x => x.ConsumedUtc).Utc(); b.Property(x => x.RevokedUtc).Utc(); b.Property(x => x.RevocationReason).HasMaxLength(250); b.Property(x => x.ReplacedByTokenId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new RefreshTokenId(x.Value) : null).HasColumnType("uuid"); b.Property(x => x.CreatedByIpAddress).HasColumnType("inet"); b.Property(x => x.RevokedByIpAddress).HasColumnType("inet");
        b.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("ux_refresh_tokens_token_hash"); b.HasIndex(x => x.UserId).HasDatabaseName("ix_refresh_tokens_user_id"); b.HasIndex(x => x.DeviceId).HasDatabaseName("ix_refresh_tokens_device_id"); b.HasIndex(x => x.LoginSessionId).HasDatabaseName("ix_refresh_tokens_login_session_id"); b.HasIndex(x => x.ExpiresUtc).HasDatabaseName("ix_refresh_tokens_expires_utc"); b.HasIndex(x => x.RevokedUtc).HasDatabaseName("ix_refresh_tokens_revoked_utc"); b.HasIndex(x => x.ReplacedByTokenId).HasDatabaseName("ix_refresh_tokens_replaced_by_token_id");
        b.HasOne<Device>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_refresh_tokens_devices_device_id"); b.HasOne<LoginSession>().WithMany().HasForeignKey(x => x.LoginSessionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_refresh_tokens_login_sessions_login_session_id"); b.HasOne<RefreshToken>().WithMany().HasForeignKey(x => x.ReplacedByTokenId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_refresh_tokens_refresh_tokens_replaced_by_token_id");
    }
}

internal sealed class PasswordHistoryConfiguration : IEntityTypeConfiguration<PasswordHistory>
{
    public void Configure(EntityTypeBuilder<PasswordHistory> b)
    {
        b.ToTable("password_history", IdentityPersistenceConstants.Schema, t => t.HasCheckConstraint("ck_password_history_replaced", "replaced_utc IS NULL OR replaced_utc >= became_active_utc")); b.HasKey(x => x.Id).HasName("pk_password_history"); b.Property(x => x.Id).IdentityId(x => x.Value, x => new PasswordHistoryId(x)); b.Property(x => x.UserId).IdentityId(x => x.Value, x => new UserId(x)); b.Property(x => x.PasswordHash).HasConversion(x => x.Value, x => new PasswordHash(x)).HasMaxLength(PasswordHash.MaximumLength); b.Property(x => x.BecameActiveUtc).Utc(); b.Property(x => x.ReplacedUtc).Utc(); b.Property(x => x.CreatedUtc).Utc(); b.HasIndex(x => x.UserId).HasDatabaseName("ix_password_history_user_id"); b.HasIndex(x => new { x.UserId, x.BecameActiveUtc }).HasDatabaseName("ix_password_history_user_id_became_active_utc");
    }
}

internal sealed class LoginHistoryConfiguration : IEntityTypeConfiguration<LoginHistory>
{
    public void Configure(EntityTypeBuilder<LoginHistory> b)
    {
        b.ToTable("login_history", IdentityPersistenceConstants.Schema, t => { t.HasCheckConstraint("ck_login_history_result", "result BETWEEN 1 AND 2"); t.HasCheckConstraint("ck_login_history_consistency", "(result = 1 AND failure_reason IS NULL) OR (result = 2 AND failure_reason IS NOT NULL AND login_session_id IS NULL)"); }); b.HasKey(x => x.Id).HasName("pk_login_history"); b.Property(x => x.Id).IdentityId(x => x.Value, x => new LoginHistoryId(x)); b.Property(x => x.UserId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new UserId(x.Value) : null).HasColumnType("uuid"); b.Property(x => x.DeviceId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new DeviceId(x.Value) : null).HasColumnType("uuid"); b.Property(x => x.LoginSessionId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new LoginSessionId(x.Value) : null).HasColumnType("uuid"); b.Property(x => x.AttemptedIdentifierHash).HasMaxLength(64).IsFixedLength(); b.Property(x => x.Result).HasConversion<short>().HasColumnType("smallint"); b.Property(x => x.FailureReason).HasConversion<short?>().HasColumnType("smallint"); b.Property(x => x.IpAddress).HasColumnType("inet"); b.Property(x => x.UserAgent).HasMaxLength(1024); b.Property(x => x.OccurredUtc).Utc(); b.Property(x => x.CorrelationId).HasMaxLength(128);
        b.HasIndex(x => x.UserId).HasDatabaseName("ix_login_history_user_id"); b.HasIndex(x => x.DeviceId).HasDatabaseName("ix_login_history_device_id"); b.HasIndex(x => x.LoginSessionId).HasDatabaseName("ix_login_history_login_session_id"); b.HasIndex(x => x.OccurredUtc).HasDatabaseName("ix_login_history_occurred_utc"); b.HasIndex(x => x.Result).HasDatabaseName("ix_login_history_result"); b.HasIndex(x => x.AttemptedIdentifierHash).HasDatabaseName("ix_login_history_attempted_identifier_hash"); b.HasIndex(x => new { x.UserId, x.OccurredUtc }).HasDatabaseName("ix_login_history_user_id_occurred_utc");
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_login_history_users_user_id"); b.HasOne<Device>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_login_history_devices_device_id"); b.HasOne<LoginSession>().WithMany().HasForeignKey(x => x.LoginSessionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_login_history_login_sessions_login_session_id");
    }
}
