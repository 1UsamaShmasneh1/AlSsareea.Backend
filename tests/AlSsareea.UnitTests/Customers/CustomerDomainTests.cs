using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Customers.Domain;

namespace AlSsareea.UnitTests.Customers;

public sealed class CustomerDomainTests
{
    private static readonly DateTime Now = new(2026, 7, 24, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void CreationNormalizesNamesAndCreatesDefaultPreferences()
    {
        Customer customer = Create("  Usama ", " Saleh  ");
        Assert.Equal("Usama", customer.FirstName);
        Assert.Equal("Saleh", customer.LastName);
        Assert.Equal("Usama Saleh", customer.DisplayName);
        Assert.Equal(CustomerStatus.Active, customer.Status);
        Assert.Equal("ar", customer.Preferences.PreferredLanguage);
        Assert.Equal("ILS", customer.Preferences.PreferredCurrency);
        Assert.Single(customer.DomainEvents.OfType<CustomerCreatedDomainEvent>());
    }

    [Theory]
    [InlineData("", "Last")]
    [InlineData("First", " ")]
    public void CreationRejectsMissingNames(string first, string last)
        => Assert.Throws<DomainException>(() => Create(first, last));

    [Fact]
    public void CreationRejectsEmptyUserId()
        => Assert.Throws<DomainException>(() => Customer.Create(CustomerId.New(), Guid.Empty, "First", "Last", null, Now, User));

    [Fact]
    public void FutureBirthDateIsRejected()
        => Assert.Throws<DomainException>(() => Customer.Create(CustomerId.New(), User, "First", "Last", DateOnly.FromDateTime(Now).AddDays(1), Now, User));

    [Fact]
    public void ProfileUpdateChangesConcurrencyStamp()
    {
        Customer customer = Create();
        Guid stamp = customer.ConcurrencyStamp;
        customer.UpdateProfile("New", "Name", null, Now.AddMinutes(1), User);
        Assert.NotEqual(stamp, customer.ConcurrencyStamp);
        Assert.Equal("New Name", customer.DisplayName);
    }

    [Fact]
    public void StatusChangeRequiresReasonAndAppendsExactlyOneHistoryRecord()
    {
        Customer customer = Create();
        Assert.Throws<DomainException>(() => customer.Suspend(" ", Now.AddMinutes(1), User, "correlation"));
        customer.Suspend("manual review", Now.AddMinutes(1), User, "correlation");
        Assert.Single(customer.StatusHistory);
        Assert.Equal(CustomerStatus.Suspended, customer.Status);
        Assert.Throws<DomainException>(() => customer.Suspend("again", Now.AddMinutes(2), User, "correlation"));
        Assert.Single(customer.StatusHistory);
    }

    [Fact]
    public void DeletedStatusIsTerminal()
    {
        Customer customer = Create();
        customer.Delete("requested", Now.AddMinutes(1), User, "correlation");
        Assert.Equal(Now.AddMinutes(1), customer.DeletedAtUtc);
        Assert.Throws<DomainException>(() => customer.Activate(Now.AddMinutes(2), User, "correlation"));
    }

    [Fact]
    public void FirstAddressAlwaysBecomesDefaultAndExplicitSecondDefaultClearsIt()
    {
        Customer customer = Create();
        CustomerAddress first = Add(customer, "Home", false, Now.AddMinutes(1));
        CustomerAddress second = Add(customer, "Work", true, Now.AddMinutes(2));
        Assert.False(first.IsDefault);
        Assert.True(second.IsDefault);
        Assert.Single(customer.Addresses, x => x.IsDefault);
    }

    [Fact]
    public void DefaultDeletionSelectsOldestThenIdAndLastDeletionLeavesNoDefault()
    {
        Customer customer = Create();
        CustomerAddress first = Add(customer, "First", false, Now.AddMinutes(1));
        CustomerAddress second = Add(customer, "Second", false, Now.AddMinutes(2));
        CustomerAddress third = Add(customer, "Third", true, Now.AddMinutes(3));
        customer.DeleteAddress(third.Id, Now.AddMinutes(4), User);
        Assert.True(first.IsDefault);
        Assert.False(second.IsDefault);
        customer.DeleteAddress(first.Id, Now.AddMinutes(5), User);
        Assert.True(second.IsDefault);
        customer.DeleteAddress(second.Id, Now.AddMinutes(6), User);
        Assert.DoesNotContain(customer.Addresses, x => x.DeletedAtUtc is null && x.IsDefault);
    }

    [Fact]
    public void DeletedAddressCannotBeUpdated()
    {
        Customer customer = Create();
        CustomerAddress address = Add(customer, "Home", false, Now.AddMinutes(1));
        customer.DeleteAddress(address.Id, Now.AddMinutes(2), User);
        Assert.Throws<DomainException>(() => customer.UpdateAddress(address.Id, "Other", CustomerAddressType.Home, "City", null, "Street", null, null, null, null, null, null, null, Now.AddMinutes(3), User));
    }

    [Theory]
    [InlineData(-91, 0)]
    [InlineData(91, 0)]
    [InlineData(0, -181)]
    [InlineData(0, 181)]
    public void CoordinatesEnforceRanges(double latitude, double longitude)
        => Assert.Throws<DomainException>(() => new GeoCoordinate(latitude, longitude));

    [Fact]
    public void PreferencesValidateLanguageCurrencyAndChangeStamp()
    {
        Customer customer = Create();
        Guid stamp = customer.Preferences.ConcurrencyStamp;
        customer.UpdatePreferences("he", "usd", true, false, true, Now.AddMinutes(1), User);
        Assert.Equal("he", customer.Preferences.PreferredLanguage);
        Assert.Equal("USD", customer.Preferences.PreferredCurrency);
        Assert.NotEqual(stamp, customer.Preferences.ConcurrencyStamp);
        Assert.Throws<DomainException>(() => customer.UpdatePreferences("fr", "USD", false, true, false, Now.AddMinutes(2), User));
        Assert.Throws<DomainException>(() => customer.UpdatePreferences("en", "US", false, true, false, Now.AddMinutes(2), User));
    }

    private static Customer Create(string first = "First", string last = "Last")
        => Customer.Create(CustomerId.New(), User, first, last, null, Now, User);

    private static CustomerAddress Add(Customer customer, string label, bool isDefault, DateTime now)
        => customer.AddAddress(CustomerAddressId.New(), label, CustomerAddressType.Home, "City", null, "Street", null, null, null, null, null, new GeoCoordinate(31.7, 35.2), null, isDefault, now, User);
}
