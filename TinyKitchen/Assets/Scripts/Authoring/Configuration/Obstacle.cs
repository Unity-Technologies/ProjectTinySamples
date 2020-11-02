using System;
using Unity.Mathematics;
using UnityEngine;

namespace TinyKitchen
{
    [Serializable]
    public class Obstacle
    {
        public GameObject prefab;
        public float3 position;
        public float3 scale;
    }
}