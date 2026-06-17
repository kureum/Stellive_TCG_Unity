using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DebugCheatManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private CardFunctionAuditManager cardFunctionAuditManager;

    [Header("Cheat Panel")]
    [SerializeField] private GameObject cheatPanelRoot;
    [SerializeField] private TMP_InputField cheatCodeInputField;
    [SerializeField] private Button executeButton;
    [SerializeField] private Button closeButton;

    [Header("Cheat Settings")]
    [SerializeField] private bool enableCheats = true;

    private enum CheatKey
    {
        Up,
        Down
    }

    private readonly CheatKey[] broadcastAutoSetupCode =
    {
        CheatKey.Up,
        CheatKey.Up,
        CheatKey.Down,
        CheatKey.Down
    };

    private readonly CheatKey[] cheatPanelCode =
    {
        CheatKey.Down,
        CheatKey.Down,
        CheatKey.Down,
        CheatKey.Up,
        CheatKey.Up,
        CheatKey.Up
    };

    private int broadcastAutoSetupInputIndex = 0;
    private int cheatPanelInputIndex = 0;

    private void Awake()
    {
        if (battleManager == null)
            battleManager = FindAnyObjectByType<BattleManager>();

        if (cardFunctionAuditManager == null)
            cardFunctionAuditManager = FindAnyObjectByType<CardFunctionAuditManager>();

        SetupCheatPanel();
    }

    private void Update()
    {
        if (!enableCheats)
            return;

        CheckCheatCodes();
    }

    private void OnDestroy()
    {
        if (executeButton != null)
            executeButton.onClick.RemoveListener(ExecuteCheatFromInput);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(HideCheatPanel);

        if (cheatCodeInputField != null)
            cheatCodeInputField.onSubmit.RemoveListener(ExecuteCheatFromSubmit);
    }

    private void SetupCheatPanel()
    {
        if (executeButton != null)
        {
            executeButton.onClick.RemoveListener(ExecuteCheatFromInput);
            executeButton.onClick.AddListener(ExecuteCheatFromInput);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(HideCheatPanel);
            closeButton.onClick.AddListener(HideCheatPanel);
        }

        if (cheatCodeInputField != null)
        {
            cheatCodeInputField.onSubmit.RemoveListener(ExecuteCheatFromSubmit);
            cheatCodeInputField.onSubmit.AddListener(ExecuteCheatFromSubmit);
        }

        HideCheatPanel();
    }

    private void CheckCheatCodes()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        CheatKey? pressedKey = null;

        if (keyboard.upArrowKey.wasPressedThisFrame)
            pressedKey = CheatKey.Up;
        else if (keyboard.downArrowKey.wasPressedThisFrame)
            pressedKey = CheatKey.Down;
        else if (keyboard.leftArrowKey.wasPressedThisFrame ||
                 keyboard.rightArrowKey.wasPressedThisFrame)
            pressedKey = null;
        else
            return;

        ProcessCheatSequence(
            pressedKey,
            broadcastAutoSetupCode,
            ref broadcastAutoSetupInputIndex,
            ExecuteBroadcastAutoSetupCheat
        );

        ProcessCheatSequence(
            pressedKey,
            cheatPanelCode,
            ref cheatPanelInputIndex,
            ShowCheatPanel
        );
    }

    private void ProcessCheatSequence(
        CheatKey? pressedKey,
        CheatKey[] sequence,
        ref int inputIndex,
        Action onComplete)
    {
        if (sequence == null || sequence.Length == 0)
            return;

        if (pressedKey == null)
        {
            inputIndex = 0;
            return;
        }

        CheatKey expectedKey = sequence[inputIndex];

        if (pressedKey.Value == expectedKey)
        {
            inputIndex++;

            if (inputIndex >= sequence.Length)
            {
                inputIndex = 0;
                onComplete?.Invoke();
            }

            return;
        }

        // 입력이 틀렸지만, 현재 입력이 치트 코드의 첫 입력과 같다면 1단계부터 다시 시작
        if (pressedKey.Value == sequence[0])
            inputIndex = 1;
        else
            inputIndex = 0;
    }

    public void ShowCheatPanel()
    {
        if (cheatPanelRoot == null)
        {
            Debug.LogWarning("DebugCheatManager: CheatPanel_Master가 연결되어 있지 않습니다.");
            return;
        }

        cheatPanelRoot.SetActive(true);

        if (cheatCodeInputField != null)
        {
            cheatCodeInputField.ActivateInputField();
            cheatCodeInputField.Select();
        }
    }

    public void HideCheatPanel()
    {
        if (cheatPanelRoot != null)
            cheatPanelRoot.SetActive(false);
    }

    public void ToggleCheatPanel()
    {
        if (cheatPanelRoot == null)
            return;

        if (cheatPanelRoot.activeSelf)
            HideCheatPanel();
        else
            ShowCheatPanel();
    }

    public void ExecuteCheatFromInput()
    {
        string command = cheatCodeInputField != null
            ? cheatCodeInputField.text
            : "";

        ExecuteCheat(command);
    }

    private void ExecuteCheatFromSubmit(string command)
    {
        ExecuteCheat(command);
    }

    private void ExecuteCheat(string rawCommand)
    {
        string command = rawCommand != null ? rawCommand.Trim() : "";

        if (string.IsNullOrEmpty(command))
        {
            PublishCheatMessage("Cheat failed: empty command");
            return;
        }

        string[] parts = command.Split(
            new[] { ' ', '\t', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries
        );

        if (parts.Length <= 0)
        {
            PublishCheatMessage("Cheat failed: empty command");
            return;
        }

        string verb = parts[0].ToLowerInvariant();
        bool success = false;
        string message;

        if (string.Equals(command, "show me the money", StringComparison.OrdinalIgnoreCase))
        {
            success = ExecuteShowMeTheMoneyCheat(out message);
        }
        else switch (verb)
        {
            case "audit":
            case "cards":
            case "effectcheck":
                success = ExecuteCardFunctionAuditCheat(parts, out message);
                break;

            case "unbusy":
            case "resetbusy":
            case "clearpending":
                success = ExecuteClearPendingCheat(out message);
                break;

            case "actionstate":
            case "turnstate":
                success = ExecuteActionStateCheat(out message);
                break;

            case "battleactiontest":
            case "actionserialization":
                success = ExecuteBattleActionSerializationTestCheat(out message);
                break;

            case "actionidguide":
                success = ExecuteActionIdGuideCheat(out message);
                break;

            case "actionidcheck":
                success = ExecuteActionIdCheckCheat(out message);
                break;

            case "actionvalidatortest":
                success = ExecuteActionValidatorTestCheat(out message);
                break;

            case "randomusageguide":
                success = ExecuteRandomUsageGuideCheat(out message);
                break;

            case "actionresulttest":
                success = ExecuteActionResultTestCheat(out message);
                break;

            case "hostrandompolicy":
                success = ExecuteHostRandomPolicyCheat(out message);
                break;

            case "actionresultapplytest":
                success = ExecuteActionResultApplyTestCheat(out message);
                break;

            case "actionpipelinetest":
            case "networksimtest":
                success = ExecuteActionPipelineTestCheat(out message);
                break;

            case "mockguestjoin":
                success = ExecuteMockGuestJoinCheat(parts, out message);
                break;

            case "summon":
                success = ExecuteSummonCheat(parts, out message);
                break;

            case "give":
                success = ExecuteGiveCheat(parts, out message);
                break;

            default:
                message = GetUsageMessage();
                break;
        }

        PublishCheatMessage(message);

        if (success && cheatCodeInputField != null)
            cheatCodeInputField.text = "";
    }

    private bool ExecuteSummonCheat(string[] parts, out string message)
    {
        if (battleManager == null)
        {
            message = "Cheat failed: BattleManager not found";
            return false;
        }

        if (parts == null || parts.Length != 4)
        {
            message = GetUsageMessage();
            return false;
        }

        if (!TryParseTargetOwner(parts[1], out BattleSlotOwner owner))
        {
            message = "Cheat failed: unknown player target";
            return false;
        }

        return battleManager.DebugSummonCharacterToSlot(
            owner,
            parts[2],
            parts[3],
            out message
        );
    }

    private bool ExecuteGiveCheat(string[] parts, out string message)
    {
        if (battleManager == null)
        {
            message = "Cheat failed: BattleManager not found";
            return false;
        }

        if (parts == null || parts.Length != 3)
        {
            message = GetUsageMessage();
            return false;
        }

        if (!TryParseTargetOwner(parts[1], out BattleSlotOwner owner))
        {
            message = "Cheat failed: unknown player target";
            return false;
        }

        return battleManager.DebugGiveCardToHand(
            owner,
            parts[2],
            out message
        );
    }

    private bool ExecuteShowMeTheMoneyCheat(out string message)
    {
        const int viewerAmount = 100000;

        if (battleManager == null)
        {
            message = "Cheat failed: BattleManager not found";
            return false;
        }

        battleManager.ModifyViewersFromExternal(BattleSlotOwner.My, viewerAmount);
        battleManager.RefreshAllUIFromExternal();

        message = "치트 적용: 내 시청자 +100,000";
        Debug.Log("[Cheat] show me the money: my viewers +100000");
        return true;
    }

    private bool ExecuteCardFunctionAuditCheat(string[] parts, out string message)
    {
        if (cardFunctionAuditManager == null)
            cardFunctionAuditManager = FindAnyObjectByType<CardFunctionAuditManager>();

        if (cardFunctionAuditManager == null)
            cardFunctionAuditManager = gameObject.AddComponent<CardFunctionAuditManager>();

        bool printDetail = parts != null &&
            parts.Length > 1 &&
            parts[1].Equals("detail", StringComparison.OrdinalIgnoreCase);

        if (printDetail)
        {
            cardFunctionAuditManager.PrintEffectAuditDetail();
            message = "카드 효과 상세 감사 결과를 로그로 출력했습니다.";
        }
        else
        {
            cardFunctionAuditManager.PrintEffectAuditSummary();
            message = "카드 효과 감사 요약을 로그로 출력했습니다.";
        }

        return true;
    }

    private bool ExecuteClearPendingCheat(out string message)
    {
        if (battleManager == null)
        {
            message = "Cheat failed: BattleManager not found";
            return false;
        }

        battleManager.DebugClearPendingAndBusyState();
        message = "처리 상태를 초기화했습니다.";
        return true;
    }

    private bool ExecuteActionStateCheat(out string message)
    {
        if (battleManager == null)
        {
            message = "Cheat failed: BattleManager not found";
            return false;
        }

        battleManager.DebugPrintActionState("DebugCheat");
        message = "행동권 상태를 로그로 출력했습니다.";
        return true;
    }

    private bool ExecuteBattleActionSerializationTestCheat(out string message)
    {
        if (battleManager == null)
            battleManager = FindAnyObjectByType<BattleManager>();

        bool result = BattleActionSerializationTester.RunAll(battleManager);
        message = result
            ? "BattleAction 직렬화 테스트가 통과했습니다. 자세한 결과는 로그를 확인하세요."
            : "BattleAction 직렬화 테스트가 실패했습니다. 경고 로그를 확인하세요.";
        return result;
    }

    private bool ExecuteActionIdGuideCheat(out string message)
    {
        BattleActionFieldUsageGuide.LogSummary();
        message = "BattleAction ID 필드 사용 가이드를 로그로 출력했습니다.";
        return true;
    }

    private bool ExecuteActionIdCheckCheat(out string message)
    {
        if (battleManager == null)
            battleManager = FindAnyObjectByType<BattleManager>();

        if (battleManager == null)
        {
            message = "Cheat failed: BattleManager not found";
            return false;
        }

        battleManager.DebugPrintCardInstanceIdState();
        message = "현재 카드 instanceId 상태를 로그로 출력했습니다.";
        return true;
    }

    private bool ExecuteActionValidatorTestCheat(out string message)
    {
        if (battleManager == null)
            battleManager = FindAnyObjectByType<BattleManager>();

        if (battleManager == null)
        {
            message = "Cheat failed: BattleManager not found";
            return false;
        }

        bool result = BattleActionValidatorTester.Run(battleManager);
        message = result
            ? "BattleAction Validator 테스트를 로그로 출력했습니다."
            : "BattleAction Validator 테스트 중 예상과 다른 결과가 있습니다. 경고 로그를 확인하세요.";
        return result;
    }

    private bool ExecuteRandomUsageGuideCheat(out string message)
    {
        BattleRandomUsageGuide.LogSummary();
        message = "랜덤 사용 지점과 온라인 동기화 정책 가이드를 로그로 출력했습니다.";
        return true;
    }

    private bool ExecuteActionResultTestCheat(out string message)
    {
        bool result = BattleActionResultTester.RunAll();
        message = result
            ? "BattleActionResult 직렬화 테스트가 통과했습니다. 자세한 결과는 로그를 확인하세요."
            : "BattleActionResult 직렬화 테스트가 실패했습니다. 경고 로그를 확인하세요.";
        return result;
    }

    private bool ExecuteHostRandomPolicyCheat(out string message)
    {
        BattleRandomUsageGuide.LogHostConfirmedPolicy();
        message = "호스트 확정 랜덤 정책을 로그로 출력했습니다.";
        return true;
    }

    private bool ExecuteActionResultApplyTestCheat(out string message)
    {
        if (battleManager == null)
            battleManager = FindAnyObjectByType<BattleManager>();

        if (battleManager == null)
        {
            message = "Cheat failed: BattleManager not found";
            return false;
        }

        bool result = BattleActionResultTester.RunApplyTests(battleManager);
        message = result
            ? "BattleActionResult 적용 테스트가 통과했습니다. 자세한 결과는 로그를 확인하세요."
            : "BattleActionResult 적용 테스트가 실패했습니다. 경고 로그를 확인하세요.";
        return result;
    }

    private bool ExecuteActionPipelineTestCheat(out string message)
    {
        if (battleManager == null)
            battleManager = FindAnyObjectByType<BattleManager>();

        if (battleManager == null)
        {
            message = "Cheat failed: BattleManager not found";
            return false;
        }

        BattleActionPipelineTester tester = new BattleActionPipelineTester(battleManager);
        tester.RunAllTests();
        message = "BattleAction 송수신 시뮬레이션 테스트를 로그로 출력했습니다.";
        return true;
    }

    private bool ExecuteMockGuestJoinCheat(string[] parts, out string message)
    {
        LobbyManager lobbyManager = FindAnyObjectByType<LobbyManager>();
        if (lobbyManager == null)
        {
            message = "Cheat failed: LobbyManager not found";
            return false;
        }

        string roomCode = parts != null && parts.Length > 1 ? parts[1] : "";
        bool result = lobbyManager.DebugMockGuestJoin(roomCode);
        message = result
            ? "Mock guest join 처리 완료"
            : "Mock guest join 실패";
        return result;
    }

    private bool TryParseTargetOwner(string value, out BattleSlotOwner owner)
    {
        owner = BattleSlotOwner.My;

        if (string.Equals(value, "me", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "my", StringComparison.OrdinalIgnoreCase))
        {
            owner = BattleSlotOwner.My;
            return true;
        }

        if (string.Equals(value, "enemy", StringComparison.OrdinalIgnoreCase))
        {
            owner = BattleSlotOwner.Enemy;
            return true;
        }

        return false;
    }

    private void PublishCheatMessage(string message)
    {
        if (battleManager != null)
            battleManager.SetSystemMessageFromExternal(message);
        else
            Debug.Log(message);
    }

    private string GetUsageMessage()
    {
        return
            "Cheat usage:\n" +
            "summon me 11 CARD-ID\n" +
            "summon enemy 23 CARD-ID\n" +
            "give me CARD-ID\n" +
            "give enemy CARD-ID\n" +
            "show me the money\n" +
            "audit\n" +
            "audit detail\n" +
            "unbusy\n" +
            "actionstate\n" +
            "battleactiontest\n" +
            "actionidguide\n" +
            "actionidcheck\n" +
            "actionvalidatortest\n" +
            "randomusageguide\n" +
            "actionresulttest\n" +
            "hostrandompolicy\n" +
            "actionresultapplytest\n" +
            "actionpipelinetest\n" +
            "networksimtest\n" +
            "mockguestjoin ROOMCODE";
    }

    private void ExecuteBroadcastAutoSetupCheat()
    {
        if (battleManager == null)
        {
            Debug.LogWarning("DebugCheatManager: BattleManager가 연결되어 있지 않습니다.");
            return;
        }

        battleManager.Debug_AutoPlaceAllBroadcastsRandomly();
    }
}
