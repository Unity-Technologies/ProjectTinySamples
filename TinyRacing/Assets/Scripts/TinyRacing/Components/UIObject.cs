using Unity.Entities;

namespace TinyRacing
{
    [GenerateAuthoringComponent]
    public struct UIObject : IComponentData
    {
        public enum UITypes
        {
            MainMenuScreen,
            GameScreen,
            EndScreen
        }

        public UITypes UIType;
    }
}
