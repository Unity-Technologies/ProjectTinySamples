using Unity.Entities;
using Unity.Transforms;


namespace BlendShapeDemo
{
    /// <summary>
    /// Show panel according to game state
    /// </summary>

    [UpdateAfter(typeof(TransformSystemGroup))]
    public class ShowPanelSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var game = GetSingleton<Game>();

            Entities.WithEntityQueryOptions(EntityQueryOptions.IncludeDisabled).ForEach((Entity entity, ref Panel panel) =>
            {
                EntityManager.SetEnabled(entity, panel.gameState == game.gameState);

            }).WithStructuralChanges().Run();
        }
    }
}
