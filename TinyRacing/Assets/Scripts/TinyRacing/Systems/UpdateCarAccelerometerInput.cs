using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Input;
using Unity.Tiny.UI;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Fill the CarAccelerometerSteering component with the current user accelerometer steering input.
    /// </summary>
    [UpdateBefore(typeof(UpdateCarInputs))]
    public class UpdateCarAccelerometerInput : SystemBase
    {
        private const float kDeadZone = 0.1f;
        private const float kMaxAngle = 0.6f;

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<CarAccelerometerSteering>();
        }

        protected override void OnUpdate()
        {
            var carSteering = GetSingleton<CarAccelerometerSteering>();
            var Input = World.GetExistingSystem<InputSystem>();
            if (!Input.IsAvailable<AccelerometerSensor>())
            {
                var rectTransform = EntityManager.GetComponentData<RectTransform>(carSteering.UIToggle);
                rectTransform.Hidden = true;
                EntityManager.SetComponentData(carSteering.UIToggle, rectTransform);
                Enabled = false;
                return;
            }

            if (carSteering.State == SensorState.NotAvailable)
            {
                carSteering.State = SensorState.Disabled;
            }

            var useAccelerometer = false;
            Entities.ForEach(
                (Entity e, ref Toggleable toggleable, ref RectTransform rectTransform,
                    ref CarAccelerometerSteering carAccelerometerSteering) =>
                {
                    useAccelerometer = toggleable.IsToggled;
                }).Run();

            if (carSteering.State == SensorState.Disabled)
            {
                carSteering.State = SensorState.NoData;
                Input.EnableSensor<AccelerometerSensor>();
                Input.SetSensorSamplingFrequency<AccelerometerSensor>(30);
            }

            if (useAccelerometer)
            {
                var dir = 0.0f;
                if (carSteering.State == SensorState.NoData && HasSingleton<AccelerometerSensor>())
                {
                    carSteering.State = SensorState.Available;
                }

                if (carSteering.State == SensorState.Available)
                {
                    var data = GetSingleton<AccelerometerSensor>();
                    var x = data.Acceleration.y;
                    var y = -data.Acceleration.x;
                    if (x < 0)
                    {
                        x = -x;
                        y = -y;
                    }

                    var angle = math.atan2(y, x);
                    if (angle < -kDeadZone)
                    {
                        if (angle < -kMaxAngle)
                        {
                            angle = -kMaxAngle;
                        }

                        dir = (angle + kDeadZone) / (kMaxAngle - kDeadZone);
                    }
                    else if (angle > kDeadZone)
                    {
                        if (angle > kMaxAngle)
                        {
                            angle = kMaxAngle;
                        }

                        dir = (angle - kDeadZone) / (kMaxAngle - kDeadZone);
                    }
                }

                carSteering.HorizontalAxis = dir;
            }
            else
            {
                carSteering.State = SensorState.NoData;
            }

            SetSingleton(carSteering);
        }
    }
}
