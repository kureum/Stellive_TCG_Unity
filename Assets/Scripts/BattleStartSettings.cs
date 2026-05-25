using UnityEngine;

public static class BattleStartSettings
{
    private const string MyPresetPlayerPrefsKey = "BattleStartSettings.SelectedMyPresetIndex";

    public static int SelectedMyPresetIndex { get; private set; } =
        PlayerPrefs.GetInt(MyPresetPlayerPrefsKey, -1);

    public static bool HasSelectedMyPreset
    {
        get { return SelectedMyPresetIndex >= 0; }
    }

    public static void SelectMyPreset(int presetIndex)
    {
        SelectedMyPresetIndex = presetIndex;
        PlayerPrefs.SetInt(MyPresetPlayerPrefsKey, presetIndex);
        PlayerPrefs.Save();
    }

    public static void Clear()
    {
        SelectedMyPresetIndex = -1;
        PlayerPrefs.DeleteKey(MyPresetPlayerPrefsKey);
        PlayerPrefs.Save();
    }
}
