using Unity.Build;

namespace Unity.Entities.Runtime.Build
{
    internal sealed class DotsRuntimeRunInstance : IRunInstance
    {
        public bool IsRunning => throw new System.NotImplementedException();

        public void Dispose()
        {
        }
    }
}
