using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Media.Application;
using AlSsareea.Modules.Media.Infrastructure;
using Microsoft.Extensions.Options;

namespace AlSsareea.UnitTests.Media;

public sealed class ImageValidationTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public async Task ValidPngIsDecodedHashedAndCanProduceVariant()
    {
        var processor = new ImageSharpMediaProcessor(Options.Create(new MediaOptions()));
        await using var input = new MemoryStream(Png);
        var request = new MediaUploadRequest(input, "meal.png", "image/png", Png.Length, Guid.NewGuid(), "CatalogProduct", Guid.NewGuid(), "Public");

        ValidatedImage validated = await processor.ValidateAsync(request, CancellationToken.None);
        await using (validated.Content)
        {
            Assert.Equal(64, validated.Sha256Hash.Length);
            Assert.Equal(1, validated.Width);
            IReadOnlyList<ProcessedMediaVariant> variants = await processor.CreateVariantsAsync(validated, CancellationToken.None);
            ProcessedMediaVariant thumbnail = Assert.Single(variants);
            Assert.Equal("image/webp", thumbnail.MimeType);
            await thumbnail.Content.DisposeAsync();
        }
    }

    [Theory]
    [InlineData("meal.png.exe", "image/png")]
    [InlineData("meal.png", "image/jpeg")]
    public async Task DangerousNameOrMimeMismatchIsRejected(string fileName, string mime)
    {
        var processor = new ImageSharpMediaProcessor(Options.Create(new MediaOptions()));
        await using var input = new MemoryStream(Png);
        var request = new MediaUploadRequest(input, fileName, mime, Png.Length, Guid.NewGuid(), "CatalogProduct", Guid.NewGuid(), "Public");

        await Assert.ThrowsAsync<DomainException>(() => processor.ValidateAsync(request, CancellationToken.None));
    }
}
