using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Tiny;
using Unity.Tiny.Input;
using Unity.Tiny.Rendering;

namespace BlendShapeDemo
{
     /// <summary>
     /// Translate the cursor pressed according to the mouse position in the X axis
     /// Remap the value of the sliders to fit with the BlendShape weight
     /// Modify the character mesh according to the sliders
     /// </summary>
     
    public class BlendShapeSystem : SystemBase
    {
        private InputSystem inputsystem;

        protected override void OnCreate()
        {
            inputsystem = World.GetExistingSystem<InputSystem>();
        }

        protected override void OnUpdate()
        {
            var game = GetSingleton<Game>();
            var input = World.GetExistingSystem<InputSystem>();
            var inputPos = inputsystem.GetInputPosition();
            var mousePos = ScreenToWorldSystem.ScreenPointToWorldPoint(World, inputPos.x);

            if (game.gameState == GameState.Billy || game.gameState == GameState.Emily) 
            {
                Entities.ForEach((ref Entity entity, ref Cursor cursor, ref Tappable tappable, ref Translation translation) =>
                {                   
                    if (tappable.IsPressed) 
                    {
                        // Check if the current cursor is between the right range
                        if (translation.Value.x >= cursor.minRange && translation.Value.x <= cursor.maxRange) 
                        {
                            translation.Value.x = mousePos.x;                          
                            SetBlendShape(cursor.blendShapeOrder.ToString(), RemapSlider(cursor.minRange, cursor.maxRange, 0, 100, translation.Value.x));                         
                        }  
                        
                        // Clamp min slider
                        if(translation.Value.x <= cursor.minRange) 
                            translation.Value.x = cursor.minRange;

                        // Clamp max slider
                        if (translation.Value.x >= cursor.maxRange) 
                            translation.Value.x = cursor.maxRange;
                    }                       
                }).WithStructuralChanges().Run();          
            }
        }

        // Remap slider value to fit with the BlendShape weight (0, 100)
        public float RemapSlider(float rawMin, float rawMax, float newMin, float newMax, float rawValue)
        {
            float rawRange = (rawMax - rawMin);
            float newRange = (newMax - newMin);
            float newCurrentValue = (((rawValue - rawMin) * newRange) / rawRange) + newMin;

            return newCurrentValue;       
        }

        // Select the BlendShape we want and modify his weight according to the slider value
        public void SetBlendShape(string blendShapeName, float blendShapeWeight)
        {
            #if UNITY_DOTSRUNTIME

                EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

                Entities.ForEach((Entity entity, DynamicBuffer<BlendShapeWeight> smrBlendShapeWeightBuffer, ref SkinnedMeshRenderer smr, ref Character character) =>
                {
                    SetBlendShapeWeight setWeight = new SetBlendShapeWeight();
                 
                    setWeight.NameHash = BlendShapeChannel.GetNameHash(blendShapeName);
                    setWeight.ModifiedWeight = blendShapeWeight;
                    DynamicBuffer<SetBlendShapeWeight> setWeightsBuffer = ecb.AddBuffer<SetBlendShapeWeight>(entity);
                    setWeightsBuffer.Add(setWeight);
                }).WithoutBurst().Run();

                ecb.Playback(EntityManager);
                ecb.Dispose();

            #endif
        }
    }
}

