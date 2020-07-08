using Unity.Entities;
using Unity.Jobs;
using Unity.Tiny.Rendering;

namespace TinyPhysics.Systems
{
    /// <summary>
    ///     Detect when the player collides with the button and change its color
    /// </summary>
    [UpdateAfter(typeof(CollisionSystem))]
    public class ButtonCollisionSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ref Button button, ref Collideable collideable, ref MeshRenderer meshRenderer) =>
            {
                bool isColliding = collideable.CollisionEntity != Entity.Null;

                // Update color
                var material = EntityManager.GetComponentData<LitMaterial>(meshRenderer.material);
                material.constAlbedo = isColliding ? button.colorOn : button.colorOff;
                EntityManager.SetComponentData(meshRenderer.material, material);

                // Consume collision
                collideable.CollisionEntity = Entity.Null;
            }).WithStructuralChanges().Run();
        }
    }
}