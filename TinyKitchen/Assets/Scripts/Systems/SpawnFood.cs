using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Tiny.Rendering;

namespace TinyKitchen
{
    ///<summary>
    /// Instantiate next food item in the list
    /// and make it transparent to visualize the spatula angle
    ///</summary>
    [UpdateAfter(typeof(ReadGameInput))]
    public class SpawnFood : ComponentSystem
    {
        Random m_Random;

        protected override void OnCreate()
        {
            RequireSingletonForUpdate<FoodSpawnerComponent>();
            m_Random = new Random(314159);
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            var game = GetSingleton<Game>();

            if (game.gameState == GameState.Aiming)
            {
                EntityQuery query = EntityManager.CreateEntityQuery(typeof(FoodInstanceComponent));

                if (query.CalculateEntityCount() == 0)
                {
                    var settingsEntity = GetSingletonEntity<FoodSpawnerComponent>();
                    var foodPrefabs = EntityManager.GetBuffer<FoodComponent>(settingsEntity);

                    // Check if the list of prefab is empty
                    if (foodPrefabs.Length == 0)
                        throw new System.Exception("No food prefabs to instantiate");

                    // Instantiate next food in the list hierarchy
                    var foodType = m_Random.NextInt(foodPrefabs.Length);
                    var food = EntityManager.Instantiate(foodPrefabs[foodType].Food);
                    EntityManager.SetComponentData(food, new Rotation()
                    {
                        Value = m_Random.NextQuaternionRotation(),
                    });

                    // Make the food transparent
                    var instance = EntityManager.GetComponentData<FoodInstanceComponent>(food);
                    var renderer = EntityManager.GetComponentData<MeshRenderer>(instance.Child);
                    var mat = EntityManager.GetComponentData<LitMaterial>(renderer.material);
                    mat.transparent = true;
                    EntityManager.SetComponentData(renderer.material, mat);

                    game.FoodOnSpatula = food;
                    SetSingleton(game);
                }
            }
        }
    }
}