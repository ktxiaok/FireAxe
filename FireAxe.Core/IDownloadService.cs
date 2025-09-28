using System;

namespace FireAxe;

public interface IDownloadService : IDisposable
{
    IDownloadItem Download(string url, string filePath);
}
