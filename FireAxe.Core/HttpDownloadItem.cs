using System.Diagnostics;

namespace FireAxe;

// Streaming transport for sites that require curl's browser-compatible TLS handshake.
// Implements the same UI contract as the Steam downloader.
internal sealed class HttpDownloadItem : IDownloadItem
{
    private readonly object _sync = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _task;
    private TaskCompletionSource _resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private DownloadStatus _status = DownloadStatus.Preparing;
    private Exception? _exception;
    private long _downloadedBytes;
    private long _totalBytes;
    private double _speed;

    public HttpDownloadItem(HttpClient client, string url, string filePath, string referer)
    {
        Url = url;
        FilePath = filePath;
        _resume.SetResult();
        _task = Task.Run(async () =>
        {
            string temporaryPath = filePath + IDownloadService.DownloadingFileExtension;
            try
            {
                var token = _cancellation.Token;
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Referrer = new Uri(referer);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentType?.MediaType is "text/html" or "application/xhtml+xml")
                    throw new InvalidDataException("GameMaps returned a web page instead of the mod file.");
                Interlocked.Exchange(ref _totalBytes, response.Content.Headers.ContentLength ?? 0);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
                await using (var output = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                await using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
                {
                    lock (_sync)
                    {
                        if (_status == DownloadStatus.Preparing) _status = DownloadStatus.Running;
                        else if (_status == DownloadStatus.PreparingAndPaused) _status = DownloadStatus.Paused;
                    }
                    byte[] buffer = new byte[81920];
                    var stopwatch = Stopwatch.StartNew();
                    while (true)
                    {
                        Task resume;
                        lock (_sync) resume = _resume.Task;
                        await resume.WaitAsync(token).ConfigureAwait(false);
                        int count = await input.ReadAsync(buffer, token).ConfigureAwait(false);
                        if (count == 0) break;
                        await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                        long downloaded = Interlocked.Add(ref _downloadedBytes, count);
                        Volatile.Write(ref _speed, downloaded / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001));
                    }
                }
                token.ThrowIfCancellationRequested();
                if (TotalBytes > 0 && DownloadedBytes != TotalBytes)
                    throw new EndOfStreamException("The GameMaps download ended before the complete file was received.");
                File.Move(temporaryPath, filePath, true);
                lock (_sync) _status = DownloadStatus.Succeeded;
            }
            catch (OperationCanceledException)
            {
                lock (_sync) _status = DownloadStatus.Cancelled;
            }
            catch (Exception ex)
            {
                lock (_sync)
                {
                    _exception = ex;
                    _status = DownloadStatus.Failed;
                }
            }
            finally
            {
                lock (_sync) _cancellation.Dispose();
            }
        });
    }

    public string Url { get; }
    public string FilePath { get; }
    public long DownloadedBytes => Interlocked.Read(ref _downloadedBytes);
    public long TotalBytes => Interlocked.Read(ref _totalBytes);
    public double BytesPerSecondSpeed => Volatile.Read(ref _speed);
    public DownloadStatus Status { get { lock (_sync) return _status; } }
    public Exception Exception { get { lock (_sync) return _exception ?? throw new InvalidOperationException("Exception not set."); } }

    public void Pause()
    {
        lock (_sync)
        {
            if (_status is not (DownloadStatus.Preparing or DownloadStatus.Running)) return;
            _resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _status = _status == DownloadStatus.Preparing ? DownloadStatus.PreparingAndPaused : DownloadStatus.Paused;
        }
    }

    public void Resume()
    {
        lock (_sync)
        {
            if (_status is not (DownloadStatus.PreparingAndPaused or DownloadStatus.Paused)) return;
            _status = _status == DownloadStatus.PreparingAndPaused ? DownloadStatus.Preparing : DownloadStatus.Running;
            _resume.TrySetResult();
        }
    }

    public void Cancel()
    {
        lock (_sync)
        {
            if (!_status.IsCompleted()) _cancellation.Cancel();
        }
    }

    public void Wait() => _task.GetAwaiter().GetResult();
    public Task WaitAsync() => _task;
    public void Dispose() => Cancel();
}
