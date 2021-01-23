using Unity.Entities;

namespace BlendShapeDemo 
{
    public enum CameraState 
    {
        Stable,
        Moving   
    }

    [GenerateAuthoringComponent]
    public struct Camera : IComponentData
    {
        public bool destinationReached;
        public float cameraSpeed;
        public CameraState cameraState;
    }
}
