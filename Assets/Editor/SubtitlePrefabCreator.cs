using TMPro;
using UnityEditor;
using UnityEngine;

public static class SubtitlePrefabCreator
{
    private const string PrefabDirectory = "Assets/Prefabs";
    private const string PrefabPath = "Assets/Prefabs/SubtitleObstacle.prefab";
    private const string DefaultFontPath = "Assets/Font/Recipekorea SDF.asset";

    [MenuItem("Tools/Meme Game/Create SubtitleObstacle Prefab")]
    public static void CreateOrUpdateSubtitleObstaclePrefab()
    {
        if (!AssetDatabase.IsValidFolder(PrefabDirectory))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        GameObject root = new GameObject("SubtitleObstacle");
        try
        {
            TextMeshPro textMesh = root.AddComponent<TextMeshPro>();
            textMesh.text = "자막 샘플";
            textMesh.fontSize = 12f;
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.textWrappingMode = TextWrappingModes.NoWrap;
            textMesh.overflowMode = TextOverflowModes.Overflow;
            textMesh.sortingOrder = 5;

            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultFontPath);
            if (fontAsset != null)
            {
                textMesh.font = fontAsset;
            }

            BoxCollider2D boxCollider = root.AddComponent<BoxCollider2D>();
            boxCollider.isTrigger = true;

            Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            SubtitleObstacle obstacle = root.AddComponent<SubtitleObstacle>();
            SerializedObject serializedObstacle = new SerializedObject(obstacle);
            serializedObstacle.FindProperty("textMesh").objectReferenceValue = textMesh;
            serializedObstacle.FindProperty("boxCollider").objectReferenceValue = boxCollider;
            serializedObstacle.ApplyModifiedPropertiesWithoutUndo();

            obstacle.UpdateSizeToFitText();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (savedPrefab != null)
            {
                AssignPrefabToSceneSpawner(savedPrefab.GetComponent<SubtitleObstacle>(), fontAsset);
                Selection.activeObject = savedPrefab;
                EditorGUIUtility.PingObject(savedPrefab);
                Debug.Log($"[SubtitlePrefabCreator] TextMeshPro 전용 SubtitleObstacle 프리팹을 생성/갱신했습니다: {PrefabPath}");
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void AssignPrefabToSceneSpawner(SubtitleObstacle prefab, TMP_FontAsset fontAsset)
    {
        if (prefab == null)
        {
            return;
        }

        SubtitleSpawner spawner = Object.FindAnyObjectByType<SubtitleSpawner>();
        if (spawner == null)
        {
            return;
        }

        SerializedObject serializedSpawner = new SerializedObject(spawner);
        serializedSpawner.FindProperty("obstaclePrefab").objectReferenceValue = prefab;
        if (fontAsset != null)
        {
            serializedSpawner.FindProperty("fontAsset").objectReferenceValue = fontAsset;
        }
        serializedSpawner.ApplyModifiedProperties();
        EditorUtility.SetDirty(spawner);
    }
}
