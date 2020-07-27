using Unity.Entities;
using TinyColor = Unity.Tiny.Color;
using UnityEngine;

namespace TinyTime.Authoring
{
    public class ThemeAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public Color m_DaylightAmbientColor;
        public Color m_DaylightBackground;
        public Color m_DaylightUIColor;
        public Color m_NightAmbientColor;
        public Color m_NightBackground;
        public Color m_NightUIColor;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            var theme = new Theme
            {
                DaylightBackground = ToTinyColor(m_DaylightBackground),
                DaylightAmbientColor = ToTinyColor(m_DaylightAmbientColor),
                DaylightUIColor = ToTinyColor(m_DaylightUIColor),
                NightBackground = ToTinyColor(m_NightBackground),
                NightAmbientColor = ToTinyColor(m_NightAmbientColor),
                NightUIColor = ToTinyColor(m_NightUIColor)
            };
            dstManager.AddComponentData(entity, theme);
        }

        public static TinyColor ToTinyColor(Color UTcolor)
        {
            return new TinyColor(UTcolor.linear.r, UTcolor.linear.g, UTcolor.linear.b, UTcolor.linear.a);
        }
    }
}
