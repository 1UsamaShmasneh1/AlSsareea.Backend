using AlSsareea.Modules.Media.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlSsareea.Modules.Media.Infrastructure.Persistence;

internal sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> b)
    {
        b.ToTable("media_assets", MediaPersistence.Schema, t =>
        {
            t.HasCheckConstraint("ck_media_assets_size", "size_in_bytes > 0");
            t.HasCheckConstraint("ck_media_assets_dimensions", "width > 0 AND height > 0");
            t.HasCheckConstraint("ck_media_assets_status", "status BETWEEN 1 AND 5");
            t.HasCheckConstraint("ck_media_assets_access", "access_level BETWEEN 1 AND 3");
            t.HasCheckConstraint("ck_media_assets_hash", "length(content_hash) = 64");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasConversion(x => x.Value, x => new MediaAssetId(x)).HasColumnType("uuid").ValueGeneratedNever();
        b.Property(x => x.MerchantId).HasColumnType("uuid"); b.Property(x => x.OwnerId).HasColumnType("uuid"); b.Property(x => x.OwnerType).HasMaxLength(100);
        b.Property(x => x.OriginalFileName).HasMaxLength(255); b.Property(x => x.StorageKey).HasMaxLength(500); b.Property(x => x.MimeType).HasMaxLength(100);
        b.Property(x => x.FileExtension).HasMaxLength(10); b.Property(x => x.ContentHash).HasMaxLength(64).IsFixedLength(); b.Property(x => x.StorageProvider).HasMaxLength(50);
        b.Property(x => x.FailureReason).HasMaxLength(1000); b.Property(x => x.Status).HasConversion<short>(); b.Property(x => x.AccessLevel).HasConversion<short>();
        b.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone"); b.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone"); b.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.ConcurrencyStamp).IsConcurrencyToken(); b.HasIndex(x => new { x.OwnerType, x.OwnerId }); b.HasIndex(x => new { x.MerchantId, x.Status });
        b.HasIndex(x => x.ContentHash); b.HasIndex(x => x.CreatedAtUtc); b.HasIndex(x => x.DeletedAtUtc); b.HasIndex(x => x.StorageKey).IsUnique();
        b.HasMany(x => x.Variants).WithOne().HasForeignKey(x => x.MediaAssetId).OnDelete(DeleteBehavior.Restrict); b.Ignore(x => x.DomainEvents);
    }
}
internal sealed class MediaVariantConfiguration : IEntityTypeConfiguration<MediaVariant>
{
    public void Configure(EntityTypeBuilder<MediaVariant> b)
    {
        b.ToTable("media_variants", MediaPersistence.Schema, t => { t.HasCheckConstraint("ck_media_variants_size", "size_in_bytes > 0"); t.HasCheckConstraint("ck_media_variants_dimensions", "width > 0 AND height > 0"); });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasConversion(x => x.Value, x => new MediaVariantId(x)).HasColumnType("uuid").ValueGeneratedNever();
        b.Property(x => x.MediaAssetId).HasConversion(x => x.Value, x => new MediaAssetId(x)).HasColumnType("uuid").ValueGeneratedNever();
        b.Property(x => x.Type).HasConversion<short>(); b.Property(x => x.Status).HasConversion<short>(); b.Property(x => x.StorageKey).HasMaxLength(500); b.Property(x => x.MimeType).HasMaxLength(100);
        b.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone"); b.HasIndex(x => x.StorageKey).IsUnique(); b.HasIndex(x => new { x.MediaAssetId, x.Type }).IsUnique();
    }
}
