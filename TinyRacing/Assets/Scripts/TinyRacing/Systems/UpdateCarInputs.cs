using Unity.Entities;
using Unity.Tiny.Input;
using Unity.Tiny.UI;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Fill the CarInputs component with the current user input if it's not a AI controlled entity.
    /// </summary>
    [AlwaysUpdateSystem]
    [UpdateBefore(typeof(UpdateCarAIInputs))]
    public class UpdateCarInputs : SystemBase
    {
        protected override void OnUpdate()
        {
            var left = false;
            var right = false;
            var reverse = false;
            var accelerate = false;

            var Input = World.GetExistingSystem<InputSystem>();

            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                reverse = true;
            }

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                accelerate = true;
            }

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                left = true;
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                right = true;
            }

            if (HasSingleton<UIGameControls>())
            {
                var UIGameControls = GetSingleton<UIGameControls>();

                Entities.ForEach((Entity e, in UIState state) =>
                {
                    if (state.IsPressed)
                    {
                        if (e == UIGameControls.ButtonAccelerate)
                        {
                            accelerate = true;
                        }

                        if (e == UIGameControls.ButtonReverse)
                        {
                            reverse = true;
                        }

                        if (e == UIGameControls.ButtonLeft)
                        {
                            left = true;
                        }

                        if (e == UIGameControls.ButtonRight)
                        {
                            right = true;
                        }
                    }
                }).WithStructuralChanges().Run();

                CarInputs carInputs = default;

                if (accelerate)
                {
                    carInputs.AccelerationAxis = 1f;
                }
                else if (reverse)
                {
                    carInputs.AccelerationAxis = -1f;
                }

                if (left)
                {
                    carInputs.HorizontalAxis = -1f;
                }
                else if (right)
                {
                    carInputs.HorizontalAxis = 1f;
                }

                if (HasSingleton<CarAccelerometerSteering>())
                {
                    var carSteering = GetSingleton<CarAccelerometerSteering>();
                    if (carSteering.State == SensorState.Available && carSteering.HorizontalAxis != 0.0f)
                    {
                        carInputs.HorizontalAxis = carSteering.HorizontalAxis;
                    }
                }

                Entities.WithAll<Player>().ForEach((ref CarInputs ci) => { ci = carInputs; }).Run();
            }
        }
    }
}
