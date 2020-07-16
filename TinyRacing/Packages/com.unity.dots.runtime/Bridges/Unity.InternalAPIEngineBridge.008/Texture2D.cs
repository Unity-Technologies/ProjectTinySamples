using UnityEngine;

namespace TinyInternal.Bridge
{
#if UNITY_EDITOR
    internal static class Texture2DExtensions
    {
        public static float GetPixelsPerPoint(this Texture2D texture2D)
        {
            return texture2D.pixelsPerPoint;
        }
    }
#endif
}
