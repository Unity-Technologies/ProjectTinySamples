using Unity.Entities;
using Unity.Transforms;

namespace BlendShapeDemo 
{
    /// <summary>
    /// Detect the selected character
    /// </summary>

    public class CharacterDetection : SystemBase
    {
        protected override void OnUpdate()
        {
            var game = GetSingleton<Game>();

            Entities.ForEach((ref Entity entity, ref Tappable tappable, ref Character character, ref Translation translation) =>
            {
                if (game.gameState != GameState.Moving) 
                {
                    if (tappable.IsTapped)
                    {
                        character.selected = true;                    
                    } 
                }

                tappable.IsTapped = false;

            }).WithStructuralChanges().Run();
        }
    }
}
