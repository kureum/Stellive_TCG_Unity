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
    public string firstActor = "";
    public string broadcastSetupFirstActor = "";
    public BattleSlotOwner currentTurnPlayer;
    public int turnCount;
    public string nextPhase = "";
    public int hostViewerCount;
    public int clientViewerCount;
    public int hostHandCount;
    public int clientHandCount;
    public int hostDeckCount;
    public int clientDeckCount;

    public List<string> movedCardIds = new List<string>();
    public List<string> affectedCardIds = new List<string>();
    public List<string> affectedSlotIds = new List<string>();
}
