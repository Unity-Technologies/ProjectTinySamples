using Unity.Entities;
using Unity.Transforms;
#if UNITY_DOTSRUNTIME
using Unity.Tiny.Audio;

#endif

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Update the race ending UI menu
    /// </summary>
    [UpdateAfter(typeof(TransformSystemGroup))]
    public class UpdateGameOverMenu : SystemBase
    {
        protected override void OnUpdate()
        {
            var race = GetSingleton<Race>();
            SetMenuVisibility(race.IsRaceFinished);
        }

        private void SetMenuVisibility(bool isVisible)
        {
            if (isVisible)
            {
#if UNITY_DOTSRUNTIME
                Entities
                    .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabled)
                    .WithAll<GameOverMenuTag, AudioSource, Disabled>().ForEach((Entity entity) =>
                {
                    EntityManager.AddComponent<AudioSourceStart>(entity);
                }).WithStructuralChanges().Run();
#endif
                Entities
                    .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabled)
                    .WithAll<GameOverMenuTag, Disabled>().ForEach((Entity entity) =>
                {
                    EntityManager.RemoveComponent<Disabled>(entity);
                }).WithStructuralChanges().Run();
            }
            else
            {
                Entities.WithAll<GameOverMenuTag>().ForEach((Entity entity) =>
                {
                    EntityManager.AddComponent<Disabled>(entity);
                }).WithStructuralChanges().Run();
            }
        }
    }
}
