using AlSsareea.Modules.Maps.Application;
using AlSsareea.Modules.Maps.Contracts;

namespace AlSsareea.UnitTests.Maps;

public sealed class CustomerMapsServiceTests
{
    [Fact]
    public async Task GeocodeValidatesAndMapsProviderFailure()
    {
        var provider = new Provider();
        var service = new CustomerMapsService(provider, provider, new MapsModuleStub([]));
        Assert.Equal(CustomerMapsStatus.Invalid, (await service.GeocodeAsync(new(" "), default)).Status);
        Assert.Equal(CustomerMapsStatus.Success, (await service.GeocodeAsync(new("Ramallah"), default)).Status);
        provider.Fail = true;
        CustomerMapsResult<IReadOnlyList<GeocodingResult>> failure = await service.GeocodeAsync(new("Ramallah"), default);
        Assert.Equal(CustomerMapsStatus.Unavailable, failure.Status);
        Assert.Equal("maps.provider_unavailable", failure.ErrorCode);
    }

    [Fact]
    public async Task ReverseGeocodePreservesNeutralContract()
    {
        var provider = new Provider();
        var service = new CustomerMapsService(provider, provider, new MapsModuleStub([]));
        CustomerMapsResult<ReverseGeocodingResult> result = await service.ReverseGeocodeAsync(new(31.9, 35.2), default);
        Assert.Equal(CustomerMapsStatus.Success, result.Status);
        Assert.Equal(31.9, result.Value!.Latitude);
    }

    [Fact]
    public async Task EligibilityUsesOnlyActiveContainingAreas()
    {
        ServiceAreaDetails inactive = new(Guid.NewGuid(), "Inactive", null, false, DateTime.UtcNow, DateTime.UtcNow);
        ServiceAreaDetails active = new(Guid.NewGuid(), "Active", null, true, DateTime.UtcNow, DateTime.UtcNow);
        var provider = new Provider();
        var service = new CustomerMapsService(provider, provider, new MapsModuleStub([inactive, active]));
        DeliveryEligibilityResponse response = (await service.EvaluateDeliveryEligibilityAsync(new(31.9, 35.2), default)).Value!;
        Assert.True(response.Eligible);
        Assert.Equal(active.Id, response.ServiceAreaId);
    }

    private sealed class Provider : IGeocodingProvider, IReverseGeocodingProvider
    {
        public bool Fail { get; set; }
        public Task<IReadOnlyList<GeocodingResult>> GeocodeAsync(GeocodingRequest request, CancellationToken cancellationToken = default) => Fail ? throw new MapsProviderException("failed") : Task.FromResult<IReadOnlyList<GeocodingResult>>([new(request.Query, 31.9, 35.2, "place")]);
        public Task<ReverseGeocodingResult?> ReverseGeocodeAsync(ReverseGeocodingRequest request, CancellationToken cancellationToken = default) => Task.FromResult<ReverseGeocodingResult?>(new("Address", request.Latitude, request.Longitude, "place"));
    }

    private sealed class MapsModuleStub(IReadOnlyList<ServiceAreaDetails> areas) : IMapsModule
    {
        public Task<bool> ContainsPointAsync(Guid serviceAreaId, double latitude, double longitude, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<ServiceAreaDetails>> FindContainingAreasAsync(double latitude, double longitude, CancellationToken cancellationToken = default) => Task.FromResult(areas);
        public Task<ServiceAreaDetails?> GetServiceAreaAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<ServiceAreaDetails?>(null);
    }
}
