using UnityEngine;

namespace TinyInternal.Bridge
{
    public static class TinyAnimationEngineBridge
    {
        public static bool HasRootMotion(AnimationClip clip)
        {
            return clip.hasRootMotion;
        }
    }
}
