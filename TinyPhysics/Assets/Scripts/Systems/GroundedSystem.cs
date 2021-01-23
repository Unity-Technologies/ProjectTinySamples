using Unity.Entities;
using Unity.Jobs;

namespace TinyPhysics.Systems
{
    /// <summary>
    /// Detect if the character is grounded
    /// </summary>

    [UpdateAfter(typeof(CollisionSystem))]
    public class GroundedSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ref Collideable collideable, ref Character character) =>
            {
                // update grounded value
                bool isColliding = collideable.CollisionEntity != Entity.Null;
                character.isGrounded = isColliding ? character.isGrounded = true : character.isGrounded = false;

                // Consume collision
                collideable.CollisionEntity = Entity.Null;
            }).WithStructuralChanges().Run();
        }
    }

}
