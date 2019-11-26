using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Tiny;
using UnityEngine;

namespace Tiny3D
{
    public class RotationSystem : ComponentSystem
    {
        [BurstCompile]
        public struct RotateJob : IJobForEach<Rotate, Rotation>
        {
            public float time;

            public void Execute(ref Rotate rotate, ref Rotation rotation)
            {
                quaternion qx = quaternion.RotateX(time * rotate.speedX);
                quaternion qy = quaternion.RotateY(time * rotate.speedY);
                quaternion qz = quaternion.RotateZ(time * rotate.speedZ);
                rotation.Value = math.normalize(math.mul(qz, math.mul(qy, qx)));
            }
        }
        protected override void OnUpdate()
        {
            var job = new RotateJob();
            job.time = (float)Time.ElapsedTime;
            job.Schedule(this).Complete();
        }
    }
}

