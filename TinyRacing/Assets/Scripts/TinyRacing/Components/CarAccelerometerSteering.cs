using Unity.Entities;

namespace TinyRacing
{
    public enum SensorState
    {
        NotAvailable,
        Disabled,
        NoData,
        Available
    }

    public struct CarAccelerometerSteering : IComponentData
    {
        public float HorizontalAxis;
        public SensorState State;
    }
}