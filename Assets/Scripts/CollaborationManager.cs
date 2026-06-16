using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum CollaborationStartReason
{
    Normal,
    EffectMove,
    ForceBattleTargetAnywhere
}

public class CollaborationContext
{
    public BattleFieldSlot attackerOriginalSlot;
    public BattleFieldSlot attackerSlot;
    public BattleFieldSlot defenderSlot;
    public BattleFieldSlot battleLocationSlot;
    public CollaborationStartReason startReason = CollaborationStartReason.Normal;
    public BattleSlotOwner attackerOwner;
    public BattleSlotOwner defenderOwner;
    public bool attackerWasFaceDownAtCollabStart;
    public bool defenderWasFaceDownAtCollabStart;
    public bool attackerEffectsBlockedThisCollab;
    public bool defenderEffectsBlockedThisCollab;

    public BattleFieldSlot GetBattleLocationSlot()
    {
        return battleLocationSlot != null ? battleLocationSlot : defenderSlot;
    }
}

public class CollaborationManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleManager battleManager;

    [Header("Collaboration Panel")]
    [SerializeField] private GameObject collaborationPanel;

    [Header("Guest UI")]
    [SerializeField] private DeckCardItemUI guestCharacterItemUI;
    [SerializeField] private TMP_Text guestCharacterNameText;
    [SerializeField] private TMP_Text guestCharacterTensionText;
    [SerializeField] private TMP_Text guestCharacterHpText;

    [Header("Host UI")]
    [SerializeField] private DeckCardItemUI hostCharacterItemUI;
    [SerializeField] private TMP_Text hostCharacterNameText;
    [SerializeField] private TMP_Text hostCharacterTensionText;
    [SerializeField] private TMP_Text hostCharacterHpText;

    [Header("Result UI")]
    [SerializeField] private GameObject collabResultPanel;
    [SerializeField] private TMP_Text resultText;

    [Header("Animation Settings")]
    [SerializeField] private float attackMoveDistance = 45f;
    [SerializeField] private float attackMoveTime = 0.15f;
    [SerializeField] private float attackReturnTime = 0.12f;
    [SerializeField] private float hitShakeDistance = 12f;
    [SerializeField] private float hitShakeTime = 0.18f;
    [SerializeField] private float hpStepDelay = 0.12f;
    [SerializeField] private float defeatFadeTime = 0.35f;
    [SerializeField] private float animationTimeScale = 1.5f;
    [SerializeField] private float resultPanelVisibleTime = 2.5f;
    [SerializeField] private float winnerMoveDuration = 0.45f;

    private bool isResolvingCollaboration = false;
    private BaseCardData currentGuestCard;
    private BaseCardData currentHostCard;
    private BattleFieldSlot pendingGuestSlot;
    private BattleFieldSlot pendingHostSlot;
    private CollaborationContext pendingContext;
    private CollaborationContext currentContext;
    private Coroutine closePanelCoroutine;
    private GameObject winnerMoveGhostObject;
    private Image hiddenWinnerMoveSourceImage;
    private Color hiddenWinnerMoveSourceColor;

    public void Init(BattleManager manager)
    {
        battleManager = manager;

        HidePanel();
    }

    public bool IsResolvingCollaboration => isResolvingCollaboration;

    public bool IsCollaborationSequenceRunning => isResolvingCollaboration;

    public CollaborationContext CurrentCollaborationContext => currentContext;

    public bool HasPendingCollaborationChoice =>
        pendingGuestSlot != null ||
        pendingHostSlot != null;

    public bool IsCollaborationInteractionActive =>
        isResolvingCollaboration ||
        HasPendingCollaborationChoice;

    private void Awake()
    {
        if (battleManager == null)
            battleManager = GetComponentInParent<BattleManager>();

        HidePanel();
    }

    public bool StartCollaboration(BattleFieldSlot guestSlot, BattleFieldSlot hostSlot)
    {
        return StartCollaborationInternal(guestSlot, hostSlot, CollaborationStartReason.Normal);
    }

    public bool ExecuteStartCollabFromAction(BattleFieldSlot sourceSlot, BattleFieldSlot targetSlot)
    {
        return StartCollaboration(sourceSlot, targetSlot);
    }

    public bool StartEffectMoveCollaboration(BattleFieldSlot guestSlot, BattleFieldSlot hostSlot)
    {
        return StartCollaborationInternal(guestSlot, hostSlot, CollaborationStartReason.EffectMove);
    }

    public bool StartForcedIncomingCollaboration(
        BattleFieldSlot forcedAttackerSlot,
        BattleFieldSlot defenderSlot,
        CollaborationStartReason reason = CollaborationStartReason.ForceBattleTargetAnywhere)
    {
        return StartCollaborationInternal(forcedAttackerSlot, defenderSlot, reason);
    }

    private bool StartCollaborationInternal(
        BattleFieldSlot guestSlot,
        BattleFieldSlot hostSlot,
        CollaborationStartReason reason)
    {
        if (battleManager == null)
            return false;

        if (IsCollaborationInteractionActive)
        {
            battleManager.SetSystemMessageFromExternal("이미 합방 처리를 진행 중입니다.");
            return false;
        }

        if (!ValidateCollaboration(guestSlot, hostSlot))
            return false;

        currentGuestCard = guestSlot.characterCard;
        currentHostCard = hostSlot.characterCard;
        pendingGuestSlot = guestSlot;
        pendingHostSlot = hostSlot;
        pendingContext = CreateCollaborationContext(pendingGuestSlot, pendingHostSlot, reason);

        LogCollaborationStart(pendingContext);

        if (!HasHiddenParticipant(guestSlot, hostSlot))
            ShowPanelBeforeResult(guestSlot, hostSlot);

        battleManager.RequestPreCollabEffectsFromExternal(
            pendingGuestSlot,
            pendingHostSlot,
            ExecutePendingCollaboration
        );

        return true;
    }

    private void OpenResolveResultQuestion()
    {
        QuestionPanel questionPanel = battleManager.BattleQuestionPanel;

        if (questionPanel == null)
        {
            battleManager.SetSystemMessageFromExternal("QuestionPanel이 연결되어 있지 않습니다.");
            ExecutePendingCollaboration();
            return;
        }

        if (questionPanel.IsOpen())
        {
            ClearPendingCollaboration();
            HidePanel();
            battleManager.SetSystemMessageFromExternal("이미 다른 선택창이 열려 있습니다.");
            return;
        }

        if (!questionPanel.TryShowYesNoQuestion(
            "합방 결과를 처리할까요?",
            ExecutePendingCollaboration,
            CancelPendingCollaboration,
            CancelPendingCollaboration
        ))
        {
            ClearPendingCollaboration();
            HidePanel();
            battleManager.SetSystemMessageFromExternal("이미 다른 선택창이 열려 있습니다.");
            return;
        }
    }

    private void ExecutePendingCollaboration()
    {
        if (battleManager == null)
            return;

        if (isResolvingCollaboration)
        {
            battleManager.SetSystemMessageFromExternal("이미 합방 결과를 처리 중입니다.");
            return;
        }

        if (!ValidateCollaboration(pendingGuestSlot, pendingHostSlot))
        {
            ClearPendingCollaboration();
            HidePanel();
            return;
        }

        pendingContext = pendingContext ?? CreateCollaborationContext(
            pendingGuestSlot,
            pendingHostSlot,
            CollaborationStartReason.Normal);

        if (!RevealFaceDownParticipants(pendingGuestSlot, pendingHostSlot))
        {
            ClearPendingCollaboration();
            HidePanel();
            return;
        }

        battleManager.SetBattleBusyFromExternal(true, "CollaborationManager.ExecutePendingCollaboration");
        isResolvingCollaboration = true;
        StartCoroutine(ExecuteBasicCollaborationRoutine(
            pendingGuestSlot,
            pendingHostSlot,
            pendingContext
        ));
    }

    private void CancelPendingCollaboration()
    {
        ClearPendingCollaboration();
        HidePanel();

        if (battleManager != null)
            battleManager.SetSystemMessageFromExternal("합방 결과 처리를 취소했습니다.");
    }

    private void ClearPendingCollaboration()
    {
        pendingGuestSlot = null;
        pendingHostSlot = null;
        pendingContext = null;
    }

    public void CancelCollaborationStateFromExternal()
    {
        if (isResolvingCollaboration)
        {
            StopAllCoroutines();
            isResolvingCollaboration = false;
        }

        ClearPendingCollaboration();
        HidePanel();
        currentContext = null;
        CleanupWinnerMoveVisual();

        if (battleManager != null)
            battleManager.SetBattleBusyFromExternal(false, "CollaborationManager.CancelCollaborationStateFromExternal");
    }

    private bool ValidateCollaboration(BattleFieldSlot guestSlot, BattleFieldSlot hostSlot)
    {
        if (guestSlot == null || hostSlot == null)
        {
            battleManager.SetSystemMessageFromExternal("합방 슬롯 정보가 없습니다.");
            return false;
        }

        if (!guestSlot.HasCharacter || !hostSlot.HasCharacter)
        {
            battleManager.SetSystemMessageFromExternal("합방할 캐릭터 정보가 없습니다.");
            return false;
        }

        if (guestSlot.characterOwner == hostSlot.characterOwner)
        {
            battleManager.SetSystemMessageFromExternal("서로 다른 플레이어의 캐릭터끼리만 합방할 수 있습니다.");
            return false;
        }

        if (guestSlot.isCharacterFaceDown)
        {
            battleManager.SetSystemMessageFromExternal("뒷면 캐릭터는 합방을 시도할 수 없습니다.");
            return false;
        }

        CharacterCardData guestCharacter = guestSlot.characterCard as CharacterCardData;
        CharacterCardData hostCharacter = hostSlot.characterCard as CharacterCardData;

        if (guestCharacter == null || hostCharacter == null)
        {
            battleManager.SetSystemMessageFromExternal("합방은 캐릭터 카드끼리만 가능합니다.");
            return false;
        }

        return true;
    }

    private bool HasHiddenParticipant(BattleFieldSlot guestSlot, BattleFieldSlot hostSlot)
    {
        return (guestSlot != null && guestSlot.isCharacterFaceDown) ||
            (hostSlot != null && hostSlot.isCharacterFaceDown);
    }

    private bool RevealFaceDownParticipants(BattleFieldSlot guestSlot, BattleFieldSlot hostSlot)
    {
        if (!RevealFaceDownCharacter(guestSlot))
            return false;

        if (!RevealFaceDownCharacter(hostSlot))
            return false;

        return true;
    }

    private CollaborationContext CreateCollaborationContext(
        BattleFieldSlot guestSlot,
        BattleFieldSlot hostSlot,
        CollaborationStartReason reason)
    {
        CollaborationContext context = new CollaborationContext();
        context.attackerOriginalSlot = guestSlot;
        context.attackerSlot = guestSlot;
        context.defenderSlot = hostSlot;
        context.battleLocationSlot = hostSlot;
        context.startReason = reason;
        context.attackerOwner = guestSlot != null ? guestSlot.characterOwner : BattleSlotOwner.My;
        context.defenderOwner = hostSlot != null ? hostSlot.characterOwner : BattleSlotOwner.Enemy;
        context.attackerWasFaceDownAtCollabStart =
            guestSlot != null && guestSlot.isCharacterFaceDown;
        context.defenderWasFaceDownAtCollabStart =
            hostSlot != null && hostSlot.isCharacterFaceDown;
        context.attackerEffectsBlockedThisCollab =
            context.attackerWasFaceDownAtCollabStart;
        context.defenderEffectsBlockedThisCollab =
            context.defenderWasFaceDownAtCollabStart;

        return context;
    }

    private void LogCollaborationStart(CollaborationContext context)
    {
        if (context == null)
            return;

        string attackerName = context.attackerSlot != null && context.attackerSlot.characterCard != null
            ? context.attackerSlot.characterCard.name
            : "none";
        string defenderName = context.defenderSlot != null && context.defenderSlot.characterCard != null
            ? context.defenderSlot.characterCard.name
            : "none";

        Debug.Log(
            $"[CollaborationStart] reason={context.startReason}, " +
            $"forcedAttacker={attackerName}, attackerOwner={context.attackerOwner}, " +
            $"attackerOriginalSlot={FormatSlot(context.attackerOriginalSlot)}, " +
            $"defender={defenderName}, defenderOwner={context.defenderOwner}, " +
            $"defenderSlot={FormatSlot(context.defenderSlot)}, " +
            $"battleLocationSlot={FormatSlot(context.GetBattleLocationSlot())}");
    }

    private bool RevealFaceDownCharacter(BattleFieldSlot slot)
    {
        if (slot == null || !slot.isCharacterFaceDown)
            return true;

        BaseCardData card = slot.characterCard;

        if (card == null)
        {
            battleManager.SetSystemMessageFromExternal("공개할 캐릭터 정보가 없습니다.");
            return false;
        }

        Sprite sprite = battleManager.LoadCardSpriteFromExternal(card);

        if (sprite == null)
        {
            battleManager.SetSystemMessageFromExternal($"{card.name} 카드 이미지를 찾을 수 없습니다.");
            return false;
        }

        slot.SetCharacterCard(card, sprite, false, slot.characterOwner);
        battleManager.ApplyBroadcastEnterEffectsFromExternal(slot, false);
        battleManager.RefreshAllUIFromExternal();
        battleManager.SetSystemMessageFromExternal($"{card.name} 카드가 공개되었습니다.");

        return true;
    }

    private void ShowPanelBeforeResult(BattleFieldSlot guestSlot, BattleFieldSlot hostSlot)
    {
        if (collaborationPanel != null)
            collaborationPanel.SetActive(true);

        if (collabResultPanel != null)
            collabResultPanel.SetActive(false);

        SetGuestView(
            guestSlot.characterCard,
            GetEffectiveCollabTension(guestSlot, hostSlot),
            GetEffectiveCharacterHp(guestSlot, hostSlot)
        );

        SetHostView(
            hostSlot.characterCard,
            GetEffectiveCollabTension(hostSlot, hostSlot),
            GetEffectiveCharacterHp(hostSlot, hostSlot)
        );
    }

    private IEnumerator ExecuteBasicCollaborationRoutine(
        BattleFieldSlot guestSlot,
        BattleFieldSlot hostSlot,
        CollaborationContext context)
    {
        isResolvingCollaboration = true;
        currentContext = context;

        ShowPanelBeforeResult(guestSlot, hostSlot);

        BaseCardData guestCard = guestSlot.characterCard;
        BaseCardData hostCard = hostSlot.characterCard;

        BattleSlotOwner guestOwner = guestSlot.characterOwner;
        BattleSlotOwner hostOwner = hostSlot.characterOwner;

        Sprite guestSprite = guestSlot.GetCurrentCharacterSprite();
        Sprite hostSprite = hostSlot.GetCurrentCharacterSprite();

        BattleFieldSlot battleLocationSlot = context != null
            ? context.GetBattleLocationSlot()
            : hostSlot;

        int guestTension = GetEffectiveCollabTension(guestSlot, battleLocationSlot);
        int hostTension = GetEffectiveCollabTension(hostSlot, battleLocationSlot);
        int hostDamage = CalculateCollaborationDamage(
            guestTension,
            context != null && context.defenderWasFaceDownAtCollabStart
        );
        int guestDamage = CalculateCollaborationDamage(
            hostTension,
            context != null && context.attackerWasFaceDownAtCollabStart
        );

        // 초기 표시
        SetGuestView(guestCard, guestTension, GetEffectiveCharacterHp(guestSlot, battleLocationSlot));
        SetHostView(hostCard, hostTension, GetEffectiveCharacterHp(hostSlot, battleLocationSlot));

        RectTransform guestRect = GetCharacterRect(guestCharacterItemUI);
        RectTransform hostRect = GetCharacterRect(hostCharacterItemUI);

        // 1. 공격자 선공 연출
        yield return AnimateAttack(guestRect, true);
        yield return AnimateHit(hostRect);
        yield return AnimateHpDamage(
            hostSlot,
            hostCard,
            false,
            hostDamage
        );

        bool hostDefeated = GetEffectiveCharacterHp(hostSlot, battleLocationSlot) <= 0;
        string koPreventMessage = "";
        if (hostDefeated &&
            TryPreventCollabKOByBroadcastLock(hostSlot, hostCard, battleLocationSlot, out string hostPreventMessage))
        {
            hostDefeated = false;
            koPreventMessage = AppendLine(koPreventMessage, hostPreventMessage);
        }
        bool guestDefeated = false;

        // 2. 방어자가 생존하면 반격
        bool hostCanCounter =
            (!hostDefeated || ShouldDeferZeroHpDuringCollab(hostSlot)) &&
            (context == null || !context.defenderWasFaceDownAtCollabStart);

        if (hostCanCounter)
        {
            yield return new WaitForSeconds(ScaleAnimationTime(0.2f));

            yield return AnimateAttack(hostRect, false);
            yield return AnimateHit(guestRect);
            yield return AnimateHpDamage(
                guestSlot,
                guestCard,
                true,
                guestDamage
            );

            guestDefeated = GetEffectiveCharacterHp(guestSlot, battleLocationSlot) <= 0;
            if (guestDefeated &&
                TryPreventCollabKOByBroadcastLock(guestSlot, guestCard, battleLocationSlot, out string guestPreventMessage))
            {
                guestDefeated = false;
                koPreventMessage = AppendLine(koPreventMessage, guestPreventMessage);
            }
        }

        int guestFinalHp = guestSlot.currentCharacterHp;
        int hostFinalHp = hostSlot.currentCharacterHp;

        // 3. 패배 카드 페이드아웃
        if (hostDefeated)
            yield return AnimateDefeatFade(hostRect);

        if (guestDefeated)
            yield return AnimateDefeatFade(guestRect);

        string resultMessage = BuildPlayerRelativeResultMessage(
            guestOwner,
            hostOwner,
            guestDefeated,
            hostDefeated);

        if (!string.IsNullOrWhiteSpace(koPreventMessage))
            resultMessage += $"\n{koPreventMessage}";

        if (!guestDefeated)
            ResetUiAlpha(guestRect);

        if (!hostDefeated)
            ResetUiAlpha(hostRect);

        ShowResult(resultMessage);

        CollaborationResolutionData resolutionData = new CollaborationResolutionData
        {
            guestSlot = guestSlot,
            hostSlot = hostSlot,
            guestCard = guestCard,
            hostCard = hostCard,
            guestOwner = guestOwner,
            hostOwner = hostOwner,
            guestSprite = guestSprite,
            hostSprite = hostSprite,
            guestTension = guestSlot.currentCharacterTension,
            guestFinalHp = guestFinalHp,
            hostFinalHp = hostFinalHp,
            hostDefeated = hostDefeated,
            guestDefeated = guestDefeated,
            startReason = context != null
                ? context.startReason
                : CollaborationStartReason.Normal
        };

        yield return WaitForResultPanelAndHide();

        isResolvingCollaboration = false;
        yield return ResolveCollaborationResultRoutine(resolutionData);
        string koRestEffectMessage = ApplyCollaborationKoRestEffects(resolutionData);
        if (!string.IsNullOrWhiteSpace(koRestEffectMessage))
            resultMessage += $"\n{koRestEffectMessage}";

        yield return RequestPostCollabEffectsRoutine(
            resolutionData.guestSlot,
            resolutionData.hostSlot
        );

        yield return ResolvePendingAfterPostCollabEffectsRoutine();

        if (TryStartPendingPostCollabRebattle())
            yield break;

        ClearPendingCollaboration();

        battleManager.RefreshAllUIFromExternal();
        currentContext = null;
        battleManager.SetBattleBusyFromExternal(false, "CollaborationManager.ExecuteBasicCollaborationRoutine finished");
        BattleSlotOwner actionOwner = resolutionData.startReason == CollaborationStartReason.Normal
            ? resolutionData.guestOwner
            : BattleSlotOwner.My;
        battleManager.ResolveCollaborationActionUsedFromExternal(actionOwner, resultMessage);
    }

    private IEnumerator ResolvePendingAfterPostCollabEffectsRoutine()
    {
        if (battleManager == null || battleManager.effectManager == null)
            yield break;

        string message = "";
        yield return battleManager.effectManager.ResolvePendingOurTalesAfterCollabRoutineFromExternal(
            resolvedMessage => message = resolvedMessage);

        if (!string.IsNullOrWhiteSpace(message))
            battleManager.SetSystemMessageFromExternal(message);
    }

    private bool TryStartPendingPostCollabRebattle()
    {
        if (battleManager == null || battleManager.effectManager == null)
            return false;

        BattleFieldSlot attackerSlot;
        BattleFieldSlot defenderSlot;
        string message;
        if (!battleManager.effectManager.TryConsumePendingPostCollabRebattleFromExternal(
                out attackerSlot,
                out defenderSlot,
                out message))
        {
            if (!string.IsNullOrWhiteSpace(message))
                battleManager.SetSystemMessageFromExternal(message);

            return false;
        }

        ClearPendingCollaboration();
        currentContext = null;
        battleManager.RefreshAllUIFromExternal();
        battleManager.SetBattleBusyFromExternal(false, "CollaborationManager.PostCollabRebattle");

        if (!string.IsNullOrWhiteSpace(message))
            battleManager.SetSystemMessageFromExternal(message);

        return StartCollaboration(attackerSlot, defenderSlot);
    }

    private string ApplyCollaborationKoRestEffects(CollaborationResolutionData data)
    {
        if (data == null)
            return "";

        if (data.hostDefeated && !data.guestDefeated)
        {
            return ApplyReduceOpponentCollabTensionOnCollab(
                data.hostCard,
                data.hostOwner,
                data.hostSlot);
        }

        if (data.guestDefeated && !data.hostDefeated)
        {
            return ApplyReduceOpponentCollabTensionOnCollab(
                data.guestCard,
                data.guestOwner,
                data.hostSlot);
        }

        return "";
    }

    private string ApplyReduceOpponentCollabTensionOnCollab(
        BaseCardData defeatedCard,
        BattleSlotOwner defeatedOwner,
        BattleFieldSlot survivorSlot)
    {
        if (defeatedCard == null ||
            survivorSlot == null ||
            !survivorSlot.HasCharacter ||
            survivorSlot.characterCard == null ||
            survivorSlot.characterOwner == defeatedOwner)
        {
            return "";
        }

        CharacterCardData defeatedCharacter = defeatedCard as CharacterCardData;
        if (defeatedCharacter == null || defeatedCharacter.effects == null)
            return "";

        foreach (EffectData effect in defeatedCharacter.effects)
        {
            string effectRef = GetEffectRef(effect);
            if (!string.Equals(
                    effectRef,
                    "character.rest.reduceOpponentCollabTensionOnCollab",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int amount = Mathf.Max(0, GetEffectIntParam(effect, "amount", 0));
            if (amount <= 0)
                continue;

            int previousTension = survivorSlot.currentCharacterTension;
            int nextTension = Mathf.Max(0, previousTension - amount);
            survivorSlot.SetCharacterBattleStats(
                survivorSlot.currentCharacterHp,
                survivorSlot.currentCharacterMaxHp,
                nextTension);

            string targetName = survivorSlot.characterCard != null
                ? survivorSlot.characterCard.name
                : "상대 캐릭터";
            string message =
                $"{defeatedCard.name} 효과로 {targetName}의 합방 텐션이 {previousTension - nextTension} 감소했습니다.";

            Debug.Log(message);
            return message;
        }

        return "";
    }

    private string GetEffectRef(EffectData effect)
    {
        if (effect == null)
            return "";

        if (!string.IsNullOrWhiteSpace(effect.refName))
            return effect.refName;

        return effect.@ref;
    }

    private int GetEffectIntParam(EffectData effect, string key, int defaultValue)
    {
        EffectParams effectParams = effect != null ? effect.@params : null;

        if (effectParams == null || string.IsNullOrWhiteSpace(key))
            return defaultValue;

        switch (key)
        {
            case "amount":
                return effectParams.amount;
            default:
                return defaultValue;
        }
    }

    private int CalculateCollaborationDamage(int baseDamage, bool targetWasFaceDownAtCollabStart)
    {
        int safeDamage = Mathf.Max(0, baseDamage);

        if (targetWasFaceDownAtCollabStart)
            safeDamage *= 2;

        return safeDamage;
    }

    private int GetEffectiveCollabTension(
        BattleFieldSlot participantSlot,
        BattleFieldSlot battleLocationSlot)
    {
        if (participantSlot == null)
            return 0;

        if (battleManager != null)
            return battleManager.GetEffectiveCollabTensionFromExternal(participantSlot, battleLocationSlot);

        return Mathf.Max(0, participantSlot.currentCharacterTension);
    }

    private int GetEffectiveCharacterHp(
        BattleFieldSlot slot,
        BattleFieldSlot battleLocationSlot = null)
    {
        if (slot == null)
            return 0;

        if (slot.currentCharacterHp <= 0)
            return 0;

        int hp = slot.currentCharacterHp;

        if (battleManager != null)
            return battleManager.GetEffectiveCharacterHpFromExternal(slot, battleLocationSlot);

        return Mathf.Max(0, hp);
    }

    private class CollaborationResolutionData
    {
        public BattleFieldSlot guestSlot;
        public BattleFieldSlot hostSlot;
        public BaseCardData guestCard;
        public BaseCardData hostCard;
        public BattleSlotOwner guestOwner;
        public BattleSlotOwner hostOwner;
        public Sprite guestSprite;
        public Sprite hostSprite;
        public int guestTension;
        public int guestFinalHp;
        public int hostFinalHp;
        public bool hostDefeated;
        public bool guestDefeated;
        public CollaborationStartReason startReason = CollaborationStartReason.Normal;
    }

    private string BuildPlayerRelativeResultMessage(
        BattleSlotOwner guestOwner,
        BattleSlotOwner hostOwner,
        bool guestDefeated,
        bool hostDefeated)
    {
        if (guestDefeated && hostDefeated)
            return "양쪽 캐릭터가 모두 퇴장했습니다.";

        if (!guestDefeated && !hostDefeated)
            return "합방이 종료되었습니다.";

        bool myCardDefeated =
            (guestOwner == BattleSlotOwner.My && guestDefeated) ||
            (hostOwner == BattleSlotOwner.My && hostDefeated);

        bool enemyCardDefeated =
            (guestOwner == BattleSlotOwner.Enemy && guestDefeated) ||
            (hostOwner == BattleSlotOwner.Enemy && hostDefeated);

        if (enemyCardDefeated && !myCardDefeated)
            return "내 카드가 승리했습니다.";

        if (myCardDefeated && !enemyCardDefeated)
            return "내 카드가 패배했습니다.";

        return "합방이 종료되었습니다.";
    }

    private IEnumerator ResolveCollaborationResultRoutine(CollaborationResolutionData data)
    {
        if (data == null)
            yield break;

        if (data.startReason == CollaborationStartReason.ForceBattleTargetAnywhere)
        {
            yield return ResolveForcedIncomingCollaborationResultRoutine(data);
            yield break;
        }

        if (data.hostDefeated)
        {
            yield return battleManager.ResolveZeroHpCharacterRoutineFromExternal(
                data.hostSlot,
                GetCollaborationBattleLocationSlot(data)
            );

            if (!data.guestDefeated)
            {
                yield return AnimateWinnerMoveToTargetSlot(data);
                MoveGuestToHostSlot(data);
            }
            else
            {
                yield return battleManager.ResolveZeroHpCharacterRoutineFromExternal(
                    data.guestSlot,
                    GetCollaborationBattleLocationSlot(data)
                );
            }

            yield break;
        }

        if (data.guestDefeated)
        {
            yield return battleManager.ResolveZeroHpCharacterRoutineFromExternal(
                data.guestSlot,
                GetCollaborationBattleLocationSlot(data)
            );
            yield break;
        }

        if (data.startReason == CollaborationStartReason.Normal)
            data.guestSlot.SetCharacterMovedThisTurn(true);
    }

    private IEnumerator ResolveForcedIncomingCollaborationResultRoutine(
        CollaborationResolutionData data)
    {
        if (data == null)
            yield break;

        Debug.Log(
            $"[CollaborationResult] reason={data.startReason}, " +
            $"forcedAttacker={data.guestCard?.name}, attackerOwner={data.guestOwner}, " +
            $"attackerOriginalSlot={FormatSlot(data.guestSlot)}, " +
            $"defender={data.hostCard?.name}, defenderOwner={data.hostOwner}, " +
            $"defenderSlot={FormatSlot(data.hostSlot)}, " +
            $"battleLocationSlot={FormatSlot(GetCollaborationBattleLocationSlot(data))}, " +
            $"attackerDefeated={data.guestDefeated}, defenderDefeated={data.hostDefeated}, " +
            "resultRule=defender stays on win; surviving forced attacker moves to defender slot only if defender is defeated.");

        if (data.hostDefeated)
        {
            yield return battleManager.ResolveZeroHpCharacterRoutineFromExternal(
                data.hostSlot,
                GetCollaborationBattleLocationSlot(data)
            );

            if (!data.guestDefeated)
            {
                yield return AnimateWinnerMoveToTargetSlot(data);
                MoveGuestToHostSlot(data);
            }
            else
            {
                yield return battleManager.ResolveZeroHpCharacterRoutineFromExternal(
                    data.guestSlot,
                    GetCollaborationBattleLocationSlot(data)
                );
            }

            yield break;
        }

        if (data.guestDefeated)
        {
            yield return battleManager.ResolveZeroHpCharacterRoutineFromExternal(
                data.guestSlot,
                GetCollaborationBattleLocationSlot(data)
            );
            yield break;
        }

        // Forced incoming collaboration does not spend or mark normal movement when both survive.
    }

    private BattleFieldSlot GetCollaborationBattleLocationSlot(CollaborationResolutionData data)
    {
        if (data == null)
            return null;

        return data.hostSlot;
    }

    private string FormatSlot(BattleFieldSlot slot)
    {
        if (slot == null)
            return "null";

        return $"({slot.owner}, x={slot.x}, y={slot.y})";
    }

    private bool ShouldDeferZeroHpDuringCollab(BattleFieldSlot slot)
    {
        return battleManager != null &&
            battleManager.ShouldDeferZeroHpDuringCollabFromExternal(slot);
    }

    private bool TryPreventCollabKOByBroadcastLock(
        BattleFieldSlot slot,
        BaseCardData card,
        BattleFieldSlot battleLocationSlot,
        out string message)
    {
        message = "";

        if (slot == null ||
            card == null ||
            battleManager == null ||
            !battleManager.ShouldPreventCollabKOByBroadcastMoveAndKoLockFromExternal(slot))
        {
            return false;
        }

        if (GetEffectiveCharacterHp(slot, battleLocationSlot) > 0)
            return false;

        int nextHp = Mathf.Max(1, slot.currentCharacterHp);
        slot.SetCharacterBattleStats(
            nextHp,
            slot.currentCharacterMaxHp,
            slot.currentCharacterTension);

        message = $"모라하지마 효과로 {card.name}은 합방으로 퇴장하지 않습니다.";
        Debug.Log(message);
        return true;
    }

    private string AppendLine(string source, string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return source;

        if (string.IsNullOrWhiteSpace(source))
            return line;

        return $"{source}\n{line}";
    }

    private IEnumerator SendCharacterToRestZoneRoutine(
        BattleSlotOwner owner,
        BaseCardData card,
        BattleFieldSlot sourceSlot)
    {
        if (card == null || sourceSlot == null || battleManager == null)
            yield break;

        yield return battleManager.SendFieldCharacterToRestZoneRoutine(sourceSlot);
    }

    private IEnumerator RequestPostCollabEffectsRoutine(
        BattleFieldSlot attackerSlot,
        BattleFieldSlot defenderSlot)
    {
        bool effectComplete = false;
        battleManager.RequestPostCollabEffectsFromExternal(
            attackerSlot,
            defenderSlot,
            () => effectComplete = true
        );

        while (!effectComplete)
            yield return null;
    }

    private void MoveGuestToHostSlot(CollaborationResolutionData data)
    {
        if (data == null ||
            data.guestSlot == null ||
            data.hostSlot == null ||
            data.guestCard == null)
        {
            return;
        }

        bool guestWasFaceDown = data.guestSlot.isCharacterFaceDown;
        int guestMaxHp = data.guestSlot.currentCharacterMaxHp;
        bool guestActiveUsedThisTurn = data.guestSlot.characterActiveUsedThisTurn;
        int guestMovementLockedUntilTurn = data.guestSlot.movementLockedByBroadcastUntilTurn;
        int guestCollabEffectsSilencedUntilTurn = data.guestSlot.collabEffectsSilencedUntilTurn;
        int guestCollabAttackForbiddenUntilTurn = data.guestSlot.collabAttackForbiddenUntilTurn;
        int guestBroadcastHpMaxDelta = data.guestSlot.broadcastHpMaxDelta;

        data.hostSlot.SetCharacterCard(
            data.guestCard,
            data.guestSprite,
            guestWasFaceDown,
            data.guestOwner
        );

        data.hostSlot.SetCharacterBattleStats(
            data.guestFinalHp,
            guestMaxHp,
            data.guestTension
        );

        data.hostSlot.SetCharacterMovedThisTurn(data.startReason == CollaborationStartReason.Normal
            ? true
            : data.guestSlot.characterMovedThisTurn);
        data.hostSlot.SetCharacterActiveUsedThisTurn(guestActiveUsedThisTurn);
        data.hostSlot.SetMovementLockedByBroadcastUntilTurn(guestMovementLockedUntilTurn);
        data.hostSlot.SetCollabEffectsSilencedUntilTurn(guestCollabEffectsSilencedUntilTurn);
        data.hostSlot.SetCollabAttackForbiddenUntilTurn(guestCollabAttackForbiddenUntilTurn);
        data.hostSlot.SetBroadcastHpMaxDelta(guestBroadcastHpMaxDelta);
        battleManager.ApplyBroadcastEnterEffectsFromExternal(data.hostSlot, true);
        battleManager.ApplyBroadcastLeaveEffectsFromExternal(data.guestSlot);
        data.guestSlot.ClearCharacterCard();
    }

    private void SetGuestView(BaseCardData card, int tension, int hp)
    {
        currentGuestCard = card;

        if (guestCharacterItemUI != null)
        {
            guestCharacterItemUI.SetCard(
                card,
                OnClickGuestCard
            );

            guestCharacterItemUI.SetDragActions(false);
        }

        if (guestCharacterNameText != null)
            guestCharacterNameText.text = card != null ? card.name : "-";

        if (guestCharacterTensionText != null)
            guestCharacterTensionText.text = $"텐션: {tension}";

        if (guestCharacterHpText != null)
            guestCharacterHpText.text = $"체력: {hp}";
    }

    private RectTransform GetCharacterRect(DeckCardItemUI itemUI)
    {
        if (itemUI == null)
            return null;

        return itemUI.GetComponent<RectTransform>();
    }

    private IEnumerator AnimateAttack(RectTransform rect, bool moveRight)
    {
        if (rect == null)
            yield break;

        Vector2 startPos = rect.anchoredPosition;
        Vector2 targetPos = startPos + new Vector2(
            moveRight ? attackMoveDistance : -attackMoveDistance,
            0f
        );

        float timer = 0f;

        float moveDuration = ScaleAnimationTime(attackMoveTime);
        float returnDuration = ScaleAnimationTime(attackReturnTime);

        while (timer < moveDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / moveDuration);
            rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }

        timer = 0f;

        while (timer < returnDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / returnDuration);
            rect.anchoredPosition = Vector2.Lerp(targetPos, startPos, t);
            yield return null;
        }

        rect.anchoredPosition = startPos;
    }

    private IEnumerator AnimateHit(RectTransform rect)
    {
        if (rect == null)
            yield break;

        Vector2 startPos = rect.anchoredPosition;
        float timer = 0f;
        float duration = ScaleAnimationTime(hitShakeTime);

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float progress = Mathf.Clamp01(timer / duration);
            float shake = Mathf.Sin(progress * Mathf.PI * 6f) * hitShakeDistance * (1f - progress);

            rect.anchoredPosition = startPos + new Vector2(shake, 0f);

            yield return null;
        }

        rect.anchoredPosition = startPos;
    }

    private IEnumerator AnimateHpDamage(
        BattleFieldSlot targetSlot,
        BaseCardData targetCard,
        bool isGuest,
        int damage)
    {
        if (targetSlot == null || targetCard == null)
            yield break;

        int safeDamage = Mathf.Max(0, damage);

        for (int i = 0; i < safeDamage; i++)
        {
            if (GetEffectiveCharacterHp(targetSlot, GetCurrentBattleLocationSlot()) <= 0)
                break;

            ApplyEffectiveCharacterDamage(targetSlot, 1);

            if (isGuest)
            {
                SetGuestView(
                    targetCard,
                    GetEffectiveCollabTension(targetSlot, GetCurrentBattleLocationSlot()),
                    GetEffectiveCharacterHp(targetSlot, GetCurrentBattleLocationSlot())
                );
            }
            else
            {
                SetHostView(
                    targetCard,
                    GetEffectiveCollabTension(targetSlot, GetCurrentBattleLocationSlot()),
                    GetEffectiveCharacterHp(targetSlot, GetCurrentBattleLocationSlot())
                );
            }

            yield return new WaitForSeconds(ScaleAnimationTime(hpStepDelay));
        }
    }

    private void ApplyEffectiveCharacterDamage(BattleFieldSlot targetSlot, int damage)
    {
        if (targetSlot == null)
            return;

        int safeDamage = Mathf.Max(0, damage);

        if (safeDamage <= 0)
            return;

        int hpModifier = battleManager != null
            ? battleManager.GetSlotCharacterHpModifierFromExternal(targetSlot, GetCurrentBattleLocationSlot())
            : 0;
        int effectiveHp = targetSlot.currentCharacterHp > 0
            ? Mathf.Max(0, targetSlot.currentCharacterHp + hpModifier)
            : 0;
        int nextEffectiveHp = Mathf.Max(0, effectiveHp - safeDamage);
        int nextBaseHp = Mathf.Max(0, nextEffectiveHp - hpModifier);

        targetSlot.SetCharacterBattleStats(
            nextBaseHp,
            targetSlot.currentCharacterTension
        );
    }

    private BattleFieldSlot GetOpposingCollabSlot(BattleFieldSlot slot)
    {
        if (currentContext == null || slot == null)
            return null;

        if (slot == currentContext.attackerSlot)
            return currentContext.defenderSlot;

        if (slot == currentContext.defenderSlot)
            return currentContext.attackerSlot;

        return null;
    }

    private BattleFieldSlot GetCurrentBattleLocationSlot()
    {
        return currentContext != null
            ? currentContext.GetBattleLocationSlot()
            : null;
    }

    private IEnumerator AnimateDefeatFade(RectTransform rect)
    {
        if (rect == null)
            yield break;

        CanvasGroup canvasGroup = rect.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = rect.gameObject.AddComponent<CanvasGroup>();

        float startAlpha = canvasGroup.alpha;
        float timer = 0f;
        float duration = ScaleAnimationTime(defeatFadeTime);

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            yield return null;
        }

        canvasGroup.alpha = 0f;
    }

    private void ResetUiAlpha(RectTransform rect)
    {
        if (rect == null)
            return;

        CanvasGroup canvasGroup = rect.GetComponent<CanvasGroup>();

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    private float ScaleAnimationTime(float seconds)
    {
        return Mathf.Max(0.01f, seconds * Mathf.Max(0.01f, animationTimeScale));
    }

    private void SetHostView(BaseCardData card, int tension, int hp)
    {
        currentHostCard = card;

        if (hostCharacterItemUI != null)
        {
            hostCharacterItemUI.SetCard(
                card,
                OnClickHostCard
            );

            hostCharacterItemUI.SetDragActions(false);
        }

        if (hostCharacterNameText != null)
            hostCharacterNameText.text = card != null ? card.name : "-";

        if (hostCharacterTensionText != null)
            hostCharacterTensionText.text = $"텐션: {tension}";

        if (hostCharacterHpText != null)
            hostCharacterHpText.text = $"체력: {hp}";
    }

    private void ShowResult(string message)
    {
        if (collabResultPanel != null)
            collabResultPanel.SetActive(true);

        if (resultText != null)
            resultText.text = message;
    }

    private IEnumerator WaitForResultPanelAndHide()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, resultPanelVisibleTime));
        HidePanel();
    }

    private IEnumerator AnimateWinnerMoveToTargetSlot(CollaborationResolutionData data)
    {
        if (data == null ||
            data.guestSlot == null ||
            data.hostSlot == null ||
            data.guestSprite == null)
        {
            yield break;
        }

        RectTransform startRect = GetSlotCharacterRect(data.guestSlot);
        RectTransform targetRect = GetSlotCharacterRect(data.hostSlot);

        if (startRect == null || targetRect == null)
            yield break;

        Canvas canvas = ResolveAnimationCanvas(startRect);

        if (canvas == null)
            yield break;

        CleanupWinnerMoveVisual();

        winnerMoveGhostObject = new GameObject(
            "RuntimeCollaborationWinnerMove",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image)
        );

        winnerMoveGhostObject.transform.SetParent(canvas.transform, false);
        winnerMoveGhostObject.transform.SetAsLastSibling();

        RectTransform ghostRect = winnerMoveGhostObject.GetComponent<RectTransform>();
        ghostRect.position = startRect.position;
        ghostRect.rotation = startRect.rotation;
        ghostRect.sizeDelta = startRect.rect.size;

        Image ghostImage = winnerMoveGhostObject.GetComponent<Image>();
        ghostImage.sprite = data.guestSprite;
        ghostImage.color = Color.white;
        ghostImage.preserveAspect = true;
        ghostImage.raycastTarget = false;

        CanvasGroup canvasGroup = winnerMoveGhostObject.GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        if (data.guestSlot.characterCardImage != null)
        {
            hiddenWinnerMoveSourceImage = data.guestSlot.characterCardImage;
            hiddenWinnerMoveSourceColor = hiddenWinnerMoveSourceImage.color;
            Color hiddenColor = hiddenWinnerMoveSourceColor;
            hiddenColor.a = 0f;
            hiddenWinnerMoveSourceImage.color = hiddenColor;
        }

        Vector3 startPosition = startRect.position;
        Vector3 targetPosition = targetRect.position;
        float duration = Mathf.Max(0.01f, winnerMoveDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            ghostRect.position = Vector3.Lerp(startPosition, targetPosition, easedT);
            yield return null;
        }

        ghostRect.position = targetPosition;

        CleanupWinnerMoveVisual();
    }

    private void CleanupWinnerMoveVisual()
    {
        if (hiddenWinnerMoveSourceImage != null)
            hiddenWinnerMoveSourceImage.color = hiddenWinnerMoveSourceColor;

        hiddenWinnerMoveSourceImage = null;

        if (winnerMoveGhostObject != null)
            Destroy(winnerMoveGhostObject);

        winnerMoveGhostObject = null;
    }

    private RectTransform GetSlotCharacterRect(BattleFieldSlot slot)
    {
        if (slot == null)
            return null;

        if (slot.characterCardImage != null)
            return slot.characterCardImage.rectTransform;

        return slot.transform as RectTransform;
    }

    private Canvas ResolveAnimationCanvas(RectTransform referenceRect)
    {
        Canvas canvas = null;

        if (referenceRect != null)
            canvas = referenceRect.GetComponentInParent<Canvas>();

        if (canvas != null)
            return canvas;

        canvas = GetComponentInParent<Canvas>();

        if (canvas != null)
            return canvas;

        return FindAnyObjectByType<Canvas>();
    }

    private void ScheduleClosePanel()
    {
        if (closePanelCoroutine != null)
            StopCoroutine(closePanelCoroutine);

        closePanelCoroutine = StartCoroutine(ClosePanelAfterDelay());
    }

    private IEnumerator ClosePanelAfterDelay()
    {
        yield return new WaitForSeconds(2.5f);

        closePanelCoroutine = null;
        HidePanel();
    }

    public void HidePanel()
    {
        if (closePanelCoroutine != null)
        {
            StopCoroutine(closePanelCoroutine);
            closePanelCoroutine = null;
        }

        ResetUiAlpha(GetCharacterRect(guestCharacterItemUI));
        ResetUiAlpha(GetCharacterRect(hostCharacterItemUI));

        if (collaborationPanel != null)
            collaborationPanel.SetActive(false);

        if (collabResultPanel != null)
            collabResultPanel.SetActive(false);
    }

    private void OnClickGuestCard(BaseCardData card)
    {
        if (battleManager == null || card == null)
            return;

        battleManager.SelectCardFromExternal(card);
    }

    private void OnClickHostCard(BaseCardData card)
    {
        if (battleManager == null || card == null)
            return;

        battleManager.SelectCardFromExternal(card);
    }
}
