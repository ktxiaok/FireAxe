using System;

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
    }
}
