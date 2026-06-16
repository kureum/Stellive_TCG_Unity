using System;
using System.Collections.Generic;

[Serializable]
public class BattleAction
{
    public int actionSequence;
    public BattleSlotOwner actor;
    public BattleActionType actionType;

    // TODO: 온라인화 단계에서는 handIndex 대신 cardInstanceId 기반으로 전환 필요.
    public int handIndex = -1;
    public string cardInstanceId;
    public string sourceSlotId;
    public string targetSlotId;
    public string effectRef;
    public EffectTiming effectTiming = EffectTiming.Content;

    public List<string> selectedTargetIds = new List<string>();

    // TODO: 온라인화 단계에서는 selectedIndexes 대신 cardInstanceId/choiceCandidateId 기반으로 전환 필요.
    public List<string> selectedCardIds = new List<string>();
    public List<int> selectedIndexes = new List<int>();
    public string choiceId = "";
    public string choiceValue = "";
}
