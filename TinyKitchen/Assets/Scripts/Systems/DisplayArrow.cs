using Unity.Entities;

namespace TinyKitchen
{
    ///<summary>
    /// Display Arrow according to the game state
    ///</summary>
    public class DisplayArrow : SystemBase
    {
        protected override void OnUpdate()
        {
            var game = GetSingleton<Game>();
            if (game.gameState == GameState.Flying)
            {
                // Hide arrows
                Entities.ForEach((ref Entity entity, in ArrowComponent arrow) =>
                {
                    EntityManager.AddComponent<Disabled>(entity);
                }).WithoutBurst().WithStructuralChanges().Run();
            }

            if (game.gameState == GameState.Idle)
            {
                // Show arrows
                Entities.ForEach((ref Entity entity, in ArrowComponent arrow, in Disabled disabled) =>
                {
                    EntityManager.RemoveComponent<Disabled>(entity);
                }).WithoutBurst().WithStructuralChanges().Run();
            }
        }
    }
}