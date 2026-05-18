using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HorizontalScrollWheel : MonoBehaviour, IScrollHandler
{
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private float scrollSpeed = 0.02f;

    private void Awake()
    {
        if (scrollRect == null)
            scrollRect = GetComponent<ScrollRect>();
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (scrollRect == null) return;

        // 마우스 휠 위/아래 입력을 가로 스크롤로 변환
        float scrollDelta = eventData.scrollDelta.y;

        scrollRect.horizontalNormalizedPosition -= scrollDelta * scrollSpeed;

        // 0~1 범위 밖으로 나가지 않게 제한
        scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(scrollRect.horizontalNormalizedPosition);
    }
}