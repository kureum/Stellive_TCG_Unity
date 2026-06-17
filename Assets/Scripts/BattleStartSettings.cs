using UnityEngine;

public enum BattleMode
{
    LocalTest,
    OnlineHost,
    OnlineClient
}

public static class BattleStartSettings
{
    private const string MyPresetPlayerPrefsKey = "BattleStartSettings.SelectedMyPresetIndex";

    public static int SelectedMyPresetIndex { get; private set; } =
        PlayerPrefs.GetInt(MyPresetPlayerPrefsKey, -1);

    public static BattleMode BattleMode { get; private set; } = BattleMode.LocalTest;
    public static string RoomCode { get; private set; } = "";
    public static bool IsHost { get; private set; } = false;
    public static bool IsOnlineBattle { get; private set; } = false;

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

    public static void SetLocalTestMode()
    {
        BattleMode = BattleMode.LocalTest;
        RoomCode = "";
        IsHost = false;
        IsOnlineBattle = false;
        Debug.Log("[BattleStartSettings] LocalTest mode set");
    }

    public static void SetOnlineHostMode(string roomCode)
    {
        BattleMode = BattleMode.OnlineHost;
        RoomCode = roomCode ?? "";
        IsHost = true;
        IsOnlineBattle = true;
        Debug.Log($"[BattleStartSettings] OnlineHost mode set. roomCode={RoomCode}");
    }

    public static void SetOnlineClientMode(string roomCode)
    {
        BattleMode = BattleMode.OnlineClient;
        RoomCode = roomCode ?? "";
        IsHost = false;
        IsOnlineBattle = true;
        Debug.Log($"[BattleStartSettings] OnlineClient mode set. roomCode={RoomCode}");
    }

    public static void ClearOnlineSettings()
    {
        SetLocalTestMode();
    }

    public static void Clear()
    {
        SelectedMyPresetIndex = -1;
        ClearOnlineSettings();
        PlayerPrefs.DeleteKey(MyPresetPlayerPrefsKey);
        PlayerPrefs.Save();
    }
}
