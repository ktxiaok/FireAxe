using System;

namespace FireAxe
{
    public interface IDownloadItem : IDisposable
    {
        string Url { get; }

        string FilePath { get; }

        long DownloadedBytes { get; }

        long TotalBytes { get; }

        double BytesPerSecondSpeed { get; }

        DownloadStatus Status { get; }

        Exception Exception { get; }

        void Pause();

        void Resume();

        void Cancel();

        void Wait();

        Task WaitAsync();
    }
}
