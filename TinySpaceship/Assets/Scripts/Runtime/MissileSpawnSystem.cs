using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.U2D.Entities.Physics;

namespace Unity.Spaceship
{
    public class MissileEmitSystem : SystemBase
    {
        private float m_ElapsedTime;
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<Player>();
        }

        protected override void OnUpdate()
        {
            var gameState = GetSingleton<GameState>();
            if (gameState.Value != GameStates.InGame)
                return;
            
            var playerEntity = GetSingletonEntity<Player>();
            var player = GetSingleton<Player>();
            var playerRot = EntityManager.GetComponentData<Rotation>(playerEntity);
            var playerTr = EntityManager.GetComponentData<Translation>(playerEntity);
                       
            m_ElapsedTime += Time.DeltaTime;
            var timeLimit = 1.0f / player.FireRate;
            if (m_ElapsedTime > timeLimit)
            {
                var missile = EntityManager.Instantiate(player.MissilePrefab);
                
                EntityManager.SetComponentData(missile, playerRot);
                EntityManager.SetComponentData(missile, playerTr);
                EntityManager.SetComponentData(missile, new PhysicsVelocity
                {
                    Linear =  math.mul(playerRot.Value, new float3(0f, player.FireSpeed, 0f)).xy
                });
                
                AudioUtils.PlaySound(EntityManager, AudioTypes.PlayerFire);
                
                m_ElapsedTime = 0;
            }
        }
    }
}