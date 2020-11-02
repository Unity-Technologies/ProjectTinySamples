using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Tiny.Rendering;

namespace TinyKitchen
{
    ///<summary>
    /// Animate the ripple effects (arrows)
    ///</summary>
    public class AnimateRipple : SystemBase
    {
        protected override void OnUpdate()
        {
            var dt = Time.DeltaTime;

            Entities.ForEach((ref Ripple ripple, ref NonUniformScale scale, ref MeshRenderer renderer) =>
            {
                var t = ripple.Time * ripple.speed;

                // Return to initial values
                if (t > 1.0f)
                    return;

                // Decrease opacity
                var mat = EntityManager.GetComponentData<SimpleMaterial>(renderer.material);
                mat.constOpacity = 1.0f - t * t;
                EntityManager.SetComponentData(renderer.material, mat);

                // Increase size value
                scale.Value = math.lerp(ripple.initialScale, math.float3(ripple.maxScale), t * t);

                ripple.Time += dt;
            }).WithoutBurst().Run();
        }
    }
}