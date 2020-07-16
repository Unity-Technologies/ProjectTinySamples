using System.Collections.Generic;
using System.Linq;
using NiceIO;

static class NPathExtensions
{
    public static NPath[] CombineMany(this NPath path, string[] files)
    {
        return files.Select(path.Combine).ToArray();
    }

    public static IEnumerable<T> ExcludeNulls<T>(this IEnumerable<T> sequence) => sequence.Where(s => s != null);
}

