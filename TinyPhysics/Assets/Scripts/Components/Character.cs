using Unity.Entities;

namespace TinyPhysics.Systems
{
    public enum CharacterStates
    {
        Idle,
        Run,
        Jump
    }

    [GenerateAuthoringComponent]
    public struct Character : IComponentData
    {
        public CharacterStates characterStates;
        public bool hasJumped;
        public bool isGrounded;
    }
}