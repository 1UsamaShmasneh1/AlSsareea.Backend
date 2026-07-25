using AlSsareea.Modules.Maps.Application;
using AlSsareea.Modules.Maps.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Maps.Infrastructure.Persistence;

internal sealed class ServiceAreaRepository(MapsDbContext dbContext) : IServiceAreaRepository
{
    public Task<ServiceArea?> GetByIdAsync(
        ServiceAreaId id,
        CancellationToken cancellationToken = default) =>
        dbContext.ServiceAreas
            .AsNoTracking()
            .SingleOrDefaultAsync(serviceArea => serviceArea.Id == id, cancellationToken);

    public Task<bool> ContainsPointAsync(
        ServiceAreaId id,
        GeoPoint point,
        CancellationToken cancellationToken = default) =>
        dbContext.ServiceAreas.AnyAsync(
            serviceArea =>
                serviceArea.Id == id
                && serviceArea.IsActive
                && serviceArea.Boundary.Covers(point.ToPoint()),
            cancellationToken);

    public async Task<IReadOnlyList<ServiceArea>> FindContainingAreasAsync(
        GeoPoint point,
        CancellationToken cancellationToken = default) =>
        await dbContext.ServiceAreas
            .AsNoTracking()
            .Where(serviceArea =>
                serviceArea.IsActive
                && serviceArea.Boundary.Covers(point.ToPoint()))
            .OrderBy(serviceArea => serviceArea.Name)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(
        ServiceArea serviceArea,
        CancellationToken cancellationToken = default)
    {
        await dbContext.ServiceAreas.AddAsync(serviceArea, cancellationToken);
    }
}
