using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

using Random = Unity.Mathematics.Random;

namespace Unity.Spaceship
{
    [UpdateAfter(typeof(UIInputSystem))]
    [UpdateAfter(typeof(KeyboardInputSystem))]
    public class PlayerMoveSystem : SystemBase
    {
        private Random m_Random;
        private bool m_IsPlayingMoveSound = false;
        
        protected override void OnCreate()
        {
            base.OnCreate();
            
            RequireSingletonForUpdate<Player>();
            RequireSingletonForUpdate<GameState>();
            
            m_Random = new Random(314159);

            EntityManager.CreateEntity(typeof(ActiveInput));
        }

        protected override void OnUpdate()
        {
            var gameState = GetSingleton<GameState>();
            if (gameState.Value != GameStates.InGame)
            {
                if(m_IsPlayingMoveSound)
                { StopMoveSound(); }
                
                return;
            }

            var deltaTime = Time.DeltaTime;

            var playerEntity = GetSingletonEntity<Player>();
            var player = GetSingleton<Player>();
            var tr = EntityManager.GetComponentData<Translation>(playerEntity);
            var rot = EntityManager.GetComponentData<Rotation>(playerEntity);

            var activeInput = GetSingleton<ActiveInput>();
            var isMoving = activeInput.Left || activeInput.Right || activeInput.Accelerate || activeInput.Reverse;
            
            // move player
            if (activeInput.Left)
                rot.Value = math.mul(rot.Value, quaternion.RotateZ(player.RotationSpeed * deltaTime));
            if (activeInput.Right)
                rot.Value = math.mul(rot.Value, quaternion.RotateZ(-player.RotationSpeed * deltaTime));

            var pos = float3.zero;
            if (activeInput.Reverse)
                pos.y -= player.MoveSpeed * deltaTime;
            if (activeInput.Accelerate)
                pos.y += player.MoveSpeed * deltaTime;
            
            tr.Value += math.mul(rot.Value, pos);
            
            EntityManager.SetComponentData(playerEntity, tr);
            EntityManager.SetComponentData(playerEntity, rot);
            
            if(isMoving && !m_IsPlayingMoveSound)
            { PlayMoveSound(); }
            else if(!isMoving && m_IsPlayingMoveSound)
            { StopMoveSound(); }
            
            SetSingleton(new ActiveInput());
        }

        private void PlayMoveSound()
        {
            AudioUtils.PlaySound(EntityManager, AudioTypes.PlayerThruster, true);
            m_IsPlayingMoveSound = true;
        }

        private void StopMoveSound()
        {
            AudioUtils.StopSound(EntityManager, AudioTypes.PlayerThruster);
            m_IsPlayingMoveSound = false;
        }
    }
}