using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny;
#if UNITY_DOTSPLAYER
using System;
using Unity.Tiny.Input;

#else
using UnityEngine;
#endif

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Fill the CarInputs component with the current user input if it's not a AI controlled entity.
    /// </summary>
    [AlwaysUpdateSystem]
    [UpdateBefore(typeof(UpdateCarAIInputs))]
    public class UpdateCarInputs : ComponentSystem
    {
        protected override void OnUpdate()
        {
            var left = false;
            var right = false;
            var reverse = false;
            var accelerate = false;
#if UNITY_DOTSPLAYER
            var Input = World.GetExistingSystem<InputSystem>();
#endif

            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                reverse = true;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                accelerate = true;

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                left = true;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                right = true;

#if !UNITY_DOTSPLAYER
            if (Input.GetMouseButton(0))
                PressAtPosition(new float2(Input.mousePosition.x, Input.mousePosition.y), ref left, ref right, ref reverse, ref accelerate);

            for (int i = 0; i < Input.touchCount; i++)
            {
                var pos = Input.GetTouch(i).position;
                PressAtPosition(new float2(pos.x, pos.y), ref left, ref right, ref reverse, ref accelerate);
            }
#else
            if (Input.IsTouchSupported() && Input.TouchCount() > 0)
            {
                for (var i = 0; i < Input.TouchCount(); i++)
                {
                    var itouch = Input.GetTouch(i);
                    var pos = new float2(itouch.x, itouch.y);
                    PressAtPosition(pos, ref left, ref right, ref reverse, ref accelerate);
                }
            }
            else
            {
                if (Input.GetMouseButton(0))
                {
                    var xpos = (int) Input.GetInputPosition().x;
                    PressAtPosition(Input.GetInputPosition(), ref left, ref right, ref reverse, ref accelerate);
                }
            }
#endif

            CarInputs inputs = default;
            if (left)
                inputs.HorizontalAxis = -1f;
            else if (right)
                inputs.HorizontalAxis = 1f;

            if (accelerate)
                inputs.AccelerationAxis = 1f;
            else if (reverse)
                inputs.AccelerationAxis = -1f;

            Entities.WithNone<AI>().ForEach((ref CarInputs iv) => { iv = inputs; });
        }

        private void PressAtPosition(float2 inputScreenPosition, ref bool isLeftPressed, ref bool isRightPressed,
            ref bool isReversePressed, ref bool isAcceleratePressed)
        {
            // Determine which button is pressed byt checking the x value of the screen position.
            // TODO: Replace this with a UI interaction system

#if !UNITY_DOTSPLAYER
            int width = Screen.width;
#else
            var di = GetSingleton<DisplayInfo>();

            // TODO currently rendering is done with 1080p, with aspect kept.
            // We might not be using the actual width.  DisplayInfo needs to get reworked.
            var height = di.height;
            int width = di.width;
            float targetRatio = 1920.0f / 1080.0f;
            float actualRatio = (float) width / (float) height;
            if (actualRatio > targetRatio)
            {
                width = (int) (di.height * targetRatio);
                inputScreenPosition.x -= (di.width - width) / 2.0f;
            }
            // if height > width, then the full width will get used for display
#endif

            var screenRatio = inputScreenPosition.x / width;
            if (screenRatio > 0.85f)
                isAcceleratePressed = true;
            else if (screenRatio > 0.7f)
                isReversePressed = true;
            else if (screenRatio < 0.15f)
                isLeftPressed = true;
            else if (screenRatio < 0.3f)
                isRightPressed = true;
        }
    }
}