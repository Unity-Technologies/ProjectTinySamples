#if UNITY_DOTSRUNTIME
using Unity.Entities;
using Unity.Tiny;
using Unity.Tiny.UI;
using Unity.Transforms;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Switch on/off the UI menus based on the state of the game
    /// </summary>
    [UpdateAfter(typeof(TransformSystemGroup))]
    public class UIMenusHandler : SystemBase
    {
        protected override void OnStartRunning()
        {
            RequireSingletonForUpdate<Race>();
            var di = GetSingleton<DisplayInfo>();
            di.backgroundBorderColor = Colors.Black;
            SetSingleton(di);
            base.OnStartRunning();
        }

        protected override void OnUpdate()
        {
            if (!HasSingleton<Race>())
            {
                return;
            }

            var race = GetSingleton<Race>();
            Entities.ForEach((Entity e, ref UIObject uiObject, ref RectTransform rectTransform) =>
            {
                switch (uiObject.UIType)
                {
                    case UIObject.UITypes.MainMenuScreen:
                        rectTransform.Hidden = !race.IsNotStarted();
                        break;
                    case UIObject.UITypes.GameScreen:
                        rectTransform.Hidden = !race.IsInProgress();
                        break;
                    case UIObject.UITypes.EndScreen:
                        rectTransform.Hidden = !race.IsFinished();
                        break;
                }
            }).WithStructuralChanges().Run();
        }
    }
}
#endif
