using System;

namespace FireAxe;

public enum DownloadStatus
{
    Preparing,
    PreparingAndPaused,
    Running,
    Paused,
    Succeeded,
    Cancelled,
    Failed
}

public static class DownloadStatusExtensions
{
    public static bool IsCompleted(this DownloadStatus status)
    {
        return status == DownloadStatus.Succeeded || status == DownloadStatus.Cancelled || status == DownloadStatus.Failed;
    }
}
