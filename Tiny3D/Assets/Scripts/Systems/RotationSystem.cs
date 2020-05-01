using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Tiny;
using UnityEngine;

namespace Tiny3D
{
    public class RotationSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float time = (float)Time.ElapsedTime;
            Entities.ForEach((ref Rotate rotate, ref Rotation rotation) =>
            {
                quaternion qx = quaternion.RotateX(time * rotate.speedX);
                quaternion qy = quaternion.RotateY(time * rotate.speedY);
                quaternion qz = quaternion.RotateZ(time * rotate.speedZ);
                rotation.Value = math.normalize(math.mul(qz, math.mul(qy, qx)));
            }).ScheduleParallel();
        }
    }
}
