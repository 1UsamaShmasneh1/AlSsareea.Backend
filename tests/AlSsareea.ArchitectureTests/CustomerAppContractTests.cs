using AlSsareea.Modules.Carts.Contracts;
using AlSsareea.Modules.Catalog.Contracts;
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

    [Fact]
    public void CustomerProductDetailsExposeSelectionRulesWithoutDomainOrInfrastructureTypes()
    {
        Type[] contracts = [typeof(CustomerProductDetailsResponse), typeof(CustomerProductMediaResponse), typeof(CustomerProductVariantResponse), typeof(CustomerProductOptionGroupResponse), typeof(CustomerProductOptionResponse)];
        Assert.All(contracts.SelectMany(type => type.GetProperties()), property =>
        {
            string typeName = property.PropertyType.FullName ?? string.Empty;
            Assert.DoesNotContain(".Domain", typeName, StringComparison.Ordinal);
            Assert.DoesNotContain(".Infrastructure", typeName, StringComparison.Ordinal);
        });
        Assert.Equal(typeof(Guid), typeof(CustomerProductVariantResponse).GetProperty(nameof(CustomerProductVariantResponse.Id))!.PropertyType);
        Assert.Equal(typeof(Guid), typeof(CustomerProductOptionGroupResponse).GetProperty(nameof(CustomerProductOptionGroupResponse.Id))!.PropertyType);
        Assert.Equal(typeof(Guid), typeof(CustomerProductOptionResponse).GetProperty(nameof(CustomerProductOptionResponse.Id))!.PropertyType);
        Assert.NotNull(typeof(CustomerProductOptionGroupResponse).GetProperty(nameof(CustomerProductOptionGroupResponse.MinSelections)));
        Assert.NotNull(typeof(CustomerProductOptionGroupResponse).GetProperty(nameof(CustomerProductOptionGroupResponse.MaxSelections)));
    }

    [Fact]
    public void CatalogSelectionIdentifiersMatchCartMutationIdentifierTypes()
    {
        Assert.Equal(typeof(Guid), typeof(AddCartItemRequest).GetProperty(nameof(AddCartItemRequest.ProductId))!.PropertyType);
        Assert.Equal(typeof(Guid?), typeof(AddCartItemRequest).GetProperty(nameof(AddCartItemRequest.ProductVariantId))!.PropertyType);
        Assert.Equal(typeof(Guid), typeof(CartItemOptionRequest).GetProperty(nameof(CartItemOptionRequest.OptionGroupId))!.PropertyType);
        Assert.Equal(typeof(Guid), typeof(CartItemOptionRequest).GetProperty(nameof(CartItemOptionRequest.OptionItemId))!.PropertyType);
        Assert.Equal(typeof(Guid), typeof(CustomerProductDetailsResponse).GetProperty(nameof(CustomerProductDetailsResponse.Id))!.PropertyType);
    }

    [Fact]
    public void CustomerMediaContractDoesNotExposeStorageOrProviderInternals()
    {
        string[] forbidden = ["StorageKey", "Provider", "ContentHash", "OwnerType", "OwnerId", "ConcurrencyStamp"];
        Assert.DoesNotContain(typeof(CustomerProductMediaResponse).GetProperties(), property => forbidden.Contains(property.Name, StringComparer.Ordinal));
    }
}
