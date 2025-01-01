﻿using System;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace FireAxe
{
    public static class FileUtils
    {
        private const int FO_DELETE = 0x0003;
        private const int FOF_ALLOWUNDO = 0x0040;           // Preserve undo information, if possible. 
        private const int FOF_NOCONFIRMATION = 0x0010;      // Show no confirmation dialog box to the user

        // Struct which contains information that the SHFileOperation function uses to perform file operations. 
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFILEOPSTRUCT
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

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                SHFILEOPSTRUCT fileop = new SHFILEOPSTRUCT();
                fileop.wFunc = FO_DELETE;
                fileop.pFrom = path + '\0' + '\0';
                fileop.fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION;
                SHFileOperation(ref fileop);
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

            try
            {
                Directory.Move(sourcePath, targetPath);
            }
            catch (Exception ex)
            {
                throw new MoveFileException(sourcePath, targetPath, ex);
            }
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

            if (!File.Exists(Path.Join(dirPath, name)))
            {
                return name;
            }
            string extension = Path.GetExtension(name);
            string nameNoExt = Path.GetFileNameWithoutExtension(name);
            int i = 1;
            while (true)
            {
                string nameTry = nameNoExt + $"({i})" + extension;
                if (!File.Exists(Path.Join(dirPath, nameTry)))
                {
                    return nameTry;
                }
                checked
                {
                    i++;
                }
            }
        }

        private static ImmutableHashSet<char> s_fileNameReservedChars = ['/', '?', '<', '>', '\\', ':', '*', '|', '"'];
        private static ImmutableHashSet<string> s_fileNameReserved = ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];

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
                if (s_fileNameReservedChars.Contains(c))
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
            if (s_fileNameReserved.Contains(result))
            {
                result = "";
            }

            return result;
        }
    }
}
