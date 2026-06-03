using System;
using System.Collections.Generic;
using UnityEngine;

public enum EffectCardKind
{
    Any,
    Idol,
    Broadcast,
    Character,
    Content
}

public enum EffectTargetOwner
{
    Any,
    Me,
    Enemy,
    ActingOwner,
    OpponentOfActingOwner
}

public enum EffectFaceState
{
    Any,
    FaceUpOnly,
    FaceDownOnly
}

public enum TargetSelectorScope
{
    FieldCharacters,
    OwnFieldCharacters,
    OpponentFieldCharacters,
    CollabParticipants,
    AdjacentToSource,
    EmptyOwnBroadcastSlots,
    EmptyBroadcastSlots
}

[Serializable]
public class CardFilter
{
    public EffectCardKind kind = EffectCardKind.Any;
    public List<string> anyTags = new List<string>();
    public List<string> allTags = new List<string>();
    public EffectTargetOwner owner = EffectTargetOwner.Any;
    public EffectFaceState faceState = EffectFaceState.Any;

    public static CardFilter Any()
    {
        return new CardFilter();
    }
}

[Serializable]
public class TargetSelector
{
    public TargetSelectorScope scope = TargetSelectorScope.FieldCharacters;
    public EffectTargetOwner owner = EffectTargetOwner.ActingOwner;
    public CardFilter filter = new CardFilter();

    public static TargetSelector ForScope(TargetSelectorScope scope)
    {
        return new TargetSelector { scope = scope };
    }
}

public class EffectTargetCandidate
{
    public BattleFieldSlot slot;
    public BaseCardData card;
    public BattleSlotOwner owner;
    public bool isFaceDown;
}

public static class EffectTargetingService
{
    public static bool CardMatchesFilter(BaseCardData card, CardFilter filter)
    {
        CardFilter safeFilter = filter ?? CardFilter.Any();

        if (!MatchesKind(card, safeFilter.kind))
            return false;

        if (!MatchesAnyTags(card, safeFilter.anyTags))
            return false;

        if (!MatchesAllTags(card, safeFilter.allTags))
            return false;

        return true;
    }

    public static List<EffectTargetCandidate> BuildTargetCandidates(
        TargetSelector selector,
        EffectContext context)
    {
        List<EffectTargetCandidate> candidates = new List<EffectTargetCandidate>();

        if (context == null || context.battleManager == null)
            return candidates;

        TargetSelector safeSelector = selector ?? TargetSelector.ForScope(TargetSelectorScope.FieldCharacters);
        CardFilter filter = safeSelector.filter ?? CardFilter.Any();

        switch (safeSelector.scope)
        {
            case TargetSelectorScope.FieldCharacters:
                AddFieldCharacterCandidates(candidates, context, null);
                break;

            case TargetSelectorScope.OwnFieldCharacters:
                AddFieldCharacterCandidates(
                    candidates,
                    context,
                    ResolveOwner(safeSelector.owner, context));
                break;

            case TargetSelectorScope.OpponentFieldCharacters:
                AddFieldCharacterCandidates(
                    candidates,
                    context,
                    GetOpponent(ResolveOwner(safeSelector.owner, context)));
                break;

            case TargetSelectorScope.CollabParticipants:
                AddSlotCharacterCandidate(candidates, context.attackerSlot);
                AddSlotCharacterCandidate(candidates, context.defenderSlot);
                break;

            case TargetSelectorScope.AdjacentToSource:
                AddAdjacentCharacterCandidates(candidates, context);
                break;

            case TargetSelectorScope.EmptyOwnBroadcastSlots:
                AddEmptyBroadcastSlotCandidates(
                    candidates,
                    context,
                    ResolveOwner(safeSelector.owner, context));
                break;

            case TargetSelectorScope.EmptyBroadcastSlots:
                AddEmptyBroadcastSlotCandidates(candidates, context, null);
                break;
        }

        ApplyFilterInPlace(candidates, filter, context);
        return candidates;
    }

    private static void AddFieldCharacterCandidates(
        List<EffectTargetCandidate> candidates,
        EffectContext context,
        BattleSlotOwner? owner)
    {
        foreach (BattleFieldSlot slot in GetAllSlots(context))
        {
            if (owner.HasValue && slot.characterOwner != owner.Value)
                continue;

            AddSlotCharacterCandidate(candidates, slot);
        }
    }

    private static void AddAdjacentCharacterCandidates(
        List<EffectTargetCandidate> candidates,
        EffectContext context)
    {
        BattleFieldSlot sourceSlot = context != null ? context.sourceSlot : null;

        if (sourceSlot == null)
            return;

        foreach (BattleFieldSlot slot in GetAllSlots(context))
        {
            if (slot == null || slot == sourceSlot)
                continue;

            if (AreSlotsAdjacent(sourceSlot, slot))
                AddSlotCharacterCandidate(candidates, slot);
        }
    }

    private static bool AreSlotsAdjacent(BattleFieldSlot sourceSlot, BattleFieldSlot targetSlot)
    {
        if (sourceSlot == null || targetSlot == null || sourceSlot == targetSlot)
            return false;

        if (sourceSlot.owner == targetSlot.owner)
        {
            int distance =
                Mathf.Abs(sourceSlot.x - targetSlot.x) +
                Mathf.Abs(sourceSlot.y - targetSlot.y);

            return distance == 1;
        }

        return IsAdjacentAcrossFields(sourceSlot, targetSlot);
    }

    private static bool IsAdjacentAcrossFields(BattleFieldSlot sourceSlot, BattleFieldSlot targetSlot)
    {
        if (sourceSlot == null || targetSlot == null)
            return false;

        if (sourceSlot.owner == targetSlot.owner)
            return false;

        int mirroredX = 4 - sourceSlot.x;
        bool isFrontRowConnected =
            sourceSlot.y == 2 &&
            targetSlot.y == 2;

        return targetSlot.x == mirroredX && isFrontRowConnected;
    }

    private static void AddEmptyBroadcastSlotCandidates(
        List<EffectTargetCandidate> candidates,
        EffectContext context,
        BattleSlotOwner? owner)
    {
        foreach (BattleFieldSlot slot in GetAllSlots(context))
        {
            if (slot == null)
                continue;

            if (owner.HasValue && slot.owner != owner.Value)
                continue;

            if (!slot.HasBroadcast || slot.HasCharacter)
                continue;

            candidates.Add(new EffectTargetCandidate
            {
                slot = slot,
                card = slot.broadcastCard,
                owner = slot.owner,
                isFaceDown = false
            });
        }
    }

    private static void AddSlotCharacterCandidate(
        List<EffectTargetCandidate> candidates,
        BattleFieldSlot slot)
    {
        if (candidates == null ||
            slot == null ||
            !slot.HasCharacter)
        {
            return;
        }

        foreach (EffectTargetCandidate existing in candidates)
        {
            if (existing != null && existing.slot == slot)
                return;
        }

        candidates.Add(new EffectTargetCandidate
        {
            slot = slot,
            card = slot.characterCard,
            owner = slot.characterOwner,
            isFaceDown = slot.isCharacterFaceDown
        });
    }

    private static IEnumerable<BattleFieldSlot> GetAllSlots(EffectContext context)
    {
        if (context == null || context.battleManager == null)
            yield break;

        foreach (BattleFieldSlot slot in GetSlots(context.battleManager, BattleSlotOwner.My))
        {
            if (slot != null)
                yield return slot;
        }

        foreach (BattleFieldSlot slot in GetSlots(context.battleManager, BattleSlotOwner.Enemy))
        {
            if (slot != null)
                yield return slot;
        }
    }

    private static IReadOnlyList<BattleFieldSlot> GetSlots(
        BattleManager battleManager,
        BattleSlotOwner owner)
    {
        if (battleManager == null)
            return Array.Empty<BattleFieldSlot>();

        BattlePlayerSide side = owner == BattleSlotOwner.My
            ? BattlePlayerSide.My
            : BattlePlayerSide.Enemy;

        return battleManager.GetSlotsForMovement(side) ?? Array.Empty<BattleFieldSlot>();
    }

    private static void ApplyFilterInPlace(
        List<EffectTargetCandidate> candidates,
        CardFilter filter,
        EffectContext context)
    {
        if (candidates == null)
            return;

        CardFilter safeFilter = filter ?? CardFilter.Any();

        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            if (!Matches(candidates[i], safeFilter, context))
                candidates.RemoveAt(i);
        }
    }

    private static bool Matches(
        EffectTargetCandidate candidate,
        CardFilter filter,
        EffectContext context)
    {
        if (candidate == null)
            return false;

        if (!MatchesOwner(candidate.owner, filter.owner, context))
            return false;

        if (!MatchesFaceState(candidate, filter.faceState))
            return false;

        if (!MatchesKind(candidate.card, filter.kind))
            return false;

        if (!MatchesAnyTags(candidate.card, filter.anyTags))
            return false;

        if (!MatchesAllTags(candidate.card, filter.allTags))
            return false;

        return true;
    }

    private static bool MatchesOwner(
        BattleSlotOwner candidateOwner,
        EffectTargetOwner owner,
        EffectContext context)
    {
        if (owner == EffectTargetOwner.Any)
            return true;

        return candidateOwner == ResolveOwner(owner, context);
    }

    private static bool MatchesFaceState(
        EffectTargetCandidate candidate,
        EffectFaceState faceState)
    {
        switch (faceState)
        {
            case EffectFaceState.FaceUpOnly:
                return !candidate.isFaceDown;
            case EffectFaceState.FaceDownOnly:
                return candidate.isFaceDown;
            default:
                return true;
        }
    }

    private static bool MatchesKind(BaseCardData card, EffectCardKind kind)
    {
        if (kind == EffectCardKind.Any)
            return true;

        if (card == null || string.IsNullOrWhiteSpace(card.kind))
            return false;

        return string.Equals(card.kind, kind.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesAnyTags(BaseCardData card, List<string> tags)
    {
        if (tags == null || tags.Count == 0)
            return true;

        foreach (string tag in tags)
        {
            if (CardHasHashtag(card, tag))
                return true;
        }

        return false;
    }

    private static bool MatchesAllTags(BaseCardData card, List<string> tags)
    {
        if (tags == null || tags.Count == 0)
            return true;

        foreach (string tag in tags)
        {
            if (!CardHasHashtag(card, tag))
                return false;
        }

        return true;
    }

    private static bool CardHasHashtag(BaseCardData card, string tag)
    {
        if (card == null ||
            card.hashtags == null ||
            string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        string normalizedTag = tag.Trim();

        foreach (string hashtag in card.hashtags)
        {
            if (string.Equals(
                hashtag != null ? hashtag.Trim() : "",
                normalizedTag,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static BattleSlotOwner ResolveOwner(
        EffectTargetOwner owner,
        EffectContext context)
    {
        BattleSlotOwner actingOwner = context != null
            ? context.actingOwner
            : BattleSlotOwner.My;

        switch (owner)
        {
            case EffectTargetOwner.Me:
                return BattleSlotOwner.My;
            case EffectTargetOwner.Enemy:
                return BattleSlotOwner.Enemy;
            case EffectTargetOwner.OpponentOfActingOwner:
                return GetOpponent(actingOwner);
            case EffectTargetOwner.ActingOwner:
            case EffectTargetOwner.Any:
            default:
                return actingOwner;
        }
    }

    private static BattleSlotOwner GetOpponent(BattleSlotOwner owner)
    {
        return owner == BattleSlotOwner.My
            ? BattleSlotOwner.Enemy
            : BattleSlotOwner.My;
    }
}
