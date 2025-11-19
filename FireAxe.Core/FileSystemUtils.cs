using System;
using System.Collections.Frozen;
using System.Runtime.InteropServices;

namespace FireAxe;

public static class FileSystemUtils
{
    private const int FO_DELETE = 0x0003;
    private const int FOF_ALLOWUNDO = 0x0040;           // Preserve undo information, if possible. 
    private const int FOF_NOCONFIRMATION = 0x0010;      // Show no confirmation dialog box to the user

    // Struct which contains information that the SHFileOperation function uses to perform file operations. 
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.U4)]
        public int wFunc;
        public string pFrom;
        public string pTo;
        public short fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

    public static void MoveToRecycleBin(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (OperatingSystem.IsWindows())
        {
            var fileop = new SHFILEOPSTRUCT();
            fileop.wFunc = FO_DELETE;
            fileop.pFrom = path + '\0' + '\0';
            fileop.fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION;
            var result = SHFileOperation(ref fileop);
            if (result != 0)
            {
                throw new IOException($"Failed to move the file \"{path}\" to Recycle Bin. Error code: {result}");
            }
        }
        else
        {
            Delete(path);
        }
    }

    public static void Delete(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (File.Exists(path))
        {
            File.Delete(path);
        }
        else if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }

    public static void Move(string sourcePath, string targetPath)
    {
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(targetPath);
        
        Directory.Move(sourcePath, targetPath);
    }

    public static bool Exists(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return File.Exists(path) || Directory.Exists(path);
    }

    public static bool IsValidPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        try
        {
            Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }

    public static string GetUniqueFileName(string name, string dirPath)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(dirPath);

        if (!Exists(Path.Join(dirPath, name)))
        {
            return name;
        }
        string extension = Path.GetExtension(name);
        string nameNoExt = Path.GetFileNameWithoutExtension(name);
        int i = 1;
        while (true)
        {
            string nameTry = nameNoExt + $"({i})" + extension;
            if (!Exists(Path.Join(dirPath, nameTry)))
            {
                return nameTry;
            }
            checked
            {
                i++;
            }
        }
    }

    private static readonly FrozenSet<char> s_reservedFileNameChars = FrozenSet.ToFrozenSet(['/', '?', '<', '>', '\\', ':', '*', '|', '"']);
    private static readonly FrozenSet<string> s_reservedUppercaseFileNames = FrozenSet.ToFrozenSet(["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"]);

    // may return empty string
    public static string SanitizeFileName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var chars = new char[name.Length];
        int writePos = 0;
        bool checkStart = true;

        foreach (char c in name)
        {
            int cInt = c;

            // starting spaces
            if (checkStart)
            {
                if (c == ' ')
                {
                    continue;
                }
                else
                {
                    checkStart = false;
                }
            }

            // control characters
            if ((cInt >= 0x00 && cInt <= 0x1f) || (cInt >= 0x80 && cInt <= 0x9f))
            {
                continue;
            }
            
            // reserved characters
            if (s_reservedFileNameChars.Contains(c))
            {
                continue;
            }

            chars[writePos] = c;
            writePos++;
        }

        // trailing periods and spaces
        while (writePos > 0)
        {
            char c = chars[writePos - 1];
            if (c == '.' || c == ' ')
            {
                writePos--;
            }
            else
            {
                break;
            }
        }

        string result = new string(chars.AsSpan(0, writePos));
        // reserved file name
        if (s_reservedUppercaseFileNames.Contains(result.ToUpperInvariant()))
        {
            result = "";
        }

        return result;
    }

    public static string NormalizePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return path.Replace('\\', '/');
    }

    public static string GetFullAndNormalizedPath(string path) => NormalizePath(Path.GetFullPath(path));

    public static bool IsDirectoryEmpty(string dirPath)
    {
        ArgumentNullException.ThrowIfNull(dirPath);

        return !Directory.EnumerateFileSystemEntries(dirPath).Any();
    }

    public static IEnumerable<string> FindEmptyDirectories(string dirPath)
    {
        ArgumentNullException.ThrowIfNull(dirPath);

        var dirQueue = new Queue<string>();
        foreach (string subDir in Directory.EnumerateDirectories(dirPath))
        {
            dirQueue.Enqueue(subDir);
        }

        while (dirQueue.Count > 0)
        {
            string subDir = dirQueue.Dequeue();
            if (IsDirectoryEmpty(subDir))
            {
                yield return subDir;
                continue;
            }
            foreach (string subDir2 in Directory.EnumerateDirectories(subDir))
            {
                dirQueue.Enqueue(subDir2);
            }
        }
    }
}
