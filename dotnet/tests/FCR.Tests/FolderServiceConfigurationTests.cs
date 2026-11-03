using FCR.Application;
using FCR.Infrastructure;
using FCR.Infrastructure.Folders;
using Microsoft.Extensions.Options;

namespace FCR.Tests;

public class FolderServiceConfigurationTests
{
    [Fact]
    public void HostConfiguredArchiveRootOverridesStandaloneSettings()
    {
        var settings = new StubSettingsStore(@"X:\\standalone");
        var service = new FolderService(settings,
            Options.Create(new FcrOptions { AttachmentRoot = @"\\server\\fcr-archive" }));

        Assert.Equal(@"\\server\\fcr-archive", service.AttachmentRoot);
    }

    [Fact]
    public void MissingHostOverrideUsesStandaloneSettings()
    {
        var settings = new StubSettingsStore(@"X:\\standalone");
        var service = new FolderService(settings, Options.Create(new FcrOptions()));

        Assert.Equal(@"X:\\standalone", service.AttachmentRoot);
    }

    private sealed class StubSettingsStore(string root) : ISettingsStore
    {
        public FcrSettings Load() => new() { AttachmentRoot = root };
        public void Save(FcrSettings settings) { }
        public void Reset() { }
        public Dictionary<string, object?> Payload() => new();
        public string ResolveUserPath(string? value, string defaultName) =>
            string.IsNullOrWhiteSpace(value) ? defaultName : value;
    }
}
