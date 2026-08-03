using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Carts.Domain;

namespace AlSsareea.UnitTests.Carts;

public sealed class CartDomainTests
{
    private static readonly DateTime Now = new(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);
    [Fact]
    public void CreateProducesActiveExpiringCart()
    {
        Cart cart = Create();
        Assert.Equal(CartStatus.Active, cart.Status); Assert.Equal(Now.AddDays(30), cart.ExpiresAtUtc); Assert.NotEqual(Guid.Empty, cart.ConcurrencyStamp);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)]
    public void AddRejectsInvalidQuantity(int quantity) => Assert.Throws<DomainException>(() => Create().AddItem(Guid.NewGuid(), null, quantity, null, 1, [], Now));
    [Fact]
    public void EquivalentConfigurationsMerge()
    {
        Cart cart = Create(); Guid product = Guid.NewGuid(); Guid group = Guid.NewGuid(); Guid option = Guid.NewGuid();
        cart.AddItem(product, null, 2, "note", 1, [new(group, option, 1, 1)], Now);
        cart.AddItem(product, null, 3, "note", 1, [new(group, option, 1, 1)], Now.AddMinutes(1));
        Assert.Single(cart.Items); Assert.Equal(5, cart.Items.Single().Quantity);
    }
    [Fact]
    public void DifferentNotesRemainSeparate()
    {
        Cart cart = Create(); Guid product = Guid.NewGuid();
        cart.AddItem(product, null, 1, "one", 1, [], Now); cart.AddItem(product, null, 1, "two", 1, [], Now);
        Assert.Equal(2, cart.Items.Count);
    }
    [Fact]
    public void OptionsAreNormalized()
    {
        Cart cart = Create(); Guid product = Guid.NewGuid(); Guid first = Guid.Parse("00000000-0000-0000-0000-000000000001"); Guid second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        CartItem item = cart.AddItem(product, null, 1, null, 1, [new(second, second, 1, 1), new(first, first, 1, 1)], Now);
        Assert.Equal(first, item.SelectedOptions.First().OptionGroupId);
    }
    [Fact]
    public void CouponIsNormalizedAndReplaceable()
    {
        Cart cart = Create(); cart.ApplyCoupon(" save10 ", Now); cart.ApplyCoupon("other", Now);
        Assert.Equal("OTHER", cart.CouponCode); cart.RemoveCoupon(Now); Assert.Null(cart.CouponCode);
    }
    [Fact]
    public void ExpiredCartCannotBeMutated()
    {
        Cart cart = Create(); Assert.True(cart.ExpireIfNeeded(Now.AddDays(31)));
        Assert.Throws<DomainException>(() => cart.ApplyCoupon("SAVE", Now.AddDays(31))); Assert.Equal(CartStatus.Expired, cart.Status);
    }
    [Fact]
    public void ClearedCartCannotBeMutated()
    {
        Cart cart = Create(); cart.Clear(Now);
        Assert.Throws<DomainException>(() => cart.AddItem(Guid.NewGuid(), null, 1, null, 1, [], Now)); Assert.Equal(CartStatus.Cleared, cart.Status);
    }
    private static Cart Create() => Cart.Create(CartId.New(), Guid.NewGuid(), Guid.NewGuid(), null, Now, TimeSpan.FromDays(30));
}
