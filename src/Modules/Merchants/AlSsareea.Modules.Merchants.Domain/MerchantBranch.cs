using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Merchants.Domain;

public sealed class MerchantBranch : AggregateRoot<MerchantBranchId>
{
    private readonly List<BusinessHour> _businessHours = [];
    private readonly List<BranchScheduleOverride> _scheduleOverrides = [];
    private readonly List<BranchServiceArea> _serviceAreas = [];

    private MerchantBranch(MerchantBranchId id) : base(id) { Name = PhoneNumber = TimeZone = null!; Address = null!; }
    private MerchantBranch(MerchantBranchId id, MerchantId merchantId, string name, string? code, string phoneNumber, string? email, BranchAddress address, GeoCoordinate location, string timeZone, bool isPrimary, DateTime now)
        : base(id)
    {
        MerchantId = merchantId;
        ApplyProfile(name, code, phoneNumber, email, address, timeZone);
        Location = location;
        IsPrimary = isPrimary;
        Status = MerchantBranchStatus.Draft;
        CreatedAtUtc = UpdatedAtUtc = now;
        ConcurrencyStamp = Guid.NewGuid();
    }

    public MerchantId MerchantId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Code { get; private set; }
    public string PhoneNumber { get; private set; } = null!;
    public string? Email { get; private set; }
    public BranchAddress Address { get; private set; } = null!;
    public GeoCoordinate Location { get; private set; }
    public MerchantBranchStatus Status { get; private set; }
    public bool IsPrimary { get; private set; }
    public string TimeZone { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? ActivatedAtUtc { get; private set; }
    public DateTime? TemporarilyClosedAtUtc { get; private set; }
    public DateTime? ReopenedAtUtc { get; private set; }
    public DateTime? SuspendedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public string? StatusChangeReason { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public IReadOnlyCollection<BusinessHour> BusinessHours => _businessHours.AsReadOnly();
    public IReadOnlyCollection<BranchScheduleOverride> ScheduleOverrides => _scheduleOverrides.AsReadOnly();
    public IReadOnlyCollection<BranchServiceArea> ServiceAreas => _serviceAreas.AsReadOnly();

    public static MerchantBranch Create(MerchantBranchId id, MerchantId merchantId, string name, string? code, string phoneNumber, string? email, BranchAddress address, GeoCoordinate location, string timeZone, bool isPrimary, DateTime now)
    {
        MerchantRules.Utc(now, nameof(now));
        MerchantBranch branch = new(id, merchantId, name, code, phoneNumber, email, address, location, timeZone, isPrimary, now);
        branch.RaiseDomainEvent(new MerchantBranchCreatedDomainEvent(merchantId, id, now));
        return branch;
    }

    public void Update(string name, string? code, string phoneNumber, string? email, BranchAddress address, string timeZone, DateTime now)
    {
        EnsureNotClosed(); ApplyProfile(name, code, phoneNumber, email, address, timeZone); Touch(now);
    }

    public void ChangeLocation(GeoCoordinate location, DateTime now)
    {
        EnsureNotClosed(); Location = location; Touch(now); RaiseDomainEvent(new MerchantBranchLocationChangedDomainEvent(MerchantId, Id, now));
    }

    public void Activate(bool merchantIsActive, DateTime now)
    {
        if (!merchantIsActive) throw new DomainException("A branch cannot be activated while its merchant is not active.");
        if (Status is not (MerchantBranchStatus.Draft or MerchantBranchStatus.Suspended)) throw new DomainException("Branch cannot be activated from its current status.");
        _ = MerchantRules.TimeZone(TimeZone);
        Status = MerchantBranchStatus.Active; ActivatedAtUtc ??= now; SuspendedAtUtc = null; StatusChangeReason = null; Touch(now); StatusEvent(now);
    }

    public void TemporarilyClose(string? reason, DateTime now)
    {
        if (Status != MerchantBranchStatus.Active) throw new DomainException("Only an active branch can be temporarily closed.");
        Status = MerchantBranchStatus.TemporarilyClosed; TemporarilyClosedAtUtc = now; StatusChangeReason = MerchantRules.Optional(reason, 1000, nameof(reason)); Touch(now); StatusEvent(now);
    }

    public void Reopen(bool merchantIsActive, DateTime now)
    {
        if (!merchantIsActive) throw new DomainException("A branch cannot reopen while its merchant is not active.");
        if (Status != MerchantBranchStatus.TemporarilyClosed) throw new DomainException("Only a temporarily closed branch can reopen.");
        Status = MerchantBranchStatus.Active; ReopenedAtUtc = now; StatusChangeReason = null; Touch(now); StatusEvent(now);
    }

    public void Suspend(string reason, DateTime now)
    {
        if (Status is not (MerchantBranchStatus.Active or MerchantBranchStatus.TemporarilyClosed)) throw new DomainException("Branch cannot be suspended from its current status.");
        string normalizedReason = MerchantRules.Required(reason, 1000, nameof(reason));
        Status = MerchantBranchStatus.Suspended; SuspendedAtUtc = now; StatusChangeReason = normalizedReason; Touch(now); StatusEvent(now);
    }

    public void Close(string reason, DateTime now)
    {
        if (Status == MerchantBranchStatus.Closed) throw new DomainException("Branch is already closed.");
        string normalizedReason = MerchantRules.Required(reason, 1000, nameof(reason));
        Status = MerchantBranchStatus.Closed; ClosedAtUtc = now; StatusChangeReason = normalizedReason; IsPrimary = false; Touch(now); StatusEvent(now);
    }

    public void SetPrimary(bool value, DateTime now)
    {
        if (value && Status == MerchantBranchStatus.Closed) throw new DomainException("A closed branch cannot become primary.");
        if (IsPrimary == value) return;
        IsPrimary = value; Touch(now); RaiseDomainEvent(new MerchantPrimaryBranchChangedDomainEvent(MerchantId, Id, value, now));
    }

    public void ReplaceBusinessHours(IEnumerable<(DayOfWeek Day, bool ClosedAllDay, IEnumerable<OpeningPeriod> Periods)> schedule, DateTime now)
    {
        EnsureNotClosed();
        var days = schedule.ToArray();
        if (days.Select(x => x.Day).Distinct().Count() != days.Length || days.Length != 7) throw new DomainException("Weekly schedule must contain each day exactly once.");
        _businessHours.Clear();
        foreach (var day in days)
        {
            OpeningPeriod[] periods = MerchantRules.Periods(day.Periods);
            if (day.ClosedAllDay && periods.Length != 0) throw new DomainException("A closed day cannot contain opening periods.");
            if (!day.ClosedAllDay && periods.Length == 0) throw new DomainException("An open day requires at least one opening period.");
            _businessHours.Add(BusinessHour.Create(BusinessHourId.New(), Id, day.Day, day.ClosedAllDay, periods));
        }
        Touch(now);
    }

    public BranchScheduleOverride AddClosure(DateOnly startDate, DateOnly endDate, string? reason, DateTime now)
    {
        EnsureNotClosed(); ValidateRange(startDate, endDate);
        EnsureNoOverrideOverlap(startDate, endDate);
        BranchScheduleOverride value = BranchScheduleOverride.Closure(ScheduleOverrideId.New(), Id, startDate, endDate, reason, now);
        _scheduleOverrides.Add(value); Touch(now); return value;
    }

    public BranchScheduleOverride SetSpecialHours(DateOnly date, IEnumerable<OpeningPeriod> periods, string? reason, DateTime now)
    {
        EnsureNotClosed(); EnsureNoOverrideOverlap(date, date);
        BranchScheduleOverride value = BranchScheduleOverride.SpecialHours(ScheduleOverrideId.New(), Id, date, MerchantRules.Periods(periods), reason, now);
        _scheduleOverrides.Add(value); Touch(now); return value;
    }

    public void CancelOverride(ScheduleOverrideId overrideId, DateTime now)
    {
        BranchScheduleOverride value = _scheduleOverrides.SingleOrDefault(x => x.Id == overrideId && x.CancelledAtUtc is null) ?? throw new DomainException("Schedule override was not found.");
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
        DateOnly localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, zone));
        if (value.EndDate < localToday) throw new DomainException("Historical schedule overrides cannot be cancelled.");
        value.Cancel(now); Touch(now);
    }

    public void AssignServiceArea(Guid serviceAreaId, DateTime now)
    {
        if (serviceAreaId == Guid.Empty) throw new DomainException("Service area ID cannot be empty.");
        if (_serviceAreas.Any(x => x.ServiceAreaId == serviceAreaId)) throw new DomainException("Service area is already assigned.");
        _serviceAreas.Add(new BranchServiceArea(Id, serviceAreaId, now));
        Touch(now); RaiseDomainEvent(new BranchServiceAreaAssignedDomainEvent(MerchantId, Id, serviceAreaId, now));
    }

    public void RemoveServiceArea(Guid serviceAreaId, DateTime now)
    {
        BranchServiceArea value = _serviceAreas.SingleOrDefault(x => x.ServiceAreaId == serviceAreaId) ?? throw new DomainException("Service-area assignment was not found.");
        _serviceAreas.Remove(value); Touch(now); RaiseDomainEvent(new BranchServiceAreaRemovedDomainEvent(MerchantId, Id, serviceAreaId, now));
    }

    public BranchAvailability GetAvailability(DateTime atUtc)
    {
        MerchantRules.Utc(atUtc, nameof(atUtc));
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
        DateTime local = TimeZoneInfo.ConvertTimeFromUtc(atUtc, zone);
        DateOnly date = DateOnly.FromDateTime(local);
        TimeOnly time = TimeOnly.FromDateTime(local);
        BranchScheduleOverride? value = _scheduleOverrides.Where(x => x.CancelledAtUtc is null && x.StartDate <= date && x.EndDate >= date).OrderByDescending(x => x.CreatedAtUtc).FirstOrDefault();
        if (Status != MerchantBranchStatus.Active) return new(false, date, "branch-status");
        if (value is not null) return value.IsClosed
            ? new(false, date, "exceptional-closure")
            : new(value.Periods.Any(x => x.Contains(time)), date, "special-hours");
        BusinessHour? weekly = _businessHours.SingleOrDefault(x => x.DayOfWeek == local.DayOfWeek);
        bool open = Status == MerchantBranchStatus.Active && weekly is not null && !weekly.ClosedAllDay && weekly.Periods.Any(x => x.Contains(time));
        return new(open, date, "weekly-hours");
    }

    private void ApplyProfile(string name, string? code, string phoneNumber, string? email, BranchAddress address, string timeZone)
    {
        Name = MerchantRules.Required(name, 200, nameof(name)); Code = MerchantRules.Optional(code, 50, nameof(code));
        PhoneNumber = MerchantRules.Required(phoneNumber, 32, nameof(phoneNumber));
        Email = MerchantRules.Optional(email, 320, nameof(email));
        if (Email is not null && (!Email.Contains('@', StringComparison.Ordinal) || Email.StartsWith('@') || Email.EndsWith('@'))) throw new DomainException("Email is invalid.");
        Address = address ?? throw new DomainException("Address is required.");
        TimeZone = MerchantRules.TimeZone(timeZone);
    }
    private void EnsureNoOverrideOverlap(DateOnly start, DateOnly end) { if (_scheduleOverrides.Any(x => x.CancelledAtUtc is null && x.StartDate <= end && x.EndDate >= start)) throw new DomainException("Schedule overrides cannot overlap."); }
    private static void ValidateRange(DateOnly start, DateOnly end) { if (end < start) throw new DomainException("End date must be on or after start date."); }
    private void EnsureNotClosed() { if (Status == MerchantBranchStatus.Closed) throw new DomainException("Closed branches cannot be modified."); }
    private void Touch(DateTime now) { MerchantRules.Utc(now, nameof(now)); UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
    private void StatusEvent(DateTime now) => RaiseDomainEvent(new MerchantBranchStatusChangedDomainEvent(MerchantId, Id, Status, now));
}

public sealed class BusinessHour : Entity<BusinessHourId>
{
    private readonly List<BusinessHourPeriod> _periods = [];
    private BusinessHour(BusinessHourId id) : base(id) { }
    private BusinessHour(BusinessHourId id, MerchantBranchId branchId, DayOfWeek day, bool closed, IEnumerable<OpeningPeriod> periods) : base(id)
    {
        BranchId = branchId; DayOfWeek = day; ClosedAllDay = closed;
        _periods.AddRange(periods.Select(x => new BusinessHourPeriod(BusinessHourPeriodId.New(), id, x.OpensAt, x.ClosesAt)));
    }
    public MerchantBranchId BranchId { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public bool ClosedAllDay { get; private set; }
    public IReadOnlyCollection<BusinessHourPeriod> Periods => _periods.AsReadOnly();
    internal static BusinessHour Create(BusinessHourId id, MerchantBranchId branchId, DayOfWeek day, bool closed, IEnumerable<OpeningPeriod> periods) => new(id, branchId, day, closed, periods);
}

public sealed class BusinessHourPeriod : Entity<BusinessHourPeriodId>
{
    private BusinessHourPeriod(BusinessHourPeriodId id) : base(id) { }
    internal BusinessHourPeriod(BusinessHourPeriodId id, BusinessHourId businessHourId, TimeOnly opensAt, TimeOnly closesAt) : base(id) { BusinessHourId = businessHourId; OpensAt = opensAt; ClosesAt = closesAt; }
    public BusinessHourId BusinessHourId { get; private set; }
    public TimeOnly OpensAt { get; private set; }
    public TimeOnly ClosesAt { get; private set; }
    internal bool Contains(TimeOnly value) => value >= OpensAt && value < ClosesAt;
}

public sealed class BranchScheduleOverride : Entity<ScheduleOverrideId>
{
    private readonly List<SpecialHourPeriod> _periods = [];
    private BranchScheduleOverride(ScheduleOverrideId id) : base(id) { Reason = null; }
    private BranchScheduleOverride(ScheduleOverrideId id, MerchantBranchId branchId, DateOnly start, DateOnly end, bool closed, string? reason, DateTime now, IEnumerable<OpeningPeriod> periods) : base(id)
    {
        BranchId = branchId; StartDate = start; EndDate = end; IsClosed = closed; Reason = MerchantRules.Optional(reason, 500, nameof(reason)); CreatedAtUtc = now;
        _periods.AddRange(periods.Select(x => new SpecialHourPeriod(SpecialHourPeriodId.New(), id, x.OpensAt, x.ClosesAt)));
    }
    public MerchantBranchId BranchId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsClosed { get; private set; }
    public string? Reason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public IReadOnlyCollection<SpecialHourPeriod> Periods => _periods.AsReadOnly();
    internal static BranchScheduleOverride Closure(ScheduleOverrideId id, MerchantBranchId branchId, DateOnly start, DateOnly end, string? reason, DateTime now) => new(id, branchId, start, end, true, reason, now, []);
    internal static BranchScheduleOverride SpecialHours(ScheduleOverrideId id, MerchantBranchId branchId, DateOnly date, IEnumerable<OpeningPeriod> periods, string? reason, DateTime now)
    {
        OpeningPeriod[] values = periods.ToArray();
        if (values.Length == 0) throw new DomainException("Special hours require at least one period.");
        return new(id, branchId, date, date, false, reason, now, values);
    }
    internal void Cancel(DateTime now) { if (CancelledAtUtc is not null) throw new DomainException("Schedule override is already cancelled."); CancelledAtUtc = now; }
}

public sealed class SpecialHourPeriod : Entity<SpecialHourPeriodId>
{
    private SpecialHourPeriod(SpecialHourPeriodId id) : base(id) { }
    internal SpecialHourPeriod(SpecialHourPeriodId id, ScheduleOverrideId overrideId, TimeOnly opensAt, TimeOnly closesAt) : base(id) { ScheduleOverrideId = overrideId; OpensAt = opensAt; ClosesAt = closesAt; }
    public ScheduleOverrideId ScheduleOverrideId { get; private set; }
    public TimeOnly OpensAt { get; private set; }
    public TimeOnly ClosesAt { get; private set; }
    internal bool Contains(TimeOnly value) => value >= OpensAt && value < ClosesAt;
}

public sealed class BranchServiceArea
{
    private BranchServiceArea() { }
    internal BranchServiceArea(MerchantBranchId branchId, Guid serviceAreaId, DateTime assignedAtUtc) { BranchId = branchId; ServiceAreaId = serviceAreaId; AssignedAtUtc = assignedAtUtc; }
    public MerchantBranchId BranchId { get; private set; }
    public Guid ServiceAreaId { get; private set; }
    public DateTime AssignedAtUtc { get; private set; }
}

public sealed record BranchAvailability(bool IsOpen, DateOnly LocalDate, string Source);
