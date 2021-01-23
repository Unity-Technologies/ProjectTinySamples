using Unity.Entities;

namespace BlendShapeDemo 
{
    public enum CharacterType
    {
        Billy,
        Emily
    }

    [GenerateAuthoringComponent]
    public struct Character : IComponentData
    {
        public CharacterType characterType;
        public bool selected;
    }
}


