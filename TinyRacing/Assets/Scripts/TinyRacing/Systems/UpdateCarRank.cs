using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Update all ranks based on progress
    /// </summary>
    [UpdateAfter(typeof(UpdateCarLapProgress))]
    public class UpdateCarRank : SystemBase
    {
        private Entity _playerCarEntity;

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _playerCarEntity = GetSingletonEntity<Player>();
        }

        protected override void OnUpdate()
        {
            var race = GetSingleton<Race>();
            if (!race.IsInProgress())
            {
                return;
            }

            var progress = new NativeList<EntityAndProgress>(race.NumCars, Allocator.Temp);
            progress.ResizeUninitialized(race.NumCars);

            var carIndex = 0;
            Entities.WithAll<Car>().ForEach((Entity entity, ref LapProgress lapProgress) =>
            {
                progress[carIndex] = new EntityAndProgress {car = entity, progress = lapProgress.TotalProgress};
                carIndex++;
            }).WithoutBurst().Run();

            progress.Sort(new EntityAndProgressComparer());

            for (var i = 0; i < race.NumCars; ++i)
            {
                var rank = GetComponent<CarRank>(progress[i].car);
                rank.Value = i + 1;
                SetComponent(progress[i].car, rank);
            }
        }

        private struct EntityAndProgress
        {
            public Entity car;
            public float progress;
        }

        private struct EntityAndProgressComparer : IComparer<EntityAndProgress>
        {
            public int Compare(EntityAndProgress x, EntityAndProgress y)
            {
                return y.progress.CompareTo(x.progress);
            }
        }
    }
}
