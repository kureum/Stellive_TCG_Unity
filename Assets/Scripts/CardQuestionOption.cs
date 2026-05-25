using System;

[Serializable]
public class CardQuestionOption
{
    public BaseCardData card;
    public BattleFieldSlot linkedSlot;

    public CardQuestionOption(BaseCardData card, BattleFieldSlot linkedSlot = null)
    {
        this.card = card;
        this.linkedSlot = linkedSlot;
    }
}
