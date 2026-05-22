using System.Collections;
using TMPro;
using UnityEngine;

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

    private bool isResolvingCollaboration = false;
    private BaseCardData currentGuestCard;
    private BaseCardData currentHostCard;
    private BattleFieldSlot pendingGuestSlot;
    private BattleFieldSlot pendingHostSlot;
    private Coroutine closePanelCoroutine;

    public void Init(BattleManager manager)
    {
        battleManager = manager;

        HidePanel();
    }

    public bool IsResolvingCollaboration => isResolvingCollaboration;

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

    public void StartCollaboration(BattleFieldSlot guestSlot, BattleFieldSlot hostSlot)
    {
        if (battleManager == null)
            return;

        if (IsCollaborationInteractionActive)
        {
            battleManager.SetSystemMessageFromExternal("이미 합방 처리를 진행 중입니다.");
            return;
        }

        if (!ValidateCollaboration(guestSlot, hostSlot))
            return;

        currentGuestCard = guestSlot.characterCard;
        currentHostCard = hostSlot.characterCard;
        pendingGuestSlot = guestSlot;
        pendingHostSlot = hostSlot;

        ShowPanelBeforeResult(guestSlot, hostSlot);
        OpenResolveResultQuestion();
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

        battleManager.SetBattleBusyFromExternal(true);
        StartCoroutine(ExecuteBasicCollaborationRoutine(pendingGuestSlot, pendingHostSlot));
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

        if (battleManager != null)
            battleManager.SetBattleBusyFromExternal(false);
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

        if (guestSlot.characterOwner != BattleSlotOwner.My)
        {
            battleManager.SetSystemMessageFromExternal("현재는 내 캐릭터만 합방을 시도할 수 있습니다.");
            return false;
        }

        if (hostSlot.characterOwner != BattleSlotOwner.Enemy)
        {
            battleManager.SetSystemMessageFromExternal("상대 캐릭터가 있는 슬롯에만 합방할 수 있습니다.");
            return false;
        }

        if (guestSlot.isCharacterFaceDown)
        {
            battleManager.SetSystemMessageFromExternal("뒷면 캐릭터는 합방할 수 없습니다.");
            return false;
        }

        if (hostSlot.isCharacterFaceDown)
        {
            battleManager.SetSystemMessageFromExternal("상대의 뒷면 캐릭터와는 아직 합방할 수 없습니다.");
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

    private void ShowPanelBeforeResult(BattleFieldSlot guestSlot, BattleFieldSlot hostSlot)
    {
        if (collaborationPanel != null)
            collaborationPanel.SetActive(true);

        if (collabResultPanel != null)
            collabResultPanel.SetActive(false);

        SetGuestView(
            guestSlot.characterCard,
            guestSlot.currentCharacterTension,
            guestSlot.currentCharacterHp
        );

        SetHostView(
            hostSlot.characterCard,
            hostSlot.currentCharacterTension,
            hostSlot.currentCharacterHp
        );
    }

    private IEnumerator ExecuteBasicCollaborationRoutine(BattleFieldSlot guestSlot, BattleFieldSlot hostSlot)
    {
        isResolvingCollaboration = true;

        BaseCardData guestCard = guestSlot.characterCard;
        BaseCardData hostCard = hostSlot.characterCard;

        BattleSlotOwner guestOwner = guestSlot.characterOwner;
        BattleSlotOwner hostOwner = hostSlot.characterOwner;

        Sprite guestSprite = guestSlot.GetCurrentCharacterSprite();
        Sprite hostSprite = hostSlot.GetCurrentCharacterSprite();

        int guestTension = guestSlot.currentCharacterTension;
        int hostTension = hostSlot.currentCharacterTension;

        // 초기 표시
        SetGuestView(guestCard, guestSlot.currentCharacterTension, guestSlot.currentCharacterHp);
        SetHostView(hostCard, hostSlot.currentCharacterTension, hostSlot.currentCharacterHp);

        RectTransform guestRect = GetCharacterRect(guestCharacterItemUI);
        RectTransform hostRect = GetCharacterRect(hostCharacterItemUI);

        // 1. 공격자 선공 연출
        yield return AnimateAttack(guestRect, true);
        yield return AnimateHit(hostRect);
        yield return AnimateHpDamage(
            hostSlot,
            hostCard,
            false,
            guestTension
        );

        bool hostDefeated = hostSlot.currentCharacterHp <= 0;
        bool guestDefeated = false;

        // 2. 방어자가 생존하면 반격
        if (!hostDefeated)
        {
            yield return new WaitForSeconds(ScaleAnimationTime(0.2f));

            yield return AnimateAttack(hostRect, false);
            yield return AnimateHit(guestRect);
            yield return AnimateHpDamage(
                guestSlot,
                guestCard,
                true,
                hostTension
            );

            guestDefeated = guestSlot.currentCharacterHp <= 0;
        }

        int guestFinalHp = guestSlot.currentCharacterHp;
        int hostFinalHp = hostSlot.currentCharacterHp;

        // 3. 패배 카드 페이드아웃
        if (hostDefeated)
            yield return AnimateDefeatFade(hostRect);

        if (guestDefeated)
            yield return AnimateDefeatFade(guestRect);

        string resultMessage = BuildResultMessage(hostDefeated, guestDefeated);

        ResolveCollaborationResult(
            guestSlot,
            hostSlot,
            guestCard,
            hostCard,
            guestOwner,
            hostOwner,
            guestSprite,
            hostSprite,
            guestTension,
            guestFinalHp,
            hostDefeated,
            guestDefeated
        );

        if (!guestDefeated)
            ResetUiAlpha(guestRect);

        if (!hostDefeated)
            ResetUiAlpha(hostRect);

        ShowResult(resultMessage);
        ScheduleClosePanel();

        ClearPendingCollaboration();

        battleManager.RefreshAllUIFromExternal();
        isResolvingCollaboration = false;
        battleManager.ResolveMyActionUsedFromExternal(resultMessage);
        battleManager.SetBattleBusyFromExternal(false);
    }

    private string BuildResultMessage(bool hostDefeated, bool guestDefeated)
    {
        if (hostDefeated && !guestDefeated)
            return "내 카드가 이겼습니다.";

        if (!hostDefeated && guestDefeated)
            return "내 카드가 졌습니다.";

        if (hostDefeated && guestDefeated)
            return "양쪽 캐릭터가 모두 퇴장했습니다.";

        return "합방이 종료되었습니다.";
    }

    private void ResolveCollaborationResult(
        BattleFieldSlot guestSlot,
        BattleFieldSlot hostSlot,
        BaseCardData guestCard,
        BaseCardData hostCard,
        BattleSlotOwner guestOwner,
        BattleSlotOwner hostOwner,
        Sprite guestSprite,
        Sprite hostSprite,
        int guestTension,
        int guestFinalHp,
        bool hostDefeated,
        bool guestDefeated)
    {
        if (hostDefeated)
        {
            battleManager.AddCharacterToRestZoneFromExternal(hostOwner, hostCard);
            hostSlot.ClearCharacterCard();

            if (!guestDefeated)
            {
                bool guestWasFaceDown = guestSlot.isCharacterFaceDown;

                hostSlot.SetCharacterCard(
                    guestCard,
                    guestSprite,
                    guestWasFaceDown,
                    guestOwner
                );

                hostSlot.SetCharacterBattleStats(
                    guestFinalHp,
                    guestTension
                );

                hostSlot.SetCharacterMovedThisTurn(true);

                guestSlot.ClearCharacterCard();
            }

            return;
        }

        if (guestDefeated)
        {
            battleManager.AddCharacterToRestZoneFromExternal(guestOwner, guestCard);
            guestSlot.ClearCharacterCard();
            return;
        }

        guestSlot.SetCharacterMovedThisTurn(true);
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
            if (targetSlot.currentCharacterHp <= 0)
                break;

            targetSlot.ApplyCharacterDamage(1);

            if (isGuest)
            {
                SetGuestView(
                    targetCard,
                    targetSlot.currentCharacterTension,
                    targetSlot.currentCharacterHp
                );
            }
            else
            {
                SetHostView(
                    targetCard,
                    targetSlot.currentCharacterTension,
                    targetSlot.currentCharacterHp
                );
            }

            yield return new WaitForSeconds(ScaleAnimationTime(hpStepDelay));
        }
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
