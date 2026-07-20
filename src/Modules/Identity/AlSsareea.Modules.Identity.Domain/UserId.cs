namespace AlSsareea.Modules.Identity.Domain;

public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
