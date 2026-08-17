using System.Security.Claims;
using AlSsareea.Api.Realtime;
using AlSsareea.Modules.Drivers.Contracts;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Domain;
using AlSsareea.Modules.Tracking.Contracts;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;

namespace AlSsareea.IntegrationTests;

public sealed class TrackingHubAuthorizationTests
{
    [Fact]
    public async Task DriverCanSubscribeOnlyToResolvedSelfGroup()
    {
        Guid userId = Guid.NewGuid(); Guid driverId = Guid.NewGuid(); RecordingGroups groups = new();
        TrackingHub hub = CreateHub(new CurrentUser(userId), new DriverProvider(new(driverId, true, true, 2, null, [], 1, 0, false)), new VisibilityProvider(null), groups);

        await hub.SubscribeSelf();

        Assert.Equal([TrackingGroups.Driver(driverId)], groups.AddedGroups);
    }

    [Fact]
    public async Task OperationsSubscriptionRequiresDedicatedPermission()
    {
        RecordingGroups deniedGroups = new(); TrackingHub denied = CreateHub(new CurrentUser(Guid.NewGuid()), new DriverProvider(null), new VisibilityProvider(null), deniedGroups);
        await Assert.ThrowsAsync<HubException>(denied.SubscribeOperations); Assert.Empty(deniedGroups.AddedGroups);

        RecordingGroups allowedGroups = new(); TrackingHub allowed = CreateHub(new CurrentUser(Guid.NewGuid(), TrackingPermissions.RealtimeOperations), new DriverProvider(null), new VisibilityProvider(null), allowedGroups);
        await allowed.SubscribeOperations(); Assert.Equal([TrackingGroups.Operations], allowedGroups.AddedGroups);
    }

    [Fact]
    public async Task OrderSubscriptionRequiresVisibilityDecision()
    {
        Guid orderId = Guid.NewGuid(); RecordingGroups deniedGroups = new(); TrackingHub denied = CreateHub(new CurrentUser(Guid.NewGuid()), new DriverProvider(null), new VisibilityProvider(null), deniedGroups);
        await Assert.ThrowsAsync<HubException>(() => denied.SubscribeOrder(orderId)); Assert.Empty(deniedGroups.AddedGroups);

        RecordingGroups allowedGroups = new(); TrackingHub allowed = CreateHub(new CurrentUser(Guid.NewGuid()), new DriverProvider(null), new VisibilityProvider(new(Guid.NewGuid(), "customer")), allowedGroups);
        await allowed.SubscribeOrder(orderId); Assert.Equal([TrackingGroups.Order(orderId)], allowedGroups.AddedGroups);
    }

    [Fact]
    public void RealtimePayloadContainsNoPersistenceOrActorFields()
    {
        string[] names = typeof(TrackingRealtimePayload).GetProperties().Select(x => x.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["AccuracyMeters", "HeadingDegrees", "Latitude", "Longitude", "RecordedAtUtc", "SpeedMetersPerSecond"], names);
    }

    private static TrackingHub CreateHub(ICurrentUser user, IDriverOperationalSnapshotProvider drivers, ITrackingVisibilityProvider visibility, RecordingGroups groups) => new(drivers, visibility, user)
    {
        Context = new CallerContext(),
        Groups = groups
    };

    private sealed class DriverProvider(DriverEligibilitySnapshot? snapshot) : IDriverOperationalSnapshotProvider
    {
        public Task<DriverEligibilitySnapshot?> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
    }

    private sealed class VisibilityProvider(TrackingVisibility? visibility) : ITrackingVisibilityProvider
    {
        public Task<TrackingVisibility?> ResolveOrderAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(visibility);
    }

    private sealed class CurrentUser(Guid userId, params string[] permissions) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public UserId? UserId { get; } = new(userId);
        public LoginSessionId? SessionId => null;
        public IReadOnlySet<string> Roles { get; } = new HashSet<string>(StringComparer.Ordinal);
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>(permissions, StringComparer.Ordinal);
    }

    private sealed class RecordingGroups : IGroupManager
    {
        public List<string> AddedGroups { get; } = [];
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) { AddedGroups.Add(groupName); return Task.CompletedTask; }
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CallerContext : HubCallerContext
    {
        public override string ConnectionId => "tracking-test-connection";
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => null;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }
}
