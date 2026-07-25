using AlSsareea.Modules.Maps.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlSsareea.Modules.Maps.Infrastructure.Persistence;

internal sealed class ServiceAreaConfiguration : IEntityTypeConfiguration<ServiceArea>
{
    public void Configure(EntityTypeBuilder<ServiceArea> builder)
    {
        builder.ToTable("service_areas", MapsDbContextSchema.Name);

        builder.HasKey(serviceArea => serviceArea.Id);

        builder.Property(serviceArea => serviceArea.Id)
            .HasConversion(id => id.Value, value => new ServiceAreaId(value))
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(serviceArea => serviceArea.Name)
            .HasColumnName("name")
            .HasMaxLength(ServiceArea.MaximumNameLength)
            .IsRequired();

        builder.Property(serviceArea => serviceArea.Description)
            .HasColumnName("description")
            .HasMaxLength(ServiceArea.MaximumDescriptionLength);

        builder.Property(serviceArea => serviceArea.Boundary)
            .HasColumnName("boundary")
            .HasColumnType("geometry(MultiPolygon,4326)")
            .IsRequired();

        builder.Property(serviceArea => serviceArea.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(serviceArea => serviceArea.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(serviceArea => serviceArea.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(serviceArea => serviceArea.Boundary)
            .HasDatabaseName("ix_service_areas_boundary")
            .HasMethod("gist");
    }
}
