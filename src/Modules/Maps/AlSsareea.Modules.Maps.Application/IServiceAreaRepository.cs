using AlSsareea.Modules.Maps.Domain;

namespace AlSsareea.Modules.Maps.Application;

public interface IServiceAreaRepository
{
    Task<ServiceArea?> GetByIdAsync(
        ServiceAreaId id,
        CancellationToken cancellationToken = default);

    Task<bool> ContainsPointAsync(
        ServiceAreaId id,
        GeoPoint point,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServiceArea>> FindContainingAreasAsync(
        GeoPoint point,
        CancellationToken cancellationToken = default);

    Task AddAsync(ServiceArea serviceArea, CancellationToken cancellationToken = default);
}
