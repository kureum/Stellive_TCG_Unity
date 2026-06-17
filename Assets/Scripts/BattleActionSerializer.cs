using UnityEngine;

public static class BattleActionSerializer
{
    public static string ToJson(BattleAction action)
    {
        if (action == null)
        {
            Debug.LogWarning("[BattleActionSerializer] ToJson failed: action is null");
            return "";
        }

        try
        {
            return JsonUtility.ToJson(action);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[BattleActionSerializer] ToJson failed: {ex.Message}");
            return "";
        }
    }

    public static BattleAction FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonUtility.FromJson<BattleAction>(json);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[BattleActionSerializer] FromJson failed: {ex.Message}");
            return null;
        }
    }
}
