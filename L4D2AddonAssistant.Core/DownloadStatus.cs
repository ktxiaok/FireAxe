using System;

namespace L4D2AddonAssistant
{
    public enum DownloadStatus
    {
        Preparing,
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
}
