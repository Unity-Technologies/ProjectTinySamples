using UnityEditor;
using UnityEngine;

namespace TinyKitchen
{
    [CustomEditor(typeof(LevelConfigurationData))]
    public class LevelConfigurationDataInspector : Editor
    {
        Vector2 m_GridSize = new Vector2(13, 7);
        Vector3 m_InitialPosition = new Vector3(-5.5f, 2.0f, 5.5f); //Left-Top
        Vector3 m_PotOffset = new Vector3(0.0f, 0.2f, 0.0f);
        int m_PotSelectedIndex = 0;
        int m_FanSelectedIndex = 0;
        Texture[] m_PotGridImages = new Texture[7 * 13];
        Texture[] m_FanGridImages = new Texture[7 * 13];
        Vector3[] m_PositionValues = new Vector3[7 * 13];

        public override void OnInspectorGUI()
        {
            PopulateGrid();

            LevelConfigurationData t = (LevelConfigurationData) target;
            GUILayout.Label("Level Configuration", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("Box");
            {
                GUILayout.Label("Pot Settings");

                m_PotSelectedIndex = t.selectedPot;
                m_PotSelectedIndex =
                    GUILayout.SelectionGrid(m_PotSelectedIndex, m_PotGridImages, 13, GUILayout.MaxHeight(256));
                GUILayout.Space(10f);
                EditorGUI.indentLevel += 1;

                EditorGUI.BeginDisabledGroup(true);
                t.potPosition = EditorGUILayout.Vector3Field(
                    "Position",
                    m_PositionValues[m_PotSelectedIndex]) + m_PotOffset;
                EditorGUI.EndDisabledGroup();

                t.radius = EditorGUILayout.Slider("Radius", t.radius, 0.5f, 5);
                t.isMoving = EditorGUILayout.Toggle("Is Moving", t.isMoving);

                if (t.isMoving)
                {
                    EditorGUI.indentLevel += 1;
                    t.potSpeed = EditorGUILayout.FloatField("Speed", t.potSpeed);
                    t.initialPosition = EditorGUILayout.Vector3Field("Initial Position", t.initialPosition);
                    t.initialPosition = EditorGUILayout.Vector3Field("Final Position", t.finalPosition);
                }

                t.selectedPot = m_PotSelectedIndex;
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10f);

            EditorGUILayout.BeginVertical("Box");
            {
                GUILayout.Label("Fan Settings");

                m_FanSelectedIndex = t.selectedFan;
                m_FanSelectedIndex =
                    GUILayout.SelectionGrid(m_FanSelectedIndex, m_FanGridImages, 13, GUILayout.MaxHeight(256));

                GUILayout.Space(10f);

                EditorGUI.BeginDisabledGroup(true);
                t.fanPosition = EditorGUILayout.Vector3Field(
                    "Position",
                    m_PositionValues[m_FanSelectedIndex]);
                EditorGUI.EndDisabledGroup();

                t.fanHeading = EditorGUILayout.Vector3Field("Heading", t.fanHeading);
                t.fanForce = EditorGUILayout.FloatField("Force", t.fanForce);
                t.isRotating = EditorGUILayout.Toggle("Is Rotating", t.isRotating);
                t.fanUIPos = EditorGUILayout.Vector3Field("Fan UI Position", t.fanUIPos);

                if (t.isRotating)
                {
                    EditorGUI.indentLevel += 1;
                    t.rotationSpeed = EditorGUILayout.FloatField("Speed", t.rotationSpeed);
                    t.initialHeading = EditorGUILayout.Vector3Field("Initial Heading", t.initialHeading);
                    t.finalHeading = EditorGUILayout.Vector3Field("Final Heading", t.finalHeading);
                }

                t.selectedFan = m_FanSelectedIndex;
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10f);

            EditorGUILayout.BeginVertical("Box");
            {
                GUILayout.Label("Obstacles");
                GUILayout.Space(10f);

                foreach (var obstacle in t.obstacles)
                {
                    EditorGUILayout.BeginVertical();

                    obstacle.prefab =
                        EditorGUILayout.ObjectField("Obstacle prefab", obstacle.prefab, typeof(GameObject), false) as
                            GameObject;
                    obstacle.position = EditorGUILayout.Vector3Field("Position", obstacle.position);
                    obstacle.scale = EditorGUILayout.Vector3Field("Scale", obstacle.scale);
                    if (GUILayout.Button("Remove Obstacle"))
                    {
                        t.obstacles.Remove(obstacle);
                        return;
                    }

                    EditorGUILayout.EndVertical();

                    GUILayout.Space(10f);
                }

                if (GUILayout.Button("New Obstacle"))
                {
                    t.obstacles.Add(new Obstacle());
                }
            }
            EditorGUILayout.EndVertical();

            if (GUI.changed)
            {
                SaveData(t);
            }
        }

        private void PopulateGrid()
        {
            int index = 0;
            for (int y = 0; y < m_GridSize.y; y++)
            {
                for (int x = 0; x < m_GridSize.x; x++)
                {
                    m_PositionValues[index] =
                        new Vector3(m_InitialPosition.x + x, m_InitialPosition.y, m_InitialPosition.z - y);
                    index++;
                }
            }

            //Populate Icons
            var emptyTexture = Resources.Load<Texture>("Icons/empty");
            var potTexture = Resources.Load<Texture>("Icons/pot");
            var fanTexture = Resources.Load<Texture>("Icons/fan");

            for (int i = 0; i < m_PotGridImages.Length; i++)
            {
                if (i == m_PotSelectedIndex)
                {
                    m_PotGridImages[i] = potTexture;
                }
                else
                {
                    m_PotGridImages[i] = emptyTexture;
                }
            }

            for (int i = 0; i < m_FanGridImages.Length; i++)
            {
                if (i == m_FanSelectedIndex)
                {
                    m_FanGridImages[i] = fanTexture;
                }
                else
                {
                    m_FanGridImages[i] = emptyTexture;
                }
            }
        }

        void SaveData(LevelConfigurationData levelConfigurationData)
        {
            EditorUtility.SetDirty(levelConfigurationData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}