using System.Globalization;

namespace AlSsareea.BuildingBlocks.Application.Localization;

public static class SupportedCultures
{
    public const string Default = Arabic;
    public const string Arabic = "ar";
    public const string Hebrew = "he";
    public const string English = "en";

    public static IReadOnlyList<CultureInfo> All { get; } =
    [
        CultureInfo.GetCultureInfo(Arabic),
        CultureInfo.GetCultureInfo(Hebrew),
        CultureInfo.GetCultureInfo(English),
    ];
}
