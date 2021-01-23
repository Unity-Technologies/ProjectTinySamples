using Unity.Entities;
using Unity.Transforms;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Spawn smoke when the car's engine is destroyed.
    /// </summary>
    public class SmokeSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var deltaTime = Time.DeltaTime;
            Entities.ForEach((Entity entity, ref Smoke smoke, ref Car car, ref Translation translation,
                ref LocalToWorld localToWorld) =>
            {
                if (smoke.ExplosionPrefab == Entity.Null)
                {
                    return;
                }

                if (car.PlayCrashAudio)
                {
                    var explosionEntity = EntityManager.Instantiate(smoke.ExplosionPrefab);
                    smoke.Explosion = explosionEntity;
                    EntityManager.AddComponent<Disabled>(smoke.CarSmoke);
                }

                if (smoke.Explosion != Entity.Null)
                {
                    EntityManager.SetComponentData(smoke.Explosion, translation);
                }
            }).WithStructuralChanges().Run();
        }
    }
}
