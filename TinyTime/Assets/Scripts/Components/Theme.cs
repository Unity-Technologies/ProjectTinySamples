using Unity.Entities;
using Unity.Tiny;

namespace TinyTime
{
    public struct Theme : IComponentData
    {
        public Color DaylightBackground;
        public Color DaylightAmbientColor;
        public Color DaylightUIColor;
        public Color NightBackground;
        public Color NightAmbientColor;
        public Color NightUIColor;
    }
}
