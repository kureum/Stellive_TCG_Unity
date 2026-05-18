using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardItemUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI")]
    public Image cardImage;

    private BaseCardData cardData;
    private Action<BaseCardData, PointerEventData.InputButton> onClickCard;

    public void SetCard(BaseCardData data, Action<BaseCardData, PointerEventData.InputButton> clickAction)
    {
        cardData = data;
        onClickCard = clickAction;

        if (cardData == null)
        {
            Debug.LogWarning("CardItemUI: cardData가 비어 있습니다.");
            return;
        }

        Sprite sprite = Resources.Load<Sprite>(cardData.image);

        if (sprite == null)
        {
            Debug.LogWarning($"카드 이미지를 찾을 수 없습니다: {cardData.image}");
        }
        else if (cardImage != null)
        {
            cardImage.sprite = sprite;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (cardData == null) return;

        onClickCard?.Invoke(cardData, eventData.button);
    }

    public BaseCardData GetCardData()
    {
        return cardData;
    }
}