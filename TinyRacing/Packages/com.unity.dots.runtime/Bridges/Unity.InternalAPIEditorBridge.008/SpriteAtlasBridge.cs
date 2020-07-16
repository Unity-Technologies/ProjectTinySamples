using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.U2D;
using UnityEditor;
using UnityEditor.U2D;
#if !UNITY_2019_3_OR_NEWER
using UnityEditor.Experimental.U2D;
#endif

namespace TinyInternal.Bridge
{
    internal static class SpriteAtlasBridge
    {
        private static Dictionary<SpriteAtlas, Texture2D> m_SpriteAtlasTextureCache = new Dictionary<SpriteAtlas, Texture2D>();
        private static Dictionary<Texture2D, SpriteAtlas> m_TextureSpriteAtlasCache = new Dictionary<Texture2D, SpriteAtlas>();

        internal static void PackAllSpriteAtlases()
        {
            SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget, canCancel: false);

            m_SpriteAtlasTextureCache.Clear();
            m_TextureSpriteAtlasCache.Clear();

            var atlases = Resources.FindObjectsOfTypeAll<SpriteAtlas>();
            foreach (var atlas in atlases)
            {
                var sprites = GetPackedSprites(atlas);
                var texture = sprites.FirstOrDefault()?.GetAtlasTexture() ?? null;
                if (texture && texture != null)
                {
                    m_SpriteAtlasTextureCache.Add(atlas, texture);
                    m_TextureSpriteAtlasCache.Add(texture, atlas);
                }
            }
        }

        internal static Sprite[] GetPackedSprites(this SpriteAtlas atlas)
        {
            return SpriteAtlasExtensions.GetPackedSprites(atlas);
        }

        internal static Texture2D GetTexture(this SpriteAtlas atlas)
        {
            if (m_SpriteAtlasTextureCache.TryGetValue(atlas, out var texture))
            {
                return texture;
            }
            return null;
        }

        internal static SpriteAtlas GetAtlas(this Texture2D texture)
        {
            if (m_TextureSpriteAtlasCache.TryGetValue(texture, out var atlas))
            {
                return atlas;
            }
            return null;
        }

        internal static bool IsInAtlas(this Sprite sprite)
        {
            return GetAtlasTexture(sprite) != null;
        }

        internal static SpriteAtlas GetAtlas(this Sprite sprite)
        {
            return SpriteEditorExtension.GetActiveAtlas(sprite);
        }

        internal static Texture2D GetAtlasTexture(this Sprite sprite)
        {
            var texture = SpriteEditorExtension.GetActiveAtlasTexture(sprite);
            if (null == texture || !texture)
            {
                return null;
            }
            return texture.GetInstanceID() == sprite.texture.GetInstanceID() ? null : texture;
        }

        internal static Texture2D GetAtlasAlphaTexture(this Sprite sprite)
        {
            return SpriteEditorExtension.GetActiveAtlasAlphaTexture(sprite);
        }

        internal static Rect GetAtlasTextureRect(this Sprite sprite)
        {
            return SpriteEditorExtension.GetActiveAtlasTextureRect(sprite);
        }

        internal static Vector2 GetAtlasTextureRectOffset(this Sprite sprite)
        {
            return SpriteEditorExtension.GetActiveAtlasTextureRectOffset(sprite);
        }
    }
}
