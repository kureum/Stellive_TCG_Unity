using UnityEngine;
using System;
using System.Collections.Generic;

public class TestEnemy : MonoBehaviour
{
    [Header("References")]
    public BattleManager battleManager;

    [Header("Settings")]
    [Tooltip("테스트 상대가 사용할 덱 프리셋 번호입니다. Preset 1이면 0입니다.")]
    public int enemyPresetIndex = 2;
    [Tooltip("꺼두면 테스트 중 100000 시청자 승리 판정을 진행하지 않습니다.")]
    public bool victoryCheckEnabled = true;
    public float actionDelay = 0.5f;

    [Header("Enemy Summon")]
    public bool enemySummonEnabled = true;
    public bool enemyBacksideSummonEnabled = true;
    public bool enemyFrontSummonEnabled = false;

    private bool isWaiting;
    private float timer;

    private void Awake()
    {
        if (battleManager == null)
            battleManager = FindAnyObjectByType<BattleManager>();

        if (battleManager != null)
        {
            battleManager.enemyPresetIndex = enemyPresetIndex;
            battleManager.victoryCheckEnabled = victoryCheckEnabled;
        }
    }

    private void Update()
    {
        if (battleManager == null)
            return;

        battleManager.victoryCheckEnabled = victoryCheckEnabled;

        if (battleManager.IsBroadcastSetupPhase())
        {
            HandleBroadcastSetupTurn();
            return;
        }

        if (battleManager.IsEnemyActionTurn())
        {
            HandleMainGameActionTurn();
            return;
        }

        isWaiting = false;
        timer = 0f;
    }

    private void HandleBroadcastSetupTurn()
    {
        if (!battleManager.IsEnemySetupTurn())
        {
            isWaiting = false;
            timer = 0f;
            return;
        }

        if (!isWaiting)
        {
            isWaiting = true;
            timer = actionDelay;
        }

        timer -= Time.deltaTime;

        if (timer > 0f)
            return;

        isWaiting = false;
        timer = 0f;

        battleManager.TestEnemyPlaceBroadcastCard();
    }

    private void HandleMainGameActionTurn()
    {
        if (!isWaiting)
        {
            isWaiting = true;
            timer = actionDelay;
        }

        timer -= Time.deltaTime;

        if (timer > 0f)
            return;

        isWaiting = false;
        timer = 0f;

        bool didAction = false;

        if (enemySummonEnabled)
        {
            if (enemyFrontSummonEnabled)
            {
                didAction =
                    battleManager.TestEnemyTryFlipSummonCharacter() ||
                    battleManager.TestEnemyTrySummonFrontCharacter();
            }

            if (!didAction && enemyBacksideSummonEnabled)
                didAction = battleManager.TestEnemyTrySummonBacksideCharacter();
        }

        if (didAction)
            return;

        battleManager.TestEnemyPassAction();
    }

    public bool TryResolveEffectActivation(
        EffectTiming timing,
        EffectContext context,
        Action onComplete)
    {
        if (battleManager == null || battleManager.effectManager == null)
            return false;

        if (timing != EffectTiming.OnRest)
            return false;

        List<EffectCandidate> candidates =
            battleManager.effectManager.GetPlayableEffects(timing, context);

        if (candidates == null || candidates.Count == 0)
            return false;

        if (!AreAllCandidatesTestEnemyMandatoryEffects(timing, candidates))
            return false;

        ResolveCandidatesSequentially(candidates, context, 0, onComplete);
        return true;
    }

    private bool AreAllCandidatesTestEnemyMandatoryEffects(
        EffectTiming timing,
        List<EffectCandidate> candidates)
    {
        if (candidates == null || candidates.Count == 0)
            return false;

        foreach (EffectCandidate candidate in candidates)
        {
            if (!IsTestEnemyMandatoryEffect(timing, candidate))
                return false;
        }

        return true;
    }

    private bool IsTestEnemyMandatoryEffect(
        EffectTiming timing,
        EffectCandidate candidate)
    {
        return timing == EffectTiming.OnRest &&
            candidate != null &&
            candidate.owner == BattleSlotOwner.Enemy &&
            candidate.card is CharacterCardData;
    }

    private void ResolveCandidatesSequentially(
        List<EffectCandidate> candidates,
        EffectContext context,
        int index,
        Action onComplete)
    {
        if (candidates == null || index >= candidates.Count)
        {
            onComplete?.Invoke();
            return;
        }

        battleManager.effectManager.ResolveEffect(
            candidates[index],
            context,
            () => ResolveCandidatesSequentially(candidates, context, index + 1, onComplete)
        );
    }
}
