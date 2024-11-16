using System;
using System.Collections.Immutable;

namespace L4D2AddonAssistant
{
    public static class FileUtils
    {
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
