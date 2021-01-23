using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

namespace BlendShapeDemo
{
    /// <summary>
    /// Translate the camera to face the selected character
    /// </summary>

    public class CameraMovementSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var game = GetSingleton<Game>();
            var camera = GetSingleton<Camera>();
            var cameraEntity = GetSingletonEntity<Camera>();
            var cameraPos = GetComponent<Translation>(cameraEntity);

            Entities.ForEach((ref Entity entity, ref Character character, ref Translation translation) =>
            {
                if (character.selected)
                {
                    // Lerp to destination
                    cameraPos.Value.x = math.lerp(cameraPos.Value.x, translation.Value.x, camera.cameraSpeed * Time.DeltaTime);
                    game.gameState = GameState.Moving;

                    switch (character.characterType)
                    {
                        case CharacterType.Billy:

                            // Set final destination and reset value
                            if (cameraPos.Value.x <= translation.Value.x + 0.01f)
                            {
                                game.gameState = GameState.Billy;
                                cameraPos.Value.x = translation.Value.x;
                                character.selected = false;
                            }
                            break;

                        case CharacterType.Emily:
                       
                            // Set final destination and reset value
                            if (cameraPos.Value.x >= translation.Value.x - 0.01f)
                            {
                                game.gameState = GameState.Emily;
                                cameraPos.Value.x = translation.Value.x;
                                character.selected = false;
                            }
                            break;     
                    }
                }
            }).WithStructuralChanges().Run();

            SetSingleton(game);
            SetSingleton(camera);
            SetComponent(cameraEntity, cameraPos);
        }
    }
}
