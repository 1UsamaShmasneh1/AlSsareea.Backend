namespace AlSsareea.Modules.Identity.Domain;

public interface IIdentityId
{
    Guid Value { get; }
}

public readonly record struct UserId : IIdentityId
{
    public UserId(Guid value) { Value = IdentityId.Validate(value, nameof(UserId)); }
    public Guid Value { get; }
    public static UserId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct RoleId : IIdentityId
{
    public RoleId(Guid value) { Value = IdentityId.Validate(value, nameof(RoleId)); }
    public Guid Value { get; }
    public static RoleId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct PermissionId : IIdentityId
{
    public PermissionId(Guid value) { Value = IdentityId.Validate(value, nameof(PermissionId)); }
    public Guid Value { get; }
    public static PermissionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct DeviceId : IIdentityId
{
    public DeviceId(Guid value) { Value = IdentityId.Validate(value, nameof(DeviceId)); }
    public Guid Value { get; }
    public static DeviceId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct LoginSessionId : IIdentityId
{
    public LoginSessionId(Guid value) { Value = IdentityId.Validate(value, nameof(LoginSessionId)); }
    public Guid Value { get; }
    public static LoginSessionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct RefreshTokenId : IIdentityId
{
    public RefreshTokenId(Guid value) { Value = IdentityId.Validate(value, nameof(RefreshTokenId)); }
    public Guid Value { get; }
    public static RefreshTokenId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct PasswordHistoryId : IIdentityId
{
    public PasswordHistoryId(Guid value) { Value = IdentityId.Validate(value, nameof(PasswordHistoryId)); }
    public Guid Value { get; }
    public static PasswordHistoryId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct LoginHistoryId : IIdentityId
{
    public LoginHistoryId(Guid value) { Value = IdentityId.Validate(value, nameof(LoginHistoryId)); }
    public Guid Value { get; }
    public static LoginHistoryId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

internal static class IdentityId
{
    internal static Guid Validate(Guid value, string typeName) => value != Guid.Empty
        ? value
        : throw new ArgumentException($"{typeName} cannot be empty.", nameof(value));
}
