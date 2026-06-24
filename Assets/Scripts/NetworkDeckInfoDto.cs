using System;

[Serializable]
public class NetworkDeckInfoDto
{
    public int actorNumber;
    public string playerSide;
    public string selectedDeckId;
    public string deckName;
    public string idolCardId;
    public string[] broadcastCardIds;
    public string[] mainDeckCardIds;
    public string deckHash;
}
