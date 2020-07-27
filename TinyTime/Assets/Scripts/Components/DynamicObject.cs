using Unity.Entities;

namespace TinyTime
{
    [GenerateAuthoringComponent]
    public struct DynamicObject:IComponentData
    {
        public enum ObjectTime
        {
            DayTime,
            NightTime
        }

        public ObjectTime ShowOn;
    }
}
