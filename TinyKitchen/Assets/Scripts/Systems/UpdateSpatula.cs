using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Physics.Systems;

namespace TinyKitchen
{
    /// <summary>
    /// Add modifications to the transform and rotation
    /// of each spatula part according to the bending value
    /// </summary>
    [UpdateBefore(typeof(BuildPhysicsWorld))]
    public class UpdateSpatula : SystemBase
    {
        protected override void OnStartRunning()
        {
            Entities.ForEach((ref SpatulaComponent spatula) => { spatula.joy = math.float2(0.0f, 0.01f); })
                .ScheduleParallel();
        }

        protected override void OnUpdate()
        {
            var settings = GetSingleton<SettingsSingleton>();
            var deltaTime = Time.DeltaTime;

            Entities.ForEach((ref SpatulaComponent spatula) =>
            {
                // Tip section
                var dist = math.length(spatula.joy);
                var t = math.min(1.0f, dist / spatula.deadzone);
                t = t * t * t;

                var dir = spatula.joy / -dist;

                if (!spatula.kinematic)
                {
                    spatula.velocity *= 1.0f - spatula.friction;
                    if (deltaTime>0)
                        spatula.velocity += dir * t * spatula.snap / deltaTime;
                    spatula.joy += deltaTime * spatula.velocity;

                }
                else
                {
                    spatula.velocity = 0.0f;
                }

                var target = math.up() * spatula.len;
                var pad = spatula.joy;
                pad *= settings.inputScale;
                t = math.sqrt(1.0f - math.min(1.0f, dist));
                var tip = math.float3(pad.x, target.y * t, pad.y);
                SetComponent<Translation>(spatula.tip, new Translation
                {
                    Value = tip
                });

                var diff = -tip;
                dist = math.length(diff);
                var point = diff / dist;
                var right = math.cross(math.up(), point);
                var up = -point;
                var fwd = math.cross(right, up);

                var bent = quaternion.LookRotation(up, -fwd);
                var unbent = quaternion.LookRotation(fwd, up);
                SetComponent<Rotation>(spatula.tip, new Rotation
                {
                    Value = math.slerp(bent, unbent, t),
                });

                // Midpoint section
                fwd = math.cross(point, right);
                up = -point;

                t = 1.0f - math.min(1.0f, dist / spatula.len);
                var mid = 0.5f * tip;
                mid += fwd * t * spatula.bend;

                SetComponent<Translation>(spatula.mid, new Translation
                {
                    Value = mid
                });

                SetComponent<Rotation>(spatula.mid, new Rotation
                {
                    Value = quaternion.LookRotation(fwd, up),
                });

                // Pin section
                SetComponent<Translation>(spatula.pin, new Translation
                {
                    Value = math.float3(0)
                });

                SetComponent<Rotation>(spatula.pin, new Rotation
                {
                    Value = quaternion.LookRotation(math.lerp(math.cross(right, math.up()), math.float3(0, 0, 1), 0.5f),
                        math.up()),
                });

                if (spatula.bendPin)
                {
                    var rotation = GetComponent<Rotation>(spatula.pin);
                    SetComponent<Rotation>(spatula.pin, new Rotation
                    {
                        Value = math.slerp(rotation.Value, unbent, 0.1f),
                    });
                }
            }).Run();
        }
    }
}