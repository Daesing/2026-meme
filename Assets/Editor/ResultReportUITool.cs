using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public static class ResultReportUITool
{
    private const string DefaultFontPath = "Assets/Font/Recipekorea SDF.asset";
    private const string PanelObjectName = "ResultReportPanel";

    private static readonly Color BackdropColor = new Color(0.02f, 0.04f, 0.09f, 0.86f);
    private static readonly Color CardBgColor = new Color(0.04f, 0.08f, 0.16f, 0.96f);
    private static readonly Color HeaderBgColor = new Color(0f, 0.92f, 1f, 0.16f);
    private static readonly Color BoxBgColor = new Color(0.02f, 0.11f, 0.2f, 0.92f);
    private static readonly Color KillerFrameBgColor = new Color(0.01f, 0.04f, 0.09f, 0.95f);
    private static readonly Color NeonCyan = new Color(0f, 0.95f, 1f, 1f);
    private static readonly Color NeonCyanSoft = new Color(0.75f, 0.98f, 1f, 1f);
    private static readonly Color NeonYellow = new Color(1f, 0.9f, 0.2f, 1f);
    private static readonly Color DarkInkColor = new Color(0.03f, 0.06f, 0.12f, 1f);

    [MenuItem("Tools/Meme Game/Create Result Report UI")]
    public static void CreateResultReportUI()
    {
        Canvas canvas = FindOrCreateCanvas();
        EnsureEventSystem();

        Transform existingPanel = canvas.transform.Find(PanelObjectName);
        if (existingPanel != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "Create Result Report UI",
                $"'{canvas.name}' 아래에 이미 '{PanelObjectName}'이 존재합니다.\n기존 패널을 교체하시겠습니까?",
                "교체",
                "취소"
            );

            if (!replace)
            {
                Selection.activeGameObject = existingPanel.gameObject;
                return;
            }

            Undo.DestroyObjectImmediate(existingPanel.gameObject);
        }

        TMP_FontAsset fontAsset = FindUIFontAsset();

        // 1. Root Panel (Full-screen dim backdrop)
        GameObject panelObj = CreateUIObject(PanelObjectName, canvas.transform);
        Undo.RegisterCreatedObjectUndo(panelObj, "Create Result Report UI");

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        StretchFull(panelRect);

        Image panelBg = panelObj.AddComponent<Image>();
        panelBg.color = BackdropColor;
        panelBg.raycastTarget = true;

        // 2. Central Report Card
        GameObject cardObj = CreateUIObject("ReportCard", panelObj.transform);
        RectTransform cardRect = cardObj.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(880f, 760f);
        cardRect.anchoredPosition = Vector2.zero;

        Image cardBg = cardObj.AddComponent<Image>();
        cardBg.color = CardBgColor;
        AddNeonOutline(cardObj, NeonCyan, new Vector2(4f, -4f));

        // 3. Header Banner
        GameObject headerObj = CreateUIObject("HeaderBanner", cardObj.transform);
        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(0f, 96f);
        headerRect.anchoredPosition = Vector2.zero;

        Image headerBg = headerObj.AddComponent<Image>();
        headerBg.color = HeaderBgColor;

        GameObject headerLineObj = CreateUIObject("HeaderNeonLine", headerObj.transform);
        RectTransform headerLineRect = headerLineObj.GetComponent<RectTransform>();
        headerLineRect.anchorMin = new Vector2(0f, 0f);
        headerLineRect.anchorMax = new Vector2(1f, 0f);
        headerLineRect.pivot = new Vector2(0.5f, 0f);
        headerLineRect.sizeDelta = new Vector2(0f, 4f);
        headerLineRect.anchoredPosition = Vector2.zero;
        headerLineObj.AddComponent<Image>().color = NeonCyan;

        TMP_Text titleText = CreateTMPText(
            "TitleText",
            headerObj.transform,
            "★ 2026 밈 자격증 시험 성적표 ★",
            fontAsset,
            38f,
            NeonCyan,
            TextAlignmentOptions.Center,
            FontStyles.Bold
        );
        StretchFull(titleText.rectTransform, 16f, 8f);

        // 4. Grade Box & Text
        GameObject gradeBoxObj = CreateUIObject("GradeBox", cardObj.transform);
        RectTransform gradeBoxRect = gradeBoxObj.GetComponent<RectTransform>();
        gradeBoxRect.anchorMin = new Vector2(0.5f, 1f);
        gradeBoxRect.anchorMax = new Vector2(0.5f, 1f);
        gradeBoxRect.pivot = new Vector2(0.5f, 1f);
        gradeBoxRect.sizeDelta = new Vector2(780f, 92f);
        gradeBoxRect.anchoredPosition = new Vector2(0f, -120f);

        Image gradeBoxBg = gradeBoxObj.AddComponent<Image>();
        gradeBoxBg.color = BoxBgColor;
        AddNeonOutline(gradeBoxObj, new Color(0f, 0.95f, 1f, 0.7f), new Vector2(2f, -2f));

        TMP_Text gradeText = CreateTMPText(
            "GradeText",
            gradeBoxObj.transform,
            "취득 급수 : 2급",
            fontAsset,
            44f,
            NeonCyan,
            TextAlignmentOptions.Center,
            FontStyles.Bold
        );
        StretchFull(gradeText.rectTransform, 16f, 8f);

        // 5. Score Text
        TMP_Text scoreText = CreateTMPText(
            "ScoreText",
            cardObj.transform,
            "최종 점수 : 0점\n최고 기록 : 0점",
            fontAsset,
            30f,
            NeonCyanSoft,
            TextAlignmentOptions.Center,
            FontStyles.Bold
        );
        RectTransform scoreRect = scoreText.rectTransform;
        scoreRect.anchorMin = new Vector2(0.5f, 1f);
        scoreRect.anchorMax = new Vector2(0.5f, 1f);
        scoreRect.pivot = new Vector2(0.5f, 1f);
        scoreRect.sizeDelta = new Vector2(780f, 96f);
        scoreRect.anchoredPosition = new Vector2(0f, -228f);
        scoreText.lineSpacing = 12f;

        // 6. Killer Meme Section
        GameObject killerSectionObj = CreateUIObject("KillerSection", cardObj.transform);
        RectTransform killerSectionRect = killerSectionObj.GetComponent<RectTransform>();
        killerSectionRect.anchorMin = new Vector2(0.5f, 1f);
        killerSectionRect.anchorMax = new Vector2(0.5f, 1f);
        killerSectionRect.pivot = new Vector2(0.5f, 1f);
        killerSectionRect.sizeDelta = new Vector2(780f, 286f);
        killerSectionRect.anchoredPosition = new Vector2(0f, -340f);

        Image killerSectionBg = killerSectionObj.AddComponent<Image>();
        killerSectionBg.color = BoxBgColor;
        AddNeonOutline(killerSectionObj, new Color(0f, 0.85f, 1f, 0.65f), new Vector2(2f, -2f));

        TMP_Text killerHeaderText = CreateTMPText(
            "KillerSectionHeader",
            killerSectionObj.transform,
            "[ 탈락 주범 밈 리포트 ]",
            fontAsset,
            24f,
            NeonCyan,
            TextAlignmentOptions.Left,
            FontStyles.Bold
        );
        RectTransform killerHeaderRect = killerHeaderText.rectTransform;
        killerHeaderRect.anchorMin = new Vector2(0f, 1f);
        killerHeaderRect.anchorMax = new Vector2(1f, 1f);
        killerHeaderRect.pivot = new Vector2(0.5f, 1f);
        killerHeaderRect.sizeDelta = new Vector2(-40f, 40f);
        killerHeaderRect.anchoredPosition = new Vector2(0f, -14f);

        // Killer Image Frame
        GameObject imageFrameObj = CreateUIObject("KillerImageFrame", killerSectionObj.transform);
        RectTransform imageFrameRect = imageFrameObj.GetComponent<RectTransform>();
        imageFrameRect.anchorMin = new Vector2(0f, 0.5f);
        imageFrameRect.anchorMax = new Vector2(0f, 0.5f);
        imageFrameRect.pivot = new Vector2(0f, 0.5f);
        imageFrameRect.sizeDelta = new Vector2(196f, 196f);
        imageFrameRect.anchoredPosition = new Vector2(28f, -18f);

        Image imageFrameBg = imageFrameObj.AddComponent<Image>();
        imageFrameBg.color = KillerFrameBgColor;
        AddNeonOutline(imageFrameObj, NeonCyan, new Vector2(2f, -2f));

        GameObject killerImageObj = CreateUIObject("KillerMemeImage", imageFrameObj.transform);
        RectTransform killerImageRect = killerImageObj.GetComponent<RectTransform>();
        StretchFull(killerImageRect, 12f, 12f);

        Image killerMemeImage = killerImageObj.AddComponent<Image>();
        killerMemeImage.preserveAspect = true;
        killerMemeImage.color = Color.white;

        // Killer Title
        TMP_Text killerTitleText = CreateTMPText(
            "KillerTitleText",
            killerSectionObj.transform,
            "탈락 주범 밈 이름",
            fontAsset,
            34f,
            NeonYellow,
            TextAlignmentOptions.Left,
            FontStyles.Bold
        );
        RectTransform killerTitleRect = killerTitleText.rectTransform;
        killerTitleRect.anchorMin = new Vector2(0f, 1f);
        killerTitleRect.anchorMax = new Vector2(1f, 1f);
        killerTitleRect.pivot = new Vector2(0f, 1f);
        killerTitleRect.offsetMin = new Vector2(250f, -120f);
        killerTitleRect.offsetMax = new Vector2(-28f, -64f);

        // Killer Comment
        TMP_Text killerCommentText = CreateTMPText(
            "KillerCommentText",
            killerSectionObj.transform,
            "여기에 탈락 한줄평이 표시됩니다.",
            fontAsset,
            24f,
            Color.white,
            TextAlignmentOptions.TopLeft,
            FontStyles.Normal
        );
        RectTransform killerCommentRect = killerCommentText.rectTransform;
        killerCommentRect.anchorMin = new Vector2(0f, 0f);
        killerCommentRect.anchorMax = new Vector2(1f, 1f);
        killerCommentRect.pivot = new Vector2(0f, 1f);
        killerCommentRect.offsetMin = new Vector2(250f, 24f);
        killerCommentRect.offsetMax = new Vector2(-28f, -126f);
        killerCommentText.textWrappingMode = TextWrappingModes.Normal;
        killerCommentText.lineSpacing = 8f;

        // 7. Retry Button
        GameObject retryBtnObj = CreateUIObject("RetryButton", cardObj.transform);
        RectTransform retryBtnRect = retryBtnObj.GetComponent<RectTransform>();
        retryBtnRect.anchorMin = new Vector2(0.5f, 0f);
        retryBtnRect.anchorMax = new Vector2(0.5f, 0f);
        retryBtnRect.pivot = new Vector2(0.5f, 0f);
        retryBtnRect.sizeDelta = new Vector2(380f, 80f);
        retryBtnRect.anchoredPosition = new Vector2(0f, 34f);

        Image retryBtnBg = retryBtnObj.AddComponent<Image>();
        retryBtnBg.color = NeonCyan;
        AddNeonOutline(retryBtnObj, Color.white, new Vector2(2f, -2f));

        Button retryButton = retryBtnObj.AddComponent<Button>();
        ColorBlock colors = retryButton.colors;
        colors.normalColor = NeonCyan;
        colors.highlightedColor = new Color(0.45f, 0.98f, 1f, 1f);
        colors.pressedColor = new Color(0f, 0.72f, 0.82f, 1f);
        colors.selectedColor = NeonCyan;
        retryButton.colors = colors;
        retryButton.targetGraphic = retryBtnBg;

        TMP_Text retryBtnLabel = CreateTMPText(
            "RetryButtonText",
            retryBtnObj.transform,
            "재응시 하기 (RETRY)",
            fontAsset,
            30f,
            DarkInkColor,
            TextAlignmentOptions.Center,
            FontStyles.Bold
        );
        StretchFull(retryBtnLabel.rectTransform, 12f, 8f);

        // 8. Attach & Wire ResultReportUI Component
        ResultReportUI reportUI = panelObj.AddComponent<ResultReportUI>();
        SerializedObject serializedReport = new SerializedObject(reportUI);
        serializedReport.FindProperty("reportPanel").objectReferenceValue = panelObj;
        serializedReport.FindProperty("gradeText").objectReferenceValue = gradeText;
        serializedReport.FindProperty("scoreText").objectReferenceValue = scoreText;
        serializedReport.FindProperty("killerMemeImage").objectReferenceValue = killerMemeImage;
        serializedReport.FindProperty("killerTitleText").objectReferenceValue = killerTitleText;
        serializedReport.FindProperty("killerCommentText").objectReferenceValue = killerCommentText;
        serializedReport.FindProperty("retryButton").objectReferenceValue = retryButton;
        serializedReport.ApplyModifiedPropertiesWithoutUndo();

        // Hide panel by default so it only appears on Game Over
        panelObj.SetActive(false);

        EditorUtility.SetDirty(reportUI);
        EditorUtility.SetDirty(panelObj);
        MarkActiveSceneDirty();

        Selection.activeGameObject = panelObj;
        EditorGUIUtility.PingObject(panelObj);

        Debug.Log($"[ResultReportUITool] '{canvas.name}/{PanelObjectName}' 생성 및 ResultReportUI 참조 자동 연결 완료! (초기 상태: 비활성화)");
    }

    private static Canvas FindOrCreateCanvas()
    {
        Canvas existingCanvas = Object.FindFirstObjectByType<Canvas>();
        if (existingCanvas != null)
        {
            return existingCanvas;
        }

        GameObject canvasObj = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static void EnsureEventSystem()
    {
        EventSystem existing = Object.FindFirstObjectByType<EventSystem>();
        if (existing != null)
        {
            return;
        }

        GameObject eventSystemObj = new GameObject("EventSystem", typeof(EventSystem));
        Undo.RegisterCreatedObjectUndo(eventSystemObj, "Create EventSystem");

#if ENABLE_INPUT_SYSTEM
        eventSystemObj.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObj.AddComponent<StandaloneInputModule>();
#endif
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.layer = LayerMask.NameToLayer("UI");
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static TMP_Text CreateTMPText(
        string name,
        Transform parent,
        string content,
        TMP_FontAsset fontAsset,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment,
        FontStyles fontStyle)
    {
        GameObject obj = CreateUIObject(name, parent);
        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = fontStyle;
        text.raycastTarget = false;

        if (fontAsset != null)
        {
            text.font = fontAsset;
        }

        return text;
    }

    private static void AddNeonOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    private static void StretchFull(RectTransform rect, float paddingX = 0f, float paddingY = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(paddingX, paddingY);
        rect.offsetMax = new Vector2(-paddingX, -paddingY);
    }

    private static TMP_FontAsset FindUIFontAsset()
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

    private static void MarkActiveSceneDirty()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
        }
    }
}
