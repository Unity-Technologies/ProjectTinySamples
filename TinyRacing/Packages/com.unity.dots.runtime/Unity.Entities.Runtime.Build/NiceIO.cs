using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Unity.Entities.Runtime.Build
{
    /// <summary>
    /// A filesystem path.
    /// </summary>
    /// <remarks>
    /// The path can be absolute or relative; the entity it refers to could be a file or a directory, and may or may not
    /// actually exist in the filesystem.
    /// </remarks>
    internal class NPath : IComparable, IEquatable<NPath>
    {
        // Assume FS is case sensitive on Linux, and case insensitive on macOS and Windows.
        static readonly bool k_IsCaseSensitiveFileSystem = Directory.Exists("/proc");
        static readonly bool k_IsWindows = Environment.OSVersion.Platform == PlatformID.Win32Windows || Environment.OSVersion.Platform == PlatformID.Win32NT;

        static readonly StringComparison PathStringComparison = k_IsCaseSensitiveFileSystem ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        private readonly string _path;

        [ThreadStatic]
        private static NPath _frozenCurrentDirectory;

        static NPath Empty => new NPath("");

        #region construction

        /// <summary>
        /// Create a new NPath.
        /// </summary>
        /// <param name="path">The path that this NPath should represent.</param>
        public NPath(string path)
        {
            if (path == null)
                throw new ArgumentNullException();
            _path = MakeCompletelyWellFormatted(path);
        }

        /// <summary>
        /// Create a new NPath.
        /// </summary>
        /// <param name="path">The path that this NPath should represent.</param>
        public NPath(FileSystemInfo fileSystemInfo)
        {
            if (fileSystemInfo == null)
                throw new ArgumentNullException();
            _path = MakeCompletelyWellFormatted(fileSystemInfo.ToString());
        }

        //keep this private, we need to guarantee all NPath's out there are guaranteed well formed.
        private NPath(string path, bool guaranteed_well_formed)
        {
            if (!guaranteed_well_formed)
                throw new ArgumentException("For not well formed paths, use the public NPath constructor");

            _path = path;
        }

        static string MakeCompletelyWellFormatted(string path, bool doubleDotsAreCollapsed = false)
        {
            if (path == ".")
                return ".";
            if (path.Length == 0)
                return ".";

            var hasNonDotsOrSeparators = false;
            var startsWithDot = false;
            char previousChar = '\0';
            for (int i = 0; i != path.Length; i++)
            {
                var c = path[i];
                var nextChar = path.Length > (i + 1) ? path[i + 1] : '\0';
                var isDot = c == '.';
                if (isDot && i == 0)
                    startsWithDot = true;
                var isSlash = IsSlash(c);
                hasNonDotsOrSeparators |= !isDot && !isSlash;

                // MakeCompletelyWellFormatted + CollapseDoubleDots is fairly expensive, so only do it when needed:
                // If we have a "..", that is not just a bunch of "../.." in front of the path and nowhere else
                // (these have nothing to collapse on anyway)
                if (!doubleDotsAreCollapsed && (hasNonDotsOrSeparators || !startsWithDot) && isDot && previousChar == '.')
                    return MakeCompletelyWellFormatted(CollapseDoubleDots(path), true);

                if (isDot && (IsSlash(previousChar) || previousChar == '\0') && (IsSlash(nextChar) || nextChar == '\0'))
                    return MakeCompletelyWellFormatted(CollapseSingleDots(path));

                if (isSlash && IsSlash(previousChar))
                    return MakeCompletelyWellFormatted(CollapseDoubleSlashes(path));

                if (c == '\\')
                    return MakeCompletelyWellFormatted(path.Replace("\\", "/"));

                previousChar = c;
            }

            var lastChar = path[path.Length - 1];
            var secondToLastChar = path.Length >= 2 ? path[path.Length - 2] : '\0';

            if (IsSlash(lastChar) && path != "/")
            {
                if (secondToLastChar != '/' && secondToLastChar != ':')
                {
                    return path.Substring(0, path.Length - 1);
                }

                if (path.Length == 3 && path[1] == ':' && IsSlash(path[2]))
                {
                    return path[0] + ":/";
                }

                throw new ArgumentException($"Unable to parse {path}");
            }

            return path;
        }

        static string CollapseSingleDots(string path)
        {
            var result = path.Replace("\\", "/").Replace("/./", "/");
            if (result.StartsWith("./", StringComparison.Ordinal))
                result = result.Substring(2);
            if (result.EndsWith("/.", StringComparison.Ordinal))
                result = result.Substring(0, result.Length - 2);
            return result;
        }

        static string CollapseDoubleSlashes(string path)
        {
            return path.Replace("\\", "/").Replace("//", "/");
        }

        static string CollapseDoubleDots(string path)
        {
            path = path.Replace("\\", "/");
            var isRegularRoot = path[0] == '/';
            var isRootWithDriveLetter = (path[1] == ':' && path[2] == '/');
            bool isRoot = isRegularRoot || isRootWithDriveLetter;

            var startIndex = 0;
            if (isRoot) startIndex = 1;
            if (isRootWithDriveLetter) startIndex = 3;

            var stack = new Stack<string>();
            int segmentStart = startIndex;
            for (int i = startIndex; i != path.Length; i++)
            {
                if (path[i] == '/' || i == path.Length - 1)
                {
                    int extra = (i == path.Length - 1) ? 1 : 0;
                    var substring = path.Substring(segmentStart, i - segmentStart + extra);
                    if (substring == "..")
                    {
                        if (stack.Count == 0)
                        {
                            if (isRoot)
                                throw new ArgumentException($"Cannot parse path because it's ..'ing beyond the root: {path}");
                            stack.Push(substring);
                        }
                        else
                        {
                            if (stack.Peek() == "..")
                                stack.Push(substring);
                            else
                                stack.Pop();
                        }
                    }
                    else
                        stack.Push(substring);

                    segmentStart = i + 1;
                }
            }

            return path.Substring(0, startIndex) + string.Join("/", stack.Reverse().ToArray());
        }

        const int MethodImplOptions_AggressiveInlining = 256; // enum value is only in .NET 4.5+

        [MethodImpl(MethodImplOptions_AggressiveInlining)]
        private static bool IsSlash(char c) => c == '/' || c == '\\';

        /// <summary>
        /// Create a new NPath by appending a path fragment.
        /// </summary>
        /// <param name="append">The path fragment to append. This can be a filename, or a whole relative path.</param>
        /// <returns>A new NPath which is the existing path with the fragment appended.</returns>
        public NPath Combine(string append)
        {
            if (IsSlash(append[0]))
                throw new ArgumentException($"You cannot .Combine a non-relative path: {append}");
            return new NPath(_path + "/" + append);
        }

        /// <summary>
        /// Create a new NPath by appending two path fragments, one after the other.
        /// </summary>
        /// <param name="append1">The first path fragment to append.</param>
        /// <param name="append2">The second path fragment to append.</param>
        /// <returns>A new NPath which is the existing path with the first fragment appended, then the second fragment appended.</returns>
        public NPath Combine(string append1, string append2)
        {
            return new NPath(_path + "/" + append1 + "/" + append2);
        }

        /// <summary>
        /// Create a new NPath by appending a path fragment.
        /// </summary>
        /// <param name="append">The path fragment to append.</param>
        /// <returns>A new NPath which is the existing path with the fragment appended.</returns>
        public NPath Combine(NPath append)
        {
            if (append == null)
                throw new ArgumentNullException(nameof(append));

            var firstChar = append._path[0];
            if (IsSlash(firstChar))
                throw new ArgumentException($"You cannot .Combine a non-relative path: {append._path}");

            //if the to-append path starts by going up directories, we need to run our normalizing constructor, if not, we can take the fast path
            if (firstChar == '.' || _path[0] == '.' || _path.Length == 1)
                return new NPath(_path + "/" + append._path);
            return new NPath(_path + "/" + append, true);
        }

        /// <summary>
        /// Create a new NPath by appending multiple path fragments.
        /// </summary>
        /// <param name="append">The path fragments to append, in order.</param>
        /// <returns>A new NPath which is this existing path with all the supplied path fragments appended, in order.</returns>
        public NPath Combine(params NPath[] append)
        {
            var sb = new StringBuilder(ToString());
            foreach (var a in append)
            {
                if (!a.IsRelative)
                    throw new ArgumentException($"You cannot .Combine a non-relative path: {a}");

                sb.Append("/");
                sb.Append(a);
            }
            return new NPath(sb.ToString());
        }

        /// <summary>
        /// The parent path fragment (i.e. the directory) of the path.
        /// </summary>
        public NPath Parent
        {
            get
            {
                if (IsRoot)
                    throw new ArgumentException($"Parent invoked on {this}");

                for (int i = _path.Length - 1; i >= 0; i--)
                {
                    if (i == 0)
                        return _path[0] == '/' ? new NPath("/") : new NPath("");
                    if (_path[i] != '/') continue;
                    var isRooted = _path[i - 1] == ':' || _path[0] == '/';

                    var length = isRooted ? (i + 1) : i;

                    var substring = _path.Substring(0, length);

                    return new NPath(substring);
                }

                return NPath.Empty;
            }
        }

        /// <summary>
        /// Create a new NPath by computing the existing path relative to some other base path.
        /// </summary>
        /// <param name="path">The base path that the result should be relative to.</param>
        /// <returns>A new NPath, which refers to the same target as the existing path, but is described relative to the given base path.</returns>
        public NPath RelativeTo(NPath path)
        {
            if (IsRelative || path.IsRelative)
                return MakeAbsolute().RelativeTo(path.MakeAbsolute());

            var thisString = _path;
            var pathString = path._path;

            if (thisString == pathString)
                return ".";

            if (!HasSameDriveLetter(path))
                return this;

            if (path.IsRoot)
                return new NPath(thisString.Substring(pathString.Length));

            if (thisString.StartsWith(pathString, PathStringComparison))
            {
                if (thisString.Length >= pathString.Length && (IsSlash(thisString[pathString.Length])))
                    return new NPath(thisString.Substring(Math.Min(pathString.Length + 1, thisString.Length)));
            }

            var sb = new StringBuilder();
            foreach (var parent in path.RecursiveParents.ToArray())
            {
                sb.Append("../");
                if (IsChildOf(parent))
                {
                    sb.Append(thisString.Substring(parent.ToString().Length));
                    return new NPath(sb.ToString());
                }
            }
            throw new ArgumentException();
        }

        /// <summary>
        /// Create an NPath by changing the extension of this one.
        /// </summary>
        /// <param name="extension">The new extension to use. Starting it with a "." character is optional. If you pass an empty string, the resulting path will have the extension stripped entirely, including the dot character.</param>
        /// <returns>A new NPath which is the existing path but with the new extension at the end.</returns>
        public NPath ChangeExtension(string extension)
        {
            ThrowIfRoot();

            var s = ToString();
            int lastDot = -1;
            for (int i = s.Length - 1; i >= 0; i--)
            {
                if (s[i] == '.')
                {
                    lastDot = i;
                    break;
                }

                if (s[i] == '/')
                    break;
            }

            var newExtension = extension.Length == 0 ? extension : WithDot(extension);
            if (lastDot == -1)
                return s + newExtension;
            return s.Substring(0, lastDot) + newExtension;
        }

        #endregion construction

        #region inspection

        /// <summary>
        /// Whether this path is relative (i.e. not absolute) or not.
        /// </summary>
        public bool IsRelative
        {
            get
            {
                if (_path[0] == '/')
                    return false;

                if (_path.Length < 3)
                    return true;

                return _path[1] != ':' || _path[2] != '/';
            }
        }

        /// <summary>
        /// The name of the file or directory given at the end of this path, including any extension.
        /// </summary>
        public string FileName
        {
            get
            {
                ThrowIfRoot();

                if (_path.Length == 0)
                    return string.Empty;

                if (_path == ".")
                    return string.Empty;

                for (int i = _path.Length - 1; i >= 0; i--)
                {
                    if (_path[i] == '/')
                    {
                        return i == _path.Length - 1 ? string.Empty : _path.Substring(i + 1);
                    }
                }

                return _path;
            }
        }

        /// <summary>
        /// The name of the file or directory given at the end of this path, excluding the extension.
        /// </summary>
        public string FileNameWithoutExtension => Path.GetFileNameWithoutExtension(FileName);

        /// <summary>
        /// Determines whether the given path is, or is a child of, a directory with the given name.
        /// </summary>
        /// <param name="dir">The name of the directory to search for.</param>
        /// <returns>True if the path describes a file/directory that is or is a child of a directory with the given name; false otherwise.</returns>
        public bool HasDirectory(string dir)
        {
            if (dir.Contains("/") || dir.Contains("\\"))
                throw new ArgumentException($"Directory cannot contain slash {dir}");
            if (dir == ".")
                throw new ArgumentException("Single dot is not an allowed argument");

            if (_path.StartsWith(dir + "/", PathStringComparison))
                return true;
            if (_path.EndsWith("/" + dir, PathStringComparison))
                return true;
            return _path.Contains("/" + dir + "/");
        }

        /// <summary>
        /// The depth of the path, determined by the number of path separators present.
        /// </summary>
        public int Depth
        {
            get
            {
                if (IsRoot)
                    return 0;
                if (IsCurrentDir)
                    return 0;

                var startIndex = 0;
                if (DriveLetter != null)
                    startIndex += 2;
                if (!IsRelative)
                    startIndex += 1;

                int depth = 1;
                for (int i = startIndex; i != _path.Length; i++)
                {
                    if (_path[i] == '/')
                        depth++;
                }

                return depth;
            }
        }

        /// <summary>
        /// Tests whether the path is the current directory string ".".
        /// </summary>
        public bool IsCurrentDir => ToString() == ".";

        /// <summary>
        /// Tests whether the path exists.
        /// </summary>
        /// <param name="append">An optional path fragment to append before testing.</param>
        /// <returns>True if the path (with optional appended fragment) exists, false otherwise.</returns>
        public bool Exists(NPath append = null)
        {
            return FileExists(append) || DirectoryExists(append);
        }

        /// <summary>
        /// Tests whether the path exists and is a directory.
        /// </summary>
        /// <param name="append">An optional path fragment to append before testing.</param>
        /// <returns>True if the path (with optional appended fragment) exists and is a directory, false otherwise.</returns>
        public bool DirectoryExists(NPath append = null)
        {
            var path = append != null ? Combine(append) : this;
            StatCallback.Invoke(path);
            return Directory.Exists(path._path);
        }

        /// <summary>
        /// Tests whether the path exists and is a file.
        /// </summary>
        /// <param name="append">An optional path fragment to append before testing.</param>
        /// <returns>True if the path (with optional appended fragment) exists and is a file, false otherwise.</returns>
        public bool FileExists(NPath append = null)
        {
            var path = append != null ? Combine(append) : this;
            StatCallback.Invoke(path);
            return File.Exists(path._path);
        }

        /// <summary>
        /// The extension of the file, excluding the initial "." character.
        /// </summary>
        public string Extension
        {
            get
            {
                if (IsRoot)
                    throw new ArgumentException("A root directory does not have an extension");

                for (int i = _path.Length - 1; i >= 0; i--)
                {
                    var c = _path[i];
                    if (c == '.' || c == '/')
                        return _path.Substring(i + 1);
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// The Windows drive letter of the path, if present. Null if not present.
        /// </summary>
        public string DriveLetter => _path.Length >= 2 && _path[1] == ':' ? _path[0].ToString() : null;

        bool HasSameDriveLetter(NPath other) => DriveLetter == other.DriveLetter;

        /// <summary>
        /// Provides a quoted version of the path as a string, with the requested path separator type.
        /// </summary>
        /// <param name="slashMode">The path separator to use. See the <see cref="SlashMode">SlashMode</see> enum for an explanation of the values. Defaults to <c>SlashMode.Forward</c>.</param>
        /// <returns>The path, with the requested path separator type, in quotes.</returns>
        public string InQuotes(SlashMode slashMode = SlashMode.Forward)
        {
            return "\"" + ToString(slashMode) + "\"";
        }

        /// <summary>
        /// Convert this path to a string, using forward slashes as path separators.
        /// </summary>
        /// <returns>The string representation of this path.</returns>
        public override string ToString()
        {
            return _path;
        }

        /// <summary>
        /// Convert this path to a string, using the requested path separator type.
        /// </summary>
        /// <param name="slashMode">The path separator type to use. See <see cref="SlashMode">SlashMode</see> for possible values.</param>
        /// <returns>The string representation of this path.</returns>
        public string ToString(SlashMode slashMode)
        {
            if (slashMode == SlashMode.Forward || (slashMode == SlashMode.Native && !k_IsWindows))
                return _path;

            return _path.Replace("/", "\\");
        }

        /// <summary>
        /// Checks if this NPath represents the same path as another object.
        /// </summary>
        /// <param name="obj">The object to compare to.</param>
        /// <returns>True if this NPath represents the same path as the other object; false if it does not, if the other object is not an NPath, or is null.</returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as NPath);
        }

        /// <summary>
        /// Checks if this NPath is equal to another NPath.
        /// </summary>
        /// <param name="p">The path to compare to.</param>
        /// <returns>True if this NPath represents the same path as the other NPath; false otherwise.</returns>
        /// <remarks>Note that the comparison requires that the paths are the same, not just that the targets are the same; "foo/bar" and "foo/baz/../bar" refer to the same target but will not be treated as equal by this comparison. However, this comparison will ignore case differences when the current operating system does not use case-sensitive filesystems.</remarks>
        public bool Equals(NPath p)
        {
            return p != null && string.Equals(p._path, _path, PathStringComparison);
        }

        /// <summary>
        /// Compare two NPaths for equality.
        /// </summary>
        /// <param name="a">The first NPath to compare.</param>
        /// <param name="b">The second NPath to compare.</param>
        /// <returns>True if the NPaths are both equal (or both null), false otherwise. See <see cref="Equals(NPath)">Equals.</see></returns>
        public static bool operator==(NPath a, NPath b)
        {
            // If both are null, or both are same instance, return true.
            if (ReferenceEquals(a, b))
                return true;

            // If one is null, but not both, return false.
            if ((object)a == null || (object)b == null)
                return false;

            // Return true if the fields match:
            return a.Equals(b);
        }

        /// <summary>
        /// Get an appropriate hash value for this NPath.
        /// </summary>
        /// <returns>A hash value for this NPath.</returns>
        public override int GetHashCode()
        {
            if (k_IsCaseSensitiveFileSystem)
                return _path.GetHashCode();

            uint hash = 27644437;
            for (int i = 0, len = _path.Length; i < len; ++i)
            {
                uint c = _path[i];
                if (c > 0x80) c = 0x80; // All non-ASCII chars may (potentially) compare Equal.
                c |= 0x20; // ASCII case folding.
                hash ^= (hash << 5) ^ c;
            }
            return unchecked((int)hash);
        }

        /// <summary>
        /// Compare this NPath to another NPath, returning a value that can be used to sort the two objects in a stable order.
        /// </summary>
        /// <param name="obj">The object to compare to. Note that this object must be castable to NPath.</param>
        /// <returns>A value that indicates the relative order of the two objects. The return value has these meanings:
        /// <list type="table">
        /// <listheader><term>Value</term><description>Meaning</description></listheader>
        /// <item><term>Less than zero</term><description>This instance precedes <c>obj</c> in the sort order.</description></item>
        /// <item><term>Zero</term><description>This instance occurs in the same position as <c>obj</c> in the sort order.</description></item>
        /// <item><term>Greater than zero</term><description>This instance follows <c>obj</c> in the sort order.</description></item>
        /// </list>
        /// </returns>

        public int CompareTo(object obj)
        {
            if (obj == null)
                return -1;

            return string.Compare(_path, ((NPath)obj)._path, PathStringComparison);
        }

        /// <summary>
        /// Compare two NPaths for inequality.
        /// </summary>
        /// <param name="a">The first NPath to compare.</param>
        /// <param name="b">The second NPath to compare.</param>
        /// <returns>True if the NPaths are not equal, false otherwise.</returns>
        public static bool operator!=(NPath a, NPath b)
        {
            return !(a == b);
        }

        /// <summary>
        /// Tests whether this NPath has one of the provided extensions, or if no extensions are provided, whether it has any extension at all.
        /// </summary>
        /// <param name="extensions">The possible extensions to test for.</param>
        /// <returns>True if this NPath has one of the provided extensions; or, if no extensions are provided, true if this NPath has an extension. False otherwise.</returns>
        /// <remarks>The extension "*" is special, and will return true for all paths if specified.</remarks>
        public bool HasExtension(params string[] extensions)
        {
            if (extensions.Contains("*"))
                return true;
            if (extensions.Length == 0)
                return FileName.Contains(".");

            var extension = ("." + Extension).ToUpperInvariant();
            return extensions.Any(e => WithDot(e).ToUpperInvariant() == extension);
        }

        private static string WithDot(string extension)
        {
            return extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension;
        }

        /// <summary>
        /// Whether this path is rooted or not (begins with a slash character or drive specifier).
        /// </summary>
        public bool IsRoot
        {
            get
            {
                if (_path == "/" || _path == "\\")
                    return true;

                return _path.Length == 3 && _path[1] == ':' && IsSlash(_path[2]);
            }
        }

        #endregion inspection

        #region directory enumeration

        /// <summary>
        /// Find all files within this path that match the given filter.
        /// </summary>
        /// <param name="filter">The filter to match against the names of files. Wildcards can be included.</param>
        /// <param name="recurse">If true, search recursively inside subdirectories of this path; if false, search only for files that are immediate children of this path. Defaults to false.</param>
        /// <returns>An array of files that were found.</returns>
        public NPath[] Files(string filter, bool recurse = false)
        {
            GlobbingCallback.Invoke(this, filter, recurse);
            return Directory.GetFiles(_path, filter, recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).Select(s => new NPath(s)).ToArray();
        }

        /// <summary>
        /// Find all files within this path.
        /// </summary>
        /// <param name="recurse">If true, search recursively inside subdirectories of this path; if false, search only for files that are immediate children of this path. Defaults to false.</param>
        /// <returns>An array of files that were found.</returns>
        public NPath[] Files(bool recurse = false)
        {
            return Files("*", recurse);
        }

        /// <summary>
        /// Find all files within this path that have one of the provided extensions.
        /// </summary>
        /// <param name="extensions">The extensions to search for.</param>
        /// <param name="recurse">If true, search recursively inside subdirectories of this path; if false, search only for files that are immediate children of this path. Defaults to false.</param>
        /// <returns>An array of files that were found.</returns>
        public NPath[] Files(string[] extensions, bool recurse = false)
        {
            if (!DirectoryExists() || extensions.Length == 0)
                return new NPath[] {};

            GlobbingCallback.Invoke(this, extensions, recurse);
            return Directory.GetFiles(_path, "*", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).Select(s => new NPath(s)).Where(p => extensions.Contains(p.Extension)).ToArray();
        }

        /// <summary>
        /// Find all files or directories within this path that match the given filter.
        /// </summary>
        /// <param name="filter">The filter to match against the names of files and directories. Wildcards can be included.</param>
        /// <param name="recurse">If true, search recursively inside subdirectories of this path; if false, search only for files and directories that are immediate children of this path. Defaults to false.</param>
        /// <returns>An array of files and directories that were found.</returns>
        public NPath[] Contents(string filter, bool recurse = false)
        {
            return Files(filter, recurse).Concat(Directories(filter, recurse)).ToArray();
        }

        /// <summary>
        /// Find all files and directories within this path.
        /// </summary>
        /// <param name="recurse">If true, search recursively inside subdirectories of this path; if false, search only for files and directories that are immediate children of this path. Defaults to false.</param>
        /// <returns>An array of files and directories that were found.</returns>
        public NPath[] Contents(bool recurse = false)
        {
            return Contents("*", recurse);
        }

        /// <summary>
        /// Find all directories within this path that match the given filter.
        /// </summary>
        /// <param name="filter">The filter to match against the names of directories. Wildcards can be included.</param>
        /// <param name="recurse">If true, search recursively inside subdirectories of this path; if false, search only for directories that are immediate children of this path. Defaults to false.</param>
        /// <returns>An array of directories that were found.</returns>
        public NPath[] Directories(string filter, bool recurse = false)
        {
            GlobbingCallback.Invoke(this, filter, recurse);
            return Directory.GetDirectories(_path, filter, recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).Select(s => new NPath(s)).ToArray();
        }

        /// <summary>
        /// Find all directories within this path.
        /// </summary>
        /// <param name="recurse">If true, search recursively inside subdirectories of this path; if false, search only for directories that are immediate children of this path. Defaults to false.</param>
        /// <returns>An array of directories that were found.</returns>
        public NPath[] Directories(bool recurse = false)
        {
            return Directories("*", recurse);
        }

        #endregion

        #region filesystem writing operations

        /// <summary>
        /// Create an empty file at this path.
        /// </summary>
        /// <returns>This NPath, for chaining further operations.</returns>
        /// <remarks>If a file already exists at this path, it will be overwritten.</remarks>
        public NPath CreateFile()
        {
            ThrowIfRelative();
            ThrowIfRoot();
            EnsureParentDirectoryExists();
            File.WriteAllBytes(_path, new byte[0]);
            return this;
        }

        /// <summary>
        /// Append the given path fragment to this path, and create an empty file there.
        /// </summary>
        /// <param name="file">The path fragment to append.</param>
        /// <returns>The path to the created file, for chaining further operations.</returns>
        /// <remarks>If a file already exists at that path, it will be overwritten.</remarks>
        public NPath CreateFile(NPath file)
        {
            if (!file.IsRelative)
                throw new ArgumentException("You cannot call CreateFile() on an existing path with a non relative argument");
            return Combine(file).CreateFile();
        }

        /// <summary>
        /// Create this path as a directory if it does not already exist.
        /// </summary>
        /// <returns>This NPath, for chaining further operations.</returns>
        /// <remark>This is identical to <see cref="EnsureDirectoryExists(NPath)"/>, except that EnsureDirectoryExists triggers "Stat" callbacks and this doesn't.</remark>
        public NPath CreateDirectory()
        {
            ThrowIfRelative();

            if (IsRoot)
                throw new NotSupportedException("CreateDirectory is not supported on a root level directory because it would be dangerous:" + ToString());

            Directory.CreateDirectory(_path);
            return this;
        }

        /// <summary>
        /// Append the given path fragment to this path, and create it as a directory if it does not already exist.
        /// </summary>
        /// <param name="directory">The path fragment to append.</param>
        /// <returns>The path to the created directory, for chaining further operations.</returns>
        public NPath CreateDirectory(NPath directory)
        {
            if (!directory.IsRelative)
                throw new ArgumentException("Cannot call CreateDirectory with an absolute argument");

            return Combine(directory).CreateDirectory();
        }

        /// <summary>
        /// Copy this NPath to the given destination.
        /// </summary>
        /// <param name="dest">The path to copy to.</param>
        /// <returns>The path to the copied result, for chaining further operations.</returns>
        public NPath Copy(NPath dest)
        {
            return Copy(dest, p => true);
        }

        /// <summary>
        /// Copy this NPath to the given destination, applying a filter function to decide which files are copied.
        /// </summary>
        /// <param name="dest">The path to copy to.</param>
        /// <param name="fileFilter">The filter function. Each candidate file is passed to this function; if the function returns true, the file will be copied, otherwise it will not.</param>
        /// <returns></returns>
        public NPath Copy(NPath dest, Func<NPath, bool> fileFilter)
        {
            ThrowIfRelative();
            if (dest.IsRelative)
                dest = Parent.Combine(dest);

            if (dest.DirectoryExists())
                return CopyWithDeterminedDestination(dest.Combine(FileName), fileFilter);

            return CopyWithDeterminedDestination(dest, fileFilter);
        }

        /// <summary>
        /// Create a new NPath by converting this path into an absolute representation.
        /// </summary>
        /// <param name="base">Optional base to use as a root for relative paths.</param>
        /// <returns></returns>
        public NPath MakeAbsolute(NPath @base = null)
        {
            if (!IsRelative)
                return this;

            return (@base ?? CurrentDirectory).Combine(this);
        }

        NPath CopyWithDeterminedDestination(NPath absoluteDestination, Func<NPath, bool> fileFilter)
        {
            if (absoluteDestination.IsRelative)
                throw new ArgumentException("absoluteDestination must be absolute");

            if (FileExists())
            {
                if (!fileFilter(absoluteDestination))
                    return null;

                absoluteDestination.EnsureParentDirectoryExists();

                File.Copy(_path, absoluteDestination._path, true);
                return absoluteDestination;
            }

            if (DirectoryExists())
            {
                absoluteDestination.EnsureDirectoryExists();
                foreach (var thing in Contents())
                    thing.CopyWithDeterminedDestination(absoluteDestination.Combine(thing.RelativeTo(this)), fileFilter);
                return absoluteDestination;
            }

            throw new ArgumentException("Copy() called on path that doesnt exist: " + ToString());
        }

        /// <summary>
        /// Deletes the file or directory referred to by the NPath.
        /// </summary>
        /// <param name="deleteMode">The deletion mode to use, see <see cref="DeleteMode">DeleteMode.</see> Defaults to DeleteMode.Normal.</param>
        /// <exception cref="System.InvalidOperationException">The path does not exist. See also <see cref="DeleteIfExists">DeleteIfExists</see>.</exception>
        public void Delete(DeleteMode deleteMode = DeleteMode.Normal)
        {
            ThrowIfRelative();

            if (IsRoot)
                throw new NotSupportedException("Delete is not supported on a root level directory because it would be dangerous:" + ToString());

            if (FileExists())
                File.Delete(_path);
            else if (DirectoryExists())
                try
                {
                    Directory.Delete(_path, true);
                }
                catch (IOException)
                {
                    if (deleteMode == DeleteMode.Normal)
                        throw;
                }
            else
                throw new InvalidOperationException("Trying to delete a path that does not exist: " + ToString());
        }

        /// <summary>
        /// Deletes the file or directory referred to by the NPath, if it exists.
        /// </summary>
        /// <param name="deleteMode">The deletion mode to use, see <see cref="DeleteMode">DeleteMode.</see> Defaults to DeleteMode.Normal.</param>
        /// <returns>This NPath, for chaining further operations.</returns>
        public NPath DeleteIfExists(DeleteMode deleteMode = DeleteMode.Normal)
        {
            ThrowIfRelative();

            if (FileExists() || DirectoryExists())
                Delete(deleteMode);

            return this;
        }

        /// <summary>
        /// Deletes all files and directories inside the directory referred to by this NPath.
        /// </summary>
        /// <returns>This NPath, for chaining further operations.</returns>
        /// <exception cref="System.InvalidOperationException">This NPath refers to a file, rather than a directory.</exception>
        public NPath DeleteContents()
        {
            ThrowIfRelative();

            if (IsRoot)
                throw new NotSupportedException("DeleteContents is not supported on a root level directory because it would be dangerous:" + ToString());

            if (FileExists())
                throw new InvalidOperationException("It is not valid to perform this operation on a file");

            if (DirectoryExists())
            {
                try
                {
                    Files().Delete();
                    Directories().Delete();
                }
                catch (IOException)
                {
                    if (Files(true).Any())
                        throw;
                }

                return this;
            }

            return EnsureDirectoryExists();
        }

        /// <summary>
        /// Create a temporary directory in the system temporary location and return the NPath of it.
        /// </summary>
        /// <param name="myprefix">A prefix to use for the name of the temporary directory.</param>
        /// <returns>A new NPath which targets the newly created temporary directory.</returns>
        public static NPath CreateTempDirectory(string myprefix)
        {
            var random = new Random();
            while (true)
            {
                var candidate = new NPath(Path.GetTempPath() + "/" + myprefix + "_" + random.Next());
                if (!candidate.Exists())
                    return candidate.CreateDirectory();
            }
        }

        /// <summary>
        /// Move the file or directory targetted by this NPath to a new location.
        /// </summary>
        /// <param name="dest">The destination for the move.</param>
        /// <returns>An NPath representing the newly moved file or directory.</returns>
        public NPath Move(NPath dest)
        {
            ThrowIfRelative();

            if (IsRoot)
                throw new NotSupportedException("Move is not supported on a root level directory because it would be dangerous:" + ToString());

            if (dest.IsRelative)
                return Move(Parent.Combine(dest));

            if (dest.DirectoryExists())
                return Move(dest.Combine(FileName));

            if (FileExists())
            {
                dest.EnsureParentDirectoryExists();
                File.Move(_path, dest._path);
                return dest;
            }

            if (DirectoryExists())
            {
                Directory.Move(_path, dest._path);
                return dest;
            }

            throw new ArgumentException("Move() called on a path that doesn't exist: " + ToString());
        }

        #endregion

        #region special paths

        /// <summary>
        /// The current directory in use by the process.
        /// </summary>
        /// <remarks>Note that every read from this property will result in an operating system query, unless <see cref="WithFrozenCurrentDirectory">WithFrozenCurrentDirectory</see> is used.</remarks>
        public static NPath CurrentDirectory
        {
            get
            {
                return _frozenCurrentDirectory ?? new NPath(Directory.GetCurrentDirectory());
            }
        }

        class SetCurrentDirectoryOnDispose : IDisposable
        {
            public NPath Directory { get; }

            public SetCurrentDirectoryOnDispose(NPath directory)
            {
                Directory = directory;
            }

            public void Dispose()
            {
                SetCurrentDirectory(Directory);
            }
        }

        /// <summary>
        /// Temporarily change the current directory for the process.
        /// </summary>
        /// <param name="directory">The new directory to set as the current directory.</param>
        /// <returns>A token representing the change in current directory. When this is disposed, the current directory will be returned to its previous value. The usual usage pattern is to capture the token with a <c>using</c> statement, such that it is automatically disposed of when the <c>using</c> block exits.</returns>
        public static IDisposable SetCurrentDirectory(NPath directory)
        {
            var result = new SetCurrentDirectoryOnDispose(NPath.CurrentDirectory);
            Directory.SetCurrentDirectory(directory._path);
            return result;
        }

        /// <summary>
        /// The current user's home directory.
        /// </summary>
        public static NPath HomeDirectory
        {
            get
            {
                if (Path.DirectorySeparatorChar == '\\')
                    return new NPath(Environment.GetEnvironmentVariable("USERPROFILE"));
                return new NPath(Environment.GetEnvironmentVariable("HOME"));
            }
        }

        /// <summary>
        /// The system temporary directory.
        /// </summary>
        public static NPath SystemTemp => new NPath(Path.GetTempPath());

        #endregion

        private void ThrowIfRelative()
        {
            if (IsRelative)
                throw new ArgumentException($"You are attempting an operation on a Path that requires an absolute path, but the path is relative: {this}");
        }

        private void ThrowIfRoot()
        {
            if (IsRoot)
                throw new ArgumentException("You are attempting an operation that is not valid on a root level directory");
        }

        /// <summary>
        /// Append an optional path fragment to this NPath, then create it as a directory if it does not already exist.
        /// </summary>
        /// <param name="append">The path fragment to append.</param>
        /// <returns>The path to the directory that is now guaranteed to exist.</returns>
        /// <remark>This is identical to <see cref="CreateDirectory()"/>, except that this triggers "Stat" callbacks and CreateDirectory doesn't.</remark>
        public NPath EnsureDirectoryExists(NPath append = null)
        {
            var combined = append != null ? Combine(append) : this;
            if (combined.DirectoryExists())
                return combined;
            combined.EnsureParentDirectoryExists();
            combined.CreateDirectory();
            return combined;
        }

        /// <summary>
        /// Create the parent directory of this NPath if it does not already exist.
        /// </summary>
        /// <returns>This NPath, for chaining further operations.</returns>
        public NPath EnsureParentDirectoryExists()
        {
            Parent.EnsureDirectoryExists();
            return this;
        }

        /// <summary>
        /// Throw an exception if this path does not exist as a file.
        /// </summary>
        /// <returns>This path, in order to chain further operations.</returns>
        /// <exception cref="System.IO.FileNotFoundException">The path does not exist, or is not a file.</exception>
        public NPath FileMustExist()
        {
            if (!FileExists())
                throw new FileNotFoundException("File was expected to exist : " + ToString());

            return this;
        }

        /// <summary>
        /// Throw an exception if this directory does not exist.
        /// </summary>
        /// <returns>This path, in order to chain further operations.</returns>
        /// <exception cref="System.IO.FileNotFoundException">The path does not exist, or is not a directory.</exception>
        public NPath DirectoryMustExist()
        {
            if (!DirectoryExists())
                throw new DirectoryNotFoundException("Expected directory to exist : " + ToString());

            return this;
        }

        /// <summary>
        /// Check if this path is a child of the given path hierarchy root (i.e. is a file or directory that is inside the given hierachy root directory or one of its descendent directories).
        /// </summary>
        /// <param name="potentialBasePath">The path hierarchy root to check.</param>
        /// <returns>True if this path is a child of the given root path, false otherwise.</returns>
        public bool IsChildOf(NPath potentialBasePath)
        {
            if (IsRelative != potentialBasePath.IsRelative)
                return MakeAbsolute().IsChildOf(potentialBasePath.MakeAbsolute());

            if (!IsRelative && !HasSameDriveLetter(potentialBasePath))
                return false;

            if (potentialBasePath.IsRoot)
                return true;

            if (IsRelative && potentialBasePath._path == ".")
                return !_path.StartsWith("..", StringComparison.Ordinal);

            var potentialBaseString = potentialBasePath._path;
            var potentialBaseStringLength = potentialBaseString.Length;

            return _path.Length > potentialBaseStringLength + 1 &&
                _path.StartsWith(potentialBaseString, PathStringComparison) &&
                _path[potentialBaseStringLength] == '/';
        }

        /// <summary>
        /// Check if this path is a child of the given path hierarchy root (i.e. is a file or directory that is inside the given hierachy root directory or one of its descendent directories), or is equal to it.
        /// </summary>
        /// <param name="potentialBasePath">The path hierarchy root to check.</param>
        /// <returns>True if this path is equal to or is a child of the given root path, false otherwise.</returns>
        public bool IsSameAsOrChildOf(NPath potentialBasePath)
        {
            return MakeAbsolute() == potentialBasePath.MakeAbsolute() || IsChildOf(potentialBasePath);
        }

        /// <summary>
        /// Return each parent directory of this path, starting with the immediate parent, then that directory's parent, and so on, until the root of the path is reached.
        /// </summary>
        public IEnumerable<NPath> RecursiveParents
        {
            get
            {
                var candidate = this;
                while (true)
                {
                    if (candidate.IsRoot || candidate._path == ".")
                        yield break;

                    candidate = candidate.Parent;
                    yield return candidate;
                }
            }
        }

        /// <summary>
        /// Search all parent directories of this path for one that contains a file or directory with the given name.
        /// </summary>
        /// <param name="needle">The name of the file or directory to search for.</param>
        /// <returns>The path to the parent directory that contains the file or directory, or null if none of the parents contained a file or directory with the requested name.</returns>
        public NPath ParentContaining(NPath needle)
        {
            ThrowIfRelative();

            return RecursiveParents.FirstOrDefault(p => p.Exists(needle));
        }

        /// <summary>
        /// Open this path as a text file, write the given string to it, then close the file.
        /// </summary>
        /// <param name="contents">The string to write to the text file.</param>
        /// <returns>The path to this file, for use in chaining further operations.</returns>
        public NPath WriteAllText(string contents)
        {
            ThrowIfRelative();
            EnsureParentDirectoryExists();
            File.WriteAllText(_path, contents);
            return this;
        }

        /// <summary>
        /// Open this file as a text file, and replace the contents with the provided string, if they do not already match. Then close the file.
        /// </summary>
        /// <param name="contents">The string to replace the file's contents with.</param>
        /// <returns>The path to this file, for use in chaining further operations.</returns>
        /// <remarks>Note that if the contents of the file already match the provided string, the file is not modified - this includes not modifying the file's "last written" timestamp.</remarks>
        public NPath ReplaceAllText(string contents)
        {
            ThrowIfRelative();
            if (FileExists() && ReadAllText() == contents)
                return this;
            WriteAllText(contents);
            return this;
        }

        /// <summary>
        /// Opens a text file, reads all the text in the file into a single string, then closes the file.
        /// </summary>
        /// <returns>The contents of the text file, as a single string.</returns>
        public string ReadAllText()
        {
            ThrowIfRelative();
            ReadContentsCallback.Invoke(this);
            return File.ReadAllText(_path);
        }

        /// <summary>
        /// Opens a text file, writes all entries of a string array as separate lines into the file, then closes the file.
        /// </summary>
        /// <param name="contents">The entries to write into the file as separate lines.</param>
        /// <returns>The path to this file.</returns>
        public NPath WriteAllLines(string[] contents)
        {
            ThrowIfRelative();
            EnsureParentDirectoryExists();
            File.WriteAllLines(_path, contents);
            return this;
        }

        /// <summary>
        /// Opens a text file, reads all lines of the file into a string array, and then closes the file.
        /// </summary>
        /// <returns>A string array containing all lines of the file.</returns>
        public string[] ReadAllLines()
        {
            ThrowIfRelative();
            ReadContentsCallback.Invoke(this);
            return File.ReadAllLines(_path);
        }

        /// <summary>
        /// Copy all files in this NPath to the given destination directory.
        /// </summary>
        /// <param name="destination">The directory to copy the files to.</param>
        /// <param name="recurse">If true, files inside subdirectories of this NPath will also be copied. If false, only immediate child files of this NPath will be copied.</param>
        /// <param name="fileFilter">An optional predicate function that can be used to filter files. It is passed each source file path in turn, and if it returns true, the file is copied; otherwise, the file is not copied.</param>
        /// <returns>The paths to all the newly copied files.</returns>
        /// <remarks>Note that the directory structure of the files relative to this NPath will be preserved within the target directory.</remarks>
        public IEnumerable<NPath> CopyFiles(NPath destination, bool recurse, Func<NPath, bool> fileFilter = null)
        {
            destination.EnsureDirectoryExists();
            return Files(recurse).Where(fileFilter ?? AlwaysTrue).Select(file => file.Copy(destination.Combine(file.RelativeTo(this)))).ToArray();
        }

        /// <summary>
        /// Move all files in this NPath to the given destination directory.
        /// </summary>
        /// <param name="destination">The directory to move the files to.</param>
        /// <param name="recurse">If true, files inside subdirectories of this NPath will also be moved. If false, only immediate child files of this NPath will be moved.</param>
        /// <param name="fileFilter">An optional predicate function that can be used to filter files. It is passed each source file path in turn, and if it returns true, the file is moved; otherwise, the file is not moved.</param>
        /// <returns>The paths to all the newly moved files.</returns>
        /// <remarks>Note that the directory structure of the files relative to this NPath will be preserved within the target directory.</remarks>
        public IEnumerable<NPath> MoveFiles(NPath destination, bool recurse, Func<NPath, bool> fileFilter = null)
        {
            if (IsRoot)
                throw new NotSupportedException("MoveFiles is not supported on this directory because it would be dangerous:" + ToString());

            destination.EnsureDirectoryExists();
            return Files(recurse).Where(fileFilter ?? AlwaysTrue).Select(file => file.Move(destination.Combine(file.RelativeTo(this)))).ToArray();
        }

        static bool AlwaysTrue(NPath p)
        {
            return true;
        }

        /// <summary>
        /// Implicitly construct a new NPath from a string.
        /// </summary>
        /// <param name="input">The string to construct the new NPath from.</param>
        public static implicit operator NPath(string input)
        {
            return input != null ? new NPath(input) : null;
        }

        /// <summary>
        /// Set the last time the file was written to, in UTC.
        /// </summary>
        /// <returns>The last time the file was written to, in UTC.</returns>
        /// <remarks>This is set automatically by the OS when the file is modified, but it can sometimes be useful
        /// to explicitly update the timestamp without modifying the file contents.</remarks>
        public NPath SetLastWriteTimeUtc(DateTime lastWriteTimeUtc)
        {
            File.SetLastWriteTimeUtc(_path, lastWriteTimeUtc);
            return this;
        }

        /// <summary>
        /// Get the last time the file was written to, in UTC.
        /// </summary>
        /// <returns>The last time the file was written to, in UTC.</returns>
        public DateTime GetLastWriteTimeUtc()
        {
            return File.GetLastWriteTimeUtc(_path);
        }

        private abstract class NPathTLSCallback<T, TConcreteType> : IDisposable
        {
            [ThreadStatic] private static List<NPathTLSCallback<T, TConcreteType>> _activeCallbacks;

            private readonly Action<T> _callback;
            internal NPathTLSCallback(Action<T> callback)
            {
                _callback = callback;

                if (_activeCallbacks == null)
                    _activeCallbacks = new List<NPathTLSCallback<T, TConcreteType>>();

                _activeCallbacks.Add(this);
            }

            public void Dispose()
            {
                if (_activeCallbacks == null || !_activeCallbacks.Remove(this))
                    throw new ObjectDisposedException(GetType().Name);

                if (_activeCallbacks.Count == 0)
                    _activeCallbacks = null;
            }

            protected internal static void Invoke(T globRequest)
            {
                if (_activeCallbacks == null)
                    return;

                foreach (var callback in _activeCallbacks)
                    callback._callback(globRequest);
            }
        }

        private sealed class GlobbingCallback : NPathTLSCallback<GlobRequest, GlobbingCallback>
        {
            public GlobbingCallback(Action<GlobRequest> callback) : base(callback)
            {
            }

            internal static void Invoke(NPath path, string filter, bool recurse)
            {
                Invoke(new GlobRequest() { Path = path, Filter = filter, Recurse = recurse});
            }

            internal static void Invoke(NPath path, string[] extensions, bool recurse)
            {
                Invoke(new GlobRequest() { Path = path, Filter = string.Join(";", extensions), Recurse = recurse});
            }
        }

        class ReadContentsCallback : NPathTLSCallback<NPath, ReadContentsCallback>
        {
            public ReadContentsCallback(Action<NPath> callback) : base(callback)
            {
            }
        }

        class StatCallback : NPathTLSCallback<NPath, StatCallback>
        {
            public StatCallback(Action<NPath> callback) : base(callback)
            {
            }
        }

        /// <summary>
        /// Register a callback to be invoked when any globbing (selection of files/directories using filesystem enumeration) is performed on the current thread.
        /// </summary>
        /// <param name="callback">The callback to invoke.</param>
        /// <returns>A token representing the registered callback. This should be disposed of when the callback is no longer required. The usual usage pattern is to capture the token with a <c>using</c> statement, such that it is automatically disposed of when the <c>using</c> block exits.</returns>
        public static IDisposable WithGlobbingCallback(Action<GlobRequest> callback) => new GlobbingCallback(callback);

        /// <summary>
        /// Register a callback to be invoked when any reading of file contents (e.g. <c>ReadAllText</c>) is performed on the current thread.
        /// </summary>
        /// <param name="callback">The callback to invoke.</param>
        /// <returns>A token representing the registered callback. This should be disposed of when the callback is no longer required. The usual usage pattern is to capture the token with a <c>using</c> statement, such that it is automatically disposed of when the <c>using</c> block exits.</returns>
        public static IDisposable WithReadContentsCallback(Action<NPath> callback) => new ReadContentsCallback(callback);

        /// <summary>
        /// Register a callback to be invoked when any checking of file existence (e.g. <c>FileExists</c>) is performed on the current thread.
        /// </summary>
        /// <param name="callback">The callback to invoke.</param>
        /// <returns>A token representing the registered callback. This should be disposed of when the callback is no longer required. The usual usage pattern is to capture the token with a <c>using</c> statement, such that it is automatically disposed of when the <c>using</c> block exits.</returns>
        public static IDisposable WithStatCallback(Action<NPath> callback) => new StatCallback(callback);


        class WithFrozenDirectoryHelper : IDisposable
        {
            public void Dispose()
            {
                NPath._frozenCurrentDirectory = null;
            }
        }

        /// <summary>
        /// Temporarily assume that the current directory is a given value, instead of querying it from the environment when needed, in order to improve performance.
        /// </summary>
        /// <param name="frozenCurrentDirectory">The current directory to assume.</param>
        /// <returns>A token representing the registered callback. This should be disposed of when the assumption is no longer required. The usual usage pattern is to capture the token with a <c>using</c> statement, such that it is automatically disposed of when the <c>using</c> block exits.</returns>
        public static IDisposable WithFrozenCurrentDirectory(NPath frozenCurrentDirectory)
        {
            if (_frozenCurrentDirectory != null)
                throw new InvalidOperationException($"{nameof(WithFrozenCurrentDirectory)} called, while there was already a frozen current directory set: {_frozenCurrentDirectory}");
            _frozenCurrentDirectory = frozenCurrentDirectory;
            return new WithFrozenDirectoryHelper();
        }
    }

    /// <summary>
    /// Describes an individual attempt to 'glob' filesystem entries (multiply-select filesystem entries using filesystem enumeration).
    /// </summary>
    internal struct GlobRequest
    {
        /// <summary>
        /// The path in which globbing was performed.
        /// </summary>
        public NPath Path;

        /// <summary>
        /// Was the attempt flagged as recursive, meaning that the results should include filesystem entries in subdirectories of <c>Path</c>?
        /// </summary>
        public bool Recurse;

        /// <summary>
        /// The filter used to select filesystem entries.
        /// </summary>
        public string Filter;
    }

    /// <summary>
    /// NPath-related extension methods for other common types.
    /// </summary>
    internal static class Extensions
    {
        /// <summary>
        /// Copy these NPaths into the given directory.
        /// </summary>
        /// <param name="self">An enumerable sequence of NPaths.</param>
        /// <param name="dest">The path to the target directory.</param>
        /// <returns>The paths to the newly copied files.</returns>
        /// <remarks>All path information in the source paths is ignored, other than the final file name; the resulting copied files and directories will all be immediate children of the target directory.</remarks>
        public static IEnumerable<NPath> Copy(this IEnumerable<NPath> self, NPath dest)
        {
            if (dest.IsRelative)
                throw new ArgumentException("When copying multiple files, the destination cannot be a relative path");
            dest.EnsureDirectoryExists();
            return self.Select(p => p.Copy(dest.Combine(p.FileName))).ToArray();
        }

        /// <summary>
        /// Move these NPaths into the given directory.
        /// </summary>
        /// <param name="self">An enumerable sequence of NPaths.</param>
        /// <param name="dest">The path to the target directory.</param>
        /// <returns>The paths to the newly moved files.</returns>
        /// <remarks>All path information in the source paths is ignored, other than the final file name; the resulting moved files and directories will all be immediate children of the target directory.</remarks>
        public static IEnumerable<NPath> Move(this IEnumerable<NPath> self, NPath dest)
        {
            if (dest.IsRelative)
                throw new ArgumentException("When moving multiple files, the destination cannot be a relative path");
            dest.EnsureDirectoryExists();
            return self.Select(p => p.Move(dest.Combine(p.FileName))).ToArray();
        }

        /// <summary>
        /// Delete the files/directories targetted by these paths.
        /// </summary>
        /// <param name="self">The paths to delete.</param>
        /// <returns>All paths that were passed in to the method.</returns>
        public static IEnumerable<NPath> Delete(this IEnumerable<NPath> self)
        {
            foreach (var p in self)
                p.Delete();
            return self;
        }

        /// <summary>
        /// Convert all these paths to quoted strings, using the requested path separator type.
        /// </summary>
        /// <param name="self">The paths to convert.</param>
        /// <param name="slashMode">The path separator type to use. Defaults to <c>SlashMode.Forward</c>.</param>
        /// <returns>The paths, converted to quoted strings.</returns>
        public static IEnumerable<string> InQuotes(this IEnumerable<NPath> self, SlashMode slashMode = SlashMode.Forward)
        {
            return self.Select(p => p.InQuotes(slashMode));
        }

        /// <summary>
        /// Construct a new NPath from this string.
        /// </summary>
        /// <param name="path">The string to construct the path from.</param>
        /// <returns>A new NPath constructed from this string.</returns>
        public static NPath ToNPath(this string path)
        {
            return new NPath(path);
        }

        /// <summary>
        /// Construct new NPaths from each of these strings.
        /// </summary>
        /// <param name="paths">The strings to construct NPaths from.</param>
        /// <returns>The newly constructed NPaths.</returns>
        public static IEnumerable<NPath> ToNPaths(this IEnumerable<string> paths)
        {
            return paths.Select(p => new NPath(p));
        }
    }

    /// <summary>
    /// Describes the different kinds of path separators that can be used when converting NPaths back into strings.
    /// </summary>
    internal enum SlashMode
    {
        /// <summary>
        /// Use the slash mode that is native for the current platform - backslashes on Windows, forward slashes on macOS and Linux systems.
        /// </summary>
        Native,

        /// <summary>
        /// Use forward slashes as path separators.
        /// </summary>
        Forward,

        /// <summary>
        /// Use backslashes as path separators.
        /// </summary>
        Backward
    }

    /// <summary>
    /// Specifies the way that directory deletion should be performed.
    /// </summary>
    internal enum DeleteMode
    {
        /// <summary>
        /// When deleting a directory, if an IOException occurs, rethrow it.
        /// </summary>
        Normal,

        /// <summary>
        /// When deleting a directory, if an IOException occurs, ignore it. The deletion request may or may not be later fulfilled by the OS.
        /// </summary>
        Soft
    }
}
