using UnityEngine;
using Unity.Entities;
using Unity.TinyGems;

public class EditorCameraMover : MonoBehaviour
{
    private EntityQuery m_CellManagerQuery;
    
    private void Start()
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        m_CellManagerQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<CellManager>());
    }

    private void Update()
    {
        if (m_CellManagerQuery.CalculateEntityCount() == 0)
            return;
        
        var cellManager = m_CellManagerQuery.GetSingleton<CellManager>();
        Camera.main.transform.position = new Vector3(cellManager.MaxCol * 0.5f, cellManager.MaxRow * 0.5f, -10.0f);

        enabled = false;
    }
}
