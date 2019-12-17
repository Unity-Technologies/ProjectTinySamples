using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using Unity.Transforms;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Spawn smoke from the car's exhaust.
    /// </summary>
    public class SpawnSmoke : ComponentSystem
    {
        private Random Random;

        protected override void OnCreate()
        {
            base.OnCreate();
            Random = new Random();
            Random.InitState();
        }

        protected override void OnUpdate()
        {
            var deltaTime = Time.DeltaTime;
            Entities.ForEach((ref SmokeSpawner smokeSpawner, ref Car car, ref Translation translation,
                ref LocalToWorld localToWorld) =>
            {
                if (smokeSpawner.SmokePrefab == Entity.Null)
                    return;

                smokeSpawner.SpawnTimer += deltaTime;
                if (smokeSpawner.SpawnTimer < smokeSpawner.SpawnInterval)
                    return;

                smokeSpawner.SpawnTimer = 0f;
                var scale = car.IsEngineDestroyed ? Random.NextFloat(2f, 3.5f) : Random.NextFloat(0.5f, 1.2f);
                var duration = car.IsEngineDestroyed ? 2f : 0.45f;
                var startAngle = Random.NextFloat(-180f, 180f);
                var smokeEntity = EntityManager.Instantiate(smokeSpawner.SmokePrefab);

                EntityManager.SetComponentData(smokeEntity,
                    new Smoke {Duration = duration, BaseScale = scale, Speed = 2f});
                EntityManager.SetComponentData(smokeEntity, new Translation
                {
                    Value =
                        translation.Value +
                        localToWorld.Right * smokeSpawner.SpawnOffset.x +
                        localToWorld.Up * smokeSpawner.SpawnOffset.y +
                        localToWorld.Forward * smokeSpawner.SpawnOffset.z
                });
                EntityManager.SetComponentData(smokeEntity,
                    new Rotation
                        {Value = math.mul(quaternion.identity, quaternion.RotateZ(math.radians(startAngle)))});
                // scale isn't present by default during transform conversion
                EntityManager.AddComponentData(smokeEntity, new Scale {Value = scale});
            });
        }
    }
}
