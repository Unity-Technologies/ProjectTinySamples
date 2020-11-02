using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Input;
using Unity.Transforms;
using Unity.Tiny;

namespace Unity.Spaceship
{
    public class RestartSystem : SystemBase
    {
        private EntityQuery m_AsteroidsQuery;
        private EntityQuery m_MissileQuery;

        protected override void OnCreate()
        {
            base.OnCreate();

            RequireSingletonForUpdate<GameState>();
            RequireSingletonForUpdate<HudShowState>();
            RequireSingletonForUpdate<Player>();

            m_AsteroidsQuery = GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadOnly<Asteroid>(), ComponentType.ReadOnly<SpriteRenderer>()
            });

            m_MissileQuery = GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadOnly<Missile>(), ComponentType.ReadOnly<SpriteRenderer>()
            });
        }

        protected override void OnUpdate()
        {
            var gameState = GetSingleton<GameState>();
            var input = World.GetExistingSystem<InputSystem>();
            var playerEntity = GetSingletonEntity<Player>();

            if (gameState.Value == GameStates.Start
                && IsPressingToStart(input))
            {
                // remove all stuff
                EntityManager.DestroyEntity(m_AsteroidsQuery);
                EntityManager.DestroyEntity(m_MissileQuery);

                // put the ship back in the center
                EntityManager.SetComponentData(playerEntity, new Translation
                {
                    Value = float3.zero
                });
                EntityManager.SetComponentData(playerEntity, new Rotation
                {
                    Value = quaternion.identity
                });

                AudioUtils.PlaySound(EntityManager, AudioTypes.ExtraShip);

                gameState.Value = GameStates.InGame;
                SetSingleton(gameState);
            }
        }

        private static bool IsPressingToStart(InputSystem input)
        {
            var isTouchSupported = input.IsTouchSupported();

            if (isTouchSupported
                && input.TouchCount() > 0
                && input.GetTouch(0).phase == TouchState.Began)
                return true;
            else if (!isTouchSupported
                && input.GetKeyDown(KeyCode.Space))
                return true;

            return false;
        }
    }
}
