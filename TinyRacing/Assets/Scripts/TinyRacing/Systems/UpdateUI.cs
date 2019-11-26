using Unity.Collections;
using Unity.Entities;
using Unity.Tiny.Rendering;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Update game the UI labels
    /// </summary>
    [UpdateAfter(typeof(ResetRace))]
    public class UpdateUI : ComponentSystem
    {
        private NativeArray<UINumberMaterial> Numbers;
        private Entity UInumber;

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<UINumbers>();
        }

        protected override void OnUpdate()
        {
            UInumber = GetSingletonEntity<UINumbers>();
            var nBuffer = EntityManager.GetBuffer<UINumberMaterial>(UInumber);
            Numbers = nBuffer.ToNativeArray(Allocator.Persistent);

            if (Numbers.Length == 0)
                return;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            Entities.ForEach((Entity entity, ref MeshRenderer renderMesh, ref LabelNumber labelNumber) =>
            {
                var e = Entity.Null;
                if (labelNumber.IsVisible)
                    e = Numbers[labelNumber.Number % 10 + 1].MaterialEntity;
                else
                    e = Numbers[0].MaterialEntity; // First element of the buffer should be the Empty material
                if (EntityManager.HasComponent<SimpleMaterial>(e) &&
                    EntityManager.HasComponent<SimpleMaterial>(renderMesh.material))
                {
                    var newMaterial = EntityManager.GetComponentData<SimpleMaterial>(e);
                    var currentMaterial = EntityManager.GetComponentData<SimpleMaterial>(renderMesh.material);
                    if (!newMaterial.Equals(currentMaterial)) ecb.SetComponent(renderMesh.material, newMaterial);
                }
            });
            ecb.Playback(EntityManager);
            ecb.Dispose();
            Numbers.Dispose();
        }
    }
}