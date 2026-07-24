using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Customers.Domain;

public readonly record struct CustomerId
{
    public CustomerId(Guid value) => Value = value != Guid.Empty ? value : throw new DomainException("Customer id is required.");
    public Guid Value { get; }
    public static CustomerId New() => new(Guid.NewGuid());
}

public readonly record struct CustomerAddressId
{
    public CustomerAddressId(Guid value) => Value = value != Guid.Empty ? value : throw new DomainException("Address id is required.");
    public Guid Value { get; }
    public static CustomerAddressId New() => new(Guid.NewGuid());
}

public readonly record struct CustomerPreferenceId
{
    public CustomerPreferenceId(Guid value) => Value = value != Guid.Empty ? value : throw new DomainException("Preference id is required.");
    public Guid Value { get; }
    public static CustomerPreferenceId New() => new(Guid.NewGuid());
}

public readonly record struct CustomerStatusHistoryId
{
    public CustomerStatusHistoryId(Guid value) => Value = value != Guid.Empty ? value : throw new DomainException("Status history id is required.");
    public Guid Value { get; }
    public static CustomerStatusHistoryId New() => new(Guid.NewGuid());
}
