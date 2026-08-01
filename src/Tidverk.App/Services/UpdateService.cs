using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace Tidverk.App.Services;

public enum UpdateStatus {
    Unavailable,
    Idle,
    Checking,
    UpToDate,
    Downloading,
    Ready,
    Failed
}

public sealed partial class UpdateService : ObservableObject {
    public const string RepositoryUrl = "https://github.com/agneswd/Tidverk";

    private readonly UpdateManager? manager;
    private readonly ILogger<UpdateService>? logger;
    private int isRunning;
    private UpdateInfo? downloadedUpdate;
    private UpdateStatus status;
    private int downloadProgress;
    private string? availableVersion;
    private string? errorMessage;
    private bool isReadyNotificationVisible;
    private bool showFailure;

    public UpdateService(ILogger<UpdateService> logger)
        : this(new UpdateManager(new GithubSource(RepositoryUrl, null, prerelease: false)), logger) {
    }

    internal UpdateService(UpdateManager manager, ILogger<UpdateService> logger) {
        this.manager = manager;
        this.logger = logger;
        status = manager.IsInstalled ? UpdateStatus.Idle : UpdateStatus.Unavailable;
        CurrentVersion = manager.CurrentVersion?.ToString() ?? AssemblyVersion();
    }

    internal UpdateService() {
        status = UpdateStatus.Unavailable;
        CurrentVersion = AssemblyVersion();
    }

    public static UpdateService Unavailable { get; } = new();

    public string CurrentVersion { get; }

    public UpdateStatus Status {
        get => status;
        private set {
            if (SetProperty(ref status, value)) {
                NotifyStatusProperties();
            }
        }
    }

    public int DownloadProgress {
        get => downloadProgress;
        private set {
            if (SetProperty(ref downloadProgress, value)) {
                OnPropertyChanged(nameof(DownloadProgressText));
            }
        }
    }

    public string DownloadProgressText => $"{DownloadProgress}%";

    public string? AvailableVersion {
        get => availableVersion;
        private set => SetProperty(ref availableVersion, value);
    }

    public string? ErrorMessage {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    public bool IsReadyNotificationVisible {
        get => isReadyNotificationVisible;
        private set => SetProperty(ref isReadyNotificationVisible, value);
    }

    public bool IsEnabled => manager?.IsInstalled == true;

    public bool IsChecking => Status == UpdateStatus.Checking;

    public bool IsUpToDate => Status == UpdateStatus.UpToDate;

    public bool IsUnavailable => Status == UpdateStatus.Unavailable;

    public bool IsDownloading => Status == UpdateStatus.Downloading;

    public bool IsReady => Status == UpdateStatus.Ready;

    public bool IsFailed => Status == UpdateStatus.Failed;

    public bool IsSidebarVisible => IsDownloading || IsReady || (IsFailed && showFailure);

    public bool CanCheck => IsEnabled && Status is not UpdateStatus.Checking and not UpdateStatus.Downloading and not UpdateStatus.Ready;

    public Task CheckAutomaticallyAsync(CancellationToken cancellationToken = default) =>
        CheckAndDownloadAsync(showErrors: false, cancellationToken);

    [RelayCommand]
    private Task CheckManuallyAsync() => CheckAndDownloadAsync(showErrors: true, CancellationToken.None);

    [RelayCommand]
    private void RestartAndUpdate() {
        if (manager is null || Status != UpdateStatus.Ready) {
            return;
        }

        try {
            manager.ApplyUpdatesAndRestart(downloadedUpdate?.TargetFullRelease ?? manager.UpdatePendingRestart);
        }
        catch (Exception exception) {
            logger?.LogError(exception, "Applying the downloaded Tidverk update failed");
            ErrorMessage = exception.Message;
            showFailure = true;
            Status = UpdateStatus.Failed;
        }
    }

    [RelayCommand]
    private void DismissReadyNotification() => IsReadyNotificationVisible = false;

    private async Task CheckAndDownloadAsync(bool showErrors, CancellationToken cancellationToken) {
        if (manager is null || !manager.IsInstalled || Interlocked.CompareExchange(ref isRunning, 1, 0) != 0) {
            return;
        }

        try {
            showFailure = false;
            ErrorMessage = null;

            if (manager.UpdatePendingRestart is { } pendingUpdate) {
                AvailableVersion = pendingUpdate.Version.ToString();
                IsReadyNotificationVisible = true;
                Status = UpdateStatus.Ready;
                return;
            }

            Status = UpdateStatus.Checking;
            UpdateInfo? update = await manager.CheckForUpdatesAsync().ConfigureAwait(true);
            if (update is null) {
                Status = UpdateStatus.UpToDate;
                return;
            }

            downloadedUpdate = update;
            AvailableVersion = update.TargetFullRelease.Version.ToString();
            DownloadProgress = 0;
            Status = UpdateStatus.Downloading;

            IProgress<int> progress = new Progress<int>(value => DownloadProgress = Math.Clamp(value, 0, 100));
            await manager.DownloadUpdatesAsync(update, progress.Report, cancellationToken).ConfigureAwait(true);

            DownloadProgress = 100;
            IsReadyNotificationVisible = true;
            Status = UpdateStatus.Ready;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            Status = UpdateStatus.Idle;
        }
        catch (Exception exception) {
            logger?.LogError(exception, "Checking for or downloading a Tidverk update failed");
            if (showErrors) {
                ErrorMessage = exception.Message;
                showFailure = true;
                Status = UpdateStatus.Failed;
            }
            else {
                Status = UpdateStatus.Idle;
            }
        }
        finally {
            Volatile.Write(ref isRunning, 0);
        }
    }

    private void NotifyStatusProperties() {
        OnPropertyChanged(nameof(IsChecking));
        OnPropertyChanged(nameof(IsUpToDate));
        OnPropertyChanged(nameof(IsUnavailable));
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(IsSidebarVisible));
        OnPropertyChanged(nameof(CanCheck));
    }

    private static string AssemblyVersion() {
        Version? version = typeof(UpdateService).Assembly.GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
