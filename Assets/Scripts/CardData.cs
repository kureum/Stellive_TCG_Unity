using System;
using System.Collections.Generic;

[Serializable]
public class CardDatabase
{
    public List<IdolCardData> idols;
    public List<BroadcastCardData> broadcasts;
    public List<CharacterCardData> characters;
    public List<ContentCardData> contents;
}

[Serializable]
public class BaseCardData
{
    public string id;
    public string name;
    public string kind;
    public string[] charm;
    public string[] hashtags;
    public string image;
    public string rarity;
}

[Serializable]
public class EffectData
{
    public string id;
    public string timing;
    public string description;
    public string refName;
}

[Serializable]
public class IdolCardData : BaseCardData
{
    public int maxBroadcastSlots;
    public int baseViewersPerPrep;
    public int activeCost;

    public EffectData[] active;
    public EffectData[] passive;
}

[Serializable]
public class BroadcastCardData : BaseCardData
{
    public int viewersModifier;

    public EffectData[] effects;
}

[Serializable]
public class CharacterCardData : BaseCardData
{
    public int tension;
    public int hpMax;
    public int appearCost;
    public int activeCost;

    public EffectData[] effects;
}

[Serializable]
public class ContentCardData : BaseCardData
{
    public string contentType;
    public int cost;

    public EffectData[] effects;
}