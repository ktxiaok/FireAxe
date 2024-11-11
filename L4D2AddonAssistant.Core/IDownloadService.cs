using System;

namespace L4D2AddonAssistant
{
    public interface IDownloadService : IDisposable
    {
        IDownloadItem Download(string url, string filePath);
    }
}
