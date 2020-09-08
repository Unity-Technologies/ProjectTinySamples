using Unity.Entities;
using Unity.Tiny.Input;

namespace Unity.Spaceship
{
    public class KeyboardInputSystem : SystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            
            RequireSingletonForUpdate<ActiveInput>();
        }

        protected override void OnUpdate()
        {
            var input = World.GetExistingSystem<InputSystem>();
            var activeInput = GetSingleton<ActiveInput>();

            if (input.GetKey(KeyCode.S) || input.GetKey(KeyCode.DownArrow))
                activeInput.Reverse = true;
            if (input.GetKey(KeyCode.W) || input.GetKey(KeyCode.UpArrow))
                activeInput.Accelerate = true;

            if (input.GetKey(KeyCode.A) || input.GetKey(KeyCode.LeftArrow))
                activeInput.Left = true;
            if (input.GetKey(KeyCode.D) || input.GetKey(KeyCode.RightArrow))
                activeInput.Right = true;            
            
            SetSingleton(activeInput);
        }
    }
}
