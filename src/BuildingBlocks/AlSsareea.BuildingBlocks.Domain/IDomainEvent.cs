namespace AlSsareea.BuildingBlocks.Domain;

public interface IDomainEvent
{
    DateTime OccurredAtUtc { get; }
}
