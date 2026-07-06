using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
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

    private enum EffectAuditStatus
    {
        Implemented,
        NotImplemented,
        NoRef,
        DataWarning
    }

    private struct EffectAuditTotals
    {
        public int totalEffects;
        public int implementedRefs;
        public int notImplementedRefs;
        public int noRefEffects;
        public int dataWarnings;
    }

    private class EffectAuditEntry
    {
        public string cardId;
        public string cardName;
        public string cardKind;
        public string contentType;
        public string effectId;
        public string timing;
        public string description;
        public string refId;
        public string paramsSummary;
        public bool implemented;
        public EffectAuditStatus status;
        public readonly List<string> warnings = new List<string>();
    }

    private void Awake()
    {
        if (effectManager == null)
            effectManager = FindAnyObjectByType<EffectManager>();
    }

    public void PrintCardFunctionAudit()
    {
        PrintEffectAuditSummary();
    }

    public void PrintEffectAuditSummary()
    {
        if (!TryLoadDatabase(out CardDatabase database, out string loadError))
        {
            Debug.LogWarning(loadError);
            SetResultText(loadError);
            return;
        }

        List<EffectAuditEntry> entries = BuildEffectAuditEntries(database);
        string report = BuildEffectAuditSummaryReport(entries);

        Debug.Log(report);
        SetResultText(report);
    }

    public void PrintEffectAuditDetail()
    {
        if (!TryLoadDatabase(out CardDatabase database, out string loadError))
        {
            Debug.LogWarning(loadError);
            SetResultText(loadError);
            return;
        }

        List<EffectAuditEntry> entries = BuildEffectAuditEntries(database);
        string report = BuildEffectAuditDetailReport(entries);

        Debug.Log(report);
        SetResultText(report);
    }

    public void PrintOnlineEffectProgress()
    {
        if (!TryLoadDatabase(out CardDatabase database, out string loadError))
        {
            Debug.LogWarning(loadError);
            SetResultText(loadError);
            return;
        }

        string report = OnlineEffectProgressReporter.BuildReport(database);
        Debug.Log(report);
        SetResultText(report);
    }

    public void PrintLegacyCardFunctionAudit()
    {
        if (effectManager == null)
            effectManager = FindAnyObjectByType<EffectManager>();

        if (!TryLoadDatabase(out CardDatabase database, out string loadError))
        {
            Debug.LogWarning(loadError);
            SetResultText(loadError);
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

    private bool TryLoadDatabase(out CardDatabase database, out string error)
    {
        if (effectManager == null)
            effectManager = FindAnyObjectByType<EffectManager>();

        database = null;
        error = "";

        TextAsset jsonAsset = Resources.Load<TextAsset>(cardsResourcePath);

        if (jsonAsset == null)
        {
            error = $"CardFunctionAudit: Resources/{cardsResourcePath}.json을 찾지 못했습니다.";
            return false;
        }

        database = JsonUtility.FromJson<CardDatabase>(jsonAsset.text);

        if (database == null)
        {
            error = "CardFunctionAudit: cards.json 파싱에 실패했습니다.";
            return false;
        }

        return true;
    }

    private List<EffectAuditEntry> BuildEffectAuditEntries(CardDatabase database)
    {
        List<EffectAuditEntry> entries = new List<EffectAuditEntry>();

        if (database == null)
            return entries;

        AddEffectAuditEntries(entries, database.idols, "Idol", null, card => card.active);
        AddEffectAuditEntries(entries, database.idols, "Idol", null, card => card.passive);
        AddEffectAuditEntries(entries, database.broadcasts, "Broadcast", null, card => card.effects);
        AddEffectAuditEntries(entries, database.characters, "Character", null, card => card.effects);
        AddEffectAuditEntries(entries, database.contents, "Content", card => card.contentType, card => card.effects);

        return entries;
    }

    private void AddEffectAuditEntries<TCard>(
        List<EffectAuditEntry> entries,
        List<TCard> cards,
        string fallbackKind,
        System.Func<TCard, string> getContentType,
        System.Func<TCard, EffectData[]> getEffects)
        where TCard : BaseCardData
    {
        if (entries == null || cards == null || getEffects == null)
            return;

        foreach (TCard card in cards)
        {
            if (card == null)
                continue;

            EffectData[] effects = getEffects(card);

            if (effects == null)
                continue;

            foreach (EffectData effect in effects)
                entries.Add(BuildEffectAuditEntry(card, fallbackKind, getContentType, effect));
        }
    }

    private EffectAuditEntry BuildEffectAuditEntry<TCard>(
        TCard card,
        string fallbackKind,
        System.Func<TCard, string> getContentType,
        EffectData effect)
        where TCard : BaseCardData
    {
        EffectAuditEntry entry = new EffectAuditEntry
        {
            cardId = SafeValue(card != null ? card.id : null),
            cardName = SafeValue(card != null ? card.name : null),
            cardKind = SafeValue(card != null && !string.IsNullOrWhiteSpace(card.kind) ? card.kind : fallbackKind),
            contentType = SafeValue(card != null && getContentType != null ? getContentType(card) : null),
            effectId = SafeValue(effect != null ? effect.id : null),
            timing = SafeValue(effect != null ? effect.timing : null),
            description = SafeValue(effect != null ? effect.description : null),
            refId = SafeValue(GetEffectRef(effect)).Trim(),
            paramsSummary = BuildParamsSummary(effect != null ? effect.@params : null)
        };

        entry.implemented = string.IsNullOrWhiteSpace(entry.refId) || IsImplemented(entry.refId);
        AddDataWarnings(entry, effect);
        entry.status = ResolveEffectStatus(entry);

        return entry;
    }

    private EffectAuditStatus ResolveEffectStatus(EffectAuditEntry entry)
    {
        if (entry != null && entry.warnings.Count > 0)
            return EffectAuditStatus.DataWarning;

        if (entry == null || string.IsNullOrWhiteSpace(entry.refId))
            return EffectAuditStatus.NoRef;

        return entry.implemented ? EffectAuditStatus.Implemented : EffectAuditStatus.NotImplemented;
    }

    private void AddDataWarnings(EffectAuditEntry entry, EffectData effect)
    {
        if (entry == null)
            return;

        string description = entry.description ?? "";
        string timing = entry.timing ?? "";
        string refId = entry.refId ?? "";
        EffectParams effectParams = effect != null ? effect.@params : null;

        if (entry.cardKind == "Content" &&
            entry.contentType == "Collab" &&
            timing == "Content")
        {
            entry.warnings.Add("contentType is Collab but timing is Content.");
        }

        if (description.Contains("합방 후") && timing != "PostCollab")
            entry.warnings.Add("description mentions 합방 후 but timing is not PostCollab.");

        if (description.Contains("합방 전") &&
            timing != "PreCollab" &&
            !IsBroadcastAlwaysEffect(entry))
        {
            entry.warnings.Add("description mentions 합방 전 but timing is not PreCollab.");
        }

        if (effectParams != null && !string.IsNullOrWhiteSpace(effectParams.tag))
            AddTagMismatchWarnings(entry, description, effectParams.tag.Trim());

        if (refId.Contains("FromHand") &&
            MentionsRestZone(description) &&
            !MentionsHandToRestFallback(description))
        {
            entry.warnings.Add("ref name contains FromHand but description mentions 휴식 존.");
        }

        if (refId.Contains("FromRest") && MentionsHand(description))
            entry.warnings.Add("ref name contains FromRest but description mentions 패.");
    }

    private void AddTagMismatchWarnings(EffectAuditEntry entry, string description, string paramTag)
    {
        if (entry == null || string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(paramTag))
            return;

        HashSet<string> mentionedTags = new HashSet<string>();
        MatchCollection matches = Regex.Matches(description, @"#[^\s,\.。，]+");

        foreach (Match match in matches)
        {
            string tag = match.Value.Trim();

            if (!string.IsNullOrWhiteSpace(tag))
                mentionedTags.Add(tag);
        }

        if (mentionedTags.Count == 0 || MentionsParamTag(mentionedTags, paramTag))
            return;

        entry.warnings.Add($"description mentions {string.Join("/", mentionedTags)} but params.tag is {paramTag}.");
    }

    private bool MentionsRestZone(string description)
    {
        return !string.IsNullOrWhiteSpace(description) &&
            (description.Contains("휴식 존") || description.Contains("휴식존"));
    }

    private bool IsBroadcastAlwaysEffect(EffectAuditEntry entry)
    {
        return entry != null &&
            entry.cardKind == "Broadcast" &&
            entry.timing == "Always";
    }

    private bool MentionsHandToRestFallback(string description)
    {
        return MentionsHand(description) &&
            MentionsRestZone(description) &&
            !string.IsNullOrWhiteSpace(description) &&
            (description.Contains("출연시킬 수 없") ||
             description.Contains("소환할 수 없") ||
             description.Contains("사용할 수 없"));
    }

    private bool MentionsParamTag(HashSet<string> mentionedTags, string paramTag)
    {
        if (mentionedTags == null || string.IsNullOrWhiteSpace(paramTag))
            return false;

        foreach (string mentionedTag in mentionedTags)
        {
            if (string.Equals(mentionedTag, paramTag, System.StringComparison.OrdinalIgnoreCase))
                return true;

            if (StartsWithParamTagParticle(mentionedTag, paramTag))
                return true;
        }

        return false;
    }

    private bool StartsWithParamTagParticle(string mentionedTag, string paramTag)
    {
        if (string.IsNullOrWhiteSpace(mentionedTag) ||
            string.IsNullOrWhiteSpace(paramTag) ||
            !mentionedTag.StartsWith(paramTag, System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string suffix = mentionedTag.Substring(paramTag.Length);

        switch (suffix)
        {
            case "은":
            case "는":
            case "이":
            case "가":
            case "을":
            case "를":
            case "과":
            case "와":
            case "의":
            case "도":
            case "만":
            case "로":
            case "으로":
            case "에게":
                return true;
            default:
                return false;
        }
    }

    private bool MentionsHand(string description)
    {
        return !string.IsNullOrWhiteSpace(description) &&
            (description.Contains("패") || description.Contains("손패"));
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

    private string BuildEffectAuditSummaryReport(List<EffectAuditEntry> entries)
    {
        EffectAuditTotals totals = CalculateEffectAuditTotals(entries);
        SortedSet<string> missingRefs = CollectRefsByImplementedState(entries, false);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== 카드 효과 감사 요약 ===");
        sb.AppendLine($"전체 effect: {totals.totalEffects}");
        sb.AppendLine($"구현 완료 ref: {totals.implementedRefs}");
        sb.AppendLine($"미구현 ref: {totals.notImplementedRefs}");
        sb.AppendLine($"ref 없는 effect: {totals.noRefEffects}");
        sb.AppendLine($"데이터 경고: {totals.dataWarnings}");
        sb.AppendLine();
        sb.AppendLine("=== 상태별 effect ===");
        AppendStatusCount(sb, entries, EffectAuditStatus.Implemented);
        AppendStatusCount(sb, entries, EffectAuditStatus.NotImplemented);
        AppendStatusCount(sb, entries, EffectAuditStatus.NoRef);
        AppendStatusCount(sb, entries, EffectAuditStatus.DataWarning);
        sb.AppendLine();
        sb.AppendLine("=== 미구현 ref 목록 ===");

        if (missingRefs.Count == 0)
        {
            sb.AppendLine("- 없음");
        }
        else
        {
            foreach (string refId in missingRefs)
                sb.AppendLine($"- {refId}");
        }

        sb.AppendLine();
        sb.AppendLine("상세 목록은 PrintEffectAuditDetail()을 호출하세요.");
        return sb.ToString();
    }

    private string BuildEffectAuditDetailReport(List<EffectAuditEntry> entries)
    {
        EffectAuditTotals totals = CalculateEffectAuditTotals(entries);
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("=== 카드 효과 감사 상세 ===");
        sb.AppendLine($"전체 effect: {totals.totalEffects}");
        sb.AppendLine($"구현 완료 ref: {totals.implementedRefs}");
        sb.AppendLine($"미구현 ref: {totals.notImplementedRefs}");
        sb.AppendLine($"ref 없는 effect: {totals.noRefEffects}");
        sb.AppendLine($"데이터 경고: {totals.dataWarnings}");
        sb.AppendLine();

        if (entries == null || entries.Count == 0)
        {
            sb.AppendLine("- effect 없음");
            return sb.ToString();
        }

        foreach (EffectAuditEntry entry in entries)
        {
            sb.AppendLine($"[{entry.status}] {entry.cardId} / {entry.cardName}");
            sb.AppendLine($"  cardKind: {entry.cardKind}");
            sb.AppendLine($"  effectId: {entry.effectId}");
            sb.AppendLine($"  timing: {entry.timing}");
            sb.AppendLine($"  ref: {DisplayRef(entry.refId)}");
            sb.AppendLine($"  params: {entry.paramsSummary}");

            if (entry.warnings.Count > 0)
                sb.AppendLine($"  warnings: {string.Join(" | ", entry.warnings)}");
            else
                sb.AppendLine("  warnings: -");
        }

        return sb.ToString();
    }

    private EffectAuditTotals CalculateEffectAuditTotals(List<EffectAuditEntry> entries)
    {
        EffectAuditTotals totals = new EffectAuditTotals();

        if (entries == null)
            return totals;

        totals.totalEffects = entries.Count;

        foreach (EffectAuditEntry entry in entries)
        {
            if (entry == null)
                continue;

            if (entry.warnings.Count > 0)
                totals.dataWarnings++;

            if (string.IsNullOrWhiteSpace(entry.refId))
            {
                totals.noRefEffects++;
            }
            else if (entry.implemented)
            {
                totals.implementedRefs++;
            }
            else
            {
                totals.notImplementedRefs++;
            }
        }

        return totals;
    }

    private SortedSet<string> CollectRefsByImplementedState(List<EffectAuditEntry> entries, bool implemented)
    {
        SortedSet<string> refs = new SortedSet<string>();

        if (entries == null)
            return refs;

        foreach (EffectAuditEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.refId))
                continue;

            if (entry.implemented == implemented)
                refs.Add(entry.refId);
        }

        return refs;
    }

    private void AppendStatusCount(StringBuilder sb, List<EffectAuditEntry> entries, EffectAuditStatus status)
    {
        int count = 0;

        if (entries != null)
        {
            foreach (EffectAuditEntry entry in entries)
            {
                if (entry != null && entry.status == status)
                    count++;
            }
        }

        sb.AppendLine($"{status}: {count}");
    }

    private string BuildParamsSummary(EffectParams effectParams)
    {
        if (effectParams == null)
            return "{}";

        List<string> parts = new List<string>();
        AddIntParam(parts, "amount", effectParams.amount);
        AddIntParam(parts, "draw", effectParams.draw);
        AddIntParam(parts, "discard", effectParams.discard);
        AddIntParam(parts, "hp", effectParams.hp);
        AddIntParam(parts, "tension", effectParams.tension);
        AddIntParam(parts, "tensionDelta", effectParams.tensionDelta);
        AddIntParam(parts, "hpMaxDelta", effectParams.hpMaxDelta);
        AddIntParam(parts, "max", effectParams.max);
        AddIntParam(parts, "maxCount", effectParams.maxCount);
        AddIntParam(parts, "range", effectParams.range);
        AddIntParam(parts, "reveal", effectParams.reveal);
        AddIntParam(parts, "extraCostPer", effectParams.extraCostPer);
        AddIntParam(parts, "viewersModifier", effectParams.viewersModifier);
        AddIntParam(parts, "healBonus", effectParams.healBonus);
        AddIntParam(parts, "donateViewers", effectParams.donateViewers);
        AddIntParam(parts, "donateAmount", effectParams.donateAmount);
        AddIntParam(parts, "viewersCost", effectParams.viewersCost);
        AddStringParam(parts, "tag", effectParams.tag);
        AddStringParam(parts, "requireTag", effectParams.requireTag);
        AddStringParam(parts, "tabiTag", effectParams.tabiTag);
        AddStringParam(parts, "bunnyTag", effectParams.bunnyTag);
        AddStringParam(parts, "kind", effectParams.kind);

        if (effectParams.allTags != null && effectParams.allTags.Length > 0)
            parts.Add($"allTags=[{string.Join(",", effectParams.allTags)}]");

        if (effectParams.oncePerTurn)
            parts.Add("oncePerTurn=true");

        if (effectParams.faceUp)
            parts.Add("faceUp=true");

        AddBoolParam(parts, "shuffleDeckAfterMove", effectParams.shuffleDeckAfterMove);
        AddBoolParam(parts, "forbidFaceDownSummon", effectParams.forbidFaceDownSummon);
        AddBoolParam(parts, "disablePreCollabEffects", effectParams.disablePreCollabEffects);
        AddBoolParam(parts, "disableIdolActiveForOccupantOwner", effectParams.disableIdolActiveForOccupantOwner);
        AddBoolParam(parts, "lockMoveOnEnterUntilNextTurn", effectParams.lockMoveOnEnterUntilNextTurn);

        return parts.Count > 0 ? string.Join(", ", parts) : "{}";
    }

    private void AddIntParam(List<string> parts, string name, int value)
    {
        if (parts != null && value != 0)
            parts.Add($"{name}={value}");
    }

    private void AddStringParam(List<string> parts, string name, string value)
    {
        if (parts != null && !string.IsNullOrWhiteSpace(value))
            parts.Add($"{name}={value}");
    }

    private void AddBoolParam(List<string> parts, string name, bool value)
    {
        if (parts != null && value)
            parts.Add($"{name}=true");
    }

    private string DisplayRef(string refId)
    {
        return string.IsNullOrWhiteSpace(refId) ? "(NoRef)" : refId;
    }

    private string SafeValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : value;
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
