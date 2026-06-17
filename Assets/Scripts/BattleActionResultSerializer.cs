using UnityEngine;

public static class BattleActionResultSerializer
{
    public static string ToJson(BattleActionResult result)
    {
        if (result == null)
        {
            Debug.LogWarning("[BattleActionResultSerializer] ToJson failed: result is null");
            return "";
        }

        try
        {
            return JsonUtility.ToJson(result);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[BattleActionResultSerializer] ToJson failed: {ex.Message}");
            return "";
        }
    }

    public static BattleActionResult FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonUtility.FromJson<BattleActionResult>(json);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[BattleActionResultSerializer] FromJson failed: {ex.Message}");
            return null;
        }
    }
}
