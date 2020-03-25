using UnityEngine;
using Unity.Entities;

namespace Tiny3D
{
    [GenerateAuthoringComponent]
    public struct Rotate : IComponentData
    {
        public float speedX;
        public float speedY;
        public float speedZ;
    }
}

