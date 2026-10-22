using Cheon.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(NoiseTerrainGenerator))]
public class NoiseTerrainGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "지형 생성은 연결된 Terrain Data의 높이와 풀·흙·암석 레이어 칠을 덮어씁니다. " +
            "수작업으로 다듬기 전 Terrain Data를 복제하면 원본을 보관할 수 있습니다.", MessageType.Info);

        var generator = (NoiseTerrainGenerator)target;
        TerrainData data = generator.GetComponent<Terrain>().terrainData;
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || data == null))
        {
            if (GUILayout.Button("현재 시드로 지형 생성"))
                Generate(generator, false);
            if (GUILayout.Button("새 시드로 지형 생성"))
                Generate(generator, true);
        }
    }

    private static void Generate(NoiseTerrainGenerator generator, bool newSeed)
    {
        Terrain terrain = generator.GetComponent<Terrain>();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("노이즈 지형 생성");
        Undo.RecordObject(generator, "지형 시드 변경");
        Undo.RegisterCompleteObjectUndo(terrain.terrainData, "지형 높이와 레이어 생성");
        if (newSeed)
            generator.SetSeed(System.Guid.NewGuid().GetHashCode());
        generator.Generate();
        EditorUtility.SetDirty(terrain.terrainData);
        EditorUtility.SetDirty(generator);
        EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
        Undo.CollapseUndoOperations(undoGroup);
        SceneView.RepaintAll();
    }
}
