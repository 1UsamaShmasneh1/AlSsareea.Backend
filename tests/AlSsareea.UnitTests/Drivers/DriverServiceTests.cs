using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Contracts;
using AlSsareea.Modules.Drivers.Application;
using AlSsareea.Modules.Drivers.Contracts;
using AlSsareea.Modules.Drivers.Domain;
using AlSsareea.Modules.Drivers.Infrastructure;
using AlSsareea.Modules.Drivers.Infrastructure.Persistence;
using AlSsareea.Modules.Identity.Contracts;
using AlSsareea.Modules.Maps.Contracts;
using AlSsareea.Modules.Media.Contracts;

namespace AlSsareea.UnitTests.Drivers;

public sealed class DriverServiceTests
{
    private static readonly DateTime Now = new(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ReplayPreservesCreatedResponseAndScopesSameKeyByActorOperationAndPayload()
    {
        FakeRepository repository = new(); DriverService service = Service(repository, Now); Guid firstActor = Guid.NewGuid(); Guid secondActor = Guid.NewGuid(); var request = new CreateDriverRequest("Original", (short)EmploymentType.Employee, 2, null);
        DriverOperationResult<DriverProfileResponse> created = await service.CreateAsync(new DriverActor(firstActor, null), request, "same-key", default); Assert.Equal(DriverOperationStatus.Created, created.Status);
        repository.Drivers[firstActor].UpdateProfile("Later Name", null, null, Now.AddMinutes(1));
        DriverOperationResult<DriverProfileResponse> replay = await service.CreateAsync(new DriverActor(firstActor, null), request, "same-key", default); Assert.Equal(DriverOperationStatus.Created, replay.Status); Assert.Equal("Original", replay.Value!.DisplayName);
        Assert.Equal(DriverOperationStatus.Conflict, (await service.CreateAsync(new DriverActor(firstActor, null), request with { DisplayName = "Mismatch" }, "same-key", default)).Status);
        Assert.Equal(DriverOperationStatus.Created, (await service.CreateAsync(new DriverActor(secondActor, null), request with { DisplayName = "Second" }, "same-key", default)).Status);
        Assert.Equal(DriverOperationStatus.Success, (await service.ChangeAvailabilityAsync(new DriverActor(firstActor, null), "offline", "same-key", default)).Status);
        Assert.Equal(3, repository.Idempotency.Count); Assert.Equal(2, repository.Audits.Count); Assert.Equal(2, repository.Events.Count);
    }

    [Fact]
    public async Task AvailabilityNoOpsPersistReplayOnlyWithoutAuditOutboxEventOrStampChange()
    {
        Guid actor = Guid.NewGuid(); FakeRepository repository = new(); Driver driver = Eligible(actor); repository.Drivers.Add(actor, driver); DriverService service = Service(repository, Now.AddMinutes(20)); Guid initialStamp = driver.ConcurrencyStamp;
        Assert.Equal(DriverOperationStatus.Success, (await service.ChangeAvailabilityAsync(new DriverActor(actor, null), "offline", "offline-one", default)).Status); Assert.Equal(initialStamp, driver.ConcurrencyStamp); Assert.Empty(repository.Audits); Assert.Empty(repository.Events);
        Assert.Equal(DriverOperationStatus.Success, (await service.ChangeAvailabilityAsync(new DriverActor(actor, null), "online", "online-one", default)).Status); Guid onlineStamp = driver.ConcurrencyStamp; Assert.Single(repository.Audits); Assert.Single(repository.Events);
        Assert.Equal(DriverOperationStatus.Success, (await service.ChangeAvailabilityAsync(new DriverActor(actor, null), "online", "online-two", default)).Status); Assert.Equal(onlineStamp, driver.ConcurrencyStamp); Assert.Single(repository.Audits); Assert.Single(repository.Events); Assert.Equal(3, repository.Idempotency.Count);
    }

    [Fact]
    public async Task FixedClockAllowsOnlineAtSuspensionEndWithoutWorker()
    {
        Guid actor = Guid.NewGuid(); FakeRepository repository = new(); Driver driver = Eligible(actor); driver.Suspend("finite", "Finite", Now.AddMinutes(10), Now.AddMinutes(20), Guid.NewGuid(), Now.AddMinutes(10)); repository.Drivers.Add(actor, driver);
        DriverOperationResult<DriverAvailabilityResponse> result = await Service(repository, Now.AddMinutes(20)).ChangeAvailabilityAsync(new DriverActor(actor, null), "online", "after-expiry", default);
        Assert.Equal(DriverOperationStatus.Success, result.Status); Assert.Equal((short)AvailabilityStatus.Online, result.Value!.Status); Assert.False(driver.HasActiveSuspension(Now.AddMinutes(20)));
    }

    [Fact]
    public async Task FailedSaveDoesNotCompleteIdempotencyAuditOrOutbox()
    {
        Guid actor = Guid.NewGuid(); FakeRepository repository = new() { FailSave = true }; Driver driver = Eligible(actor); repository.Drivers.Add(actor, driver); DriverService service = Service(repository, Now.AddMinutes(20));
        DriverOperationResult<DriverProfileResponse> result = await service.UpdateProfileAsync(new DriverActor(actor, null), new UpdateDriverProfileRequest("Failure", null, null, driver.ConcurrencyStamp), "failed-key", default);
        Assert.Equal(DriverOperationStatus.Conflict, result.Status); Assert.Empty(repository.Idempotency); Assert.Empty(repository.Audits); Assert.Empty(repository.Events);
    }

    private static DriverService Service(FakeRepository repository, DateTime now) => new(repository, new ActiveIdentity(), new Maps(), new Media(), new FixedClock(now));

    private static Driver Eligible(Guid userId)
    {
        Driver driver = Driver.Create(DriverId.New(), userId, "Eligible", EmploymentType.Employee, 2, null, Now); driver.SubmitForReview(Now.AddMinutes(1)); driver.Approve(Now.AddMinutes(2)); driver.Activate(Now.AddMinutes(3)); Vehicle vehicle = driver.AddVehicle(VehicleType.Car, "Toyota", "Corolla", 2024, "White", Guid.NewGuid().ToString("N")[..8], "IL", true, Now.AddMinutes(4)); vehicle.Approve(Now.AddMinutes(5)); driver.SetPrimaryVehicle(vehicle.Id, Now.AddMinutes(6)); driver.AssignZone(Guid.NewGuid(), true, Guid.NewGuid(), Now.AddMinutes(6));
        foreach (DocumentType type in DriverEligibilityPolicy.RequiredDocuments(VehicleType.Car)) { DriverDocument document = driver.SubmitDocument(type, Guid.NewGuid(), Now.AddDays(-1), Now.AddDays(10), Now.AddMinutes(7)); document.Approve(Guid.NewGuid(), Now.AddMinutes(8)); }
        driver.ClearDomainEvents(); return driver;
    }

    private sealed class FakeRepository : IDriverRepository
    {
        public Dictionary<Guid, Driver> Drivers { get; } = [];
        public Dictionary<(Guid Actor, string Operation, string Key), DriverIdempotencyEntry> Idempotency { get; } = [];
        public List<DriverAuditEntry> Audits { get; } = [];
        public List<IIntegrationEvent> Events { get; } = [];
        public bool FailSave { get; init; }
        public Task<Driver?> GetAsync(DriverId id, CancellationToken cancellationToken) => Task.FromResult(Drivers.Values.SingleOrDefault(x => x.Id == id));
        public Task<Driver?> GetByUserAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(Drivers.GetValueOrDefault(userId));
        public Task<bool> UserHasDriverAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(Drivers.ContainsKey(userId));
        public Task AddAsync(Driver driver, CancellationToken cancellationToken) { Drivers.Add(driver.UserId, driver); return Task.CompletedTask; }
        public Task<DriverIdempotencyResult> CheckIdempotencyAsync(Guid actorUserId, string operation, string keyHash, string requestHash, CancellationToken cancellationToken)
        {
            if (!Idempotency.TryGetValue((actorUserId, operation, keyHash), out DriverIdempotencyEntry? entry)) return Task.FromResult(new DriverIdempotencyResult(DriverIdempotencyState.New));
            return Task.FromResult(entry.RequestHash == requestHash ? new DriverIdempotencyResult(DriverIdempotencyState.SameRequest, entry.ResponseStatus, entry.ResponseJson) : new DriverIdempotencyResult(DriverIdempotencyState.DifferentRequest));
        }
        public Task<bool> SaveOperationAsync(Driver driver, DriverIdempotencyEntry? idempotency, DriverAuditEntry? audit, IReadOnlyCollection<IIntegrationEvent> integrationEvents, CancellationToken cancellationToken)
        {
            if (FailSave) return Task.FromResult(false); if (idempotency is not null) Idempotency.Add((idempotency.ActorUserId, idempotency.Operation, idempotency.KeyHash), idempotency); if (audit is not null) Audits.Add(audit); Events.AddRange(integrationEvents); driver.ClearDomainEvents(); return Task.FromResult(true);
        }
        public Task<DriverProfileResponse?> GetProfileAsync(DriverId id, CancellationToken cancellationToken) => Task.FromResult(DriverRepository.Map(Drivers.Values.SingleOrDefault(x => x.Id == id)));
        public Task<DriverProfileResponse?> GetProfileByUserAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(DriverRepository.Map(Drivers.GetValueOrDefault(userId)));
        public Task<PagedDriversResponse> ListAsync(DriverQuery query, CancellationToken cancellationToken) => Task.FromResult(new PagedDriversResponse([], 1, query.PageSize, 0));
    }

    private sealed class ActiveIdentity : IIdentityUserLookup { public Task<bool> IsActiveUserAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(true); }
    private sealed class FixedClock(DateTime now) : IClock { public DateTime UtcNow { get; } = now; }
    private sealed class Media : IMediaAssetLookup { public Task<MediaAssetReference?> FindAsync(Guid assetId, CancellationToken cancellationToken = default) => Task.FromResult<MediaAssetReference?>(null); public Task<bool> CanUseAsync(Guid assetId, Guid merchantId, string ownerType, Guid ownerId, CancellationToken cancellationToken = default) => Task.FromResult(false); }
    private sealed class Maps : IMapsModule
    {
        public Task<ServiceAreaDetails?> GetServiceAreaAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<ServiceAreaDetails?>(new(id, "Zone", null, true, Now, Now));
        public Task<bool> ContainsPointAsync(Guid serviceAreaId, double latitude, double longitude, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<IReadOnlyList<ServiceAreaDetails>> FindContainingAreasAsync(double latitude, double longitude, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ServiceAreaDetails>>([]);
    }
}
