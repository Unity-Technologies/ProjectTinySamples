using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.TinyGems
{
    public class CenterObjectSystem : ComponentSystem
    {
        private EntityQuery m_NewCamera;
        protected override void OnCreate()
        {
            m_NewCamera = GetEntityQuery(new EntityQueryDesc
            {
                All = new []
                {
                    ComponentType.ReadWrite<CenterObject>(),
                    ComponentType.ReadWrite<Translation>()
                }
            });
            
            RequireSingletonForUpdate<CellManager>();
        }

        protected override void OnUpdate()
        {
            var cellMan = GetSingleton<CellManager>();
            Entities.With(m_NewCamera).ForEach((Entity e, ref Translation t) =>
            {
                var x = cellMan.MaxCol * 0.5f;
                var y = cellMan.MaxRow * 0.5f;
                t.Value = new float3(x, y, t.Value.z);
                
                PostUpdateCommands.RemoveComponent<CenterObject>(e);
            });
        }
    }
}