using AlSsareea.Modules.Maps.Contracts;
using AlSsareea.Modules.Merchants.Contracts;

namespace AlSsareea.ArchitectureTests;

public sealed class CustomerAppContractTests
{
    [Fact]
    public void CustomerMerchantContractsExcludeAdministrativeFields()
    {
        string[] forbidden = ["OwnerUserId", "ConcurrencyStamp", "RegistrationNumber", "TaxNumber", "Email", "PhoneNumber", "StatusChangeReason"];
        Type[] contracts = [typeof(CustomerMerchantSummary), typeof(CustomerMerchantDetails), typeof(CustomerMerchantBranchSummary)];
        Assert.All(contracts, type => Assert.DoesNotContain(type.GetProperties(), property => forbidden.Contains(property.Name, StringComparer.Ordinal)));
    }

    [Fact]
    public void CustomerMapsContractsRemainProviderNeutral()
    {
        Type[] contracts = [typeof(GeocodingRequest), typeof(GeocodingResult), typeof(ReverseGeocodingRequest), typeof(ReverseGeocodingResult)];
        Assert.All(contracts.SelectMany(x => x.GetProperties()), property => Assert.DoesNotContain("Infrastructure", property.PropertyType.FullName ?? string.Empty, StringComparison.Ordinal));
    }
}
