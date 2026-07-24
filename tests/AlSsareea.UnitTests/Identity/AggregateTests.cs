using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Identity.Domain;

namespace AlSsareea.UnitTests.Identity;

public sealed class AggregateTests
{
    private static readonly DateTime Now = new(2026, 7, 21, 8, 0, 0, DateTimeKind.Utc);
    private static readonly PasswordHash Hash = new("argon2id$v=19$example-hash-material");
    private static User CreateUser() => User.Create(UserId.New(), UserType.Customer, new Email("person@example.com"), null, Hash, Now);

    [Fact] public void UserCreationRequiresContact() => Assert.Throws<DomainException>(() => User.Create(UserId.New(), UserType.Customer, null, null, Hash, Now));
    [Fact] public void UserStartsPendingWithSecurityAndConcurrencyStamps() { User user = CreateUser(); Assert.Equal(UserStatus.PendingActivation, user.Status); Assert.NotEqual(Guid.Empty, user.SecurityStamp); Assert.NotEqual(Guid.Empty, user.ConcurrencyStamp); Assert.IsType<UserCreatedDomainEvent>(Assert.Single(user.DomainEvents)); }
    [Fact] public void UserValidStateTransitionsChangeStamps() { User user = CreateUser(); Guid security = user.SecurityStamp; Guid concurrency = user.ConcurrencyStamp; user.Activate(Now.AddMinutes(1)); user.Suspend(Now.AddMinutes(2)); Assert.Equal(UserStatus.Suspended, user.Status); Assert.NotEqual(security, user.SecurityStamp); Assert.NotEqual(concurrency, user.ConcurrencyStamp); }
    [Fact] public void UserRejectsInvalidStateTransition() => Assert.Throws<DomainException>(() => CreateUser().Suspend(Now.AddMinutes(1)));
    [Fact] public void UserSoftDeleteRetainsContactAndMarksDeletion() { User user = CreateUser(); user.SoftDelete(Now.AddMinutes(1)); Assert.Equal(UserStatus.Deleted, user.Status); Assert.NotNull(user.Email); Assert.Equal(Now.AddMinutes(1), user.DeletedUtc); }
    [Fact] public void UserChangesEmailAndPhone() { User user = CreateUser(); user.ChangePhoneNumber(new PhoneNumber("+970500000001"), Now.AddMinutes(1)); user.ChangeEmail(new Email("new@example.com"), Now.AddMinutes(2)); Assert.Equal("new@example.com", user.NormalizedEmail); Assert.Equal("+970500000001", user.NormalizedPhoneNumber); }
    [Fact] public void UserCannotRemoveLastContact() => Assert.Throws<DomainException>(() => CreateUser().ChangeEmail(null, Now.AddMinutes(1)));
    [Fact] public void UserRoleAssignmentRejectsDuplicatesAndSupportsRemoval() { User user = CreateUser(); RoleId role = RoleId.New(); user.AssignRole(role, Now); Assert.Throws<DomainException>(() => user.AssignRole(role, Now)); user.RemoveRole(role, Now); Assert.Empty(user.Roles); }
    [Fact] public void PasswordChangeStoresHistoryAndRotatesSecurityStamp() { User user = CreateUser(); Guid stamp = user.SecurityStamp; user.ChangePassword(new PasswordHash("argon2id$v=19$second-hash-material"), Now.AddDays(1)); Assert.Single(user.PasswordHistory); Assert.NotEqual(stamp, user.SecurityStamp); }
    [Fact] public void RoleNormalizesAndManagesPermissions() { Role role = Role.Create(RoleId.New(), " Operators ", null, false, Now); PermissionId permission = PermissionId.New(); role.AssignPermission(permission, Now); Assert.Equal("OPERATORS", role.NormalizedName); Assert.Throws<DomainException>(() => role.AssignPermission(permission, Now)); role.RemovePermission(permission, Now); Assert.Empty(role.Permissions); }
    [Fact] public void SystemRoleCannotBeRenamedOrDeleted() { Role role = Role.Create(RoleId.New(), "System", null, true, Now); Assert.Throws<DomainException>(() => role.Rename("Changed", Now)); Assert.Throws<DomainException>(() => role.Delete(Now)); }
    [Fact] public void PermissionRequiresLowercaseDottedName() => Assert.Throws<DomainException>(() => Permission.Create(PermissionId.New(), "Identity.Users.Read", "Read", null, "identity", true, Now));
    [Fact] public void SystemPermissionCannotBeDeactivated() { Permission permission = Permission.Create(PermissionId.New(), "identity.users.read", "Read users", null, "identity", true, Now); Assert.Throws<DomainException>(() => permission.Deactivate(Now)); }
}
