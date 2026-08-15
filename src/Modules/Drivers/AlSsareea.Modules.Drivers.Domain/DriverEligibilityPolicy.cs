using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Drivers.Domain;

public static class DriverEligibilityPolicy
{
    private static readonly IReadOnlyList<DocumentType> CurrentVehicleDocumentRequirements = Array.AsReadOnly<DocumentType>(
    [
        DocumentType.DrivingLicense,
        DocumentType.VehicleRegistration,
        DocumentType.VehicleInsurance,
    ]);

    public static IReadOnlyList<DocumentType> RequiredDocuments(VehicleType vehicleType) => vehicleType switch
    {
        VehicleType.Bicycle => CurrentVehicleDocumentRequirements,
        VehicleType.Motorcycle => CurrentVehicleDocumentRequirements,
        VehicleType.Car => CurrentVehicleDocumentRequirements,
        VehicleType.Van => CurrentVehicleDocumentRequirements,
        VehicleType.Truck => CurrentVehicleDocumentRequirements,
        _ => throw new DomainException("Vehicle type is not supported by the eligibility policy."),
    };

    public static void EnsureCanGoOnline(Driver driver, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(driver);
        Driver.RequireUtc(now);

        if (!driver.IsOperationallyActiveAt(now) || driver.ActivationStatus != DriverActivationStatus.Approved)
            throw new DomainException("Driver is not active and approved.");
        if (driver.HasActiveSuspension(now))
            throw new DomainException("Suspended driver cannot go online.");
        if (!driver.ZoneAssignments.Any(x => x.IsActive))
            throw new DomainException("An active service area is required.");

        Vehicle[] primaryVehicles = driver.Vehicles.Where(x => x.IsPrimary && x.Status == VehicleStatus.Active).ToArray();
        if (primaryVehicles.Length != 1)
            throw new DomainException("Exactly one active primary vehicle is required.");

        IReadOnlyList<DocumentType> requiredDocuments = RequiredDocuments(primaryVehicles[0].Type);
        bool hasAllRequiredDocuments = requiredDocuments.All(type => driver.Documents.Any(document =>
            document.Type == type &&
            document.Status == DocumentStatus.Approved &&
            (!document.ExpiresAtUtc.HasValue || document.ExpiresAtUtc.Value > now)));

        if (!hasAllRequiredDocuments)
            throw new DomainException("Required approved documents are missing or expired.");
    }
}
