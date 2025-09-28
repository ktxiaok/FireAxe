using System;

namespace FireAxe;

public class FileExistException : Exception
{
    public FileExistException(string filePath)
    {
        FilePath = filePath;
    }
    
    public string FilePath { get; }
}
