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

    [GenerateAuthoringComponent]
    public struct CarAccelerometerSteering : IComponentData
    {
        public float HorizontalAxis;
        public SensorState State;
        public Entity UIToggle;
    }
}
