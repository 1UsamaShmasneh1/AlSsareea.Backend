using System.Net;
using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Identity.Domain;

namespace AlSsareea.UnitTests.Identity;

public sealed class LifecycleTests
{
    private static readonly DateTime Now = new(2026, 7, 21, 8, 0, 0, DateTimeKind.Utc);
    [Fact] public void DeviceLifecycleWorks() { Device device = Device.Register(DeviceId.New(), UserId.New(), new DeviceIdentifier("device-identifier"), DevicePlatform.Android, "Phone", "1.0", "15", Now); device.MarkTrusted(Now.AddMinutes(1)); device.UpdateLastSeen(Now.AddMinutes(2)); device.Revoke(Now.AddMinutes(3)); Assert.True(device.IsRevoked); Assert.False(device.IsTrusted); }
    [Fact] public void RevokedDeviceCannotBeUpdated() { Device device = Device.Register(DeviceId.New(), UserId.New(), new DeviceIdentifier("device-identifier"), DevicePlatform.Android, null, null, null, Now); device.Revoke(Now); Assert.Throws<DomainException>(() => device.UpdateLastSeen(Now.AddMinutes(1))); }
    [Fact] public void SessionLifecycleSupportsExpireRevokeAndLogout() { LoginSession expired = NewSession(); expired.Expire(Now.AddMinutes(5)); LoginSession revoked = NewSession(); revoked.Revoke(SessionEndReason.AdministratorRevoked, Now.AddMinutes(5)); LoginSession loggedOut = NewSession(); loggedOut.Logout(Now.AddMinutes(5)); Assert.Equal(SessionState.Expired, expired.State); Assert.Equal(SessionState.Revoked, revoked.State); Assert.Equal(SessionState.LoggedOut, loggedOut.State); }
    [Fact] public void SessionRejectsInvalidDatesAndRepeatedEnd() { Assert.Throws<DomainException>(() => LoginSession.Start(LoginSessionId.New(), UserId.New(), null, Now, Now, null, null)); LoginSession session = NewSession(); session.Logout(Now); Assert.Throws<DomainException>(() => session.Logout(Now)); }
    [Fact] public void RefreshTokenLifecycleSupportsConsumeRevokeAndReplace() { RefreshToken consumed = NewToken(); consumed.Consume(Now.AddMinutes(1)); RefreshToken revoked = NewToken(); revoked.Revoke("security event", Now.AddMinutes(1), IPAddress.Loopback); RefreshToken replaced = NewToken(); RefreshTokenId replacement = RefreshTokenId.New(); replaced.Replace(replacement, Now.AddMinutes(1)); Assert.NotNull(consumed.ConsumedUtc); Assert.NotNull(revoked.RevokedUtc); Assert.Equal(replacement, replaced.ReplacedByTokenId); }
    [Fact] public void RefreshTokenRejectsInvalidDatesAndSelfReplacement() { Assert.Throws<DomainException>(() => RefreshToken.Create(RefreshTokenId.New(), UserId.New(), null, LoginSessionId.New(), new RefreshTokenHash(new string('a', 64)), Guid.NewGuid(), Now, Now)); RefreshToken token = NewToken(); Assert.Throws<DomainException>(() => token.Replace(token.Id, Now)); }
    [Fact] public void PasswordHistoryValidatesReplacementDate() => Assert.Throws<DomainException>(() => PasswordHistory.Create(PasswordHistoryId.New(), UserId.New(), new PasswordHash("argon2id$v=19$example-hash-material"), Now, Now.AddMinutes(-1), Now));
    [Fact] public void LoginHistoryEnforcesSuccessConsistency() => Assert.Throws<DomainException>(() => LoginHistory.Create(LoginHistoryId.New(), UserId.New(), null, LoginSessionId.New(), new string('a', 64), LoginResult.Succeeded, LoginFailureReason.InvalidCredentials, null, null, Now, "corr"));
    [Fact] public void LoginHistoryEnforcesFailureConsistencyAndHashing() { Assert.Throws<DomainException>(() => LoginHistory.Create(LoginHistoryId.New(), null, null, LoginSessionId.New(), "raw@email.com", LoginResult.Failed, LoginFailureReason.InvalidCredentials, null, null, Now, "corr")); LoginHistory item = LoginHistory.Create(LoginHistoryId.New(), null, null, null, new string('b', 64), LoginResult.Failed, LoginFailureReason.InvalidCredentials, null, null, Now, "corr"); Assert.Null(item.LoginSessionId); }
    private static LoginSession NewSession() => LoginSession.Start(LoginSessionId.New(), UserId.New(), null, Now, Now.AddHours(1));
    private static RefreshToken NewToken() => RefreshToken.Create(RefreshTokenId.New(), UserId.New(), null, LoginSessionId.New(), new RefreshTokenHash(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")), Guid.NewGuid(), Now, Now.AddDays(1));
}
