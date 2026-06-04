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
    public string @ref;
    public string refName;
    public EffectParams @params;
}

[Serializable]
public class EffectParams
{
    public int amount;
    public int draw;
    public int discard;
    public int hp;
    public int tension;
    public int tensionDelta;
    public int hpMaxDelta;
    public int max;
    public int maxCount;
    public int count;
    public int discardCount;
    public int searchCount;
    public int range;
    public int reveal;
    public int extraCostPer;
    public int viewersModifier;
    public int healBonus;
    public int donateViewers;
    public int donateAmount;
    public int viewersCost;
    public string tag;
    public string requireTag;
    public string tabiTag;
    public string bunnyTag;
    public string kind;
    public string targetOwner;
    public string ownerScope;
    public string targetScope;
    public string scope;
    public string deckInsertPosition;
    public string[] allTags;
    public bool oncePerTurn;
    public bool faceUp;
    public bool shuffleDeckAfterMove;
    public bool forbidFaceDownSummon;
    public bool disablePreCollabEffects;
    public bool disableIdolActiveForOccupantOwner;
    public bool lockMoveOnEnterUntilNextTurn;
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
