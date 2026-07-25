namespace AlSsareea.Modules.Maps.Contracts;

public sealed record RouteRequest(
    Coordinates Origin,
    Coordinates Destination,
    IReadOnlyList<Coordinates>? Waypoints = null);

public sealed record RouteResult(
    long DistanceMeters,
    TimeSpan EstimatedDuration,
    IReadOnlyList<Coordinates> Path);

public interface IRoutingProvider
{
    Task<RouteResult> CalculateRouteAsync(
        RouteRequest request,
        CancellationToken cancellationToken = default);
}
