using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TinyInternal.Bridge
{
    public static class TinyAnimationEditorBridge
    {
        public enum RotationMode
        {
            Baked = RotationCurveInterpolation.Mode.Baked,
            NonBaked = RotationCurveInterpolation.Mode.NonBaked,
            RawQuaternions = RotationCurveInterpolation.Mode.RawQuaternions,
            RawEuler = RotationCurveInterpolation.Mode.RawEuler,
            Undefined = RotationCurveInterpolation.Mode.Undefined
        }

        public static RotationMode GetRotationMode(EditorCurveBinding binding)
        {
            return (RotationMode)RotationCurveInterpolation.GetModeFromCurveData(binding);
        }

        public static string CreateRawQuaternionsBindingName(string componentName)
        {
            return $"{RotationCurveInterpolation.GetPrefixForInterpolation(RotationCurveInterpolation.Mode.RawQuaternions)}.{componentName}";
        }

        public static AnimationClipSettings GetAnimationClipSettings(AnimationClip clip)
        {
            return AnimationUtility.GetAnimationClipSettings(clip);
        }

        public static AnimationClip[] GetAnimationClipsInAnimationPlayer(GameObject gameObject)
        {
            return AnimationUtility.GetAnimationClipsInAnimationPlayer(gameObject);
        }

        public static AnimatorController GetEffectiveAnimatorController(Animator animator)
        {
            return AnimatorController.GetEffectiveAnimatorController(animator);
        }

        public static void RegisterDirtyCallbackForAnimatorController(AnimatorController controller, Action dirtyCallback)
        {
            controller.OnAnimatorControllerDirty += dirtyCallback;
        }

        public static void UnregisterDirtyCallbackFromAnimatorController(AnimatorController controller, Action dirtyCallback)
        {
            // ReSharper disable once DelegateSubtraction
            controller.OnAnimatorControllerDirty -= dirtyCallback;
        }
    }
}
