using Unity.Collections;
using Unity.Entities;

namespace TinyTime
{
    [GenerateAuthoringComponent]
    public struct TimeData:IComponentData
    {
        public bool IsNightTime;
    }
}
