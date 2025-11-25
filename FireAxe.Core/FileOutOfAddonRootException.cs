using System;

namespace FireAxe;

public class FileOutOfAddonRootException : Exception
{
    public FileOutOfAddonRootException(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }
}