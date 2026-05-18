using UnityEngine;

public class TestEnemy : MonoBehaviour
{
    [Header("References")]
    public BattleManager battleManager;

    [Header("Settings")]
    public float actionDelay = 0.5f;

    private bool isWaiting;
    private float timer;

    private void Start()
    {
        if (battleManager == null)
            battleManager = FindAnyObjectByType<BattleManager>();
    }

    private void Update()
    {
        if (battleManager == null)
            return;

        if (battleManager.IsBroadcastSetupPhase())
        {
            HandleBroadcastSetupTurn();
            return;
        }

        if (battleManager.IsEnemyActionTurn())
        {
            HandleMainGameActionTurn();
            return;
        }

        isWaiting = false;
        timer = 0f;
    }

    private void HandleBroadcastSetupTurn()
    {
        if (!battleManager.IsEnemySetupTurn())
        {
            isWaiting = false;
            timer = 0f;
            return;
        }

        if (!isWaiting)
        {
            isWaiting = true;
            timer = actionDelay;
        }

        timer -= Time.deltaTime;

        if (timer > 0f)
            return;

        isWaiting = false;
        timer = 0f;

        battleManager.TestEnemyPlaceBroadcastCard();
    }

    private void HandleMainGameActionTurn()
    {
        if (!isWaiting)
        {
            isWaiting = true;
            timer = actionDelay;
        }

        timer -= Time.deltaTime;

        if (timer > 0f)
            return;

        isWaiting = false;
        timer = 0f;

        bool didAction = battleManager.TestEnemyTrySummonBacksideCharacter();

        if (didAction)
            return;

        battleManager.TestEnemyPassAction();
    }
}