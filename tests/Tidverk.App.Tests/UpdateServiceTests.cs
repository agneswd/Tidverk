using Microsoft.Extensions.Logging.Abstractions;
using Tidverk.App.Services;
using Velopack;
using Velopack.Locators;
using Velopack.Logging;
using Velopack.Sources;

namespace Tidverk.App.Tests;

public sealed class UpdateServiceTests {
    [Fact]
    public async Task Manual_check_reports_up_to_date_when_feed_is_empty() {
        string packagesDirectory = CreatePackagesDirectory();
        try {
            UpdateService service = CreateService(new StubUpdateSource());

            await service.CheckManuallyCommand.ExecuteAsync(null);

            Assert.True(service.IsUpToDate);
            Assert.False(service.IsSidebarVisible);
            Assert.True(service.CanCheck);
        }
        finally {
            Directory.Delete(packagesDirectory, recursive: true);
        }

        UpdateService CreateService(IUpdateSource source) => new(
            new UpdateManager(source, options: null, new TestVelopackLocator("Tidverk", "0.2.0", packagesDirectory)),
            NullLogger<UpdateService>.Instance);
    }

    [Fact]
    public async Task Manual_check_surfaces_feed_failure_and_allows_retry() {
        string packagesDirectory = CreatePackagesDirectory();
        try {
            UpdateService service = new(
                new UpdateManager(new StubUpdateSource(new InvalidOperationException("Feed unavailable")), options: null,
                    new TestVelopackLocator("Tidverk", "0.2.0", packagesDirectory)),
                NullLogger<UpdateService>.Instance);

            await service.CheckManuallyCommand.ExecuteAsync(null);

            Assert.True(service.IsFailed);
            Assert.True(service.IsSidebarVisible);
            Assert.True(service.CanCheck);
            Assert.Equal("Try again. Details were written to the local log.", service.ErrorMessage);
        }
        finally {
            Directory.Delete(packagesDirectory, recursive: true);
        }
    }

    private static string CreatePackagesDirectory() {
        string path = Path.Combine(Path.GetTempPath(), $"tidverk-update-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class StubUpdateSource(Exception? exception = null) : IUpdateSource {
        public Task<VelopackAssetFeed> GetReleaseFeed(
            IVelopackLogger logger,
            string? appId,
            string channel,
            Guid? stagingId = null,
            VelopackAsset? latestLocalRelease = null) => exception is null
                ? Task.FromResult(new VelopackAssetFeed { Assets = [] })
                : Task.FromException<VelopackAssetFeed>(exception);

        public Task DownloadReleaseEntry(
            IVelopackLogger logger,
            VelopackAsset releaseEntry,
            string localFile,
            Action<int> progress,
            CancellationToken cancelToken = default) =>
            throw new InvalidOperationException("The empty test feed must not download an update.");
    }
}
