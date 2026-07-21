using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Identity.Domain;

namespace AlSsareea.UnitTests.Identity;

public sealed class ValueObjectAndIdTests
{
    [Fact] public void IdentityIdsRejectEmptyGuid() => Assert.Throws<ArgumentException>(() => new UserId(Guid.Empty));
    [Fact] public void IdentityIdsUseValueEquality() { Guid value = Guid.NewGuid(); Assert.Equal(new UserId(value), new UserId(value)); }
    [Fact] public void DifferentIdentityIdTypesAreNotEqual() { Guid value = Guid.NewGuid(); Assert.False(new UserId(value).Equals(new RoleId(value))); }
    [Fact] public void EmailTrimsNormalizesAndUsesValueEquality() { var first = new Email(" Person@Example.COM "); var second = new Email("Person@Example.COM"); Assert.Equal("Person@Example.COM", first.Value); Assert.Equal("person@example.com", first.Normalized); Assert.Equal(first, second); }
    [Theory][InlineData("")][InlineData("missing-at.example.com")][InlineData("missing-domain@")] public void EmailRejectsInvalidValues(string value) => Assert.Throws<DomainException>(() => new Email(value));
    [Fact] public void PhoneNumberAcceptsE164AndUsesValueEquality() => Assert.Equal(new PhoneNumber("+970500000000"), new PhoneNumber(" +970500000000 "));
    [Theory][InlineData("050-000-0000")][InlineData("+0123456789")][InlineData("+123")] public void PhoneNumberRejectsInvalidValues(string value) => Assert.Throws<DomainException>(() => new PhoneNumber(value));
    [Fact] public void SecretHashesAreRedacted() { Assert.Equal("[REDACTED]", new PasswordHash("argon2id$v=19$not-a-raw-password").ToString()); Assert.Equal("[REDACTED]", new RefreshTokenHash(new string('a', 64)).ToString()); }
    [Fact] public void RefreshTokenHashRequiresSha256Hex() => Assert.Throws<DomainException>(() => new RefreshTokenHash("raw-token"));
    [Fact] public void DeviceIdentifierEnforcesLength() => Assert.Throws<DomainException>(() => new DeviceIdentifier("short"));
}
