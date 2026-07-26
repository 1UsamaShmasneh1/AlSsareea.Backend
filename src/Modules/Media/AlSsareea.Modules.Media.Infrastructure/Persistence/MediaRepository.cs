using AlSsareea.Modules.Media.Application;
using AlSsareea.Modules.Media.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Media.Infrastructure.Persistence;

internal sealed class MediaAssetRepository(MediaDbContext db) : IMediaAssetRepository
{
    public Task<MediaAsset?> GetAsync(MediaAssetId id, bool tracked = true, CancellationToken ct = default)
    {
        IQueryable<MediaAsset> query = db.Assets.Include(x => x.Variants);
        if (!tracked) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.Id == id, ct);
    }
    public async Task AddAsync(MediaAsset asset, CancellationToken ct = default) => await db.Assets.AddAsync(asset, ct);
}
