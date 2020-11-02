using Unity.Entities;

namespace TinyKitchen
{
    /// <summary>
    /// Instantiate particles effects when the player scores
    /// </summary>
    [UpdateBefore(typeof(AnimateRipple))]
    public class CreateParticleEffect : SystemBase
    {
        Entity m_PotParticleEntity;
        Entity m_PotParticleInstanceEntity;
        float m_ElapseTime;

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            var particleEffect = GetSingleton<SpecialEffectsSingleton>();
            m_PotParticleEntity = particleEffect.PotParticles;
        }

        protected override void OnUpdate()
        {
            var settings = GetSingleton<SettingsSingleton>();
            var game = GetSingleton<Game>().gameState;
            var deltaTime = Time.DeltaTime;

            if (game == GameState.Scored && m_PotParticleInstanceEntity == Entity.Null)
            {
                // Instantiate particles when the player scores
                m_PotParticleInstanceEntity = EntityManager.Instantiate(m_PotParticleEntity);

                // Reset Timer
                m_ElapseTime = 0.0f;
                return;
            }

            // Increase timer over time
            m_ElapseTime += deltaTime;

            if (m_PotParticleInstanceEntity != Entity.Null && m_ElapseTime > settings.effectTime)
            {
                // Destroy particles if time limit is reached
                EntityManager.DestroyEntity(m_PotParticleInstanceEntity);
                m_PotParticleInstanceEntity = Entity.Null;
            }
        }
    }
}