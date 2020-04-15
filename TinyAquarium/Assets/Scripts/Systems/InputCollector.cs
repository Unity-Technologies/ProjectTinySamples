using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Input;

namespace TinyAquarium
{
    public class InputCollector : SystemBase
    {
        protected override void OnUpdate()
        {
            //Get the keyboard input data
            var inputAxis = new float2(0, 0);
            var input = World.GetOrCreateSystem<InputSystem>();
            if (input.GetKey(KeyCode.W) || input.GetKey(KeyCode.UpArrow))
                inputAxis.y = 1;
            if (input.GetKey(KeyCode.S) || input.GetKey(KeyCode.DownArrow))
                inputAxis.y = -1;
            if (input.GetKey(KeyCode.A) || input.GetKey(KeyCode.LeftArrow))
                inputAxis.x = -1;
            if (input.GetKey(KeyCode.D) || input.GetKey(KeyCode.RightArrow))
                inputAxis.x = 1;

            //Get the touch input data
            if (input.TouchCount() > 0 && input.GetTouch(0).phase == TouchState.Moved)
            {
                var touchDelta = new float2(input.GetTouch(0).deltaX, input.GetTouch(0).deltaY);
                inputAxis = math.normalizesafe(touchDelta);
            }

            Entities.ForEach((ref PlayerInput playerInput) =>
            {
                playerInput.InputAxis = inputAxis;
            }).WithBurst().Run();
        }
    }
}
