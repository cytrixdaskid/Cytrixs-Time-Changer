using BepInEx;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("com.cytrix.timechanger", "Time Changer", "1.0.0")]
public class TimeChangerPlugin : BaseUnityPlugin
{
    private Rect windowRect = new Rect(100, 100, 320, 140);
    private float hourSlider = 12f;

    private void Awake()
    {
        Harmony harmony = new Harmony("com.cytrix.timechanger.harmony");
        harmony.PatchAll();

        Logger.LogInfo("Time Changer loaded successfully");
    }

    private void OnGUI()
    {
        windowRect = GUI.Window(9999, windowRect, DrawWindow, "Time Changer");
    }

    private void DrawWindow(int id)
    {
        GUI.Label(new Rect(20, 30, 280, 20),
            $"Time: {FormatTime(hourSlider)}");

        hourSlider = GUI.HorizontalSlider(
            new Rect(20, 60, 280, 20),
            hourSlider,
            0f,
            24f
        );

        if (Event.current.isMouse)
        {
            TimeOverrideState.Enabled = true;
            TimeOverrideState.ForcedTimestep = hourSlider / 24f;
        }

        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    private string FormatTime(float hour)
    {
        int h = Mathf.FloorToInt(hour);
        int m = Mathf.FloorToInt((hour - h) * 60);
        return $"{h:00}:{m:00}";
    }
}

public static class TimeOverrideState
{
    public static bool Enabled = true;
    public static float ForcedTimestep = 0.5f;
}

[HarmonyPatch(typeof(BetterDayNightManager), "Update")]
public class BetterDayNightManager_Update_Patch
{
    static void Postfix(BetterDayNightManager __instance)
    {
        if (!TimeOverrideState.Enabled)
            return;

        __instance.currentTimestep =
            Mathf.Clamp01(TimeOverrideState.ForcedTimestep);

        __instance.UpdateTimeOfDay();
    }
}