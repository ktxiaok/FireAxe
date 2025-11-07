using System;

namespace FireAxe;

public class FileNameExistsException : IOException
{
    public FileNameExistsException(string filePath)
    {
        FilePath = filePath;
    }
    
    public string FilePath { get; }
}
