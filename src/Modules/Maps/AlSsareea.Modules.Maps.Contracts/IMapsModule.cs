namespace AlSsareea.Modules.Maps.Contracts;

public sealed record ServiceAreaDetails(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public interface IMapsModule
{
    Task<ServiceAreaDetails?> GetServiceAreaAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<bool> ContainsPointAsync(
        Guid serviceAreaId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServiceAreaDetails>> FindContainingAreasAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);
}
