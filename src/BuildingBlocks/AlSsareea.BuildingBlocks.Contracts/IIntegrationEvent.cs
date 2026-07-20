namespace AlSsareea.BuildingBlocks.Contracts;

public interface IIntegrationEvent
{
    Guid Id { get; }

    DateTime OccurredAtUtc { get; }
}
