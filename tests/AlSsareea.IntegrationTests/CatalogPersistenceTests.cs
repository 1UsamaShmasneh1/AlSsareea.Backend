using System.Data.Common;
using AlSsareea.Modules.Catalog.Domain;
using AlSsareea.Modules.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CatalogAggregate = AlSsareea.Modules.Catalog.Domain.Catalog;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class CatalogPersistenceTests(PostgresFixture fixture)
{
    private static readonly DateTime Now = new(2026, 7, 26, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task MigrationCreatesOwnedSchemaTablesConstraintsAndPartialIndexes()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        CatalogDbContext db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await db.Database.OpenConnectionAsync();
        DbConnection connection = db.Database.GetDbConnection();

        Assert.Equal(17, await Scalar<long>(
            connection,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema='catalog'"));
        Assert.True(await Scalar<long>(
            connection,
            "SELECT count(*) FROM information_schema.check_constraints WHERE constraint_schema='catalog'") >= 8);
        Assert.Equal(1, await Scalar<long>(
            connection,
            "SELECT count(*) FROM pg_indexes WHERE schemaname='catalog' AND indexdef ILIKE '%WHERE%is_primary = true%'"));
        Assert.Equal(1, await Scalar<long>(
            connection,
            "SELECT count(*) FROM pg_indexes WHERE schemaname='catalog' AND indexdef ILIKE '%WHERE%is_default = true%'"));
        Assert.Equal(0, await Scalar<long>(
            connection,
            "SELECT count(*) FROM information_schema.constraint_column_usage WHERE table_schema IN ('merchants','identity') AND constraint_schema='catalog'"));
    }

    [Fact]
    public async Task ProductAggregateRoundTripsWithLocalizedChildrenAndStrongIds()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        CatalogDbContext db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        Guid merchantId = Guid.NewGuid();
        var catalog = CatalogAggregate.Create(
            CatalogId.New(), merchantId, "Catalog", null, "ar", Now);
        Product product = Product.Create(
            ProductId.New(), catalog.Id, merchantId, null, "CAT-ROUNDTRIP", 1200, "ILS", "food", 0, Now);
        product.SetTranslation("ar", "وجبة", "وصف", Now.AddMinutes(1));
        product.SetTranslation("en", "Meal", "Description", Now.AddMinutes(2));
        product.AddVariant("ar", "كبير", "CAT-ROUNDTRIP-L", 300, InventoryStatus.InStock, true, 0, Now.AddMinutes(3));
        OptionGroup group = product.AddOptionGroup("ar", "إضافات", SelectionType.MultipleChoice, false, 0, 2, 0, Now.AddMinutes(4));
        group.AddOption("ar", "جبنة", 100, false, true, 0, Now.AddMinutes(5));
        product.AddImage(null, "https://example.invalid/image", "وجبة", 0, true, Now.AddMinutes(6));
        product.AddAvailability(null, DayOfWeek.Sunday, new TimeOnly(8, 0), new TimeOnly(23, 0), "UTC", Now.AddMinutes(7));

        db.AddRange(catalog, product);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        Product loaded = await db.Products
            .Include(x => x.Translations)
            .Include(x => x.Variants).ThenInclude(x => x.Translations)
            .Include(x => x.OptionGroups).ThenInclude(x => x.Options).ThenInclude(x => x.Translations)
            .Include(x => x.Images)
            .Include(x => x.Availability)
            .SingleAsync(x => x.Id == product.Id);

        Assert.Equal(product.Id, loaded.Id);
        Assert.Equal(2, loaded.Translations.Count);
        Assert.Single(loaded.Variants);
        Assert.Single(Assert.Single(loaded.OptionGroups).Options);
        Assert.Single(loaded.Images);
        Assert.Single(loaded.Availability);
    }

    private static async Task<T> Scalar<T>(DbConnection connection, string sql)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        object result = await command.ExecuteScalarAsync() ??
            throw new InvalidOperationException("SQL scalar query returned null.");
        return (T)Convert.ChangeType(result, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }
}
