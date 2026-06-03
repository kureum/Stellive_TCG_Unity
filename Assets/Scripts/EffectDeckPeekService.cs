using System;
using System.Collections.Generic;

public enum PeekRestPolicy
{
    KeepOrderToBottom,
    ReverseOrderToBottom,
    ShuffleIntoDeck
}

[Serializable]
public class PeekTopSelectRequest
{
    public BattleSlotOwner owner;
    public int revealCount;
    public int maxTake = 1;
    public int minTake;
    public CardFilter filter = CardFilter.Any();
    public PeekRestPolicy restPolicy = PeekRestPolicy.KeepOrderToBottom;
    public ZoneMoveReason reason = ZoneMoveReason.Effect;
    public string sourceEffectRef;
    public BaseCardData sourceCard;
    public bool requireSelection;
    public int selectionCostPerCard;
}

public class PeekTopSelectResult
{
    public bool success;
    public string message;
    public BattleSlotOwner owner;
    public readonly List<BaseCardData> revealedCards = new List<BaseCardData>();
    public readonly List<BaseCardData> selectableCards = new List<BaseCardData>();
    public readonly List<BaseCardData> selectedCards = new List<BaseCardData>();
    public readonly List<BaseCardData> bottomedCards = new List<BaseCardData>();
    public readonly List<ZoneMoveResult> zoneMoveResults = new List<ZoneMoveResult>();
}

[Serializable]
public class SearchDeckSelectRequest
{
    public BattleSlotOwner owner;
    public int maxTake = 1;
    public int minTake;
    public CardFilter filter = CardFilter.Any();
    public ZoneMoveReason reason = ZoneMoveReason.Effect;
    public string sourceEffectRef;
    public BaseCardData sourceCard;
    public bool requireSelection;
    public bool shuffleDeckAfterSearch;
    public int selectionCostPerCard;
    public string questionMessage;
}

public class SearchDeckSelectResult
{
    public bool success;
    public string message;
    public BattleSlotOwner owner;
    public readonly List<BaseCardData> searchedCards = new List<BaseCardData>();
    public readonly List<BaseCardData> selectableCards = new List<BaseCardData>();
    public readonly List<BaseCardData> selectedCards = new List<BaseCardData>();
    public readonly List<ZoneMoveResult> zoneMoveResults = new List<ZoneMoveResult>();
}

public static class EffectDeckPeekService
{
    public static void PeekTopSelectToHand(
        PeekTopSelectRequest request,
        EffectContext context,
        Action<PeekTopSelectResult> onComplete)
    {
        PeekTopSelectResult result = CreateResult(request);

        if (request == null)
        {
            Complete(result, false, "덱 공개 요청 정보가 없습니다.", onComplete);
            return;
        }

        BattleManager battleManager = context != null ? context.battleManager : null;

        if (battleManager == null)
        {
            Complete(result, false, "BattleManager가 연결되어 있지 않습니다.", onComplete);
            return;
        }

        int revealCount = Math.Max(0, request.revealCount);
        IReadOnlyList<BaseCardData> revealed = battleManager.PeekTopMainDeckCardsFromExternal(
            request.owner,
            revealCount);

        AddCards(result.revealedCards, revealed);

        if (result.revealedCards.Count == 0)
        {
            Complete(result, false, "공개할 덱 카드가 없습니다.", onComplete);
            return;
        }

        foreach (BaseCardData card in result.revealedCards)
        {
            if (EffectTargetingService.CardMatchesFilter(card, request.filter))
                result.selectableCards.Add(card);
        }

        if (result.selectableCards.Count == 0)
        {
            ShowNoSelectablePeekConfirmation(request, context, battleManager, result, onComplete);
            return;
        }

        CardQuestionPanel panel = battleManager.BattleCardQuestionPanel;

        if (panel == null)
        {
            MoveRemainingRevealedCards(request, battleManager, result, result.revealedCards);
            battleManager.RefreshAllUIFromExternal();
            Complete(result, false, "CardQuestionPanel이 없어 공개 카드 선택을 처리하지 않았습니다.", onComplete);
            return;
        }

        if (panel.IsOpen())
        {
            MoveRemainingRevealedCards(request, battleManager, result, result.revealedCards);
            battleManager.RefreshAllUIFromExternal();
            Complete(result, false, "이미 카드 선택창이 열려 있어 공개 카드 선택을 처리하지 않았습니다.", onComplete);
            return;
        }

        bool canCancel = !request.requireSelection && request.minTake <= 0;
        string message = BuildQuestionMessage(request, result);

        bool opened = panel.TryShow(
            message,
            result.selectableCards,
            canCancel,
            selectedCard => ResolveSelection(request, context, battleManager, result, selectedCard, onComplete),
            () => ResolveSelection(request, context, battleManager, result, null, onComplete)
        );

        if (!opened)
        {
            MoveRemainingRevealedCards(request, battleManager, result, result.revealedCards);
            battleManager.RefreshAllUIFromExternal();
            Complete(result, false, "카드 선택창을 열 수 없어 공개 카드 선택을 처리하지 않았습니다.", onComplete);
        }
    }

    public static void SearchDeckSelectToHand(
        SearchDeckSelectRequest request,
        EffectContext context,
        Action<SearchDeckSelectResult> onComplete)
    {
        SearchDeckSelectResult result = CreateSearchResult(request);

        if (request == null)
        {
            CompleteSearch(result, false, "덱 서치 요청 정보가 없습니다.", onComplete);
            return;
        }

        BattleManager battleManager = context != null ? context.battleManager : null;

        if (battleManager == null)
        {
            CompleteSearch(result, false, "BattleManager가 연결되어 있지 않습니다.", onComplete);
            return;
        }

        IReadOnlyList<BaseCardData> deckCards = battleManager.GetMainDeckCardsFromExternal(request.owner);
        AddCards(result.searchedCards, deckCards);

        if (result.searchedCards.Count == 0)
        {
            CompleteSearch(result, false, "서치할 덱 카드가 없습니다.", onComplete);
            return;
        }

        foreach (BaseCardData card in result.searchedCards)
        {
            if (EffectTargetingService.CardMatchesFilter(card, request.filter))
                result.selectableCards.Add(card);
        }

        if (result.selectableCards.Count == 0)
        {
            CompleteSearch(result, false, "선택 가능한 카드가 없습니다.", onComplete);
            return;
        }

        CardQuestionPanel panel = battleManager.BattleCardQuestionPanel;

        if (panel == null)
        {
            CompleteSearch(result, false, "CardQuestionPanel이 없어 덱 서치를 처리하지 않았습니다.", onComplete);
            return;
        }

        if (panel.IsOpen())
        {
            CompleteSearch(result, false, "이미 카드 선택창이 열려 있어 덱 서치를 처리하지 않았습니다.", onComplete);
            return;
        }

        bool canCancel = !request.requireSelection && request.minTake <= 0;

        bool opened = panel.TryShow(
            BuildSearchQuestionMessage(request, result),
            result.selectableCards,
            canCancel,
            selectedCard => ResolveSearchSelection(request, context, battleManager, result, selectedCard, onComplete),
            () => ResolveSearchSelection(request, context, battleManager, result, null, onComplete)
        );

        if (!opened)
            CompleteSearch(result, false, "카드 선택창을 열 수 없어 덱 서치를 처리하지 않았습니다.", onComplete);
    }

    private static void ResolveSelection(
        PeekTopSelectRequest request,
        EffectContext context,
        BattleManager battleManager,
        PeekTopSelectResult result,
        BaseCardData selectedCard,
        Action<PeekTopSelectResult> onComplete)
    {
        if (selectedCard == null)
        {
            MoveRemainingRevealedCards(request, battleManager, result, result.revealedCards);
            battleManager.RefreshAllUIFromExternal();
            Complete(result, request.minTake <= 0, "공개 카드 선택을 건너뛰었습니다.", onComplete);
            return;
        }

        if (!TryPaySelectionCost(request, battleManager, 1, out string costFailReason))
        {
            MoveRemainingRevealedCards(request, battleManager, result, result.revealedCards);
            battleManager.RefreshAllUIFromExternal();
            Complete(result, false, costFailReason, onComplete);
            return;
        }

        ZoneMoveResult moveResult = EffectZoneMoveService.MoveCardBetweenZones(
            new ZoneMoveRequest
            {
                owner = request.owner,
                fromZone = EffectZone.Deck,
                toZone = EffectZone.Hand,
                card = selectedCard,
                reason = request.reason
            },
            context
        );

        result.zoneMoveResults.Add(moveResult);

        if (!moveResult.success)
        {
            MoveRemainingRevealedCards(request, battleManager, result, result.revealedCards);
            battleManager.RefreshAllUIFromExternal();
            Complete(result, false, moveResult.message, onComplete);
            return;
        }

        result.selectedCards.Add(selectedCard);

        List<BaseCardData> remainingCards = new List<BaseCardData>(result.revealedCards);
        remainingCards.Remove(selectedCard);
        MoveRemainingRevealedCards(request, battleManager, result, remainingCards);
        battleManager.RefreshAllUIFromExternal();

        string selectedMessage = $"{selectedCard.name} 카드를 패에 더했습니다.";
        if (request.selectionCostPerCard > 0)
            selectedMessage += $"\n추가 시청자 -{request.selectionCostPerCard}";

        Complete(result, true, selectedMessage, onComplete);
    }

    private static void ShowNoSelectablePeekConfirmation(
        PeekTopSelectRequest request,
        EffectContext context,
        BattleManager battleManager,
        PeekTopSelectResult result,
        Action<PeekTopSelectResult> onComplete)
    {
        CardQuestionPanel panel = battleManager.BattleCardQuestionPanel;

        if (panel == null || panel.IsOpen())
        {
            MoveRemainingRevealedCards(request, battleManager, result, result.revealedCards);
            battleManager.RefreshAllUIFromExternal();
            Complete(result, false, "공개된 카드 중 선택할 수 있는 카드가 없습니다.", onComplete);
            return;
        }

        battleManager.SetSystemMessageFromExternal("대상 카드가 없습니다.");

        List<BaseCardData> revealedCards = new List<BaseCardData>(result.revealedCards);
        string message = BuildNoSelectableConfirmationMessage(request, result);

        bool opened = panel.TryShowCardsForConfirmation(
            message,
            revealedCards,
            () =>
            {
                MoveRemainingRevealedCards(request, battleManager, result, result.revealedCards);
                battleManager.RefreshAllUIFromExternal();
                Complete(result, false, "공개된 카드 중 선택할 수 있는 카드가 없습니다.", onComplete);
            }
        );

        if (!opened)
        {
            MoveRemainingRevealedCards(request, battleManager, result, result.revealedCards);
            battleManager.RefreshAllUIFromExternal();
            Complete(result, false, "카드 확인창을 열 수 없어 공개 카드를 처리했습니다.", onComplete);
        }
    }

    private static void ResolveSearchSelection(
        SearchDeckSelectRequest request,
        EffectContext context,
        BattleManager battleManager,
        SearchDeckSelectResult result,
        BaseCardData selectedCard,
        Action<SearchDeckSelectResult> onComplete)
    {
        if (selectedCard == null)
        {
            CompleteSearch(result, request.minTake <= 0, "덱 서치 선택을 건너뛰었습니다.", onComplete);
            return;
        }

        if (!TryPaySelectionCost(request, battleManager, 1, out string costFailReason))
        {
            CompleteSearch(result, false, costFailReason, onComplete);
            return;
        }

        ZoneMoveResult moveResult = EffectZoneMoveService.MoveCardBetweenZones(
            new ZoneMoveRequest
            {
                owner = request.owner,
                fromZone = EffectZone.Deck,
                toZone = EffectZone.Hand,
                card = selectedCard,
                reason = request.reason
            },
            context
        );

        result.zoneMoveResults.Add(moveResult);

        if (!moveResult.success)
        {
            CompleteSearch(result, false, moveResult.message, onComplete);
            return;
        }

        result.selectedCards.Add(selectedCard);

        if (request.shuffleDeckAfterSearch)
            battleManager.ShuffleMainDeckFromExternal(request.owner);

        battleManager.RefreshAllUIFromExternal();

        string message = $"{selectedCard.name} 카드를 패에 더했습니다.";
        if (request.selectionCostPerCard > 0)
            message += $"\n추가 시청자 -{request.selectionCostPerCard}";

        CompleteSearch(result, true, message, onComplete);
    }

    private static void MoveRemainingRevealedCards(
        PeekTopSelectRequest request,
        BattleManager battleManager,
        PeekTopSelectResult result,
        IReadOnlyList<BaseCardData> remainingCards)
    {
        if (request == null ||
            battleManager == null ||
            remainingCards == null ||
            remainingCards.Count == 0)
        {
            return;
        }

        AddCards(result.bottomedCards, remainingCards);

        switch (request.restPolicy)
        {
            case PeekRestPolicy.ShuffleIntoDeck:
                battleManager.ShuffleMainDeckFromExternal(request.owner);
                break;
            case PeekRestPolicy.ReverseOrderToBottom:
                battleManager.MoveMainDeckCardsToBottomFromExternal(request.owner, remainingCards, true);
                break;
            case PeekRestPolicy.KeepOrderToBottom:
            default:
                battleManager.MoveMainDeckCardsToBottomFromExternal(request.owner, remainingCards, false);
                break;
        }
    }

    private static PeekTopSelectResult CreateResult(PeekTopSelectRequest request)
    {
        return new PeekTopSelectResult
        {
            owner = request != null ? request.owner : BattleSlotOwner.My
        };
    }

    private static SearchDeckSelectResult CreateSearchResult(SearchDeckSelectRequest request)
    {
        return new SearchDeckSelectResult
        {
            owner = request != null ? request.owner : BattleSlotOwner.My
        };
    }

    private static void Complete(
        PeekTopSelectResult result,
        bool success,
        string message,
        Action<PeekTopSelectResult> onComplete)
    {
        if (result != null)
        {
            result.success = success;
            result.message = message;
            UnityEngine.Debug.Log(
                $"[PeekTopSelectToHand] owner={result.owner}, revealed={result.revealedCards.Count}, " +
                $"selectable={result.selectableCards.Count}, selected={result.selectedCards.Count}, " +
                $"bottomed={result.bottomedCards.Count}, moves={result.zoneMoveResults.Count}, " +
                $"success={success}, message={message}"
            );
        }

        onComplete?.Invoke(result);
    }

    private static void CompleteSearch(
        SearchDeckSelectResult result,
        bool success,
        string message,
        Action<SearchDeckSelectResult> onComplete)
    {
        if (result != null)
        {
            result.success = success;
            result.message = message;
            UnityEngine.Debug.Log(
                $"[SearchDeckSelectToHand] owner={result.owner}, searched={result.searchedCards.Count}, " +
                $"selectable={result.selectableCards.Count}, selected={result.selectedCards.Count}, " +
                $"success={success}, message={message}"
            );
        }

        onComplete?.Invoke(result);
    }

    private static void AddCards(
        List<BaseCardData> target,
        IReadOnlyList<BaseCardData> source)
    {
        if (target == null || source == null)
            return;

        foreach (BaseCardData card in source)
        {
            if (card != null)
                target.Add(card);
        }
    }

    private static bool TryPaySelectionCost(
        PeekTopSelectRequest request,
        BattleManager battleManager,
        int selectedCount,
        out string failReason)
    {
        failReason = "";

        int cost = request != null
            ? Math.Max(0, request.selectionCostPerCard) * Math.Max(0, selectedCount)
            : 0;

        if (cost <= 0)
            return true;

        if (battleManager != null && battleManager.TryPayViewerCostFromExternal(request.owner, cost))
            return true;

        failReason = "추가 시청자가 부족하여 선택한 카드를 패에 더할 수 없습니다.";
        return false;
    }

    private static bool TryPaySelectionCost(
        SearchDeckSelectRequest request,
        BattleManager battleManager,
        int selectedCount,
        out string failReason)
    {
        failReason = "";

        int cost = request != null
            ? Math.Max(0, request.selectionCostPerCard) * Math.Max(0, selectedCount)
            : 0;

        if (cost <= 0)
            return true;

        if (battleManager != null && battleManager.TryPayViewerCostFromExternal(request.owner, cost))
            return true;

        failReason = "추가 시청자가 부족하여 선택한 카드를 패에 더할 수 없습니다.";
        return false;
    }

    private static string BuildQuestionMessage(
        PeekTopSelectRequest request,
        PeekTopSelectResult result)
    {
        string sourceName = request.sourceCard != null
            ? request.sourceCard.name
            : "카드 효과";

        return
            $"{sourceName}: 공개한 카드 중 패에 더할 카드를 선택하세요.\n" +
            $"공개 {result.revealedCards.Count}장 / 선택 가능 {result.selectableCards.Count}장";
    }

    private static string BuildNoSelectableConfirmationMessage(
        PeekTopSelectRequest request,
        PeekTopSelectResult result)
    {
        string sourceName = request.sourceCard != null
            ? request.sourceCard.name
            : "카드 효과";

        return
            $"{sourceName}: 공개된 카드 중 선택할 수 있는 카드가 없습니다.\n" +
            $"공개 {result.revealedCards.Count}장 / 선택 가능 0장";
    }

    private static string BuildSearchQuestionMessage(
        SearchDeckSelectRequest request,
        SearchDeckSelectResult result)
    {
        if (request != null && !string.IsNullOrWhiteSpace(request.questionMessage))
            return request.questionMessage;

        string sourceName = request.sourceCard != null
            ? request.sourceCard.name
            : "카드 효과";

        return
            $"{sourceName}: 덱에서 패에 더할 카드를 선택하세요.\n" +
            $"서치 {result.searchedCards.Count}장 / 선택 가능 {result.selectableCards.Count}장";
    }
}
