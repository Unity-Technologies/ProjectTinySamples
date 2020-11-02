using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

namespace TinyKitchen
{
    ///<summary>
    /// Change camera transform values according to the spatula and food
    ///</summary>
    [UpdateAfter(typeof(UpdateSpatula))]
    public class AnimateCamera : SystemBase
    {
        Entity m_CameraEntity;
        CamAnimComponent m_CameraAnimation;

        protected override void OnStartRunning()
        {
            // get access to camera values
            m_CameraEntity = GetSingletonEntity<CamAnimComponent>();
            m_CameraAnimation = EntityManager.GetComponentData<CamAnimComponent>(m_CameraEntity);
            m_CameraAnimation.origin = m_CameraAnimation.lastPos = EntityManager.GetComponentData<Translation>(m_CameraEntity).Value;
            m_CameraAnimation.id = m_CameraAnimation.lastRot = EntityManager.GetComponentData<Rotation>(m_CameraEntity).Value;
            
        }

        protected override void OnUpdate()
        {
            var settings = GetSingleton<SettingsSingleton>();
            var deltaTime = Time.DeltaTime;
            // Define spatula information
            var spatula = GetSingleton<SpatulaComponent>();
            var joy = spatula.joy;
            var target = m_CameraAnimation.origin + math.float3(joy.x, joy.y, joy.y) * -settings.cameraFollowAmount;
            target = math.lerp(m_CameraAnimation.lastPos, target, deltaTime * settings.cameraDamp);
            // Make camera move accordingly to the spatula 
            m_CameraAnimation.lastPos = target;
            EntityManager.SetComponentData<Translation>(m_CameraEntity, new Translation
            {
                Value = target,
            });
            
            Entities.ForEach((ref Translation pos, in FoodInstanceComponent food) =>
            {
                // Return if there is no food flying
                if (!food.isLaunched)
                    return;

                // Make camera bounce accordingly to the food position
                var dir = math.normalize(pos.Value - target);
                var rot = quaternion.LookRotation(dir, math.up());
                var look = math.slerp(m_CameraAnimation.id, rot, settings.cameraTrackAmount);
                look = math.slerp(m_CameraAnimation.lastRot, look, deltaTime * settings.cameraDamp);

                EntityManager.SetComponentData<Rotation>(m_CameraEntity, new Rotation
                {
                    Value = look,
                });

                m_CameraAnimation.lastRot = look;
            }).WithoutBurst().Run();

            EntityManager.SetComponentData<CamAnimComponent>(m_CameraEntity, m_CameraAnimation);
        }
    }
}