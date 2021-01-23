using Unity.Entities;
using Unity.Tiny.Animation;
using Unity.Physics;
using Unity.Transforms;
using Unity.Mathematics;

namespace TinyPhysics.Systems
{
    /// <summary>
    /// Set animation to the character according to the situation he faces
    /// </summary>
    public class PlayerAnimationSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Check if the scene has a character
            if (HasSingleton<Character>()) 
            {
                var characterEntity = GetSingletonEntity<Character>();
                UpdateCharacterAnimation();
            }
        }

        public void UpdateCharacterAnimation() 
        {
            Entities.ForEach((ref Entity entity, ref Character character) =>
            {
                switch (character.characterStates)
                {
                    // Idle
                    case CharacterStates.Idle:
                        PlayAnimation(entity, (int)CharacterStates.Idle);
                        break;

                    // Walk
                    case CharacterStates.Run:
                        PlayAnimation(entity, (int)CharacterStates.Run);
                        break;

                    // Jump
                    case CharacterStates.Jump:
                        if (!character.hasJumped)
                        {
                            PlayAnimation(entity, (int)CharacterStates.Jump);
                            character.hasJumped = true;
                        }                 
                        break;
                }
            }).WithStructuralChanges().Run();
        }

        public void PlayAnimation(Entity entity, int clipIndex)
        {
            TinyAnimation.SelectClipAtIndex(World, entity, clipIndex);
            TinyAnimation.Play(World, entity);
        }
    }
}
