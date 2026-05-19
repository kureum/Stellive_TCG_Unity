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
            battleManager.SetSystemMessageFromExternal("이미 다른 선택창이 열려 있습니다.");
            return;
        }

        questionPanel.ShowYesNoQuestion(
            "합방 결과를 처리할까요?",
            ExecutePendingCollaboration,
            CancelPendingCollaboration,
            CancelPendingCollaboration
        );
    }

    private void ExecutePendingCollaboration()
    {
        if (battleManager == null)
            return;

        if (!ValidateCollaboration(pendingGuestSlot, pendingHostSlot))
        {
            ClearPendingCollaboration();
            HidePanel();
            return;
        }

        ExecuteBasicCollaboration(pendingGuestSlot, pendingHostSlot);
        ClearPendingCollaboration();
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

    private void ExecuteBasicCollaboration(BattleFieldSlot guestSlot, BattleFieldSlot hostSlot)
    {
        BaseCardData guestCard = guestSlot.characterCard;
        BaseCardData hostCard = hostSlot.characterCard;

        BattleSlotOwner guestOwner = guestSlot.characterOwner;
        BattleSlotOwner hostOwner = hostSlot.characterOwner;

        Sprite guestSprite = guestSlot.GetCurrentCharacterSprite();
        Sprite hostSprite = hostSlot.GetCurrentCharacterSprite();

        int guestTension = guestSlot.currentCharacterTension;
        int hostTension = hostSlot.currentCharacterTension;

        // 1. 공격자 선공
        hostSlot.ApplyCharacterDamage(guestTension);

        bool hostDefeated = hostSlot.currentCharacterHp <= 0;
        bool guestDefeated = false;

        // 2. 수비자가 생존하면 반격
        if (!hostDefeated)
        {
            guestSlot.ApplyCharacterDamage(hostTension);
            guestDefeated = guestSlot.currentCharacterHp <= 0;
        }

        int guestFinalHp = guestSlot.currentCharacterHp;
        int hostFinalHp = hostSlot.currentCharacterHp;

        SetGuestView(guestCard, guestTension, guestFinalHp);
        SetHostView(hostCard, hostTension, hostFinalHp);

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

        ShowResult(resultMessage);
        ScheduleClosePanel();

        battleManager.RefreshAllUIFromExternal();
        battleManager.ResolveMyActionUsedFromExternal(resultMessage);
    }

    private string BuildResultMessage(bool hostDefeated, bool guestDefeated)
    {
        if (hostDefeated && !guestDefeated)
            return "내 카드가 이겼습니다.";

        return "내 카드가 졌습니다.";
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
