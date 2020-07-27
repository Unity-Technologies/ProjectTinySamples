using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using Unity.Tiny.Text;
using Unity.Transforms;

namespace TinyTime
{
    [UpdateAfter(typeof(TransformSystemGroup))]
    public class ThemeSystem : SystemBase
    {
        private bool nightTime;
        private bool refresh = true;

        protected override void OnCreate()
        {
            RequireSingletonForUpdate<TimeData>();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            var timeData = GetSingleton<TimeData>();
            var theme = GetSingleton<Theme>();
            var isNight = timeData.IsNightTime;
            if (nightTime != isNight)
            {
                nightTime = isNight;
                refresh = true;
            }

            // Change theme only when required
            if (refresh)
            {
                ChangeSceneAmbientColor(isNight, theme);
                ChangeCameraBackground(isNight, theme);
                ChangeUITextColor(isNight, theme);
                SetObjectsVisibility(isNight);
                SetFireLightIntensity(isNight);
                refresh = false;
            }
        }

        private void ChangeCameraBackground(bool isNight, Theme theme)
        {
            Entities.ForEach((ref Camera camera) =>
            {
                if (isNight)
                    camera.backgroundColor = theme.NightBackground;
                else
                    camera.backgroundColor = theme.DaylightBackground;
            }).ScheduleParallel();
        }

        private void ChangeSceneAmbientColor(bool isNight, Theme theme)
        {
            Entities.ForEach((ref AmbientLight ambientLight, ref Light light) =>
            {
                if (isNight)
                    light.color = new float3(theme.NightAmbientColor.r, theme.NightAmbientColor.g,
                        theme.NightAmbientColor.b);
                else
                    light.color = new float3(theme.DaylightAmbientColor.r, theme.DaylightAmbientColor.g,
                        theme.DaylightAmbientColor.b);
            }).ScheduleParallel();
        }

        private void ChangeUITextColor(bool isNight, Theme theme)
        {
            Entities.ForEach((ref TextRenderer textRenderer) =>
            {
                if (isNight)
                    textRenderer.MeshColor = theme.NightUIColor;
                else
                    textRenderer.MeshColor = theme.DaylightUIColor;
            }).ScheduleParallel();
        }

        private void SetObjectsVisibility(bool isNight)
        {
            Entities.WithEntityQueryOptions(EntityQueryOptions.IncludeDisabled)
                .ForEach((Entity entity, ref DynamicObject dynamicObject) =>
                {
                    if (isNight)
                        EntityManager.SetEnabled(entity, dynamicObject.ShowOn == DynamicObject.ObjectTime.NightTime);
                    else
                        EntityManager.SetEnabled(entity, dynamicObject.ShowOn == DynamicObject.ObjectTime.DayTime);
                }).WithStructuralChanges().Run();
        }

        private void SetFireLightIntensity(bool isNight)
        {
            Entities.ForEach((ref Light light, ref FireLight dynamicLight) =>
            {
                if (isNight)
                {
                    if (light.intensity == 0)
                        light.intensity = dynamicLight.Intensity;
                }
                else
                {
                    dynamicLight.Intensity = light.intensity;
                    light.intensity = 0;
                }
            }).WithStructuralChanges().Run();
        }
    }
}
