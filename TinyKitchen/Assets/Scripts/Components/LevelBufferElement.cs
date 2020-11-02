using Unity.Entities;
using Unity.Mathematics;

namespace TinyKitchen
{
    //<summary>
    // Settings to have different levels
    //</summary>


    public struct LevelBufferElement : IBufferElementData
    {
        //Pot Settings
        public float3 potPosition;
        public float radius;
        public bool isMoving;
        public float potSpeed;
        public float3 initialPosition;
        public float3 finalPosition;

        //Fan Settings
        public float3 fanPosition;
        public float3 fanHeading;
        public float fanForce;
        public bool isRotating;
        public float rotationSpeed;
        public float3 initialHeading;
        public float3 finalHeading;
        public float3 fanUIPos;
    }
}
