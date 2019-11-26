using Unity.Entities;

namespace TinyRacing
{
    [GenerateAuthoringComponent]
    public struct Car : IComponentData
    {
        public float CurrentSpeed;
        public float MaxSpeed;
        public float RotationSpeed;
        public bool IsEngineDestroyed;
        public bool PlayCrashAudio;
    }
}