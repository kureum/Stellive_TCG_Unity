using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DeckCardItemUI : MonoBehaviour,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    public Image cardImage;

    [Header("Double Click")]
    public float doubleClickTime = 0.3f;

    [Header("Drag")]
    [Tooltip("이 카드 UI가 드래그 가능한 상태인지 여부입니다.")]
    public bool isDraggable = false;

    private BaseCardData cardData;

    private Action<BaseCardData> onLeftClick;
    private Action<BaseCardData> onRightClick;
    private Action<BaseCardData> onDoubleClick;

    private Action<DeckCardItemUI, BaseCardData, PointerEventData> onBeginDrag;
    private Action<DeckCardItemUI, BaseCardData, PointerEventData> onDrag;
    private Action<DeckCardItemUI, BaseCardData, PointerEventData> onEndDrag;

    private float lastLeftClickTime = -1f;

    private bool isDragging = false;
    private bool originalRaycastTarget = true;

    public void SetCard(
        BaseCardData card,
        Action<BaseCardData> leftClickAction = null,
        Action<BaseCardData> rightClickAction = null,
        Action<BaseCardData> doubleClickAction = null
    )
    {
        cardData = card;
        onLeftClick = leftClickAction;
        onRightClick = rightClickAction;
        onDoubleClick = doubleClickAction;

        lastLeftClickTime = -1f;
        isDragging = false;

        if (card == null)
        {
            ClearVisual();
            return;
        }

        CacheCardImageIfNeeded();
        SetCardImage(card);
    }

    public void SetDragActions(
        bool draggable,
        Action<DeckCardItemUI, BaseCardData, PointerEventData> beginDragAction = null,
        Action<DeckCardItemUI, BaseCardData, PointerEventData> dragAction = null,
        Action<DeckCardItemUI, BaseCardData, PointerEventData> endDragAction = null
    )
    {
        isDraggable = draggable;
        onBeginDrag = beginDragAction;
        onDrag = dragAction;
        onEndDrag = endDragAction;
    }

    public BaseCardData GetCardData()
    {
        return cardData;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (cardData == null)
            return;

        if (isDragging)
            return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            HandleLeftClick();
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            onRightClick?.Invoke(cardData);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanDrag())
            return;

        isDragging = true;

        CacheCardImageIfNeeded();

        if (cardImage != null)
        {
            originalRaycastTarget = cardImage.raycastTarget;
            cardImage.raycastTarget = false;
        }

        onBeginDrag?.Invoke(this, cardData, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        if (cardData == null)
            return;

        onDrag?.Invoke(this, cardData, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        isDragging = false;

        if (cardImage != null)
            cardImage.raycastTarget = originalRaycastTarget;

        if (cardData == null)
            return;

        onEndDrag?.Invoke(this, cardData, eventData);
    }

    private bool CanDrag()
    {
        if (!isDraggable)
            return false;

        if (cardData == null)
            return false;

        return true;
    }

    private void HandleLeftClick()
    {
        float currentTime = Time.unscaledTime;

        bool isDoubleClick =
            lastLeftClickTime > 0f &&
            currentTime - lastLeftClickTime <= doubleClickTime;

        if (isDoubleClick)
        {
            lastLeftClickTime = -1f;

            if (onDoubleClick != null)
            {
                onDoubleClick.Invoke(cardData);
            }
            else
            {
                onLeftClick?.Invoke(cardData);
            }

            return;
        }

        lastLeftClickTime = currentTime;
        onLeftClick?.Invoke(cardData);
    }

    private void CacheCardImageIfNeeded()
    {
        if (cardImage != null)
            return;

        Transform cardImageTransform = transform.Find("CardImage");

        if (cardImageTransform != null)
            cardImage = cardImageTransform.GetComponent<Image>();

        if (cardImage == null)
            cardImage = GetComponent<Image>();
    }

    private void SetCardImage(BaseCardData card)
    {
        if (cardImage == null)
        {
            Debug.LogWarning("DeckCardItemUI: cardImage가 없습니다.");
            return;
        }

        if (string.IsNullOrEmpty(card.image))
        {
            Debug.LogWarning($"DeckCardItemUI: 카드 이미지 경로가 비어 있습니다. card={card.name}");
            cardImage.sprite = null;
            return;
        }

        Sprite sprite = Resources.Load<Sprite>(card.image);

        if (sprite == null)
        {
            Debug.LogWarning($"덱 카드 이미지를 찾을 수 없습니다: {card.image}");
            cardImage.sprite = null;
            return;
        }

        cardImage.sprite = sprite;
        cardImage.preserveAspect = true;
        cardImage.color = Color.white;
    }

    private void ClearVisual()
    {
        CacheCardImageIfNeeded();

        if (cardImage == null)
            return;

        cardImage.sprite = null;
        cardImage.color = Color.clear;
    }
}