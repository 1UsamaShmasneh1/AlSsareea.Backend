using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Drivers.Contracts;
using AlSsareea.Modules.Tracking.Application;
using AlSsareea.Modules.Tracking.Contracts;
using AlSsareea.Modules.Tracking.Domain;
using Microsoft.Extensions.Options;

namespace AlSsareea.UnitTests.Tracking;

public sealed class TrackingServiceTests
{
    private static readonly Guid DriverId = Guid.Parse("d274c329-0dce-41bc-890c-e966dd362f59");
    private static readonly Guid UserId = Guid.Parse("ffcb867b-e02b-4da3-b325-78548a1d4d51");

    [Fact]
    public async Task FutureBeyondToleranceIsRejected() { Harness h = new(); TrackingResult<LocationUpdateResponse> result = await h.Service.UpdateAsync(new(UserId), Harness.Request(h.Now.AddSeconds(31), 1), default); Assert.Equal(TrackingStatus.Rejected, result.Status); }
    [Fact]
    public async Task FutureWithinToleranceIsAccepted() { Harness h = new(); TrackingResult<LocationUpdateResponse> result = await h.Service.UpdateAsync(new(UserId), Harness.Request(h.Now.AddSeconds(20), 1), default); Assert.Equal(TrackingStatus.Accepted, result.Status); }
    [Fact]
    public async Task StaleLiveReadingIsHistoryOnly() { Harness h = new(); TrackingResult<LocationUpdateResponse> result = await h.Service.UpdateAsync(new(UserId), Harness.Request(h.Now.AddMinutes(-6), 1), default); Assert.Equal(TrackingStatus.HistoryOnly, result.Status); }
    [Fact]
    public async Task OfflineReadingWithinWindowCanBecomeLatest() { Harness h = new(); TrackingResult<LocationBatchResponse> result = await h.Service.BatchAsync(new(UserId), new(Guid.NewGuid(), [Harness.Request(h.Now.AddHours(-2), 1)]), default); Assert.Equal(1, result.Value!.Accepted); }
    [Fact]
    public async Task OfflineReadingOutsideWindowIsRejected() { Harness h = new(); TrackingResult<LocationBatchResponse> result = await h.Service.BatchAsync(new(UserId), new(Guid.NewGuid(), [Harness.Request(h.Now.AddHours(-25), 1)]), default); Assert.Equal(1, result.Value!.Rejected); }
    [Fact]
    public async Task ExcessiveAccuracyCannotBecomeLatest() { Harness h = new(); TrackingResult<LocationUpdateResponse> result = await h.Service.UpdateAsync(new(UserId), Harness.Request(h.Now, 1) with { AccuracyMeters = 251 }, default); Assert.Equal(TrackingStatus.HistoryOnly, result.Status); }
    [Fact]
    public async Task DuplicateIsStableAndDoesNotPublish() { DriverLocationId existingId = DriverLocationId.New(); Harness h = new() { StoreResult = new(true, false, existingId) }; TrackingResult<LocationUpdateResponse> result = await h.Service.UpdateAsync(new(UserId), Harness.Request(h.Now, 1), default); Assert.Equal("duplicate", result.Value!.Status); Assert.Equal(existingId.Value, result.Value.LocationId); Assert.Equal(0, h.Published); }
    [Fact]
    public async Task IneligibleDriverIsForbidden() { Harness h = new() { Eligible = false }; TrackingResult<LocationUpdateResponse> result = await h.Service.UpdateAsync(new(UserId), Harness.Request(h.Now, 1), default); Assert.Equal(TrackingStatus.Forbidden, result.Status); }
    [Fact]
    public async Task BatchSizeIsBounded() { Harness h = new(); LocationUpdateRequest[] items = Enumerable.Range(0, 201).Select(i => Harness.Request(h.Now, i)).ToArray(); TrackingResult<LocationBatchResponse> result = await h.Service.BatchAsync(new(UserId), new(Guid.NewGuid(), items), default); Assert.Equal(TrackingStatus.Rejected, result.Status); }
    [Theory]
    [InlineData((short)1, 120)]
    [InlineData((short)2, 30)]
    [InlineData((short)3, 10)]
    public async Task AdaptiveIntervalMatchesOperationalState(short availability, int expected) { Harness h = new() { Availability = availability }; TrackingResult<LocationUpdateResponse> result = await h.Service.UpdateAsync(new(UserId), Harness.Request(h.Now, 1), default); Assert.Equal(expected, result.Value!.RecommendedUpdateIntervalSeconds); }

    private sealed class Harness : ITrackingStore, IDriverOperationalSnapshotProvider, IClock, ILocationRealtimePublisher
    {
        public Harness() => Service = new TrackingService(this, this, this, this, Options.Create(new TrackingOptions()));
        public TrackingService Service { get; }
        public DateTime Now { get; } = new(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);
        public DateTime UtcNow => Now;
        public bool Eligible { get; init; } = true;
        public short Availability { get; init; } = 2;
        public StoreLocationResult StoreResult { get; init; } = new(false, true);
        public int Published { get; private set; }
        public static LocationUpdateRequest Request(DateTime at, long sequence) => new(at, 31.7683, 35.2137, 5, null, null, null, sequence);
        public Task<DriverEligibilitySnapshot?> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<DriverEligibilitySnapshot?>(Eligible && userId == UserId ? new(DriverId, true, true, Availability, 1, [], 2, 0, false) : null);
        public Task<DriverLatestLocation?> GetLatestEntityAsync(Guid driverId, CancellationToken cancellationToken) => Task.FromResult<DriverLatestLocation?>(null);
        public Task<StoreLocationResult> StoreAsync(DriverLocation location, bool promoteLatest, CancellationToken cancellationToken) => Task.FromResult(StoreResult with { LatestUpdated = StoreResult.LatestUpdated && promoteLatest });
        public Task<DriverLocationResponse?> GetLatestAsync(Guid driverId, CancellationToken cancellationToken) => Task.FromResult<DriverLocationResponse?>(null);
        public Task<DriverLocationHistoryResponse> GetHistoryAsync(Guid driverId, DateTime fromUtc, DateTime toUtc, int page, int pageSize, CancellationToken cancellationToken) => Task.FromResult(new DriverLocationHistoryResponse([], page, pageSize, 0));
        public Task PublishAsync(Guid driverId, TrackingRealtimePayload payload, CancellationToken cancellationToken) { Published++; return Task.CompletedTask; }
    }
}
