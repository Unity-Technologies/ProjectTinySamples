using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TinyKitchen
{
    ///<summary>
    /// Reset each component of the spatula at the initial tip one
    ///</summary>
    [UpdateAfter(typeof(SpawnFood))]
    [UpdateAfter(typeof(UpdateSpatula))]
    public class FollowSpatula : SystemBase
    {
        protected override void OnUpdate()
        {
            var game = GetSingleton<Game>();
            var spatulaPosition = GetSingleton<SpatulaComponent>().spatulaPos;

            if (game.gameState == GameState.Aiming)
            {
                // Reset each component of the spatula at the initial tip one
                Entities.ForEach((Entity entity, in SpatulaComponent spatula) =>
                {
                    var tipPos = EntityManager.GetComponentData<LocalToWorld>(spatula.tip).Position;
                    spatulaPosition = tipPos;
                }).WithoutBurst().Run();

                EntityManager.SetComponentData(game.FoodOnSpatula, new Translation()
                {
                    Value = spatulaPosition
                });
            }
        }
    }
}