namespace AlSsareea.Modules.Maps.Domain;

public readonly record struct ServiceAreaId(Guid Value)
{
    public static ServiceAreaId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
