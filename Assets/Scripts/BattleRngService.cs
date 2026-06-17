using System.Collections.Generic;
using UnityEngine;

// BattleRngService is for the host or current authority to confirm random
// results before placing resolved IDs in BattleActionResult.
// Non-host clients should not call this service for authoritative random
// results; they should apply resolved cardInstanceId/slotId/choiceId values
// from BattleActionResult. Debug/local modes may still use this directly.
public class BattleRngService
{
    private int seed;
    private System.Random random;

    public BattleRngService()
    {
        SetSeed(System.Environment.TickCount);
    }

    public void SetSeed(int seed)
    {
        this.seed = seed;
        random = new System.Random(seed);
        Debug.Log($"[BattleRngService] Seed set: {seed}");
    }

    public int GetSeed()
    {
        return seed;
    }

    public int Range(int minInclusive, int maxExclusive)
    {
        if (random == null)
            SetSeed(seed);

        if (maxExclusive <= minInclusive)
        {
            Debug.LogWarning($"[BattleRngService] Invalid range: min={minInclusive}, max={maxExclusive}");
            return minInclusive;
        }

        int result = random.Next(minInclusive, maxExclusive);
        Debug.Log($"[BattleRngService] Range({minInclusive}, {maxExclusive}) => {result}");
        return result;
    }

    public T PickOne<T>(List<T> list)
    {
        if (list == null || list.Count <= 0)
        {
            Debug.LogWarning("[BattleRngService] PickOne failed: list is null or empty");
            return default;
        }

        int index = Range(0, list.Count);
        return list[index];
    }
}
