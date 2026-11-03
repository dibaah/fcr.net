using FCR.Domain;
using FCR.Infrastructure;
using FCR.Infrastructure.Attachments;
using Microsoft.Extensions.Options;

namespace FCR.Tests;

public sealed class AttachmentStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"fcr_attachment_test_{Guid.NewGuid():N}");
    private readonly AttachmentStore _store;

    public AttachmentStoreTests()
    {
        Directory.CreateDirectory(_dir);
        _store = new AttachmentStore(Options.Create(new FcrOptions
        {
            DataDir = _dir,
            RequestAttachmentDir = Path.Combine(_dir, "request-attachments"),
        }));
    }

    [Fact]
    public void Resolve_RejectsPathTraversalOutsideRoot()
    {
        Assert.Throws<FcrValidationException>(() => _store.Resolve("../outside.txt"));
        Assert.Throws<FcrValidationException>(() => _store.Resolve("request/../../outside.txt"));
    }

    [Fact]
    public void SanitizeFileName_RemovesClientPathComponents()
    {
        Assert.Equal("document.pdf", AttachmentStore.SanitizeFileName("../../folder/document.pdf"));
        Assert.Equal("document.pdf", AttachmentStore.SanitizeFileName(@"C:\\fakepath\\document.pdf"));
    }

    [Fact]
    public async Task SaveAsync_OverLimitFile_RemovesPartialUpload()
    {
        var requestDir = Path.Combine(_store.RootDir, "request-1");
        using var content = new MemoryStream(new byte[(int)AttachmentStore.MaxFileBytes + 1]);

        await Assert.ThrowsAsync<FcrValidationException>(() =>
            _store.SaveAsync("request-1", "too-large.bin", content, CancellationToken.None));

        Assert.True(Directory.Exists(requestDir));
        Assert.Empty(Directory.GetFiles(requestDir));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}
