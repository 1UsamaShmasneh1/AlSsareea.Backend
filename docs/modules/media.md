# Media module

Media is the Phase 8 owner of image files and their technical metadata. Its Domain,
Application, Contracts, and Infrastructure projects follow the same inward dependency
direction as the other modules. `MediaDbContext` owns PostgreSQL schema `media`, its own
migration history, `media_assets`, and `media_variants`. There are no cross-schema foreign
keys and Media never reads another module's DbContext.

## Storage and lifecycle

The current `IMediaStorage` implementation writes to a configured relative directory below
the API application base directory. It generates opaque keys, rejects path traversal, and publishes writes
atomically through a temporary file. PostgreSQL stores only metadata, ownership, hashes,
status, and keys. The abstraction is intentionally suitable for a future object-storage
provider; local paths are never returned by the API.

Assets transition from `Pending` to `Processing`, then `Ready` or `Failed`, and may become
`Deleted`. Invalid transitions are rejected and mutations rotate an optimistic concurrency
stamp. Uploads first validate and decode the image, calculate SHA-256, persist lifecycle
metadata, store the original, generate variants, and finally mark the asset ready. Failures
mark the asset failed and make a best-effort removal of written files.

Deletion is idempotent and soft-deletes metadata while attempting physical removal.
`IMediaCleanupService` provides bounded cleanup for deleted and expired failed assets. No
background scheduler is introduced in this phase; an operations host can invoke that
contract later.

## Validation and processing

Only JPEG, PNG, and WebP are accepted. Validation checks the safe base filename and
extension, declared MIME type and length, magic bytes, successful ImageSharp decoding,
configured byte/dimension/pixel limits, and dangerous double extensions. SVG, executables,
remote URL imports, and user-selected storage paths are rejected. Derived WebP
`thumbnail`, `small`, `medium`, and `large` variants preserve aspect ratio, auto-orient the
image, and remove EXIF, ICC, and XMP profiles.

The configurable defaults are 10 MB, 8000 by 8000 pixels, 40 million total pixels,
160/480/960/1600 variant bounds, and WebP quality 82. ImageSharp is the single new package
because the repository previously had no safe image decoder or resizer. The registered
malware scanner is explicitly a `NoOp` seam and does not provide real malware detection.

## API, ownership, and Catalog

Management upload and delete endpoints require dynamic Media permissions and then validate
merchant scope through `IMerchantCatalogScopeProvider`. Private/internal content is returned
only to an authorized merchant actor and otherwise appears not found. Public ready content
supports range requests, ETags, and immutable cache headers.

An upload carries a merchant ID plus generic `OwnerType` and `OwnerId`; Media does not
reference Catalog aggregate types. Catalog consumes only `IMediaAssetLookup`. Before a
product image or optional category image is linked, it verifies that the asset is ready,
not deleted, belongs to the same merchant, and has the expected owner tuple. Catalog stores
the media ID and its own ordering/primary-image business data without a database foreign key.

## Configuration and local development

Configure `ConnectionStrings:MediaDatabase` and the `Media` section. `Media:StorageRoot`
must be relative; absolute or invalid settings fail startup validation.

```powershell
$mediaProject = ".\src\Modules\Media\AlSsareea.Modules.Media.Infrastructure\AlSsareea.Modules.Media.Infrastructure.csproj"
dotnet ef database update --project $mediaProject --context MediaDbContext
dotnet ef migrations has-pending-model-changes --project $mediaProject --context MediaDbContext
dotnet test tests/AlSsareea.UnitTests/AlSsareea.UnitTests.csproj
dotnet test tests/AlSsareea.ArchitectureTests/AlSsareea.ArchitectureTests.csproj
dotnet test tests/AlSsareea.IntegrationTests/AlSsareea.IntegrationTests.csproj
```

Known limitations are local storage, no CDN or signed private URLs, no real malware engine,
no distributed transaction between PostgreSQL and the filesystem, and no background
scheduler. The failure lifecycle and cleanup seam make partial failures observable and
recoverable while those capabilities remain future work.
