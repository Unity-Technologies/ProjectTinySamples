

using Unity.Entities;
using Unity.Tiny;

namespace TinyRacing.Systems
{
    [AlwaysUpdateSystem]
    public class Loader:SystemBase
    {
        public bool IsReady;

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            IsReady = false;
        }

        protected override void OnUpdate()
        {
            if (IsReady)
                return;
            Debug.Log("Loading...");
            var loading = false;
            Entities.WithAll<Image2DLoadFromFile>().ForEach((Entity e, ref Image2D img) =>
            {
                if (img.status == ImageStatus.LoadError)
                    Debug.Log("Error loading images");
                loading = true;
            }).WithStructuralChanges().Run();
            IsReady = !loading;

            if (IsReady)
                Debug.Log("Ready!");
        }
    }
}