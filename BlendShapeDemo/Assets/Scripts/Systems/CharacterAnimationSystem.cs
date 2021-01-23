using System.Collections;
using Unity.Entities;
using Unity.Tiny.Animation;



namespace BlendShapeDemo
{
    /// <summary>
    /// Update character animation according to the game states
    /// </summary>
   
    public class CharacterAnimationSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var game = GetSingleton<Game>();

            Entities.ForEach((ref Entity entity, ref Character character, ref Tappable tappable) =>
            {

                if (!character.selected)
                {
                    // Idle animation
                    UpdateCharacterAnimation(entity, 0);
                }
                else 
                {
                    // Celebration animation
                    UpdateCharacterAnimation(entity, 1);
                }


            }).WithStructuralChanges().Run();
        }

        public void UpdateCharacterAnimation(Entity entity, int clipIndex)
        {
           // TinyAnimation.SetTime(World, entity, 0);
            TinyAnimation.SelectClipAtIndex(World, entity, clipIndex);
            TinyAnimation.Play(World, entity);

        }
    }
}

