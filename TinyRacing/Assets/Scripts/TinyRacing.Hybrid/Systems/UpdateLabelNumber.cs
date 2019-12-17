using System;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace TinyRacing.Systems.Hybrid
{
    /// <summary>
    /// Update temporary quad materials used as labels for displaying numbers since there's no UI text at the moment in Tiny/DOTS 
    /// </summary>
#if !UNITY_DOTSPLAYER

    [UpdateAfter(typeof(ResetRace))]
    public class UpdateLabelNumber : ComponentSystem
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<MaterialReferences>();
        }

        protected override void OnUpdate()
        {
            var materialReferencesEntity = GetSingletonEntity<MaterialReferences>();
            var materialReferences = EntityManager.GetSharedComponentData<MaterialReferences>(materialReferencesEntity);
            Entities.ForEach((Entity entity, RenderMesh renderMesh, ref LabelNumber labelNumber) =>
            {
                Material newMaterial = null;

                if (labelNumber.IsVisible)
                    newMaterial = materialReferences.Numbers[labelNumber.Number % 10];
                else
                    newMaterial = materialReferences.Empty;

                if (newMaterial != renderMesh.material)
                {
                    renderMesh.material = newMaterial;
                    PostUpdateCommands.SetSharedComponent(entity, renderMesh);
                }
            });
        }
    }

    [Serializable]
    public struct MaterialReferences : ISharedComponentData, IEquatable<MaterialReferences>
    {
        public Material Empty;
        public Material[] Numbers;

        public bool Equals(MaterialReferences other)
        {
            return Empty == other.Empty;
        }

        public override int GetHashCode()
        {
            return Empty.GetHashCode();
        }
    }
#endif
}

