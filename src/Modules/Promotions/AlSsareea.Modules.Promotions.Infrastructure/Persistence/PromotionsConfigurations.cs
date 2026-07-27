using AlSsareea.Modules.Promotions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AlSsareea.Modules.Promotions.Infrastructure.Persistence;

internal sealed class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> b)
    {
        b.ToTable("promotions", PromotionsPersistence.Schema, table =>
        {
            table.HasCheckConstraint("ck_promotions_status", "status BETWEEN 1 AND 5");
            table.HasCheckConstraint("ck_promotions_type", "type BETWEEN 1 AND 6");
            table.HasCheckConstraint("ck_promotions_priority", "priority BETWEEN -100000 AND 100000");
            table.HasCheckConstraint("ck_promotions_validity", "ends_at_utc > starts_at_utc");
            table.HasCheckConstraint("ck_promotions_usage_limits", "(global_usage_limit IS NULL OR global_usage_limit > 0) AND (per_customer_usage_limit IS NULL OR per_customer_usage_limit > 0) AND (budget_limit_minor IS NULL OR budget_limit_minor > 0)");
            table.HasCheckConstraint("ck_promotions_benefit", "benefit_value >= 0 AND (maximum_discount_minor IS NULL OR maximum_discount_minor >= 0)");
            table.HasCheckConstraint("ck_promotions_funding", "platform_share_basis_points >= 0 AND merchant_share_basis_points >= 0 AND platform_share_basis_points + merchant_share_basis_points = 10000");
        });
        b.HasKey(x => x.Id).HasName("pk_promotions");
        b.Property(x => x.Id).HasConversion(x => x.Value, x => new PromotionId(x)).HasColumnType("uuid");
        b.Property(x => x.InternalName).HasMaxLength(100);
        b.Property(x => x.Type).HasConversion<short>().HasColumnType("smallint");
        b.Property(x => x.Status).HasConversion<short>().HasColumnType("smallint");
        b.Property(x => x.Stackability).HasConversion<short>().HasColumnType("smallint");
        b.Property(x => x.ConflictGroup).HasMaxLength(64);
        b.Property(x => x.CouponCode).HasConversion(
            x => x.HasValue ? x.Value.Value : null,
            x => x == null ? null : new CouponCode(x)).HasMaxLength(64).HasColumnName("normalized_coupon_code");
        b.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.ActivatedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.SuspendedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.ArchivedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.ConcurrencyStamp).HasColumnType("uuid").IsConcurrencyToken();
        ConfigureText(b.OwnsOne(x => x.DisplayName), "display_name");
        ConfigureText(b.OwnsOne(x => x.Description), "description");
        b.OwnsOne(x => x.Validity, owned =>
        {
            owned.Property(x => x.StartsAtUtc).HasColumnName("starts_at_utc").HasColumnType("timestamp with time zone");
            owned.Property(x => x.EndsAtUtc).HasColumnName("ends_at_utc").HasColumnType("timestamp with time zone");
            owned.HasIndex(x => new { x.StartsAtUtc, x.EndsAtUtc }).HasDatabaseName("ix_promotions_validity");
        });
        b.OwnsOne(x => x.UsageLimits, owned =>
        {
            owned.Property(x => x.GlobalLimit).HasColumnName("global_usage_limit");
            owned.Property(x => x.PerCustomerLimit).HasColumnName("per_customer_usage_limit");
            owned.Property(x => x.BudgetLimitMinor).HasColumnName("budget_limit_minor");
            owned.Property(x => x.MaximumRedemptionsPerOrder).HasColumnName("maximum_redemptions_per_order");
            owned.Ignore(x => x.IsUnlimited);
        });
        b.OwnsOne(x => x.Eligibility, owned =>
        {
            owned.Property(x => x.MinimumSubtotalMinor).HasColumnName("minimum_subtotal_minor");
            owned.Property(x => x.CustomerId).HasColumnName("eligible_customer_id").HasColumnType("uuid");
            owned.Property(x => x.FirstOrderOnly).HasColumnName("first_order_only");
        });
        b.OwnsOne(x => x.Funding, owned =>
        {
            owned.Property(x => x.Source).HasColumnName("funding_source").HasConversion<short>().HasColumnType("smallint");
            owned.Property(x => x.PlatformShareBasisPoints).HasColumnName("platform_share_basis_points");
            owned.Property(x => x.MerchantShareBasisPoints).HasColumnName("merchant_share_basis_points");
        });
        b.OwnsOne(x => x.Scope, owned =>
        {
            owned.Property(x => x.Type).HasColumnName("scope_type").HasConversion<short>().HasColumnType("smallint");
            owned.Property(x => x.MerchantId).HasColumnName("scope_merchant_id").HasColumnType("uuid");
            owned.Property(x => x.TargetIds).HasColumnName("scope_target_ids").HasColumnType("uuid[]");
            owned.HasIndex(x => x.MerchantId).HasDatabaseName("ix_promotions_scope_merchant_id");
            owned.HasIndex(x => x.TargetIds).HasMethod("gin").HasDatabaseName("ix_promotions_scope_target_ids");
        });
        b.OwnsOne(x => x.Benefit, owned =>
        {
            owned.Property(x => x.Kind).HasColumnName("benefit_kind").HasConversion<short>().HasColumnType("smallint");
            owned.Property(x => x.Currency).HasConversion(x => x.Value, x => new Currency(x)).HasColumnName("currency").HasMaxLength(3).IsFixedLength();
            owned.Property(x => x.Value).HasColumnName("benefit_value");
            owned.Property(x => x.MaximumDiscountMinor).HasColumnName("maximum_discount_minor");
        });
        b.HasIndex(x => x.InternalName).IsUnique().HasDatabaseName("ux_promotions_internal_name");
        b.HasIndex(x => x.CouponCode).IsUnique().HasFilter("normalized_coupon_code IS NOT NULL").HasDatabaseName("ux_promotions_normalized_coupon_code");
        b.HasIndex(x => x.Status).HasDatabaseName("ix_promotions_status");
        b.HasIndex(x => x.Type).HasDatabaseName("ix_promotions_type");
        b.HasIndex(x => x.Priority).HasDatabaseName("ix_promotions_priority");
        b.HasIndex(x => new { x.Status, x.Priority }).HasFilter("status = 2").HasDatabaseName("ix_promotions_active_priority");
        b.Ignore(x => x.DomainEvents);
    }

    private static void ConfigureText(OwnedNavigationBuilder<Promotion, LocalizedText> owned, string prefix)
    {
        owned.Property(x => x.Arabic).HasColumnName(prefix + "_ar").HasMaxLength(200);
        owned.Property(x => x.Hebrew).HasColumnName(prefix + "_he").HasMaxLength(200);
        owned.Property(x => x.English).HasColumnName(prefix + "_en").HasMaxLength(200);
    }
}

internal sealed class PromotionRedemptionConfiguration : IEntityTypeConfiguration<PromotionRedemption>
{
    public void Configure(EntityTypeBuilder<PromotionRedemption> b)
    {
        b.ToTable("promotion_redemptions", PromotionsPersistence.Schema, table =>
            table.HasCheckConstraint("ck_promotion_redemptions_amount", "discount_amount_minor >= 0"));
        b.HasKey(x => x.Id).HasName("pk_promotion_redemptions");
        b.Property(x => x.Id).HasConversion(x => x.Value, x => new PromotionRedemptionId(x)).HasColumnType("uuid");
        b.Property(x => x.PromotionId).HasConversion(x => x.Value, x => new PromotionId(x)).HasColumnType("uuid");
        b.Property(x => x.CustomerId).HasColumnType("uuid");
        b.Property(x => x.ExternalReference).HasMaxLength(128);
        b.Property(x => x.Currency).HasMaxLength(3).IsFixedLength();
        b.Property(x => x.OccurredAtUtc).HasColumnType("timestamp with time zone");
        b.HasIndex(x => x.ExternalReference).IsUnique().HasDatabaseName("ux_promotion_redemptions_external_reference");
        b.HasIndex(x => x.PromotionId).HasDatabaseName("ix_promotion_redemptions_promotion_id");
        b.HasIndex(x => x.CustomerId).HasDatabaseName("ix_promotion_redemptions_customer_id");
        b.HasOne<Promotion>().WithMany().HasForeignKey(x => x.PromotionId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.DomainEvents);
    }
}

internal sealed class PromotionAuditConfiguration : IEntityTypeConfiguration<PromotionAudit>
{
    public void Configure(EntityTypeBuilder<PromotionAudit> b)
    {
        b.ToTable("promotion_audit", PromotionsPersistence.Schema);
        b.HasKey(x => x.Id).HasName("pk_promotion_audit");
        b.Property(x => x.Id).HasConversion(x => x.Value, x => new PromotionAuditId(x)).HasColumnType("uuid");
        b.Property(x => x.PromotionId).HasConversion(x => x.Value, x => new PromotionId(x)).HasColumnType("uuid");
        b.Property(x => x.ActorUserId).HasColumnType("uuid");
        b.Property(x => x.Action).HasMaxLength(80);
        b.Property(x => x.OccurredAtUtc).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.PromotionId, x.OccurredAtUtc }).HasDatabaseName("ix_promotion_audit_promotion_occurred");
        b.HasOne<Promotion>().WithMany().HasForeignKey(x => x.PromotionId).OnDelete(DeleteBehavior.Restrict);
    }
}
