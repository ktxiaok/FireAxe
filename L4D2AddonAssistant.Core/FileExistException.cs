using System;

namespace L4D2AddonAssistant
{
    public class FileExistException : Exception
    {
        public FileExistException(string filePath)
        {
            FilePath = filePath;
        }
        
        public string FilePath { get; }
    }
}
