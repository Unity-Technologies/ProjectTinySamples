using System;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Tiny.Codec;
using Unity.Entities.Runtime;

namespace Unity.Entities.Runtime.Build
{
    internal static class WorldExport
    {
        public static bool WriteWorldToFile(World world, FileInfo outputFile)
        {
            // TODO need to bring this back
#if false
            // Check for missing assembly references
            var unresolvedComponentTypes = GetAllUsedComponentTypes(world).Where(t => !DomainCache.IsIncludedInProject(project, t.GetManagedType())).ToArray();
            if (unresolvedComponentTypes.Length > 0)
            {
                foreach (var unresolvedComponentType in unresolvedComponentTypes)
                {
                    var type = unresolvedComponentType.GetManagedType();
                    Debug.LogError($"Could not resolve component type '{type.FullName}' while exporting {scenePath.ToHyperLink()}. Are you missing an assembly reference to '{type.Assembly.GetName().Name}' ?");
                }
                return false;
            }
#endif

            var directoryName = Path.GetDirectoryName(outputFile.FullName);
            if (string.IsNullOrEmpty(directoryName))
            {
                throw new ArgumentException($"Invalid output file directory: {directoryName}", nameof(outputFile));
            }

            if (!Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }

            // Merges the entities and shared component streams, (optionally) compresses them, and finally serializes to disk with a small header in front
            using (var fileStream = new StreamBinaryWriter(outputFile.FullName))
            using (var entitiesWriter = new MemoryBinaryWriter())
            {
                var entityRemapInfos = new NativeArray<EntityRemapUtility.EntityRemapInfo>(world.EntityManager.EntityCapacity, Allocator.Temp);
                SerializeUtility.SerializeWorldInternal(world.EntityManager, entitiesWriter, out var referencedObjects, entityRemapInfos, isDOTSRuntime: true);
                entityRemapInfos.Dispose();

                if (referencedObjects != null)
                {
                    throw new ArgumentException("We are serializing a world that contains UnityEngine.Object references which are not supported in Dots Runtime.");
                }

                unsafe
                {
                    var worldHeader = new SceneHeader();
                    worldHeader.DecompressedSize = entitiesWriter.Length;
                    worldHeader.Codec = Codec.LZ4;
                    worldHeader.SerializeHeader(fileStream);

                    if (worldHeader.Codec != Codec.None)
                    {
                        int compressedSize = CodecService.Compress(worldHeader.Codec, entitiesWriter.Data, entitiesWriter.Length, out var compressedData);
                        fileStream.WriteBytes(compressedData, compressedSize);
                    }
                    else
                    {
                        fileStream.WriteBytes(entitiesWriter.Data, entitiesWriter.Length);
                    }
                }
            }

            return true;
        }
    }
}
