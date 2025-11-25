using Avalonia.Threading;
using ReactiveUI;
using Serilog;
using System;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;

namespace FireAxe.ViewModels;

public class DownloadItemViewModel : ViewModelBase, IActivatableViewModel
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);

    private const string UnknownEtaString = "??h:??m:??s";

    private readonly IDownloadItem _download;

    private readonly string _fileName;

    private long _totalBytes = 0;
    private string _totalBytesReadable = "0B";
    private long _downloadedBytes = 0;
    private string _downloadedBytesReadable = "0B";
    private double _downloadSpeed = 0;
    private string _downloadSpeedReadable = "0B/s";
    private double _eta = 0;
    private string _etaReadable = UnknownEtaString;

    private bool _preparing = true;
    private bool _preparingAndPaused = false;
    private bool _running = false;
    private bool _paused = false;
    private bool _completed = false;

    public DownloadItemViewModel(IDownloadItem downloadItem)
    {
        ArgumentNullException.ThrowIfNull(downloadItem);
        _download = downloadItem;

        try
        {
            _fileName = Path.GetFileName(_download.FilePath);
        }
        catch (Exception ex)
        {
            _fileName = "UNKNOWN";
            Log.Error(ex, "Exception occurred during getting the file name of the path: {FilePath}", _download.FilePath);
        }

        this.WhenAnyValue(x => x.IsRunning,  x => x.IsPaused)
            .Subscribe((_) => this.RaisePropertyChanged(nameof(IsProgressAvailable)));
        this.WhenAnyValue(x => x.IsPreparing, x => x.IsPreparingAndPaused,  x => x.IsRunning)
            .Subscribe((_) => this.RaisePropertyChanged(nameof(IsPauseable)));
        this.WhenAnyValue(x => x.IsPreparingAndPaused,  x => x.IsPaused)
            .Subscribe((_) => this.RaisePropertyChanged(nameof(IsResumeable)));

        CancelCommand = ReactiveCommand.Create(() => { _download.Cancel(); Refresh(); });
        ResumeCommand = ReactiveCommand.Create(() => { _download.Resume(); Refresh(); });
        PauseCommand = ReactiveCommand.Create(() => { _download.Pause(); Refresh(); });

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            DispatcherTimer.Run(() =>
            {
                Refresh();
                return true;
            }, RefreshInterval).DisposeWith(disposables);
        });

        Refresh();
    }

    public ViewModelActivator Activator { get; } = new();

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public ReactiveCommand<Unit, Unit> ResumeCommand { get; }

    public ReactiveCommand<Unit, Unit> PauseCommand { get; }

    public IDownloadItem DownloadItem => _download;

    public string FileName => _fileName;

    public long TotalBytes
    {
        get => _totalBytes;
        private set
        {
            _totalBytes = value;
            this.RaisePropertyChanged();
            TotalBytesReadable = Utils.GetReadableBytes(_totalBytes);
        }
    }

    public string TotalBytesReadable
    {
        get => _totalBytesReadable;
        private set => this.RaiseAndSetIfChanged(ref _totalBytesReadable, value);
    }

    public long DownloadedBytes
    {
        get => _downloadedBytes;
        private set
        {
            if (value == _downloadedBytes)
            {
                return;
            }
            _downloadedBytes = value;
            this.RaisePropertyChanged();
            DownloadedBytesReadable = Utils.GetReadableBytes(_downloadedBytes);
        }
    }

    public string DownloadedBytesReadable
    {
        get => _downloadedBytesReadable;
        private set => this.RaiseAndSetIfChanged(ref _downloadedBytesReadable, value);
    }

    public double DownloadSpeed
    {
        get => _downloadSpeed;
        private set
        {
            if (value == _downloadSpeed)
            {
                return;
            }
            _downloadSpeed = value;
            this.RaisePropertyChanged();
            DownloadSpeedReadable = Utils.GetReadableBytes(_downloadSpeed) + "/s";
        }
    }

    public string DownloadSpeedReadable
    {
        get => _downloadSpeedReadable;
        private set => this.RaiseAndSetIfChanged(ref _downloadSpeedReadable, value);
    }

    public double Eta
    {
        get => _eta;
        private set
        {
            _eta = value;
            this.RaisePropertyChanged();
            TimeSpan? timeSpanNullable = null;
            try
            {
                timeSpanNullable = TimeSpan.FromSeconds(_eta);
            }
            catch (Exception)
            {

            }
            if (timeSpanNullable == null)
            {
                EtaReadable = UnknownEtaString;
            }
            else
            {
                var timeSpan = timeSpanNullable.Value;
                double hoursDouble = timeSpan.TotalHours;
                int hours;
                if (hoursDouble > 99)
                {
                    hours = 99;
                }
                else
                {
                    hours = (int)hoursDouble;
                }
                int minutes = timeSpan.Minutes;
                int seconds = timeSpan.Seconds;
                EtaReadable = string.Format("{0:D2}h:{1:D2}m:{2:D2}s", hours, minutes, seconds);
            }
        }
    }

    public string EtaReadable
    {
        get => _etaReadable;
        private set => this.RaiseAndSetIfChanged(ref _etaReadable, value);
    }

    public bool IsPreparing
    {
        get => _preparing;
        private set => this.RaiseAndSetIfChanged(ref _preparing, value);
    }

    public bool IsPreparingAndPaused
    {
        get => _preparingAndPaused;
        private set => this.RaiseAndSetIfChanged(ref _preparingAndPaused, value);
    }

    public bool IsRunning
    {
        get => _running;
        private set => this.RaiseAndSetIfChanged(ref _running, value);
    }

    public bool IsPaused
    {
        get => _paused;
        private set => this.RaiseAndSetIfChanged(ref _paused, value);
    }

    public bool IsCompleted
    {
        get => _completed;
        private set
        {
            _completed = value;
            this.RaisePropertyChanged();
        }
    }

    public bool IsProgressAvailable => IsRunning || IsPaused;

    public bool IsPauseable => (IsPreparing && !IsPreparingAndPaused) || IsRunning;

    public bool IsResumeable => IsPreparingAndPaused || IsPaused; 

    public void Refresh()
    {
        if (IsCompleted)
        {
            return;
        }

        var status = _download.Status;

        if (IsPreparing)
        {
            if (status == DownloadStatus.Preparing)
            {
                IsPreparingAndPaused = false;
            }
            else if (status == DownloadStatus.PreparingAndPaused)
            {
                IsPreparingAndPaused = true;
            }
            else
            {
                IsPreparing = false;
                IsPreparingAndPaused = false;
                TotalBytes = _download.TotalBytes;
            }
        }
        else
        {
            DownloadedBytes = _download.DownloadedBytes;
            DownloadSpeed = _download.BytesPerSecondSpeed;
            Eta = (TotalBytes - DownloadedBytes) / DownloadSpeed;
            if (status == DownloadStatus.Running)
            {
                IsRunning = true;
                IsPaused = false;
            }
            else if (status == DownloadStatus.Paused)
            {
                IsRunning = false;
                IsPaused = true;
            }
            else
            {
                IsRunning = false;
                IsPaused = false;
                IsCompleted = true;
            }
        }
    }
}
