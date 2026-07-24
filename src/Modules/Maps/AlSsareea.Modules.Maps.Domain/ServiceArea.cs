using AlSsareea.BuildingBlocks.Domain;
using NetTopologySuite.Geometries;

namespace AlSsareea.Modules.Maps.Domain;

public sealed class ServiceArea : AggregateRoot<ServiceAreaId>
{
    public const int MaximumNameLength = 200;
    public const int MaximumDescriptionLength = 2000;

    private ServiceArea(
        ServiceAreaId id,
        string name,
        string? description,
        MultiPolygon boundary,
        DateTime createdAtUtc)
        : base(id)
    {
        Name = name;
        Description = description;
        Boundary = boundary;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    private ServiceArea()
        : base(default)
    {
        Name = string.Empty;
        Boundary = null!;
    }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public MultiPolygon Boundary { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public static ServiceArea Create(
        ServiceAreaId id,
        string name,
        string? description,
        MultiPolygon boundary,
        DateTime createdAtUtc)
    {
        string validatedName = ValidateName(name);
        string? validatedDescription = ValidateDescription(description);
        MultiPolygon validatedBoundary = ValidateAndCopyBoundary(boundary);
        ValidateUtc(createdAtUtc, "Creation time");

        return new ServiceArea(
            id,
            validatedName,
            validatedDescription,
            validatedBoundary,
            createdAtUtc);
    }

    public void Rename(string name, DateTime updatedAtUtc)
    {
        Name = ValidateName(name);
        Touch(updatedAtUtc);
    }

    public void ChangeDescription(string? description, DateTime updatedAtUtc)
    {
        Description = ValidateDescription(description);
        Touch(updatedAtUtc);
    }

    public void ChangeBoundary(MultiPolygon boundary, DateTime updatedAtUtc)
    {
        Boundary = ValidateAndCopyBoundary(boundary);
        Touch(updatedAtUtc);
    }

    public void Activate(DateTime updatedAtUtc)
    {
        IsActive = true;
        Touch(updatedAtUtc);
    }

    public void Deactivate(DateTime updatedAtUtc)
    {
        IsActive = false;
        Touch(updatedAtUtc);
    }

    public bool Contains(GeoPoint point) => Boundary.Covers(point.ToPoint());

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Service area name cannot be empty.");
        }

        string trimmedName = name.Trim();
        if (trimmedName.Length > MaximumNameLength)
        {
            throw new DomainException($"Service area name cannot exceed {MaximumNameLength} characters.");
        }

        return trimmedName;
    }

    private static string? ValidateDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        string trimmedDescription = description.Trim();
        if (trimmedDescription.Length > MaximumDescriptionLength)
        {
            throw new DomainException(
                $"Service area description cannot exceed {MaximumDescriptionLength} characters.");
        }

        return trimmedDescription;
    }

    private static MultiPolygon ValidateAndCopyBoundary(MultiPolygon boundary)
    {
        ArgumentNullException.ThrowIfNull(boundary);

        if (boundary.IsEmpty)
        {
            throw new DomainException("Service area boundary cannot be empty.");
        }

        if (boundary.SRID != GeoPoint.SpatialReferenceId)
        {
            throw new DomainException(
                $"Service area boundary must use SRID {GeoPoint.SpatialReferenceId}.");
        }

        if (!boundary.IsValid)
        {
            throw new DomainException("Service area boundary geometry is invalid.");
        }

        foreach (Coordinate coordinate in boundary.Coordinates)
        {
            _ = GeoPoint.Create(coordinate.Y, coordinate.X);
        }

        return (MultiPolygon)boundary.Copy();
    }

    private void Touch(DateTime updatedAtUtc)
    {
        ValidateUtc(updatedAtUtc, "Update time");
        if (updatedAtUtc < CreatedAtUtc || updatedAtUtc < UpdatedAtUtc)
        {
            throw new DomainException("Update time cannot move backwards.");
        }

        UpdatedAtUtc = updatedAtUtc;
    }

    private static void ValidateUtc(DateTime value, string fieldName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new DomainException($"{fieldName} must be UTC.");
        }
    }
}
