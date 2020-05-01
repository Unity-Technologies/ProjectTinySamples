#if UNITY_DOTSPLAYER
using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny;
using Unity.Tiny.Input;

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
        private Entity _accelerometerOff;
        private Entity _accelerometerOn;

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<Race>();
            RequireSingletonForUpdate<CarAccelerometerSteering>();
            RequireSingletonForUpdate<ButtonAccelerometerOnTag>();
            RequireSingletonForUpdate<ButtonAccelerometerOffTag>();

            var Input = World.GetExistingSystem<InputSystem>();
            var carSteering = EntityManager.CreateEntity(typeof(CarAccelerometerSteering));
            var carSteeringData = new CarAccelerometerSteering
            {
                HorizontalAxis = 0.0f,
                State = Input.IsAvailable<AccelerometerSensor>() ? SensorState.Disabled : SensorState.NotAvailable
            };
            EntityManager.SetComponentData(carSteering, carSteeringData);
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _accelerometerOn = GetSingletonEntity<ButtonAccelerometerOnTag>();
            _accelerometerOff = GetSingletonEntity<ButtonAccelerometerOffTag>();
        }

        protected override void OnUpdate()
        {
            var race = GetSingleton<Race>();
            var carSteering = GetSingleton<CarAccelerometerSteering>();

            if (!race.IsRaceStarted || carSteering.State == SensorState.NotAvailable)
            {
                EntityManager.SetEnabled(_accelerometerOn, false);
                EntityManager.SetEnabled(_accelerometerOff, false);
                return;
            }

            var Input = World.GetExistingSystem<InputSystem>();
            if (Input.IsTouchSupported() && Input.TouchCount() > 0)
                for (var i = 0; i < Input.TouchCount(); i++)
                {
                    var itouch = Input.GetTouch(i);
                    var pos = new float2(itouch.x, itouch.y);
                    if (itouch.phase == TouchState.Ended)
                    {
                        var di = GetSingleton<DisplayInfo>();
                        // TODO currently rendering is done with 1080p, with aspect kept.
                        // We might not be using the actual width.  DisplayInfo needs to get reworked.
                        var height = di.height;
                        var width = di.width;
                        var targetRatio = 1920.0f / 1080.0f;
                        var actualRatio = width / (float)height;
                        if (actualRatio > targetRatio)
                        {
                            width = (int)(di.height * targetRatio);
                            pos.x -= (di.width - width) / 2.0f;
                        }

                        var screenRatio = pos.x / width;
                        if (pos.x / width < 0.15f && pos.y / height > 0.75f)
                        {
                            if (carSteering.State == SensorState.Disabled)
                            {
                                carSteering.State = SensorState.NoData;
                                Input.EnableSensor<AccelerometerSensor>();
                                Input.SetSensorSamplingFrequency<AccelerometerSensor>(30);
                            }
                            else if (carSteering.State > SensorState.Disabled)
                            {
                                carSteering.State = SensorState.Disabled;
                                Input.DisableSensor<AccelerometerSensor>();
                            }
                        }
                    }
                }

            var dir = 0.0f;
            if (carSteering.State == SensorState.NoData && HasSingleton<AccelerometerSensor>())
                carSteering.State = SensorState.Available;
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
                    if (angle < -kMaxAngle) angle = -kMaxAngle;
                    dir = (angle + kDeadZone) / (kMaxAngle - kDeadZone);
                }
                else if (angle > kDeadZone)
                {
                    if (angle > kMaxAngle) angle = kMaxAngle;
                    dir = (angle - kDeadZone) / (kMaxAngle - kDeadZone);
                }
            }

            carSteering.HorizontalAxis = dir;
            SetSingleton(carSteering);
            EntityManager.SetEnabled(_accelerometerOn, carSteering.State > SensorState.Disabled);
            EntityManager.SetEnabled(_accelerometerOff, carSteering.State == SensorState.Disabled);
        }
    }
}
#endif
