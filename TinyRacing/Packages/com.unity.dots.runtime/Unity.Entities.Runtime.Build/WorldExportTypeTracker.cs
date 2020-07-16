using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;

namespace Unity.Entities.Runtime.Build
{
    internal class WorldExportTypeTracker
    {
        readonly HashSet<Type> m_TypesInUse = new HashSet<Type>();
        public IReadOnlyList<Type> TypesInUse => m_TypesInUse.ToList();

        public void AddTypesFromWorld(World world)
        {
            using (var archetypes = new NativeList<EntityArchetype>(Allocator.TempJob))
            {
                world.EntityManager.GetAllArchetypes(archetypes);
                foreach (var arch in archetypes)
                {
                    if (arch.ChunkCount == 0)
                        continue;
                    using (var types = arch.GetComponentTypes())
                    {
                        foreach (var atype in types)
                        {
                            m_TypesInUse.Add(TypeManager.GetTypeInfo(atype.TypeIndex).Type);
                        }
                    }
                }
            }
        }
    }
}
