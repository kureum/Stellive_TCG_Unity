using UnityEngine;
using UnityEngine.InputSystem;

public class DebugCheatManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleManager battleManager;

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

    private int inputIndex = 0;

    private void Awake()
    {
        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();
    }

    private void Update()
    {
        if (!enableCheats)
            return;

        CheckBroadcastAutoSetupCode();
    }

    private void CheckBroadcastAutoSetupCode()
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

        if (pressedKey == null)
        {
            inputIndex = 0;
            return;
        }

        CheatKey expectedKey = broadcastAutoSetupCode[inputIndex];

        if (pressedKey.Value == expectedKey)
        {
            inputIndex++;

            if (inputIndex >= broadcastAutoSetupCode.Length)
            {
                inputIndex = 0;
                ExecuteBroadcastAutoSetupCheat();
            }

            return;
        }

        // 입력이 틀렸지만, 현재 입력이 치트 코드의 첫 입력과 같다면 1단계부터 다시 시작
        if (pressedKey.Value == broadcastAutoSetupCode[0])
            inputIndex = 1;
        else
            inputIndex = 0;
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