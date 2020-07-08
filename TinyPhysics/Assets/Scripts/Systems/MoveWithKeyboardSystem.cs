using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Tiny.Input;

namespace TinyPhysics.Systems
{
    /// <summary>
    ///     Listen for the keys used to set a movement direction vector
    ///     Listen for the key used to set the jump trigger
    /// </summary>
    [UpdateBefore(typeof(MovementSystem))]
    public class MoveWithKeyboardSystem : SystemBase
    {
        private InputSystem m_InputSystem;

        protected override void OnCreate()
        {
            m_InputSystem = World.GetExistingSystem<InputSystem>();
        }

        protected override void OnUpdate()
        {
            UpdateMovement();
            UpdateJump();
        }

        private void UpdateMovement()
        {
            float3 moveDirection = float3.zero;

            // Determine force vector based on WASD keys
            if (m_InputSystem.GetKey(KeyCode.W)) moveDirection.z += 1f;
            if (m_InputSystem.GetKey(KeyCode.S)) moveDirection.z -= 1f;
            if (m_InputSystem.GetKey(KeyCode.A)) moveDirection.x -= 1f;
            if (m_InputSystem.GetKey(KeyCode.D)) moveDirection.x += 1f;

            if (moveDirection.x != 0 || moveDirection.z != 0)
            {
                // Normalize force vector
                moveDirection = math.normalize(moveDirection);
            }

            // Set move direction of all entities that can move
            Entities.WithAll<MoveWithKeyboard>().ForEach((ref Moveable moveable) =>
            {
                moveable.moveDirection = moveDirection;
            }).Run();
        }

        private void UpdateJump()
        {
           if (m_InputSystem.GetKeyDown(KeyCode.Space))
            {
                Entities.WithAll<MoveWithKeyboard>().ForEach((ref Jumper jumper) =>
                {
                    jumper.JumpTrigger = true;
                }).Run();
            }
        }
    }
}
