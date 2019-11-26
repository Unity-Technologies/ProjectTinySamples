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
        private PrefabReferences PrefabReferences;
        private Entity PrefabReferencesEntity;

        protected override void OnCreate()
        {
            base.OnCreate();
            Random = new Random();
            Random.InitState();
            RequireSingletonForUpdate<PrefabReferences>();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            PrefabReferencesEntity = GetSingletonEntity<PrefabReferences>();
            PrefabReferences = EntityManager.GetComponentData<PrefabReferences>(PrefabReferencesEntity);
        }

        protected override void OnUpdate()
        {
            var deltaTime = Time.DeltaTime;
            Entities.ForEach((ref SmokeSpawner smokeSpawner, ref Car car, ref Translation translation,
                ref LocalToWorld localToWorld) =>
            {
                smokeSpawner.SpawnTimer += deltaTime;
                if (smokeSpawner.SpawnTimer > smokeSpawner.SpawnInterval)
                {
                    smokeSpawner.SpawnTimer = 0f;
                    var scale = car.IsEngineDestroyed ? Random.NextFloat(2f, 3.5f) : Random.NextFloat(0.5f, 1.2f);
                    var duration = car.IsEngineDestroyed ? 2f : 0.45f;
                    var startAngle = Random.NextFloat(-180f, 180f);
                    Entity smokeEntity;

                    if (car.IsEngineDestroyed)
                        smokeEntity = EntityManager.Instantiate(PrefabReferences.carSmokeDestroyedPrefab);
                    else
                        smokeEntity = EntityManager.Instantiate(PrefabReferences.carSmokePrefab);

                    PostUpdateCommands.AddComponent(smokeEntity,
                        new Smoke {Duration = duration, BaseScale = scale, Speed = 2f});
                    PostUpdateCommands.AddComponent(smokeEntity, new Translation
                    {
                        Value =
                            translation.Value +
                            localToWorld.Right * smokeSpawner.SpawnOffset.x +
                            localToWorld.Up * smokeSpawner.SpawnOffset.y +
                            localToWorld.Forward * smokeSpawner.SpawnOffset.z
                    });
                    PostUpdateCommands.AddComponent(smokeEntity, new LocalToWorld());
                    PostUpdateCommands.AddComponent(smokeEntity,
                        new Rotation
                            {Value = math.mul(quaternion.identity, quaternion.RotateZ(math.radians(startAngle)))});
                    PostUpdateCommands.AddComponent(smokeEntity, new Scale {Value = scale});
#if UNITY_DOTSPLAYER
                    MeshRenderer meshRenderer = EntityManager.GetComponentData<MeshRenderer>(smokeEntity);
                    LitMaterial litMaterial = EntityManager.GetComponentData<LitMaterial>(meshRenderer.material);

                    PostUpdateCommands.AddComponent(smokeEntity, litMaterial);
                    meshRenderer.material = smokeEntity;
                    PostUpdateCommands.AddComponent(smokeEntity, meshRenderer);
                    PostUpdateCommands.AddComponent(smokeEntity, new DynamicMaterial());
#endif
                }
            });
        }
    }
}
