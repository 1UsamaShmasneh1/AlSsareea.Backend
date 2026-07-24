using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Customers.Domain;

public sealed class CustomerStatusHistory : Entity<CustomerStatusHistoryId>
{
    private CustomerStatusHistory(CustomerStatusHistoryId id) : base(id)
    {
        CorrelationId = null!;
    }

    internal CustomerStatusHistory(CustomerStatusHistoryId id, CustomerId customerId, CustomerStatus previousStatus, CustomerStatus newStatus, string? reason, DateTime changedAtUtc, Guid changedByUserId, string correlationId)
        : base(id)
    {
        CustomerId = customerId;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        Reason = reason;
        ChangedAtUtc = changedAtUtc;
        ChangedByUserId = changedByUserId;
        CorrelationId = correlationId;
    }

    public CustomerId CustomerId { get; private set; }
    public CustomerStatus PreviousStatus { get; private set; }
    public CustomerStatus NewStatus { get; private set; }
    public string? Reason { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
    public Guid ChangedByUserId { get; private set; }
    public string CorrelationId { get; private set; }
}
