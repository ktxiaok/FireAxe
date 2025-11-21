using System;

namespace FireAxe;

public static class AppSettingsExtensions
{
    public static DownloadServiceSettings GetDownloadServiceSettings(this AppSettings appSettings)
    {
        ArgumentNullException.ThrowIfNull(appSettings);

        return new DownloadServiceSettings
        {
            Proxy = appSettings.WebProxy
        };
    }
}