using System;
using System.Collections.Generic;
using UnityEngine;

public enum EffectStatType
{
    CurrentHp,
    MaxHp,
    CurrentTension
}

public enum EffectStatDuration
{
    Instant,
    ThisTurn,
    UntilNextTurn,
    Permanent
}

[Serializable]
public class StatDelta
{
    public EffectStatType statType;
    public int amount;
    public EffectStatDuration duration = EffectStatDuration.Instant;
    public bool clampHpToMax = true;
    public bool allowBelowZero;
}

[Serializable]
public class ModifyCharacterStatsRequest
{
    public BattleSlotOwner owner;
    public TargetSelector selector;
    public List<StatDelta> deltas = new List<StatDelta>();
    public BaseCardData sourceCard;
    public string sourceEffectRef;
    public bool requireTargetSelection;
    public int maxTargets = 1;
}

public class StatChangeRecord
{
    public BattleSlotOwner owner;
    public BattleFieldSlot slot;
    public string cardId;
    public string cardName;
    public EffectStatType statType;
    public EffectStatDuration duration;
    public int amount;
    public int beforeValue;
    public int afterValue;
}

public class ModifyCharacterStatsResult
{
    public bool success;
    public string message;
    public readonly List<EffectTargetCandidate> targets = new List<EffectTargetCandidate>();
    public readonly List<StatChangeRecord> changes = new List<StatChangeRecord>();
}

public class TemporaryStatModifier
{
    public string sourceEffectRef;
    public string sourceCardId;
    public BaseCardData sourceCard;
    public BattleFieldSlot targetSlot;
    public BattleSlotOwner targetOwner;
    public string targetCardId;
    public BaseCardData targetCard;
    public EffectStatType statType;
    public int amount;
    public EffectStatDuration duration;
    public int appliedTurn;
    public int expireTurn;
    public bool isReverted;
}

public static class EffectStatService
{
    private static readonly List<TemporaryStatModifier> temporaryModifiers =
        new List<TemporaryStatModifier>();

    public static void ModifyCharacterStats(
        ModifyCharacterStatsRequest request,
        EffectContext context,
        Action<ModifyCharacterStatsResult> onComplete)
    {
        ModifyCharacterStatsResult result = new ModifyCharacterStatsResult();

        if (request == null)
        {
            Complete(result, false, "스탯 변경 요청 정보가 없습니다.", onComplete);
            return;
        }

        BattleManager battleManager = context != null ? context.battleManager : null;

        if (battleManager == null)
        {
            Complete(result, false, "BattleManager가 연결되어 있지 않습니다.", onComplete);
            return;
        }

        if (request.deltas == null || request.deltas.Count == 0)
        {
            Complete(result, false, "적용할 스탯 변경 정보가 없습니다.", onComplete);
            return;
        }

        if (request.requireTargetSelection && request.maxTargets > 1)
        {
            Complete(result, false, "현재 스탯 변경 효과는 선택형 다중 대상을 지원하지 않습니다.", onComplete);
            return;
        }

        List<EffectTargetCandidate> candidates =
            EffectTargetingService.BuildTargetCandidates(request.selector, context);

        if (candidates == null || candidates.Count == 0)
        {
            Complete(result, false, "스탯을 변경할 대상이 없습니다.", onComplete);
            return;
        }

        if (!request.requireTargetSelection)
        {
            List<EffectTargetCandidate> targets = ResolveAutomaticTargets(request, candidates);

            if (targets.Count == 0)
            {
                Complete(result, false, "자동으로 적용할 스탯 변경 대상이 없습니다.", onComplete);
                return;
            }

            foreach (EffectTargetCandidate target in targets)
                ApplyToTarget(request, context, target, result);

            battleManager.RefreshAllUIFromExternal();
            Complete(result, result.changes.Count > 0, BuildResultMessage(result), onComplete);
            return;
        }

        CardQuestionPanel panel = battleManager.BattleCardQuestionPanel;

        if (panel == null)
        {
            Complete(result, false, "CardQuestionPanel이 없어 대상 선택을 처리하지 않았습니다.", onComplete);
            return;
        }

        if (panel.IsOpen())
        {
            Complete(result, false, "이미 카드 선택창이 열려 있어 대상 선택을 처리하지 않았습니다.", onComplete);
            return;
        }

        List<CardQuestionOption> options = BuildOptions(candidates);
        bool opened = panel.TryShowOptions(
            BuildQuestionMessage(request, candidates),
            options,
            !request.requireTargetSelection,
            selectedOption =>
            {
                EffectTargetCandidate selectedTarget = FindCandidate(candidates, selectedOption);
                ApplyToTarget(request, context, selectedTarget, result);
                battleManager.RefreshAllUIFromExternal();
                Complete(result, result.changes.Count > 0, BuildResultMessage(result), onComplete);
            },
            () => Complete(result, false, "스탯 변경 대상 선택을 취소했습니다.", onComplete)
        );

        if (!opened)
            Complete(result, false, "카드 선택창을 열 수 없어 대상 선택을 처리하지 않았습니다.", onComplete);
    }

    private static void ApplyToTarget(
        ModifyCharacterStatsRequest request,
        EffectContext context,
        EffectTargetCandidate target,
        ModifyCharacterStatsResult result)
    {
        if (target == null || target.slot == null || !target.slot.HasCharacter)
            return;

        result.targets.Add(target);

        foreach (StatDelta delta in request.deltas)
        {
            if (delta == null)
                continue;

            ApplyDelta(context, target, delta, result);
        }
    }

    public static void ExpireTurnEndModifiers(BattleManager battleManager, int currentTurn)
    {
        ExpireTemporaryStatModifiers(battleManager, EffectStatDuration.ThisTurn, currentTurn);
    }

    public static void ExpireTemporaryStatModifiers(
        BattleManager battleManager,
        EffectStatDuration duration,
        int currentTurn)
    {
        bool changed = false;

        foreach (TemporaryStatModifier modifier in temporaryModifiers)
        {
            if (modifier == null ||
                modifier.isReverted ||
                modifier.duration != duration ||
                !ShouldExpireModifier(modifier, currentTurn))
            {
                continue;
            }

            changed |= TryRevertTemporaryStatModifier(modifier);
        }

        temporaryModifiers.RemoveAll(modifier => modifier == null || modifier.isReverted);

        if (changed && battleManager != null)
            battleManager.RefreshAllUIFromExternal();
    }

    public static void RemoveModifiersForSlot(BattleFieldSlot slot)
    {
        if (slot == null)
            return;

        foreach (TemporaryStatModifier modifier in temporaryModifiers)
        {
            if (modifier != null && modifier.targetSlot == slot)
                modifier.isReverted = true;
        }

        temporaryModifiers.RemoveAll(modifier => modifier == null || modifier.isReverted);
    }

    private static List<EffectTargetCandidate> ResolveAutomaticTargets(
        ModifyCharacterStatsRequest request,
        List<EffectTargetCandidate> candidates)
    {
        List<EffectTargetCandidate> targets = new List<EffectTargetCandidate>();

        if (candidates == null || candidates.Count == 0)
            return targets;

        int maxTargets = request != null ? request.maxTargets : 1;

        if (maxTargets == 1 && candidates.Count > 1)
            return targets;

        int takeCount = maxTargets <= 0
            ? candidates.Count
            : Mathf.Min(maxTargets, candidates.Count);

        for (int i = 0; i < takeCount; i++)
        {
            if (candidates[i] != null)
                targets.Add(candidates[i]);
        }

        return targets;
    }

    private static void ApplyDelta(
        EffectContext context,
        EffectTargetCandidate target,
        StatDelta delta,
        ModifyCharacterStatsResult result)
    {
        BattleManager battleManager = context != null ? context.battleManager : null;
        BattleFieldSlot slot = target.slot;
        int beforeValue = GetStatValue(slot, delta.statType);
        int afterValue = beforeValue;

        if (IsTemporaryDuration(delta.duration) &&
            delta.statType != EffectStatType.CurrentTension)
        {
            Debug.LogWarning(
                $"EffectStatService: {delta.duration} {delta.statType} 변경은 아직 지원하지 않습니다."
            );
            return;
        }

        switch (delta.statType)
        {
            case EffectStatType.CurrentHp:
                afterValue = ApplyCurrentHpDelta(battleManager, context, slot, delta);
                break;

            case EffectStatType.CurrentTension:
                afterValue = Mathf.Max(
                    delta.allowBelowZero ? int.MinValue : 0,
                    slot.currentCharacterTension + delta.amount
                );
                slot.SetCharacterBattleStats(slot.currentCharacterHp, afterValue);
                afterValue = slot.currentCharacterTension;
                break;

            case EffectStatType.MaxHp:
                slot.ModifyCharacterMaxHp(delta.amount);
                afterValue = slot.currentCharacterMaxHp;
                break;
        }

        result.changes.Add(new StatChangeRecord
        {
            owner = target.owner,
            slot = slot,
            cardId = target.card != null ? target.card.id : "",
            cardName = target.card != null ? target.card.name : "",
            statType = delta.statType,
            duration = delta.duration,
            amount = delta.amount,
            beforeValue = beforeValue,
            afterValue = afterValue
        });

        RegisterTemporaryStatModifierIfNeeded(context, target, delta);
    }

    private static bool IsTemporaryDuration(EffectStatDuration duration)
    {
        return duration == EffectStatDuration.ThisTurn ||
            duration == EffectStatDuration.UntilNextTurn;
    }

    private static void RegisterTemporaryStatModifierIfNeeded(
        EffectContext context,
        EffectTargetCandidate target,
        StatDelta delta)
    {
        if (context == null ||
            target == null ||
            target.slot == null ||
            target.card == null ||
            !IsTemporaryDuration(delta.duration))
        {
            return;
        }

        if (delta.statType != EffectStatType.CurrentTension)
            return;

        BattleManager battleManager = context.battleManager;
        int currentTurn = battleManager != null
            ? battleManager.GetCurrentTurnCountFromExternal()
            : 0;

        RegisterTemporaryStatModifier(new TemporaryStatModifier
        {
            sourceEffectRef = context.sourceEffect != null
                ? GetEffectRef(context.sourceEffect)
                : "",
            sourceCardId = context.sourceCard != null ? context.sourceCard.id : "",
            sourceCard = context.sourceCard,
            targetSlot = target.slot,
            targetOwner = target.owner,
            targetCardId = target.card.id,
            targetCard = target.card,
            statType = delta.statType,
            amount = delta.amount,
            duration = delta.duration,
            appliedTurn = currentTurn,
            expireTurn = delta.duration == EffectStatDuration.ThisTurn
                ? currentTurn
                : currentTurn + 1
        });
    }

    public static void RegisterTemporaryStatModifier(TemporaryStatModifier modifier)
    {
        if (modifier == null)
            return;

        temporaryModifiers.Add(modifier);

        Debug.Log(
            $"[TemporaryStatModifier] registered ref={modifier.sourceEffectRef}, " +
            $"target={modifier.targetCardId}, stat={modifier.statType}, amount={modifier.amount}, " +
            $"duration={modifier.duration}, appliedTurn={modifier.appliedTurn}, expireTurn={modifier.expireTurn}"
        );
    }

    private static bool ShouldExpireModifier(TemporaryStatModifier modifier, int currentTurn)
    {
        if (modifier.duration == EffectStatDuration.ThisTurn)
            return currentTurn > modifier.appliedTurn;

        if (modifier.duration == EffectStatDuration.UntilNextTurn)
            return currentTurn >= modifier.expireTurn;

        return false;
    }

    private static bool TryRevertTemporaryStatModifier(TemporaryStatModifier modifier)
    {
        if (modifier == null || modifier.isReverted)
            return false;

        BattleFieldSlot slot = modifier.targetSlot;

        if (slot == null ||
            !slot.HasCharacter ||
            slot.characterCard == null ||
            !IsSameCard(slot.characterCard, modifier.targetCard, modifier.targetCardId))
        {
            modifier.isReverted = true;
            return false;
        }

        switch (modifier.statType)
        {
            case EffectStatType.CurrentTension:
                int revertedTension = Mathf.Max(0, slot.currentCharacterTension - modifier.amount);
                slot.SetCharacterBattleStats(slot.currentCharacterHp, revertedTension);
                modifier.isReverted = true;
                Debug.Log(
                    $"[TemporaryStatModifier] reverted target={modifier.targetCardId}, " +
                    $"stat={modifier.statType}, amount={modifier.amount}, tension={revertedTension}"
                );
                return true;

            default:
                modifier.isReverted = true;
                return false;
        }
    }

    private static bool IsSameCard(
        BaseCardData currentCard,
        BaseCardData originalCard,
        string originalCardId)
    {
        if (currentCard == null)
            return false;

        if (originalCard != null && ReferenceEquals(currentCard, originalCard))
            return true;

        return !string.IsNullOrEmpty(originalCardId) &&
            string.Equals(currentCard.id, originalCardId, StringComparison.Ordinal);
    }

    private static string GetEffectRef(EffectData effect)
    {
        if (effect == null)
            return "";

        if (!string.IsNullOrWhiteSpace(effect.@ref))
            return effect.@ref;

        return !string.IsNullOrWhiteSpace(effect.refName)
            ? effect.refName
            : "";
    }

    private static int ApplyCurrentHpDelta(
        BattleManager battleManager,
        EffectContext context,
        BattleFieldSlot slot,
        StatDelta delta)
    {
        int beforeHp = slot.currentCharacterHp;

        if (delta.amount > 0 && delta.clampHpToMax && battleManager != null)
        {
            battleManager.HealCharacterFromExternal(slot, delta.amount);
        }
        else
        {
            int nextHp = beforeHp + delta.amount;

            if (!delta.allowBelowZero)
                nextHp = Mathf.Max(0, nextHp);

            slot.SetCharacterBattleStats(nextHp, slot.currentCharacterTension);
        }

        if (battleManager != null &&
            battleManager.GetEffectiveCharacterHpFromExternal(slot, ResolveEffectLocationSlot(context, slot)) <= 0)
        {
            battleManager.RequestResolveZeroHpCharacterFromExternal(slot, ResolveEffectLocationSlot(context, slot));
        }

        return slot.currentCharacterHp;
    }

    private static BattleFieldSlot ResolveEffectLocationSlot(
        EffectContext context,
        BattleFieldSlot fallback)
    {
        if (context == null)
            return fallback;

        if (context.battleLocationSlot != null)
            return context.battleLocationSlot;

        if (context.defenderSlot != null)
            return context.defenderSlot;

        return fallback;
    }

    private static int GetStatValue(BattleFieldSlot slot, EffectStatType statType)
    {
        if (slot == null)
            return 0;

        switch (statType)
        {
            case EffectStatType.CurrentHp:
                return slot.currentCharacterHp;
            case EffectStatType.MaxHp:
                return slot.currentCharacterMaxHp;
            case EffectStatType.CurrentTension:
                return slot.currentCharacterTension;
            default:
                return 0;
        }
    }

    private static List<CardQuestionOption> BuildOptions(List<EffectTargetCandidate> candidates)
    {
        List<CardQuestionOption> options = new List<CardQuestionOption>();

        if (candidates == null)
            return options;

        foreach (EffectTargetCandidate candidate in candidates)
        {
            if (candidate == null || candidate.card == null)
                continue;

            options.Add(new CardQuestionOption(candidate.card, candidate.slot));
        }

        return options;
    }

    private static EffectTargetCandidate FindCandidate(
        List<EffectTargetCandidate> candidates,
        CardQuestionOption selectedOption)
    {
        if (candidates == null || selectedOption == null)
            return null;

        foreach (EffectTargetCandidate candidate in candidates)
        {
            if (candidate == null)
                continue;

            if (candidate.slot == selectedOption.linkedSlot &&
                candidate.card == selectedOption.card)
            {
                return candidate;
            }
        }

        return null;
    }

    private static string BuildQuestionMessage(
        ModifyCharacterStatsRequest request,
        List<EffectTargetCandidate> candidates)
    {
        string sourceName = request != null && request.sourceCard != null
            ? request.sourceCard.name
            : "카드 효과";

        int count = candidates != null ? candidates.Count : 0;
        return $"{sourceName}: 스탯을 변경할 대상을 선택하세요. 후보 {count}장";
    }

    private static string BuildResultMessage(ModifyCharacterStatsResult result)
    {
        if (result == null || result.targets.Count == 0)
            return "스탯 변경 대상이 없습니다.";

        if (result.changes.Count == 0)
            return "적용된 스탯 변경이 없습니다.";

        if (result.targets.Count == 1)
        {
            EffectTargetCandidate target = result.targets[0];
            string cardName = target.card != null ? target.card.name : "선택 캐릭터";
            return $"{cardName} 스탯 변경 {result.changes.Count}건을 적용했습니다.";
        }

        return $"대상 {result.targets.Count}체에 스탯 변경 {result.changes.Count}건을 적용했습니다.";
    }

    private static void Complete(
        ModifyCharacterStatsResult result,
        bool success,
        string message,
        Action<ModifyCharacterStatsResult> onComplete)
    {
        if (result != null)
        {
            result.success = success;
            result.message = message;

            Debug.Log(
                $"[ModifyCharacterStats] success={success}, targets={result.targets.Count}, " +
                $"changes={result.changes.Count}, message={message}"
            );
        }

        onComplete?.Invoke(result);
    }
}
