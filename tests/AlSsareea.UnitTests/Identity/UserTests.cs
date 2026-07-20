using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Identity.Domain;

namespace AlSsareea.UnitTests.Identity;

public sealed class UserTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 7, 20, 8, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateWithValidValuesCreatesUser()
    {
        UserId id = UserId.New();

        User user = User.Create(id, "+970500000000", PreferredLanguage.Arabic, CreatedAtUtc);

        Assert.Equal(id, user.Id);
        Assert.Equal("+970500000000", user.PhoneNumber);
        Assert.Equal(PreferredLanguage.Arabic, user.PreferredLanguage);
        Assert.Equal(CreatedAtUtc, user.CreatedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateWithEmptyPhoneNumberThrowsDomainException(string phoneNumber)
    {
        Action action = () => User.Create(
            UserId.New(),
            phoneNumber,
            PreferredLanguage.Arabic,
            CreatedAtUtc);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void CreateWithUnsupportedLanguageThrowsDomainException()
    {
        const PreferredLanguage unsupportedLanguage = (PreferredLanguage)999;

        Action action = () => User.Create(
            UserId.New(),
            "+970500000000",
            unsupportedLanguage,
            CreatedAtUtc);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void CreateSetsStatusToPendingVerification()
    {
        User user = User.Create(
            UserId.New(),
            "+970500000000",
            PreferredLanguage.English,
            CreatedAtUtc);

        Assert.Equal(UserStatus.PendingVerification, user.Status);
    }
}
