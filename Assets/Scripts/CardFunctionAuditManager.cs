using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class CardFunctionAuditManager : MonoBehaviour
{
    [SerializeField] private EffectManager effectManager;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private string cardsResourcePath = "cards";

    private struct CardAuditSummary
    {
        public int total;
        public int functioning;
    }

    private void Awake()
    {
        if (effectManager == null)
            effectManager = FindAnyObjectByType<EffectManager>();
    }

    public void PrintCardFunctionAudit()
    {
        if (effectManager == null)
            effectManager = FindAnyObjectByType<EffectManager>();

        TextAsset jsonAsset = Resources.Load<TextAsset>(cardsResourcePath);

        if (jsonAsset == null)
        {
            string message = $"CardFunctionAudit: Resources/{cardsResourcePath}.json을 찾지 못했습니다.";
            Debug.LogWarning(message);
            SetResultText(message);
            return;
        }

        CardDatabase database = JsonUtility.FromJson<CardDatabase>(jsonAsset.text);

        if (database == null)
        {
            string message = "CardFunctionAudit: cards.json 파싱에 실패했습니다.";
            Debug.LogWarning(message);
            SetResultText(message);
            return;
        }

        CardAuditSummary idolSummary = AuditCards(database.idols, CollectIdolRefs, out List<string> idolIncomplete, out HashSet<string> idolMissingRefs);
        CardAuditSummary broadcastSummary = AuditCards(database.broadcasts, CollectBroadcastRefs, out List<string> broadcastIncomplete, out HashSet<string> broadcastMissingRefs);
        CardAuditSummary characterSummary = AuditCards(database.characters, CollectCharacterRefs, out List<string> characterIncomplete, out HashSet<string> characterMissingRefs);
        CardAuditSummary contentSummary = AuditCards(database.contents, CollectContentRefs, out List<string> contentIncomplete, out HashSet<string> contentMissingRefs);

        int totalCards =
            idolSummary.total +
            broadcastSummary.total +
            characterSummary.total +
            contentSummary.total;
        int functioningCards =
            idolSummary.functioning +
            broadcastSummary.functioning +
            characterSummary.functioning +
            contentSummary.functioning;

        List<string> incompleteCards = new List<string>();
        incompleteCards.AddRange(idolIncomplete);
        incompleteCards.AddRange(broadcastIncomplete);
        incompleteCards.AddRange(characterIncomplete);
        incompleteCards.AddRange(contentIncomplete);

        SortedSet<string> missingRefs = new SortedSet<string>();
        AddRefs(missingRefs, idolMissingRefs);
        AddRefs(missingRefs, broadcastMissingRefs);
        AddRefs(missingRefs, characterMissingRefs);
        AddRefs(missingRefs, contentMissingRefs);

        string report = BuildReport(
            functioningCards,
            totalCards,
            idolSummary,
            broadcastSummary,
            characterSummary,
            contentSummary,
            incompleteCards,
            missingRefs
        );

        Debug.Log(report);
        SetResultText(report);
    }

    private CardAuditSummary AuditCards<TCard>(
        List<TCard> cards,
        System.Func<TCard, List<string>> collectRefs,
        out List<string> incompleteCards,
        out HashSet<string> missingRefs)
        where TCard : BaseCardData
    {
        CardAuditSummary summary = new CardAuditSummary();
        incompleteCards = new List<string>();
        missingRefs = new HashSet<string>();

        if (cards == null)
            return summary;

        summary.total = cards.Count;

        foreach (TCard card in cards)
        {
            if (card == null)
                continue;

            List<string> refs = collectRefs(card);

            if (refs.Count == 0)
            {
                summary.functioning++;
                continue;
            }

            bool allImplemented = true;

            foreach (string refId in refs)
            {
                if (IsImplemented(refId))
                    continue;

                allImplemented = false;
                missingRefs.Add(refId);
            }

            if (allImplemented)
                summary.functioning++;
            else
                incompleteCards.Add($"{card.id} / {card.name}");
        }

        return summary;
    }

    private bool IsImplemented(string refId)
    {
        return effectManager != null &&
            effectManager.IsEffectRefImplementedFromExternal(refId);
    }

    private List<string> CollectIdolRefs(IdolCardData card)
    {
        List<string> refs = new List<string>();
        AddEffectRefs(refs, card != null ? card.active : null);
        AddEffectRefs(refs, card != null ? card.passive : null);
        return refs;
    }

    private List<string> CollectBroadcastRefs(BroadcastCardData card)
    {
        List<string> refs = new List<string>();
        AddEffectRefs(refs, card != null ? card.effects : null);
        return refs;
    }

    private List<string> CollectCharacterRefs(CharacterCardData card)
    {
        List<string> refs = new List<string>();
        AddEffectRefs(refs, card != null ? card.effects : null);
        return refs;
    }

    private List<string> CollectContentRefs(ContentCardData card)
    {
        List<string> refs = new List<string>();
        AddEffectRefs(refs, card != null ? card.effects : null);
        return refs;
    }

    private void AddEffectRefs(List<string> refs, EffectData[] effects)
    {
        if (refs == null || effects == null)
            return;

        foreach (EffectData effect in effects)
        {
            string refId = GetEffectRef(effect);

            if (!string.IsNullOrWhiteSpace(refId))
                refs.Add(refId.Trim());
        }
    }

    private string GetEffectRef(EffectData effect)
    {
        if (effect == null)
            return "";

        if (!string.IsNullOrWhiteSpace(effect.refName))
            return effect.refName;

        return effect.@ref;
    }

    private string BuildReport(
        int functioningCards,
        int totalCards,
        CardAuditSummary idolSummary,
        CardAuditSummary broadcastSummary,
        CardAuditSummary characterSummary,
        CardAuditSummary contentSummary,
        List<string> incompleteCards,
        SortedSet<string> missingRefs)
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("=== 기능 카드 현황 ===");
        sb.AppendLine($"전체: {functioningCards} / {totalCards}");
        sb.AppendLine($"아이돌: {idolSummary.functioning} / {idolSummary.total}");
        sb.AppendLine($"방송: {broadcastSummary.functioning} / {broadcastSummary.total}");
        sb.AppendLine($"캐릭터: {characterSummary.functioning} / {characterSummary.total}");
        sb.AppendLine($"콘텐츠: {contentSummary.functioning} / {contentSummary.total}");
        sb.AppendLine();
        sb.AppendLine("=== 부분 구현 / 미구현 카드 ===");

        if (incompleteCards.Count == 0)
        {
            sb.AppendLine("- 없음");
        }
        else
        {
            foreach (string card in incompleteCards)
                sb.AppendLine($"- {card}");
        }

        sb.AppendLine();
        sb.AppendLine("=== 미구현 ref ===");

        if (missingRefs.Count == 0)
        {
            sb.AppendLine("- 없음");
        }
        else
        {
            foreach (string refId in missingRefs)
                sb.AppendLine($"- {refId}");
        }

        return sb.ToString();
    }

    private void AddRefs(SortedSet<string> target, HashSet<string> source)
    {
        if (target == null || source == null)
            return;

        foreach (string refId in source)
            target.Add(refId);
    }

    private void SetResultText(string message)
    {
        if (resultText != null)
            resultText.text = message;
    }
}
