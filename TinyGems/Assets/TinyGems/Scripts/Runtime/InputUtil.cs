using Unity.Tiny.Input;
using Unity.Mathematics;

namespace Unity.TinyGems
{
    public static class InputUtil
    {
        public static bool GetInputDown(InputSystem input)
        {
            if (input.IsMousePresent())
                return input.GetMouseButtonDown(0);

            return input.TouchCount() > 0 &&
                input.GetTouch(0).phase == TouchState.Began;
        }

        public static bool GetInputUp(InputSystem input)
        {
            if (input.IsMousePresent())
                return input.GetMouseButtonUp(0);

            return input.TouchCount() > 0 &&
                input.GetTouch(0).phase == TouchState.Ended;            
        }

        public static float2 GetInputPosition(InputSystem input)
        {
            if (input.IsMousePresent())
                return input.GetInputPosition();

            return input.TouchCount() > 0 ? new float2(input.GetTouch(0).x, input.GetTouch(0).y) : float2.zero;
        }
    }
}