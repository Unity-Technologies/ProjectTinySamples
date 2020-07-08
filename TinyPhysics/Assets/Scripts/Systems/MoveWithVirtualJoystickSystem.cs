using Unity.Entities;
using Unity.Mathematics;

namespace TinyPhysics.Systems
{
    /// <summary>
    ///     Read the referenced VirtualJoystick component to set the movement vector
    ///     Read the referenced Tappable component to set the jump trigger
    /// </summary>
    [UpdateBefore(typeof(MovementSystem))]
    [UpdateAfter(typeof(MoveWithKeyboardSystem))]
    public class MoveWithVirtualJoystickSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            UpdateMovement();
            UpdateJump();
        }

        private void UpdateMovement()
        {
            Entities.ForEach((ref MoveWithVirtualJoystick moveWithVirtualJoystick, ref Moveable moveable) =>
            {
                // The joystick used for movement is referenced in the MoveWithVirtualJoystick component
                if (HasComponent<VirtualJoystick>(moveWithVirtualJoystick.movementJoystick))
                {
                    var virtualJoystick = GetComponent<VirtualJoystick>(moveWithVirtualJoystick.movementJoystick);
                    if (virtualJoystick.IsPressed)
                    {
                        // Translate joystick direction into MoveData
                        moveable.moveDirection = new float3(virtualJoystick.Value.x, 0, virtualJoystick.Value.y);
                    }
                }
            }).Run();
        }

        private void UpdateJump()
        {
            Entities.ForEach((ref MoveWithVirtualJoystick moveWithVirtualJoystick, ref Jumper jumper) =>
            {
                // The button used for jumping is referenced in the MoveWithVirtualJoystick component
                if (HasComponent<Tappable>(moveWithVirtualJoystick.jumpButton))
                {
                    var jumpButton = GetComponent<Tappable>(moveWithVirtualJoystick.jumpButton);
                    if (jumpButton.IsTapped)
                    {
                        // Set jump trigger
                        jumper.JumpTrigger = true;

                        // Consume tap
                        jumpButton.IsTapped = false;
                        EntityManager.SetComponentData(moveWithVirtualJoystick.jumpButton, jumpButton);
                    }
                }

            }).WithoutBurst().Run();
        }
    }
}
