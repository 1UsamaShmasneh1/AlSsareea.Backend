using AlSsareea.Modules.Identity.Domain;
using AlSsareea.Modules.Identity.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlSsareea.Modules.Identity.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", IdentityPersistenceConstants.Schema);
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id)
            .HasConversion(id => id.Value, value => new UserId(value))
            .ValueGeneratedNever();

        builder.Property(user => user.PhoneNumber)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(user => user.PreferredLanguage).IsRequired();
        builder.Property(user => user.Status).IsRequired();
        builder.Property(user => user.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(user => user.PhoneNumber);
    }
}
