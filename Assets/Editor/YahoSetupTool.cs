using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class YahoSetupTool
{
    private const string PrefabDirectory = "Assets/Prefabs";
    private const string PrefabPath = "Assets/Prefabs/YahoObstacle.prefab";
    private const string PatternDirectory = "Assets/Patterns";
    private const string PatternPath = "Assets/Patterns/YahoPattern.asset";
    private const string DefaultFontPath = "Assets/Font/Recipekorea SDF.asset";
    private const string WarningMarkName = "WarningMark";
    private const float TargetPixelsPerUnit = 400f;
    private const float HitboxScale = 0.75f;
    private const float WarningDuration = 0.6f;
    private const int WarningBlinkCount = 3;
    private const float WarningFontSize = 26f;

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

        MemeAttackObstacle prefabComponent = CreateOrUpdateYahoPrefab(yahoSprite, yahoMaterial);
        if (prefabComponent == null)
        {
            Debug.LogError("[YahoSetupTool] YahoObstacle 프리팹 생성에 실패했습니다.");
            return;
        }

        MemePatternData yahoPattern = CreateOrUpdateYahoPattern(prefabComponent, yahoSprite, yahoMaterial, yahoClip);
        MemeAttackSpawner spawner = SetupSceneSpawner(yahoPattern);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (spawner != null)
        {
            Selection.activeGameObject = spawner.gameObject;
            EditorGUIUtility.PingObject(yahoPattern != null ? (UnityEngine.Object)yahoPattern : prefabComponent.gameObject);
        }

        Debug.Log(
            $"[YahoSetupTool] 설정 완료!\n" +
            $"- 이미지: {yahoTexturePath} (Sprite, PPU={TargetPixelsPerUnit})\n" +
            $"- 머티리얼: {(yahoMaterial != null ? matPath : "기본값")}\n" +
            $"- 효과음: {(yahoClip != null ? audioPath : "없음")}\n" +
            $"- 프리팹: {PrefabPath} (경고용 '!' TextMeshPro 자식 포함)\n" +
            $"- 패턴 에셋: {PatternPath}\n" +
            $"- 씬 스포너: {(spawner != null ? $"{spawner.name} (MemeAttackSpawner, 패턴 등록 완료)" : "없음")}"
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

    private static MemeAttackObstacle CreateOrUpdateYahoPrefab(Sprite yahoSprite, Material yahoMaterial)
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

            TextMeshPro warningText = CreateWarningMarkChild(root.transform, sr.sortingOrder + 1);

            MemeAttackObstacle obstacle = root.AddComponent<MemeAttackObstacle>();
            SerializedObject serializedObstacle = new SerializedObject(obstacle);
            serializedObstacle.FindProperty("spriteRenderer").objectReferenceValue = sr;
            serializedObstacle.FindProperty("obstacleCollider").objectReferenceValue = boxCollider;
            serializedObstacle.FindProperty("warningText").objectReferenceValue = warningText;
            serializedObstacle.FindProperty("warningDuration").floatValue = WarningDuration;
            serializedObstacle.FindProperty("warningBlinkCount").intValue = WarningBlinkCount;
            serializedObstacle.ApplyModifiedPropertiesWithoutUndo();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return savedPrefab != null ? savedPrefab.GetComponent<MemeAttackObstacle>() : null;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static TextMeshPro CreateWarningMarkChild(Transform parent, int sortingOrder)
    {
        GameObject warningObj = new GameObject(WarningMarkName);
        warningObj.transform.SetParent(parent, false);
        warningObj.transform.localPosition = Vector3.zero;

        TextMeshPro warningText = warningObj.AddComponent<TextMeshPro>();
        warningText.text = "!";
        warningText.fontSize = WarningFontSize;
        warningText.fontStyle = FontStyles.Bold;
        warningText.color = new Color(1f, 0.1f, 0.1f, 1f);
        warningText.alignment = TextAlignmentOptions.Center;
        warningText.textWrappingMode = TextWrappingModes.NoWrap;
        warningText.overflowMode = TextOverflowModes.Overflow;
        warningText.sortingOrder = sortingOrder;
        warningText.rectTransform.sizeDelta = new Vector2(3f, 3f);

        TMP_FontAsset fontAsset = FindWarningFontAsset();
        if (fontAsset != null)
        {
            warningText.font = fontAsset;
        }

        return warningText;
    }

    private static TMP_FontAsset FindWarningFontAsset()
    {
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultFontPath);
        if (fontAsset != null)
        {
            return fontAsset;
        }

        if (AssetDatabase.IsValidFolder("Assets/Font"))
        {
            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Font" });
            if (guids.Length > 0)
            {
                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }

        return TMP_Settings.defaultFontAsset;
    }

    private static MemePatternData CreateOrUpdateYahoPattern(
        MemeAttackObstacle prefabComponent,
        Sprite yahoSprite,
        Material yahoMaterial,
        AudioClip yahoClip)
    {
        if (!AssetDatabase.IsValidFolder(PatternDirectory))
        {
            AssetDatabase.CreateFolder("Assets", "Patterns");
        }

        MemePatternData pattern = AssetDatabase.LoadAssetAtPath<MemePatternData>(PatternPath);
        if (pattern == null)
        {
            pattern = ScriptableObject.CreateInstance<MemePatternData>();
            AssetDatabase.CreateAsset(pattern, PatternPath);
        }

        pattern.obstaclePrefab = prefabComponent;
        pattern.overrideSprite = yahoSprite;
        pattern.overrideMaterial = yahoMaterial;
        pattern.attackSfx = yahoClip;
        pattern.soundVolume = 1f;
        pattern.spawnWeight = 1f;
        pattern.allowLeft = true;
        pattern.allowRight = true;
        pattern.allowCenter = true;

        EditorUtility.SetDirty(pattern);
        return pattern;
    }

    private static MemeAttackSpawner SetupSceneSpawner(MemePatternData yahoPattern)
    {
        MemeAttackSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MemeAttackSpawner>();
        if (spawner == null)
        {
            GameObject legacySpawnerObj = GameObject.Find("YahoAttackSpawner");
            if (legacySpawnerObj != null)
            {
                Undo.RegisterCompleteObjectUndo(legacySpawnerObj, "Convert YahoAttackSpawner to MemeAttackSpawner");
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(legacySpawnerObj);
                legacySpawnerObj.name = "MemeAttackSpawner";
                spawner = legacySpawnerObj.GetComponent<MemeAttackSpawner>();
                if (spawner == null)
                {
                    spawner = Undo.AddComponent<MemeAttackSpawner>(legacySpawnerObj);
                }
            }
            else
            {
                GameObject spawnerObj = new GameObject("MemeAttackSpawner");
                spawner = spawnerObj.AddComponent<MemeAttackSpawner>();
                Undo.RegisterCreatedObjectUndo(spawnerObj, "Create MemeAttackSpawner");
            }
        }

        SerializedObject serializedSpawner = new SerializedObject(spawner);
        serializedSpawner.FindProperty("orderMode").enumValueIndex = (int)MemeOrderMode.ShuffleBag;
        serializedSpawner.FindProperty("warningDuration").floatValue = WarningDuration;
        serializedSpawner.FindProperty("repeatSpawn").boolValue = false;

        SerializedProperty patternsProp = serializedSpawner.FindProperty("patterns");
        if (patternsProp.arraySize == 0)
        {
            patternsProp.InsertArrayElementAtIndex(0);
        }
        patternsProp.GetArrayElementAtIndex(0).objectReferenceValue = yahoPattern;

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
