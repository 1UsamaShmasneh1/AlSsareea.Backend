namespace AlSsareea.Modules.Maps.Infrastructure.Configuration;

public sealed class MapsOptions
{
    public const string SectionName = "Maps";

    public MapsProvider Provider { get; init; } = MapsProvider.Fake;

    public FakeMapsOptions Fake { get; init; } = new();
}

public enum MapsProvider
{
    Fake = 1,
}

public sealed class FakeMapsOptions
{
    public bool FailAllRequests { get; init; }
}
