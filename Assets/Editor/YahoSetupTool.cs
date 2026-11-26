using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class YahoSetupTool
{
    private const string PrefabDirectory = "Assets/Prefabs";
    private const string PrefabPath = "Assets/Prefabs/YahoObstacle.prefab";
    private const float TargetPixelsPerUnit = 400f;
    private const float HitboxScale = 0.75f;

    [MenuItem("Tools/Setup Yaho Attack")]
    public static void SetupYahoAttack()
    {
        string[] spriteSearchFolders = GetSpriteSearchFolders();

        string yahoTexturePath = FindAssetPathByName("t:Texture2D", "yaho", spriteSearchFolders);
        if (string.IsNullOrEmpty(yahoTexturePath))
        {
            EditorUtility.DisplayDialog(
                "Setup Yaho Attack",
                "Assets/Sprites(또는 Assets/Sprite) 폴더에서 'yaho' 이미지 파일을 찾지 못했습니다.",
                "확인"
            );
            return;
        }

        string matPath = FindAssetPathByName("t:Material", "mat", spriteSearchFolders);
        string audioPath = FindYahoAudioPath();

        ConfigureYahoTextureImporter(yahoTexturePath);

        Sprite yahoSprite = LoadSpriteAtPath(yahoTexturePath);
        if (yahoSprite == null)
        {
            Debug.LogError($"[YahoSetupTool] '{yahoTexturePath}'에서 Sprite를 불러오지 못했습니다.");
            return;
        }

        Material yahoMaterial = !string.IsNullOrEmpty(matPath)
            ? AssetDatabase.LoadAssetAtPath<Material>(matPath)
            : null;

        if (yahoMaterial == null)
        {
            Debug.LogWarning("[YahoSetupTool] 'mat' 머티리얼을 찾지 못해 기본 Sprite 머티리얼로 진행합니다.");
        }

        AudioClip yahoClip = !string.IsNullOrEmpty(audioPath)
            ? AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath)
            : null;

        if (yahoClip == null)
        {
            Debug.LogWarning("[YahoSetupTool] Assets/Audio 폴더에서 yaho 효과음 클립을 찾지 못했습니다.");
        }

        YahoAttackObstacle prefabComponent = CreateOrUpdateYahoPrefab(yahoSprite, yahoMaterial);
        if (prefabComponent == null)
        {
            Debug.LogError("[YahoSetupTool] YahoObstacle 프리팹 생성에 실패했습니다.");
            return;
        }

        YahoAttackSpawner spawner = SetupSceneSpawner(prefabComponent, yahoClip);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (spawner != null)
        {
            Selection.activeGameObject = spawner.gameObject;
            EditorGUIUtility.PingObject(prefabComponent.gameObject);
        }

        Debug.Log(
            $"[YahoSetupTool] 설정 완료!\n" +
            $"- 이미지: {yahoTexturePath} (Sprite, PPU={TargetPixelsPerUnit})\n" +
            $"- 머티리얼: {(yahoMaterial != null ? matPath : "기본값")}\n" +
            $"- 효과음: {(yahoClip != null ? audioPath : "없음")}\n" +
            $"- 프리팹: {PrefabPath}\n" +
            $"- 씬 스포너: {(spawner != null ? spawner.name : "없음")}"
        );
    }

    private static string[] GetSpriteSearchFolders()
    {
        var folders = new System.Collections.Generic.List<string>();
        if (AssetDatabase.IsValidFolder("Assets/Sprites"))
        {
            folders.Add("Assets/Sprites");
        }
        if (AssetDatabase.IsValidFolder("Assets/Sprite"))
        {
            folders.Add("Assets/Sprite");
        }
        return folders.Count > 0 ? folders.ToArray() : new[] { "Assets" };
    }

    private static string FindYahoAudioPath()
    {
        string[] audioFolders = AssetDatabase.IsValidFolder("Assets/Audio")
            ? new[] { "Assets/Audio" }
            : new[] { "Assets" };

        string[] keywords = { "geoje_yaho", "geoje-yaho", "yaho" };
        for (int i = 0; i < keywords.Length; i++)
        {
            string matched = FindAssetPathByName("t:AudioClip", keywords[i], audioFolders);
            if (!string.IsNullOrEmpty(matched))
            {
                return matched;
            }
        }

        if (AssetDatabase.IsValidFolder("Assets/Audio"))
        {
            string[] allClips = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" });
            if (allClips.Length > 0)
            {
                return AssetDatabase.GUIDToAssetPath(allClips[0]);
            }
        }

        return null;
    }

    private static string FindAssetPathByName(string typeFilter, string keyword, string[] folders)
    {
        string[] guids = AssetDatabase.FindAssets($"{keyword} {typeFilter}", folders);
        string exactMatch = null;
        string containsMatch = null;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string fileName = Path.GetFileNameWithoutExtension(path);

            if (string.Equals(fileName, keyword, StringComparison.OrdinalIgnoreCase))
            {
                exactMatch = path;
                break;
            }

            if (containsMatch == null && fileName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                containsMatch = path;
            }
        }

        return exactMatch ?? containsMatch;
    }

    private static void ConfigureYahoTextureImporter(string texturePath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[YahoSetupTool] TextureImporter를 가져올 수 없습니다: {texturePath}");
            return;
        }

        bool changed = false;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (!Mathf.Approximately(importer.spritePixelsPerUnit, TargetPixelsPerUnit))
        {
            importer.spritePixelsPerUnit = TargetPixelsPerUnit;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static Sprite LoadSpriteAtPath(string texturePath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
        if (sprite != null)
        {
            return sprite;
        }

        return AssetDatabase.LoadAllAssetsAtPath(texturePath)
            .OfType<Sprite>()
            .FirstOrDefault();
    }

    private static YahoAttackObstacle CreateOrUpdateYahoPrefab(Sprite yahoSprite, Material yahoMaterial)
    {
        if (!AssetDatabase.IsValidFolder(PrefabDirectory))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        GameObject root = new GameObject("YahoObstacle");
        try
        {
            SpriteRenderer sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = yahoSprite;
            sr.sortingOrder = 15;
            if (yahoMaterial != null)
            {
                sr.sharedMaterial = yahoMaterial;
            }

            BoxCollider2D boxCollider = root.AddComponent<BoxCollider2D>();
            boxCollider.isTrigger = true;
            Vector2 spriteSize = yahoSprite.bounds.size;
            boxCollider.size = new Vector2(
                Mathf.Max(0.5f, spriteSize.x * HitboxScale),
                Mathf.Max(0.5f, spriteSize.y * HitboxScale)
            );
            boxCollider.offset = yahoSprite.bounds.center;

            Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            YahoAttackObstacle obstacle = root.AddComponent<YahoAttackObstacle>();
            SerializedObject serializedObstacle = new SerializedObject(obstacle);
            serializedObstacle.FindProperty("spriteRenderer").objectReferenceValue = sr;
            serializedObstacle.FindProperty("obstacleCollider").objectReferenceValue = boxCollider;
            serializedObstacle.ApplyModifiedPropertiesWithoutUndo();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return savedPrefab != null ? savedPrefab.GetComponent<YahoAttackObstacle>() : null;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static YahoAttackSpawner SetupSceneSpawner(YahoAttackObstacle prefabComponent, AudioClip yahoClip)
    {
        YahoAttackSpawner spawner = UnityEngine.Object.FindAnyObjectByType<YahoAttackSpawner>();
        if (spawner == null)
        {
            GameObject spawnerObj = new GameObject("YahoAttackSpawner");
            spawner = spawnerObj.AddComponent<YahoAttackSpawner>();
            Undo.RegisterCreatedObjectUndo(spawnerObj, "Create YahoAttackSpawner");
        }

        AudioSource audioSource = spawner.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = Undo.AddComponent<AudioSource>(spawner.gameObject);
        }
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        EditorUtility.SetDirty(audioSource);

        SerializedObject serializedSpawner = new SerializedObject(spawner);
        serializedSpawner.FindProperty("yahoObstaclePrefab").objectReferenceValue = prefabComponent;
        serializedSpawner.FindProperty("audioSource").objectReferenceValue = audioSource;
        serializedSpawner.FindProperty("yahoSound").objectReferenceValue = yahoClip;
        serializedSpawner.FindProperty("repeatSpawn").boolValue = false;
        serializedSpawner.ApplyModifiedProperties();

        EditorUtility.SetDirty(spawner);
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
        }

        return spawner;
    }
}
