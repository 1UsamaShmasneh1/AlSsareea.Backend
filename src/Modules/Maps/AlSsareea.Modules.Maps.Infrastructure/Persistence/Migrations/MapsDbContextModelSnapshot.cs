using AlSsareea.Modules.Maps.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

namespace AlSsareea.Modules.Maps.Infrastructure.Persistence.Migrations;

[DbContext(typeof(MapsDbContext))]
internal sealed class MapsDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.4")
            .HasAnnotation("Relational:MaxIdentifierLength", 63)
            .HasPostgresExtension("postgis");

        modelBuilder.Entity("AlSsareea.Modules.Maps.Domain.ServiceArea", builder =>
        {
            builder.Property<ServiceAreaId>("Id")
                .HasConversion<Guid>()
                .HasColumnType("uuid")
                .HasColumnName("id");

            builder.Property<MultiPolygon>("Boundary")
                .IsRequired()
                .HasColumnType("geometry(MultiPolygon,4326)")
                .HasColumnName("boundary");

            builder.Property<DateTime>("CreatedAtUtc")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at_utc");

            builder.Property<string>("Description")
                .HasMaxLength(2000)
                .HasColumnType("character varying(2000)")
                .HasColumnName("description");

            builder.Property<bool>("IsActive")
                .HasColumnType("boolean")
                .HasColumnName("is_active");

            builder.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("character varying(200)")
                .HasColumnName("name");

            builder.Property<DateTime>("UpdatedAtUtc")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at_utc");

            builder.HasKey("Id");

            builder.HasIndex("Boundary")
                .HasDatabaseName("ix_service_areas_boundary")
                .HasMethod("gist");

            builder.ToTable("service_areas", "maps");
        });
    }
}
