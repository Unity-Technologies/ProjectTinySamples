using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace TinyKitchen
{
    [CreateAssetMenu(fileName = "Level_0", menuName = "Configuration/Level", order = 1)]
    public class LevelConfigurationData : ScriptableObject
    {
        public int selectedPot;
        public int selectedFan;

        [Header("Pot Settings")]
        public float3 potPosition;
        [Range(0.5f, 5)] //Need to define the range
        public float radius = 1;
        public bool isMoving; //If the pot is moving we need to fill the InitialPosition and the FinalPosition
        [Min(0)] public float potSpeed;
        public float3 initialPosition;
        public float3 finalPosition;

        [Space(10)]
        [Header("Fan Settings")]
        public float3 fanPosition;
        public float3 fanHeading;
        public float fanForce;
        public bool isRotating;
        public float rotationSpeed;
        public float3 initialHeading; //Check the best way for the rotation ranges
        public float3 finalHeading;
        public float3 fanUIPos;

        [Space(10)]
        [Header("Obstacles Settings")]
        public List<Obstacle> obstacles = new List<Obstacle>();
    }

}
