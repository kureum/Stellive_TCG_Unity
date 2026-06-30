using System;
using System.Collections.Generic;

[Serializable]
public class BattleActionResult
{
    public int actionSequence;
    public BattleSlotOwner actor;
    public BattleActionType requestActionType;
    public bool isAccepted;
    public string rejectReason = "";
    public string message = "";

    public List<string> resolvedRandomCardIds = new List<string>();
    public List<string> resolvedTargetSlotIds = new List<string>();
    public List<string> resolvedChoiceIds = new List<string>();
    public List<string> playerMainDeckOrderIds = new List<string>();
    public List<string> enemyMainDeckOrderIds = new List<string>();
    public List<string> hostInitialHandCardInstanceIds = new List<string>();
    public List<string> clientInitialHandCardInstanceIds = new List<string>();
    public List<string> hostRemainingMainDeckOrderIds = new List<string>();
    public List<string> clientRemainingMainDeckOrderIds = new List<string>();
    public string firstActor = "";
    public string broadcastSetupFirstActor = "";
    public BattleSlotOwner currentTurnPlayer;
    public int turnCount;
    public string nextPhase = "";
    public bool didAdvanceTurn;
    public bool hostPassedThisTurn;
    public bool clientPassedThisTurn;
    public bool hostNoActionPassed;
    public bool clientNoActionPassed;
    public bool hostActedInCurrentPassCycle;
    public bool clientActedInCurrentPassCycle;
    public int consecutiveNoActionPassCount;
    public int hostViewerCount;
    public int clientViewerCount;
    public int hostViewerGain;
    public int clientViewerGain;
    public int hostHandCount;
    public int clientHandCount;
    public int hostDeckCount;
    public int clientDeckCount;
    public bool faceDown;
    public int paidViewerCost;
    public BattleSlotOwner drawnPlayer;
    public List<string> drawnCardInstanceIds = new List<string>();
    public List<string> hostDrawnCardInstanceIds = new List<string>();
    public List<string> clientDrawnCardInstanceIds = new List<string>();

    public List<string> movedCardIds = new List<string>();
    public List<string> affectedCardIds = new List<string>();
    public List<string> affectedSlotIds = new List<string>();
}
