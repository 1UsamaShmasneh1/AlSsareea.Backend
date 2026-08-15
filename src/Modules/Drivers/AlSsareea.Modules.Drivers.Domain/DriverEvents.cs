using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Drivers.Domain;

public sealed record DriverCreatedDomainEvent(Guid DriverId, Guid UserId, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record DriverActivationChangedDomainEvent(Guid DriverId, DriverActivationStatus Status, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record DriverAvailabilityChangedDomainEvent(Guid DriverId, AvailabilityStatus Previous, AvailabilityStatus Current, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record DriverSuspendedDomainEvent(Guid DriverId, Guid SuspensionId, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record DriverSuspensionLiftedDomainEvent(Guid DriverId, Guid SuspensionId, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record DriverVehicleChangedDomainEvent(Guid DriverId, Guid VehicleId, DateTime OccurredAtUtc) : IDomainEvent;
