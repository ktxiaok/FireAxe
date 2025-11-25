using System;
using System.Net;

namespace FireAxe;

public class DownloadServiceSettings
{
    public IWebProxy? Proxy { get; init; } = null;
}