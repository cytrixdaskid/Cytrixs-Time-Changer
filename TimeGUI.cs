using BepInEx;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;

[BepInPlugin("com.cytrix.timechanger", "Time Changer", "4.1.0")]
public class TimeChanger : BaseUnityPlugin
{
    private RectTransform panel;
    private Slider slider;
    private Text timeText;

    private bool dragging;
    private Vector2 dragOffset;

    private object dayNight;
    private FieldInfo timeField;
    private PropertyInfo timeProperty;
    private MethodInfo refreshMethod;

    void Awake()
    {
        SceneManager.sceneLoaded += (_, __) => StartCoroutine(Init());
    }

    IEnumerator Init()
    {
        yield return null;
        yield return null;
        yield return null;

        FindDayNightManager();
        if (dayNight == null)
        {
            Logger.LogError("BetterDayNightManager not found!");
            yield break;
        }

        SetupEventSystem();
        CreateGUI();

        // Force initial update
        ApplyTime(12f);
    }

    FieldInfo[] allTimeFields;
    FieldInfo progressionField;
    MethodInfo updateMethod;

    void FindDayNightManager()
    {
        dayNight = FindObjectOfType(Type.GetType("BetterDayNightManager, Assembly-CSharp"));
        if (dayNight == null) return;

        Type t = dayNight.GetType();

        // Grab ALL float fields that look time-related
        allTimeFields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => f.FieldType == typeof(float) && LooksLikeTime(f.Name))
            .ToArray();

        // Find a progression / speed field (if exists)
        progressionField = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(f =>
                f.FieldType == typeof(float) &&
                (f.Name.ToLower().Contains("speed") ||
                 f.Name.ToLower().Contains("rate") ||
                 f.Name.ToLower().Contains("progress")));

        updateMethod = t.GetMethod("UpdateTimeOfDay",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Logger.LogInfo("[TimeChanger] Found time fields:");
        foreach (var f in allTimeFields)
            Logger.LogInfo("  - " + f.Name);

        Logger.LogInfo($"[TimeChanger] Progression field: {progressionField?.Name}");
    }

    bool LooksLikeTime(string name)
    {
        name = name.ToLower();
        return name.Contains("time") || name.Contains("day") || name.Contains("night");
    }

    void ApplyTime(float hour)
    {
        if (dayNight == null || allTimeFields == null)
            return;

        float timestep = Mathf.Clamp01(hour / 24f);

        // 🔒 Freeze progression if possible
        if (progressionField != null)
            progressionField.SetValue(dayNight, 0f);

        // 🔥 Force ALL time-related fields
        foreach (var field in allTimeFields)
            field.SetValue(dayNight, timestep);

        // 🔁 Force update
        updateMethod?.Invoke(dayNight, null);
    }


    // ---------------- UI ----------------

    void SetupEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
        DontDestroyOnLoad(es);
    }

    void CreateGUI()
    {
        var canvasGO = new GameObject("TimeChangerCanvas");
        DontDestroyOnLoad(canvasGO);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Panel
        var panelGO = new GameObject("Panel", typeof(Image));
        panelGO.transform.SetParent(canvasGO.transform, false);
        panel = panelGO.GetComponent<RectTransform>();
        panel.sizeDelta = new Vector2(420, 160);

        panelGO.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.15f, 0.95f);

        // Header
        var header = new GameObject("Header", typeof(Image));
        header.transform.SetParent(panel, false);
        header.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.2f);

        var headerRT = header.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0, 1);
        headerRT.anchorMax = new Vector2(1, 1);
        headerRT.sizeDelta = new Vector2(0, 40);

        AddDragHandlers(header);

        // Title
        var title = CreateText(panel, "Time of Day", 20);
        title.rectTransform.anchoredPosition = new Vector2(0, -20);

        // Slider
        slider = DefaultControls.CreateSlider(new DefaultControls.Resources()).GetComponent<Slider>();
        slider.transform.SetParent(panel, false);
        slider.minValue = 0;
        slider.maxValue = 24;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };

        var srt = slider.GetComponent<RectTransform>();
        srt.sizeDelta = new Vector2(340, 25);
        srt.anchoredPosition = new Vector2(0, -80);

        slider.onValueChanged.AddListener(OnSliderChanged);

        // Time text
        timeText = CreateText(panel, "12:00", 18);
        timeText.rectTransform.anchoredPosition = new Vector2(0, -120);

        slider.value = 12f;
    }

    Text CreateText(Transform parent, string text, int size)
    {
        var go = new GameObject("Text", typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        return t;
    }

    void AddDragHandlers(GameObject header)
    {
        var trigger = header.AddComponent<EventTrigger>();

        AddTrigger(trigger, EventTriggerType.PointerDown, e =>
        {
            dragging = true;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                panel, ((PointerEventData)e).position, null, out dragOffset);
        });

        AddTrigger(trigger, EventTriggerType.PointerUp, _ => dragging = false);
    }

    void AddTrigger(EventTrigger t, EventTriggerType type, Action<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action(_));
        t.triggers.Add(entry);
    }

    void Update()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.sendNavigationEvents = false;
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (!dragging) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)panel.parent, Input.mousePosition, null, out var pos);
        panel.anchoredPosition = pos - dragOffset;
    }

    void OnSliderChanged(float hour)
    {
        int h = Mathf.FloorToInt(hour);
        int m = Mathf.FloorToInt((hour - h) * 60);
        timeText.text = $"{h:00}:{m:00}";

        ApplyTime(hour);
    }
}