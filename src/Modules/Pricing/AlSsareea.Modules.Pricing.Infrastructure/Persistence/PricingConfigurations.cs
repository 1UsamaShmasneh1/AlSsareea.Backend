using AlSsareea.Modules.Pricing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlSsareea.Modules.Pricing.Infrastructure.Persistence;

internal sealed class PricingPolicyConfiguration : IEntityTypeConfiguration<PricingPolicy>
{
    public void Configure(EntityTypeBuilder<PricingPolicy> builder)
    {
        builder.ToTable("pricing_policies", PricingPersistence.Schema, table =>
        {
            table.HasCheckConstraint("ck_pricing_policies_status", "status BETWEEN 1 AND 4");
            table.HasCheckConstraint("ck_pricing_policies_scope_type", "scope_type BETWEEN 1 AND 4");
            table.HasCheckConstraint("ck_pricing_policies_priority", "priority BETWEEN 0 AND 1000");
            table.HasCheckConstraint("ck_pricing_policies_version", "version >= 1");
            table.HasCheckConstraint("ck_pricing_policies_period", "effective_until_utc IS NULL OR effective_until_utc > effective_from_utc");
            table.HasCheckConstraint("ck_pricing_policies_scope", "(scope_type = 1 AND merchant_id IS NULL AND branch_id IS NULL AND zone_id IS NULL) OR (scope_type = 2 AND merchant_id IS NULL AND branch_id IS NULL AND zone_id IS NOT NULL) OR (scope_type = 3 AND merchant_id IS NOT NULL AND branch_id IS NULL AND zone_id IS NULL) OR (scope_type = 4 AND merchant_id IS NOT NULL AND branch_id IS NOT NULL AND zone_id IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(x => x.Value, x => new PricingPolicyId(x)).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.ScopeType).HasConversion<short>();
        builder.Property(x => x.MerchantId).HasColumnType("uuid");
        builder.Property(x => x.BranchId).HasColumnType("uuid");
        builder.Property(x => x.ZoneId).HasColumnType("uuid");
        builder.Property(x => x.ScopeKey).HasMaxLength(120);
        builder.Property(x => x.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(x => x.Status).HasConversion<short>();
        builder.Property(x => x.EffectiveFromUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.EffectiveUntilUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ActivatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeactivatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ArchivedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
        builder.HasIndex(x => new { x.ScopeKey, x.Currency, x.Status, x.Priority, x.EffectiveFromUtc });
        builder.HasIndex(x => new { x.Status, x.Currency, x.EffectiveFromUtc, x.EffectiveUntilUtc });
        builder.HasIndex(x => new { x.MerchantId, x.BranchId });
        builder.HasMany(x => x.Rules).WithOne().HasForeignKey("pricing_policy_id").IsRequired().OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(x => x.Scope);
        builder.Ignore(x => x.DomainEvents);
    }
}

internal sealed class PricingRuleConfiguration : IEntityTypeConfiguration<PricingRule>
{
    public void Configure(EntityTypeBuilder<PricingRule> builder)
    {
        builder.ToTable("pricing_rules", PricingPersistence.Schema, table =>
        {
            table.HasCheckConstraint("ck_pricing_rules_type", "type BETWEEN 1 AND 8");
            table.HasCheckConstraint("ck_pricing_rules_kind", "kind BETWEEN 0 AND 2");
            table.HasCheckConstraint("ck_pricing_rules_base", "calculation_base BETWEEN 1 AND 3");
            table.HasCheckConstraint("ck_pricing_rules_priority", "priority BETWEEN 0 AND 1000");
            table.HasCheckConstraint("ck_pricing_rules_amount", "amount_minor >= 0");
            table.HasCheckConstraint("ck_pricing_rules_percentage", "percentage_basis_points BETWEEN 0 AND 10000");
            table.HasCheckConstraint("ck_pricing_rules_money_limits", "(threshold_minor IS NULL OR threshold_minor >= 0) AND (minimum_minor IS NULL OR minimum_minor >= 0) AND (maximum_minor IS NULL OR maximum_minor >= 0) AND (minimum_minor IS NULL OR maximum_minor IS NULL OR minimum_minor <= maximum_minor)");
            table.HasCheckConstraint("ck_pricing_rules_distance", "(included_distance_meters IS NULL OR included_distance_meters >= 0) AND (maximum_distance_meters IS NULL OR maximum_distance_meters > 0) AND (additional_fee_per_kilometer_minor IS NULL OR additional_fee_per_kilometer_minor >= 0)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(x => x.Value, x => new PricingRuleId(x)).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(x => x.Type).HasConversion<short>();
        builder.Property(x => x.Kind).HasConversion<short>();
        builder.Property(x => x.CalculationBase).HasConversion<short>();
        builder.HasIndex("pricing_policy_id", nameof(PricingRule.Type), nameof(PricingRule.Priority));
    }
}
