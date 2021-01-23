using Unity.Entities;
using Unity.Physics;
using Unity.Mathematics;
using Unity.Transforms;

namespace TinyPhysics.Systems
{
    /// <summary>
    /// Set the character state according to the input and velocity
    /// </summary>


    [UpdateBefore(typeof(JumpSystem))]
    public class CharacterStateSystem : SystemBase
    {
        protected override void OnUpdate()
        {       
            Entities.ForEach((ref Entity entity, ref Character character, ref PhysicsVelocity physicsVelocity, ref Translation translation, ref Jumper jumper) =>
            {
                if (character.isGrounded)
                {
                    character.hasJumped = false;

                    if (!jumper.JumpTrigger)
                    {
                        if ((physicsVelocity.Linear.x <= 0.1 && physicsVelocity.Linear.x >= -0.1) &&
                            (physicsVelocity.Linear.z <= 0.1 && physicsVelocity.Linear.z >= -0.1))
                        {
                            // Character is immobile
                            physicsVelocity.Linear = float3.zero;
                            character.characterStates = CharacterStates.Idle;
                        }
                        else
                        {
                            // Character move
                            character.characterStates = CharacterStates.Run;
                        }
                    }
                }
                else 
                {
                    character.characterStates = CharacterStates.Jump;
                }
            }).WithStructuralChanges().Run();
        }
    }
}
