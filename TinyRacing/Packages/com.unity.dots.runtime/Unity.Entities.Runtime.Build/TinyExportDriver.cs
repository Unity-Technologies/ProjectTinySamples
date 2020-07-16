using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Build;
using Unity.Entities.Runtime.Hashing;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Unity.Entities.Runtime.Build
{
    internal class TinyExportDriver : GameObjectConversionSettings
    {
        class Item
        {
            public Guid Guid;
            public string AssetPath;
            public FileInfo ExportFileInfo;
            public bool Exported;
        }

        readonly DirectoryInfo m_ExportDataRoot;
        readonly Dictionary<Object, Item> m_Items = new Dictionary<Object, Item>();

#if USE_INCREMENTAL_CONVERSION
        public TinyExportDriver(BuildConfiguration config, DirectoryInfo exportDataRoot, World destinationWorld, BlobAssetStore blobAssetStore) : base(destinationWorld, GameObjectConversionUtility.ConversionFlags.AddEntityGUID, blobAssetStore)
        {
            BuildConfiguration = config;
            m_ExportDataRoot = exportDataRoot;
            FilterFlags = WorldSystemFilterFlags.DotsRuntimeGameObjectConversion;
        }

#else
        public TinyExportDriver(BuildConfiguration config, DirectoryInfo exportDataRoot)
        {
            BuildConfiguration = config;
            m_ExportDataRoot = exportDataRoot;
            FilterFlags = WorldSystemFilterFlags.DotsRuntimeGameObjectConversion;
        }

#endif

        public override Guid GetGuidForAssetExport(Object asset)
        {
            if (!m_Items.TryGetValue(asset, out var found))
            {
                var assetPath = AssetDatabase.GetAssetPath(asset);
                var guid = GetGuidForUnityObject(asset);
                if (guid.Equals(Guid.Empty))
                {
                    return Guid.Empty;
                }

                var exportFileInfo = m_ExportDataRoot.GetFile(guid.ToString("N"));

                m_Items.Add(asset, found = new Item
                {
                    Guid = guid,
                    AssetPath = assetPath,
                    ExportFileInfo = exportFileInfo,
                });
            }

            return found.Guid;
        }

        public override Stream TryCreateAssetExportWriter(Object asset)
        {
            if (!m_Items.ContainsKey(asset))
            {
                UnityEngine.Debug.LogError($"TinyExportDriver: Trying to create export writer for asset {asset}, but it was never exported");
                return null;
            }

            var item = m_Items[asset];
            if (item.Exported)
                return null;

            item.Exported = true;
            item.ExportFileInfo.Directory.Create();

            return item.ExportFileInfo.Create();
        }

        public void Write(BuildManifest manifest)
        {
            foreach (var thing in m_Items.Values.Where(i => i.Exported))
                manifest.Add(thing.Guid, thing.AssetPath, EnumerableExtensions.ToSingleEnumerable<FileInfo>(thing.ExportFileInfo));
        }

        internal static Guid GetGuidForUnityObject(Object obj)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out var guid, out long fileId))
            {
                return Guid.Empty;
            }

            if (String.IsNullOrEmpty(guid) || guid == "00000000000000000000000000000000")
            {
                // Special case for memory textures
                if (obj is UnityEngine.Texture texture)
                {
                    return new Guid(texture.imageContentsHash.ToString());
                }

                UnityEngine.Debug.LogWarning($"Could not get {nameof(Guid)} for object type '{obj.GetType().FullName}'.");
                return Guid.Empty;
            }

            // Merge asset database guid and file identifier
            var bytes = new byte[guid.Length + sizeof(long)];
            Encoding.ASCII.GetBytes(guid).CopyTo(bytes, 0);
            BitConverter.GetBytes(fileId).CopyTo(bytes, guid.Length);
            return GuidUtility.NewGuid(bytes);
        }
    }
}
