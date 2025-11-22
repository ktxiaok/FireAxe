using System;

namespace FireAxe;

public class InvalidFilePathException : Exception
{
    public InvalidFilePathException(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }
}