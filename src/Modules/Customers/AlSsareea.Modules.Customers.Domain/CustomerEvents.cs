using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Customers.Domain;

public abstract record CustomerDomainEvent(DateTime OccurredAtUtc) : IDomainEvent;
public sealed record CustomerCreatedDomainEvent(CustomerId CustomerId, DateTime OccurredAtUtc) : CustomerDomainEvent(OccurredAtUtc);
public sealed record CustomerProfileUpdatedDomainEvent(CustomerId CustomerId, DateTime OccurredAtUtc) : CustomerDomainEvent(OccurredAtUtc);
public sealed record CustomerStatusChangedDomainEvent(CustomerId CustomerId, CustomerStatus PreviousStatus, CustomerStatus NewStatus, DateTime OccurredAtUtc) : CustomerDomainEvent(OccurredAtUtc);
public sealed record CustomerAddressAddedDomainEvent(CustomerId CustomerId, CustomerAddressId AddressId, DateTime OccurredAtUtc) : CustomerDomainEvent(OccurredAtUtc);
public sealed record CustomerAddressUpdatedDomainEvent(CustomerId CustomerId, CustomerAddressId AddressId, DateTime OccurredAtUtc) : CustomerDomainEvent(OccurredAtUtc);
public sealed record CustomerAddressDeletedDomainEvent(CustomerId CustomerId, CustomerAddressId AddressId, DateTime OccurredAtUtc) : CustomerDomainEvent(OccurredAtUtc);
public sealed record CustomerDefaultAddressChangedDomainEvent(CustomerId CustomerId, CustomerAddressId? AddressId, DateTime OccurredAtUtc) : CustomerDomainEvent(OccurredAtUtc);
public sealed record CustomerPreferencesUpdatedDomainEvent(CustomerId CustomerId, DateTime OccurredAtUtc) : CustomerDomainEvent(OccurredAtUtc);
