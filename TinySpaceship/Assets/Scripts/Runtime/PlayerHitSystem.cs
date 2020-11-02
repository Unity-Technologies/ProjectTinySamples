using Unity.Entities;
using Unity.Transforms;
using Unity.U2D.Entities.Physics;

namespace Unity.Spaceship
{
    [AlwaysSynchronizeSystem]
    public class PlayerHitSystem : SystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();

            RequireSingletonForUpdate<GameState>();
        }

        protected override void OnUpdate()
        {
            var gameState = GetSingleton<GameState>();
            if (gameState.Value != GameStates.InGame)
                return;

            var gameStateEntity = GetSingletonEntity<GameState>();

            var physicsWorldSystem = World.GetExistingSystem<PhysicsWorldSystem>();
            var physicsWorld = physicsWorldSystem.PhysicsWorld;

            var wasPlayerHit = false;
            Entities
                .WithAll<Player>()
                .WithoutBurst()
                .ForEach((
                    in Entity e,
                    in PhysicsColliderBlob collider,
                    in Translation tr,
                    in Rotation rot) =>
                {
                    // check with player
                    if (physicsWorld.OverlapCollider(
                        new OverlapColliderInput
                        {
                            Collider = collider.Collider,
                            Transform = new PhysicsTransform(tr.Value, rot.Value),
                            Filter = collider.Collider.Value.Filter
                        },
                        out OverlapColliderHit hit))
                    {
                        wasPlayerHit = true;

                        // stop everything
                        EntityManager.SetComponentData(gameStateEntity, new GameState
                        {
                            Value = GameStates.Start
                        });
                    }
                }).Run();

            if(wasPlayerHit)
                AudioUtils.PlaySound(EntityManager, AudioTypes.PlayerExplosion);
        }
    }
}
