using System.Data.Common;
using AlSsareea.Modules.Media.Domain;
using AlSsareea.Modules.Media.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class MediaPersistenceTests(PostgresFixture fixture)
{
    private static readonly DateTime Now = new(2026, 7, 26, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task MigrationCreatesOnlyMediaOwnedTablesAndForeignKeys()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        MediaDbContext db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        await db.Database.OpenConnectionAsync();
        DbConnection connection = db.Database.GetDbConnection();

        Assert.Equal(3, await Scalar<long>(connection, "SELECT count(*) FROM information_schema.tables WHERE table_schema='media'"));
        Assert.Equal(0, await Scalar<long>(connection, "SELECT count(*) FROM information_schema.table_constraints WHERE constraint_schema='media' AND constraint_type='FOREIGN KEY' AND table_name='media_assets'"));
        Assert.Equal(1, await Scalar<long>(connection, "SELECT count(*) FROM information_schema.table_constraints WHERE constraint_schema='media' AND constraint_type='FOREIGN KEY' AND table_name='media_variants'"));
        Assert.Equal(0, await Scalar<long>(connection, "SELECT count(*) FROM information_schema.constraint_column_usage WHERE table_schema <> 'media' AND constraint_schema='media'"));
    }

    [Fact]
    public async Task ReadyAssetAndVariantsRoundTripWithStrongIds()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        MediaDbContext db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        MediaAsset asset = MediaAsset.Create(
            MediaAssetId.New(), Guid.NewGuid(), "CatalogProduct", Guid.NewGuid(), "meal.png",
            $"original/{Guid.NewGuid():N}.png", "image/png", ".png", 256, new string('b', 64),
            100, 100, MediaAccessLevel.Public, "local", Now);
        asset.StartProcessing(Now.AddMinutes(1));
        asset.AddOrReplaceVariant(MediaVariantType.Thumbnail, $"variants/{Guid.NewGuid():N}.webp", "image/webp", 100, 80, 80, Now.AddMinutes(2));
        asset.MarkReady(Now.AddMinutes(3));

        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        MediaAsset loaded = await db.Assets.Include(x => x.Variants).SingleAsync(x => x.Id == asset.Id);
        Assert.Equal(MediaAssetStatus.Ready, loaded.Status);
        Assert.Single(loaded.Variants);
    }

    private static async Task<T> Scalar<T>(DbConnection connection, string sql)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        object result = await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("SQL scalar query returned null.");
        return (T)Convert.ChangeType(result, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }
}
