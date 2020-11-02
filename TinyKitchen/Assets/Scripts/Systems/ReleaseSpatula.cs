using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Tiny.Audio;
using Random = Unity.Mathematics.Random;

namespace TinyKitchen
{
    public class ReleaseSpatula : SystemBase
    {
        Random m_Random;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Random = new Random(1337);
        }

        protected override void OnUpdate()
        {
            // Get access to settings
            var settings = GetSingleton<SettingsSingleton>();

            // Get access to audio
            var flipper = GetSingleton<Flipper>();
            var flipperSound = flipper.FlipperAudio;
            var flipperSoundData = EntityManager.GetComponentData<AudioSource>(flipperSound);

            Entities.ForEach((Entity entity, ref FoodInstanceComponent food, ref PhysicsVelocity velocity,
                in PhysicsMass mass, in LaunchComponent launchComponent) =>
            {
                food.isLaunched = true;

                // Add propulsion to food
                float power = launchComponent.strength * settings.launchStrengthMultiplier;
                power = math.pow(power, settings.launchExp);
                float3 impulse = launchComponent.direction * power;

                float t = 1.0f - launchComponent.strength;
                t = math.pow(t, settings.heightExp);
                t = 1.0f - t;
                impulse.y = settings.launchHeight * t;

                velocity.ApplyLinearImpulse(mass, impulse);
                velocity.ApplyAngularImpulse(mass, m_Random.NextFloat3() * settings.launchRotateAmount);

                World.EntityManager.AddComponent<PhysicsGravityFactor>(entity);
                PhysicsGravityFactor physicsGravityFactor = new PhysicsGravityFactor();
                physicsGravityFactor.Value = 1.0f;
                World.EntityManager.SetComponentData(entity, physicsGravityFactor);

                // Play audio
                EntityManager.RemoveComponent<LaunchComponent>(entity);
                EntityManager.AddComponent<AudioSourceStart>(flipperSound);
            }).WithStructuralChanges().WithoutBurst().Run();
        }
    }
}