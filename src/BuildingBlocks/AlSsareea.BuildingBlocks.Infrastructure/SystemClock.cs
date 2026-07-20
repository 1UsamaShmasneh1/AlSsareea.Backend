using AlSsareea.BuildingBlocks.Application;

namespace AlSsareea.BuildingBlocks.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
