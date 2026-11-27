using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MemeSetupTool
{
    private const string PrefabDirectory = "Assets/Prefabs";
    private const string DefaultPrefabPath = "Assets/Prefabs/YahoObstacle.prefab";
    private const string PatternDirectory = "Assets/Patterns";
    private const string DefaultPatternPath = "Assets/Patterns/YahoPattern.asset";
    private const string DefaultFontPath = "Assets/Font/Recipekorea SDF.asset";
    private const string WarningMarkName = "WarningMark";
    private const float TargetPixelsPerUnit = 400f;
    private const float HitboxScale = 0.75f;
    private const float WarningDuration = 0.6f;
    private const int WarningBlinkCount = 3;
    private const float WarningFontSize = 26f;

    [MenuItem("Tools/Meme Game/Setup Meme Attack")]
    public static void SetupMemeAttack()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Setup Meme Attack",
            "기존 프리팹, 패턴 에셋 및 씬의 MemeAttackSpawner / AudioManager 세팅을 덮어쓰거나 갱신합니다.\n계속 진행하시겠습니까?",
            "덮어쓰기 및 진행",
            "취소"
        );

        if (!confirmed)
        {
            return;
        }

        string[] spriteSearchFolders = GetSpriteSearchFolders();

        string texturePath = FindDefaultSpritePath(spriteSearchFolders);
        if (string.IsNullOrEmpty(texturePath))
        {
            EditorUtility.DisplayDialog(
                "Setup Meme Attack",
                "Assets/Sprites(또는 Assets/Sprite) 폴더에서 사용할 이미지 파일을 찾지 못했습니다.",
                "확인"
            );
            return;
        }

        string matPath = FindAssetPathByName("t:Material", "mat", spriteSearchFolders);
        string audioPath = FindDefaultAudioPath();

        ConfigureSpriteTextureImporter(texturePath);

        Sprite obstacleSprite = LoadSpriteAtPath(texturePath);
        if (obstacleSprite == null)
        {
            Debug.LogError($"[MemeSetupTool] '{texturePath}'에서 Sprite를 불러오지 못했습니다.");
            return;
        }

        Material obstacleMaterial = !string.IsNullOrEmpty(matPath)
            ? AssetDatabase.LoadAssetAtPath<Material>(matPath)
            : null;

        if (obstacleMaterial == null)
        {
            Debug.LogWarning("[MemeSetupTool] 'mat' 머티리얼을 찾지 못해 기본 Sprite 머티리얼로 진행합니다.");
        }

        AudioClip attackClip = !string.IsNullOrEmpty(audioPath)
            ? AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath)
            : null;

        if (attackClip == null)
        {
            Debug.LogWarning("[MemeSetupTool] Assets/Audio 폴더에서 효과음 클립을 찾지 못했습니다.");
        }

        MemeAttackObstacle prefabComponent = CreateOrUpdateObstaclePrefab(obstacleSprite, obstacleMaterial);
        if (prefabComponent == null)
        {
            Debug.LogError("[MemeSetupTool] MemeAttackObstacle 프리팹 생성에 실패했습니다.");
            return;
        }

        MemePatternData defaultPattern = CreateOrUpdateDefaultPattern(prefabComponent, obstacleSprite, obstacleMaterial, attackClip);
        MemeAttackSpawner spawner = SetupSceneSpawner(defaultPattern);
        AudioManager audioManager = SetupSceneAudioManager();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (spawner != null)
        {
            Selection.activeGameObject = spawner.gameObject;
            EditorGUIUtility.PingObject(defaultPattern != null ? (UnityEngine.Object)defaultPattern : prefabComponent.gameObject);
        }

        Debug.Log(
            $"[MemeSetupTool] 설정 완료!\n" +
            $"- 이미지: {texturePath} (Sprite, PPU={TargetPixelsPerUnit})\n" +
            $"- 머티리얼: {(obstacleMaterial != null ? matPath : "기본값")}\n" +
            $"- 효과음: {(attackClip != null ? audioPath : "없음")}\n" +
            $"- 프리팹: {DefaultPrefabPath} (MemeAttackObstacle + 경고용 '!' TextMeshPro 자식 포함)\n" +
            $"- 패턴 에셋: {DefaultPatternPath}\n" +
            $"- 씬 스포너: {(spawner != null ? $"{spawner.name} (MemeAttackSpawner, 패턴 등록 완료)" : "없음")}\n" +
            $"- 오디오 매니저: {(audioManager != null ? $"{audioManager.name} (AudioManager 확인 완료)" : "없음")}"
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

    private static string FindDefaultSpritePath(string[] spriteSearchFolders)
    {
        string matched = FindAssetPathByName("t:Texture2D", "yaho", spriteSearchFolders);
        if (!string.IsNullOrEmpty(matched))
        {
            return matched;
        }

        string[] allTextures = AssetDatabase.FindAssets("t:Texture2D", spriteSearchFolders);
        if (allTextures.Length > 0)
        {
            return AssetDatabase.GUIDToAssetPath(allTextures[0]);
        }

        return null;
    }

    private static string FindDefaultAudioPath()
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

    private static void ConfigureSpriteTextureImporter(string texturePath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[MemeSetupTool] TextureImporter를 가져올 수 없습니다: {texturePath}");
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

    private static MemeAttackObstacle CreateOrUpdateObstaclePrefab(Sprite obstacleSprite, Material obstacleMaterial)
    {
        if (!AssetDatabase.IsValidFolder(PrefabDirectory))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        string prefabName = Path.GetFileNameWithoutExtension(DefaultPrefabPath);
        GameObject root = new GameObject(prefabName);
        try
        {
            SpriteRenderer sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = obstacleSprite;
            sr.sortingOrder = 15;
            if (obstacleMaterial != null)
            {
                sr.sharedMaterial = obstacleMaterial;
            }

            BoxCollider2D boxCollider = root.AddComponent<BoxCollider2D>();
            boxCollider.isTrigger = true;
            Vector2 spriteSize = obstacleSprite.bounds.size;
            boxCollider.size = new Vector2(
                Mathf.Max(0.5f, spriteSize.x * HitboxScale),
                Mathf.Max(0.5f, spriteSize.y * HitboxScale)
            );
            boxCollider.offset = obstacleSprite.bounds.center;

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

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, DefaultPrefabPath);
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

    private static MemePatternData CreateOrUpdateDefaultPattern(
        MemeAttackObstacle prefabComponent,
        Sprite obstacleSprite,
        Material obstacleMaterial,
        AudioClip attackClip)
    {
        if (!AssetDatabase.IsValidFolder(PatternDirectory))
        {
            AssetDatabase.CreateFolder("Assets", "Patterns");
        }

        MemePatternData pattern = AssetDatabase.LoadAssetAtPath<MemePatternData>(DefaultPatternPath);
        if (pattern == null)
        {
            pattern = ScriptableObject.CreateInstance<MemePatternData>();
            AssetDatabase.CreateAsset(pattern, DefaultPatternPath);
        }

        pattern.obstaclePrefab = prefabComponent;
        pattern.overrideSprite = obstacleSprite;
        pattern.overrideMaterial = obstacleMaterial;
        pattern.attackSfx = attackClip;
        pattern.soundVolume = 1f;
        pattern.spawnWeight = 1f;
        pattern.allowLeft = true;
        pattern.allowRight = true;
        pattern.allowCenter = true;

        EditorUtility.SetDirty(pattern);
        return pattern;
    }

    private static MemeAttackSpawner SetupSceneSpawner(MemePatternData defaultPattern)
    {
        MemeAttackSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MemeAttackSpawner>();
        if (spawner == null)
        {
            GameObject spawnerObj = new GameObject("MemeAttackSpawner");
            spawner = spawnerObj.AddComponent<MemeAttackSpawner>();
            Undo.RegisterCreatedObjectUndo(spawnerObj, "Create MemeAttackSpawner");
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
        patternsProp.GetArrayElementAtIndex(0).objectReferenceValue = defaultPattern;

        serializedSpawner.ApplyModifiedProperties();

        EditorUtility.SetDirty(spawner);
        MarkActiveSceneDirty();

        return spawner;
    }

    private static AudioManager SetupSceneAudioManager()
    {
        AudioManager audioManager = UnityEngine.Object.FindAnyObjectByType<AudioManager>();
        if (audioManager == null)
        {
            GameObject audioObj = new GameObject("AudioManager");
            audioManager = audioObj.AddComponent<AudioManager>();
            Undo.RegisterCreatedObjectUndo(audioObj, "Create AudioManager");
            EditorUtility.SetDirty(audioManager);
            MarkActiveSceneDirty();
        }

        return audioManager;
    }

    private static void MarkActiveSceneDirty()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
        }
    }
}
