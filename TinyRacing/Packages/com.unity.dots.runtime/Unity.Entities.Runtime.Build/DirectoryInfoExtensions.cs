using System.IO;
using System.Linq;

namespace Unity.Entities.Runtime.Build
{
    internal static class DirectoryInfoExtensions
    {
        public static DirectoryInfo Combine(this DirectoryInfo directoryInfo, params string[] paths)
        {
            return new DirectoryInfo(Path.Combine(new[] { directoryInfo.FullName }.Concat(paths).ToArray()));
        }

        public static FileInfo GetFile(this DirectoryInfo directoryInfo, string fileName)
        {
            return new FileInfo(Path.Combine(directoryInfo.FullName, fileName));
        }

        public static FileInfo GetFile(this DirectoryInfo directoryInfo, FileInfo file)
        {
            return new FileInfo(Path.Combine(directoryInfo.FullName, file.Name));
        }

        public static void EnsureExists(this DirectoryInfo directoryInfo)
        {
            if (!Directory.Exists(directoryInfo.FullName))
            {
                directoryInfo.Create();
            }
        }

        public static void CopyTo(this DirectoryInfo directoryInfo, DirectoryInfo destination, bool recursive)
        {
            if (!Directory.Exists(directoryInfo.FullName))
            {
                throw new DirectoryNotFoundException($"Directory '{directoryInfo.FullName}' not found.");
            }

            // Make sure destination exist
            destination.EnsureExists();

            // Copy files
            foreach (var file in directoryInfo.GetFiles())
            {
                file.CopyTo(Path.Combine(destination.FullName, file.Name), true);
            }

            // Copy subdirs
            if (recursive)
            {
                foreach (var subdir in directoryInfo.GetDirectories())
                {
                    CopyTo(subdir, destination.Combine(subdir.Name), recursive);
                }
            }
        }
    }
}
