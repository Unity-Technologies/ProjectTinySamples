using System.Collections.Generic;
using System.IO;

namespace Unity.Entities.Runtime.Build
{
    internal static class Constants
    {
        static NPath _dotsRuntimePackagePath;

        internal static NPath DotsRuntimePackagePath
        {
            get
            {
                if (_dotsRuntimePackagePath == null)
                {
                    _dotsRuntimePackagePath = Path.GetFullPath("Packages/com.unity.dots.runtime");
                }
                return _dotsRuntimePackagePath;
            }
        }
    }
}
