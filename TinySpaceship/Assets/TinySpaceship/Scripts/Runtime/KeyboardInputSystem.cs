using Unity.Entities;
using Unity.Jobs;
using Unity.Tiny.Input;

namespace Unity.Spaceship
{
    public class KeyboardInputSystem : JobComponentSystem
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            
            RequireSingletonForUpdate<ActiveInput>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
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
            
            return inputDeps;
        }
    }
}
