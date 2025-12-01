using Serilog;
using System;
using System.Net;

namespace FireAxe;

public static class AppSettingsExtensions
{
    public static IWebProxy? GetWebProxy(this AppSettings appSettings)
    {
        ArgumentNullException.ThrowIfNull(appSettings);

        try
        {
            var address = appSettings.WebProxyAddress;
            if (address.Length > 0)
            {
                var userName = appSettings.WebProxyCredentialUserName;
                var password = appSettings.WebProxyCredentialPassword;
                if (userName.Length > 0 || password.Length > 0)
                {
                    return new WebProxy(address, false, null, new NetworkCredential(userName, password));
                }
                else
                {
                    return new WebProxy(address);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get the IWebProxy of the AppSettings.");
        }
        
        return null;
    }

    public static DownloadServiceSettings GetDownloadServiceSettings(this AppSettings appSettings)
    {
        ArgumentNullException.ThrowIfNull(appSettings);

        return new DownloadServiceSettings
        {
            Proxy = appSettings.GetWebProxy()
        };
    }
}