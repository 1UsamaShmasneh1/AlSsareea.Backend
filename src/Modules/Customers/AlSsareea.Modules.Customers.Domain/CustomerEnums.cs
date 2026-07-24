namespace AlSsareea.Modules.Customers.Domain;

public enum CustomerStatus : short
{
    Active = 1,
    Suspended = 2,
    Blocked = 3,
    Deleted = 4,
}

public enum CustomerAddressType : short
{
    Home = 1,
    Work = 2,
    Other = 3,
}
