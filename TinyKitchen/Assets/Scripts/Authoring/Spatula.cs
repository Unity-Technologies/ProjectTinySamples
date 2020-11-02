using UnityEngine;
using UnityEngine.Assertions;
using Unity.Entities;
using Unity.Mathematics;

namespace TinyKitchen
{
    public class Spatula : MonoBehaviour, IConvertGameObjectToEntity
    {

        public Entity AudioSpatula;
        public Transform tip, mid, pin;
        [Min(0)] public float len = 1.0f;
        public float snap = 0.25f;
        [Range(0, 1)] public float friction = 0.01f;
        [Min(0)] public float deadzone = 1.0f;
        public float bend = 1.0f;
        public bool bendPin = false;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            Assert.IsNotNull(tip);
            Assert.IsNotNull(mid);
            Assert.IsNotNull(pin);

            var spatula = default(SpatulaComponent);
            spatula.len = len;
            spatula.snap = snap;
            spatula.deadzone = deadzone;
            spatula.friction = friction;
            spatula.bend = bend;
            spatula.bendPin = bendPin;

            spatula.tip = conversionSystem.GetPrimaryEntity(tip);
            spatula.mid = conversionSystem.GetPrimaryEntity(mid);
            spatula.pin = conversionSystem.GetPrimaryEntity(pin);

            spatula.joy = math.float2(0);
            spatula.velocity = math.float2(0);
            spatula.kinematic = true;

            dstManager.AddComponentData(entity, spatula);
        }
    }
}
