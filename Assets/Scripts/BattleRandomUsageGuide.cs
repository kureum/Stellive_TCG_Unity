using UnityEngine;

public static class BattleRandomUsageGuide
{
    public static void LogSummary()
    {
        Debug.Log(
            "[BattleRandomUsageGuide]\n" +
            "Current random usage points:\n" +
            "1. BattleManager.StartBattleSetup: Shuffle(myPlayer.mainDeck), Shuffle(enemyPlayer.mainDeck). Purpose: initial main deck order. Affects game state: yes. Online share required: yes. Represent result as ordered cardInstanceId list; seed is acceptable only if both clients use identical algorithm/input order.\n" +
            "2. BattleManager.StartBroadcastSetupPhase: UnityEngine.Random.Range(0, 2) for firstPlayerSide. Purpose: first setup/action side. Affects game state: yes. Online share required: yes. Represent result as BattlePlayerSide.\n" +
            "3. BattleManager.Debug_AutoPlaceBroadcastsForSide: random slot/card indices for debug broadcast setup. Purpose: debug-only automatic broadcast placement. Affects game state: yes in debug. Online share required: no for production; if used online, represent chosen cardInstanceId and target slotId.\n" +
            "4. BattleManager.ShuffleMainDeckFromExternal / AddCardToMainDeckFromExternal with DeckInsertPosition.Shuffle: Shuffle(targetPlayer.mainDeck). Purpose: effect-driven deck shuffle. Affects game state: yes. Online share required: yes. Represent result as ordered cardInstanceId list.\n" +
            "5. BattleManager.Shuffle: UnityEngine.Random.Range(i, deck.Count). Purpose: Fisher-Yates swap index. Affects game state through callers. Online share required: yes through caller result. Represent low-level result as swap indices only for logs; synchronize deck order instead.\n" +
            "6. EffectManager.ResolveForceOpponentSummonOrSackFromHandEffect: UnityEngine.Random.Range(0, opponentHand.Count). Purpose: random opponent hand reveal/resolve. Affects game state: yes. Online share required: yes. Represent result as selected cardInstanceId; keep handIndex only as fallback/debug.\n" +
            "\n" +
            "Online recommended policy:\n" +
            "- Do not let both clients independently call UnityEngine.Random for authoritative gameplay results.\n" +
            "- The authority/host rolls once, records the result, and sends deterministic results to the peer.\n" +
            "- For cards, send cardInstanceId. For board targets, send slotId. For deck shuffles, send final ordered cardInstanceId list or a seed plus exact algorithm/input order.\n" +
            "- Peers apply received results and do not reroll.\n" +
            "\n" +
            "Deck shuffle policy candidates:\n" +
            "- Preferred initial policy: host shuffles and sends final deck order as cardInstanceId list.\n" +
            "- Seed policy: host sends seed only if deck input order and shuffle algorithm are fixed and versioned.\n" +
            "- Debug policy: log seed, swap indices, and final cardInstanceId order for replay diagnostics.\n" +
            "\n" +
            "Random card selection policy:\n" +
            "- Authority selects one card once and includes selected cardInstanceId in an ActionResult.\n" +
            "- Receiver verifies that the cardInstanceId is in the expected zone, then applies the result.\n" +
            "- handIndex/index values remain fallback or validation hints, not the authoritative identity.\n" +
            "\n" +
            "Random effect policy:\n" +
            "- Effect resolution should produce an ActionResult containing selected cardInstanceId, targetSlotId, affected values, and optional rng log metadata.\n" +
            "- Receiver should not run random choice again during effect playback."
        );
    }

    public static void LogHostConfirmedPolicy()
    {
        Debug.Log(
            "[BattleRandomUsageGuide:HostConfirmedPolicy]\n" +
            "Project default online random policy: host-confirmed resolved results.\n" +
            "- Only the host or current authority rolls random values that affect game state.\n" +
            "- Non-host clients do not roll authoritative random values.\n" +
            "- Non-host clients apply resolved results from BattleActionResult.\n" +
            "- Deck shuffle synchronization should prefer sharing the completed cardInstanceId order over sharing only a seed.\n" +
            "- Random hand selection should share the selected cardInstanceId.\n" +
            "- Random field target selection should share targetSlotId.\n" +
            "- Random choices should share choiceId or cardInstanceId, depending on the choice domain.\n" +
            "- Seed-based replay can remain as debug metadata, but the gameplay contract is resolved ID application."
        );
    }
}
