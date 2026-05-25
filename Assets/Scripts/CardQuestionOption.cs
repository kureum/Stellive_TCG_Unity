using System;

[Serializable]
public class CardQuestionOption
{
    public BaseCardData card;
    public BattleFieldSlot linkedSlot;
    public EffectCandidate linkedCandidate;

    public CardQuestionOption(
        BaseCardData card,
        BattleFieldSlot linkedSlot = null,
        EffectCandidate linkedCandidate = null)
    {
        this.card = card;
        this.linkedSlot = linkedSlot;
        this.linkedCandidate = linkedCandidate;
    }
}
