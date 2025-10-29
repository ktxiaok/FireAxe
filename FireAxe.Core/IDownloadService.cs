using System;

namespace FireAxe;

public interface IDownloadService : IDisposable
{
    const string DownloadingFileExtension = ".downloading";

    const string DownloadInfoFileExtension = ".downloadinfo";

    IDownloadItem Download(string url, string filePath);
}
