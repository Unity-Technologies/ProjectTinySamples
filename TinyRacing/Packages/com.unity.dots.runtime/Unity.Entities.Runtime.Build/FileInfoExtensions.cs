using System.IO;
using System.Linq;
using System.Text;

namespace Unity.Entities.Runtime.Build
{
    internal static class FileInfoExtensions
    {
        public static FileInfo Combine(this FileInfo fileInfo, params string[] paths)
        {
            return new FileInfo(Path.Combine(new[] { fileInfo.FullName }.Concat(paths).ToArray()));
        }

        public static FileInfo ChangeExtension(this FileInfo fileInfo, string extension)
        {
            return new FileInfo(Path.ChangeExtension(fileInfo.FullName, extension));
        }

        public static string GetRelativePath(this FileInfo fileInfo, DirectoryInfo relativeTo)
        {
            if (fileInfo.FullName.StartsWith(relativeTo.FullName))
            {
                return fileInfo.FullName.Remove(0, relativeTo.FullName.Length).TrimStart('\\').TrimStart('/');
            }
            else
            {
                return null;
            }
        }

        public static bool IsRelativeTo(this FileInfo fileInfo, DirectoryInfo relativeTo)
        {
            return !string.IsNullOrEmpty(GetRelativePath(fileInfo, relativeTo));
        }

        public static FileInfo CopyTo(this FileInfo fileInfo, FileInfo destination)
        {
            destination.Directory.EnsureExists();
            return fileInfo.CopyTo(destination.FullName);
        }

        public static FileInfo CopyTo(this FileInfo fileInfo, FileInfo destination, bool overwrite)
        {
            destination.Directory.EnsureExists();
            return fileInfo.CopyTo(destination.FullName, overwrite);
        }

        public static byte[] ReadAllBytes(this FileInfo fileInfo)
        {
            return File.ReadAllBytes(fileInfo.FullName);
        }

        public static string[] ReadAllLines(this FileInfo fileInfo)
        {
            return File.ReadAllLines(fileInfo.FullName);
        }

        public static string[] ReadAllLines(this FileInfo fileInfo, Encoding encoding)
        {
            return File.ReadAllLines(fileInfo.FullName, encoding);
        }

        public static string ReadAllText(this FileInfo fileInfo)
        {
            return File.ReadAllText(fileInfo.FullName);
        }

        public static string ReadAllText(this FileInfo fileInfo, Encoding encoding)
        {
            return File.ReadAllText(fileInfo.FullName, encoding);
        }

        public static void WriteAllBytes(this FileInfo fileInfo, byte[] content)
        {
            fileInfo.Directory.EnsureExists();
            File.WriteAllBytes(fileInfo.FullName, content);
        }

        public static void WriteAllLines(this FileInfo fileInfo, string[] content)
        {
            fileInfo.Directory.EnsureExists();
            File.WriteAllLines(fileInfo.FullName, content);
        }

        public static void WriteAllLines(this FileInfo fileInfo, string[] content, Encoding encoding)
        {
            fileInfo.Directory.EnsureExists();
            File.WriteAllLines(fileInfo.FullName, content, encoding);
        }

        public static void WriteAllText(this FileInfo fileInfo, string content)
        {
            fileInfo.Directory.EnsureExists();
            File.WriteAllText(fileInfo.FullName, content);
        }

        public static void WriteAllText(this FileInfo fileInfo, string content, Encoding encoding)
        {
            fileInfo.Directory.EnsureExists();
            File.WriteAllText(fileInfo.FullName, content, encoding);
        }

        public static void UpdateAllBytes(this FileInfo fileInfo, byte[] content)
        {
            fileInfo.Directory.EnsureExists();
            if (!fileInfo.Exists || !File.ReadAllBytes(fileInfo.FullName).SequenceEqual(content))
            {
                File.WriteAllBytes(fileInfo.FullName, content);
            }
        }

        public static void UpdateAllLines(this FileInfo fileInfo, string[] content)
        {
            fileInfo.Directory.EnsureExists();
            if (!fileInfo.Exists || !File.ReadAllLines(fileInfo.FullName).SequenceEqual(content))
            {
                File.WriteAllLines(fileInfo.FullName, content);
            }
        }

        public static void UpdateAllLines(this FileInfo fileInfo, string[] content, Encoding encoding)
        {
            fileInfo.Directory.EnsureExists();
            if (!fileInfo.Exists || !File.ReadAllLines(fileInfo.FullName, encoding).SequenceEqual(content))
            {
                File.WriteAllLines(fileInfo.FullName, content, encoding);
            }
        }

        public static void UpdateAllText(this FileInfo fileInfo, string content)
        {
            fileInfo.Directory.EnsureExists();
            if (!fileInfo.Exists || File.ReadAllText(fileInfo.FullName) != content)
            {
                File.WriteAllText(fileInfo.FullName, content);
            }
        }

        public static void UpdateAllText(this NPath file, string content)
        {
            if (!file.FileExists() || file.ReadAllText() != content)
            {
                file.WriteAllText(content);
            }
        }

        public static void UpdateAllText(this FileInfo fileInfo, string content, Encoding encoding)
        {
            fileInfo.Directory.EnsureExists();
            if (!fileInfo.Exists || File.ReadAllText(fileInfo.FullName, encoding) != content)
            {
                File.WriteAllText(fileInfo.FullName, content, encoding);
            }
        }
    }
}
