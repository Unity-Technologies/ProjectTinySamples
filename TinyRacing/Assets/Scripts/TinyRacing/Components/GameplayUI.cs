using Unity.Entities;

namespace TinyRacing
{
    [GenerateAuthoringComponent]
    public struct GameplayUI : IComponentData
    {
        public Entity CountdownLabel;
        public Entity LapLabel;
        public Entity LapTotalLabel;
        public Entity RankLabel;
        public Entity RankTotalLabel;
        public Entity LapProgressLabel;

        public Entity CurrentTimeLabel;
        public Entity TimeFromLeaderLabel;
    }
}
