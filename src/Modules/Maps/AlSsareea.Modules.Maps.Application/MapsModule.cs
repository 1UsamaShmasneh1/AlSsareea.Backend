using AlSsareea.Modules.Maps.Contracts;
using AlSsareea.Modules.Maps.Domain;

namespace AlSsareea.Modules.Maps.Application;

public sealed class MapsModule(IServiceAreaRepository serviceAreas) : IMapsModule
{
    public async Task<ServiceAreaDetails?> GetServiceAreaAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        ServiceArea? serviceArea = await serviceAreas.GetByIdAsync(
            new ServiceAreaId(id),
            cancellationToken);

        return serviceArea is null ? null : ToDetails(serviceArea);
    }

    public Task<bool> ContainsPointAsync(
        Guid serviceAreaId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        GeoPoint point = GeoPoint.Create(latitude, longitude);
        return serviceAreas.ContainsPointAsync(
            new ServiceAreaId(serviceAreaId),
            point,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceAreaDetails>> FindContainingAreasAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        GeoPoint point = GeoPoint.Create(latitude, longitude);
        IReadOnlyList<ServiceArea> serviceAreasContainingPoint =
            await serviceAreas.FindContainingAreasAsync(point, cancellationToken);

        return serviceAreasContainingPoint.Select(ToDetails).ToArray();
    }

    private static ServiceAreaDetails ToDetails(ServiceArea serviceArea) =>
        new(
            serviceArea.Id.Value,
            serviceArea.Name,
            serviceArea.Description,
            serviceArea.IsActive,
            serviceArea.CreatedAtUtc,
            serviceArea.UpdatedAtUtc);
}
