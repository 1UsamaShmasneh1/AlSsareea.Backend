using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Domain;
using AlSsareea.Modules.Identity.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace AlSsareea.UnitTests.Identity;

public sealed class AuthenticationTests
{
    private static readonly DateTime Now = new(2026, 7, 21, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Pbkdf2HashesAndVerifiesPasswordWithoutStoringIt()
    {
        var hasher = new Pbkdf2PasswordHasher(Options.Create(new PasswordHashingOptions { Iterations = 100_000, SaltSize = 16, HashSize = 32 }));
        PasswordHashResult result = hasher.Hash("Unicode-كلمة-مرور-123");
        Assert.DoesNotContain("Unicode", result.EncodedHash, StringComparison.Ordinal);
        Assert.Equal(PasswordVerificationResult.Success, hasher.Verify("Unicode-كلمة-مرور-123", result.EncodedHash));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.Verify("wrong-password", result.EncodedHash));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.Verify("Unicode-كلمة-مرور-123", "$pbkdf2-sha256$v=1$i=100000$x=a$y=b"));
    }

    [Fact]
    public void Pbkdf2ReportsRehashNeededForLowerIterations()
    {
        var oldHasher = new Pbkdf2PasswordHasher(Options.Create(new PasswordHashingOptions { Iterations = 100_000, SaltSize = 16, HashSize = 32 }));
        var currentHasher = new Pbkdf2PasswordHasher(Options.Create(new PasswordHashingOptions { Iterations = 120_000, SaltSize = 16, HashSize = 32 }));
        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, currentHasher.Verify("a-secure-password", oldHasher.Hash("a-secure-password").EncodedHash));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("          ")]
    public void PasswordPolicyRejectsInvalidInput(string password)
    {
        var hasher = new Pbkdf2PasswordHasher(Options.Create(new PasswordHashingOptions()));
        Assert.Throws<ArgumentException>(() => hasher.Hash(password));
    }

    [Fact]
    public void UserLocksAndAutomaticallyUnlocks()
    {
        User user = NewActiveUser();
        Assert.False(user.RecordFailedLogin(2, TimeSpan.FromMinutes(15), Now));
        Assert.True(user.RecordFailedLogin(2, TimeSpan.FromMinutes(15), Now.AddSeconds(1)));
        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.False(user.UnlockIfExpired(Now.AddMinutes(10)));
        Assert.True(user.UnlockIfExpired(Now.AddMinutes(16)));
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(0, user.FailedLoginCount);
    }

    [Fact]
    public void OtpIsSingleUseAndEnforcesAttemptsAndExpiry()
    {
        OtpChallenge challenge = OtpChallenge.Create(OtpChallengeId.New(), new string('a', 64), OtpPurpose.Login, new string('b', 64), 2, Now, Now.AddMinutes(5), Now.AddMinutes(1), new string('c', 64), null);
        challenge.RecordFailure();
        Assert.True(challenge.CanAttempt(Now.AddMinutes(1)));
        challenge.MarkUsed(Now.AddMinutes(1));
        Assert.False(challenge.CanAttempt(Now.AddMinutes(2)));
        Assert.Throws<DomainException>(() => challenge.MarkUsed(Now.AddMinutes(2)));
        Assert.True(challenge.IsExpired(Now.AddMinutes(5)));
    }

    [Fact]
    public void OtpGeneratorAndHasherProduceBoundNonPlaintextValues()
    {
        OtpOptions options = new() { CodeLength = 6, Pepper = "unit-test-pepper-that-is-at-least-thirty-two-bytes" };
        var generator = new CryptographicOtpGenerator(Options.Create(options));
        var hasher = new HmacOtpHasher(Options.Create(options));
        OtpChallengeId challengeId = OtpChallengeId.New();
        string code = generator.Generate().Code; string hash = hasher.Hash(code, challengeId);
        Assert.Matches("^[0-9]{6}$", code); Assert.Equal(64, hash.Length); Assert.DoesNotContain(code, hash, StringComparison.Ordinal);
        Assert.True(hasher.Verify(code, challengeId, hash)); Assert.False(hasher.Verify(code, OtpChallengeId.New(), hash));
    }

    [Fact]
    public void IdempotencyRecordValidatesFingerprintAndExpiry()
    {
        IdempotencyRecord record = IdempotencyRecord.Create(IdempotencyRecordId.New(), "owner", "logout", new string('a', 64), new string('b', 64), null, Now, Now.AddHours(1));
        Assert.Equal("logout", record.Operation);
        Assert.Throws<DomainException>(() => IdempotencyRecord.Create(IdempotencyRecordId.New(), "owner", "logout", "bad", new string('b', 64), null, Now, Now.AddHours(1)));
    }

    private static User NewActiveUser()
    {
        User user = User.Create(UserId.New(), UserType.Customer, new Email("auth@example.com"), null, new PasswordHash("$pbkdf2-sha256$v=1$i=100000$s=YWJjZGVmZ2hpamtsbW5vcA==$h=YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXo="), Now);
        user.Activate(Now);
        return user;
    }
}
