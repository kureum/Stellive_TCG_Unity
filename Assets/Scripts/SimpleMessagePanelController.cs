using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum SimpleMessageExitDirection
{
    None,
    LeftToRight,
    RightToLeft
}

public class SimpleMessagePanelController : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private float fadeInTime = 0.5f;
    [SerializeField] private float visibleTime = 2f;
    [SerializeField] private float fadeOutTime = 0.5f;
    [SerializeField] private float slideDistance = 900f;

    private Coroutine messageRoutine;
    private Vector2 initialAnchoredPosition;
    private bool hasInitialPosition;
    private bool isNonInterruptible;

    public bool IsPlaying { get; private set; }

    private void Awake()
    {
        ResolveReferences();
        DisableGraphicRaycasts();
        CacheInitialPosition();
        HideImmediate();
    }

    public void Configure(TMP_Text text, CanvasGroup group)
    {
        messageText = text != null ? text : messageText;
        canvasGroup = group != null ? group : canvasGroup;
        ResolveReferences();
        DisableGraphicRaycasts();
        CacheInitialPosition();
        HideImmediate();
    }

    public void SetTimings(float fadeIn, float visible, float fadeOut)
    {
        fadeInTime = Mathf.Max(0f, fadeIn);
        visibleTime = Mathf.Max(0f, visible);
        fadeOutTime = Mathf.Max(0f, fadeOut);
    }

    public void Show(string message)
    {
        Play(message, SimpleMessageExitDirection.None);
    }

    public Coroutine Play(string message, SimpleMessageExitDirection exitDirection)
    {
        return Play(message, exitDirection, visibleTime);
    }

    public Coroutine Play(string message, SimpleMessageExitDirection exitDirection, float overrideVisibleTime)
    {
        return Play(message, exitDirection, overrideVisibleTime, false);
    }

    public Coroutine Play(
        string message,
        SimpleMessageExitDirection exitDirection,
        float overrideVisibleTime,
        bool nonInterruptible)
    {
        ResolveReferences();
        DisableGraphicRaycasts();
        CacheInitialPosition();

        if (isNonInterruptible && messageRoutine != null)
        {
            Debug.Log($"SimpleMessagePanel: non-interruptible message active. Ignored message={message}");
            return messageRoutine;
        }

        if (messageRoutine != null)
            StopCoroutine(messageRoutine);

        messageRoutine = StartCoroutine(PlayRoutine(message, exitDirection, overrideVisibleTime, nonInterruptible));
        return messageRoutine;
    }

    private IEnumerator PlayRoutine(
        string message,
        SimpleMessageExitDirection exitDirection,
        float overrideVisibleTime,
        bool nonInterruptible)
    {
        IsPlaying = true;
        isNonInterruptible = nonInterruptible;

        if (messageText != null)
            messageText.text = message;

        if (panelRect != null && hasInitialPosition)
            panelRect.anchoredPosition = initialAnchoredPosition;

        if (canvasGroup == null)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, overrideVisibleTime));
            IsPlaying = false;
            isNonInterruptible = false;
            messageRoutine = null;
            yield break;
        }

        canvasGroup.gameObject.SetActive(true);
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        yield return FadeRoutine(0f, 1f, fadeInTime);

        float safeVisibleTime = Mathf.Max(0f, overrideVisibleTime);

        if (safeVisibleTime > 0f)
            yield return new WaitForSeconds(safeVisibleTime);

        if (exitDirection == SimpleMessageExitDirection.None)
        {
            yield return FadeRoutine(1f, 0f, fadeOutTime);
        }
        else
        {
            yield return SlideOutRoutine(exitDirection);
        }

        HideImmediate();
        IsPlaying = false;
        isNonInterruptible = false;
        messageRoutine = null;
    }

    private IEnumerator FadeRoutine(float from, float to, float duration)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            canvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        canvasGroup.alpha = to;
    }

    private IEnumerator SlideOutRoutine(SimpleMessageExitDirection direction)
    {
        if (panelRect == null)
        {
            yield return FadeRoutine(1f, 0f, fadeOutTime);
            yield break;
        }

        Vector2 start = hasInitialPosition ? initialAnchoredPosition : panelRect.anchoredPosition;
        float signedDistance = direction == SimpleMessageExitDirection.LeftToRight
            ? Mathf.Abs(slideDistance)
            : -Mathf.Abs(slideDistance);
        Vector2 end = start + new Vector2(signedDistance, 0f);
        float safeDuration = Mathf.Max(0.01f, fadeOutTime);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            panelRect.anchoredPosition = Vector2.Lerp(start, end, easedT);
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, easedT);
            yield return null;
        }

        panelRect.anchoredPosition = end;
        canvasGroup.alpha = 0f;
    }

    private void ResolveReferences()
    {
        if (messageText == null)
            messageText = GetComponentInChildren<TMP_Text>(true);

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null && messageText != null)
            canvasGroup = messageText.GetComponentInParent<CanvasGroup>();

        if (panelRect == null && canvasGroup != null)
            panelRect = canvasGroup.GetComponent<RectTransform>();

        if (panelRect == null)
            panelRect = transform as RectTransform;
    }

    private void DisableGraphicRaycasts()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            if (graphic != null)
                graphic.raycastTarget = false;
        }
    }

    private void CacheInitialPosition()
    {
        if (panelRect == null || hasInitialPosition)
            return;

        initialAnchoredPosition = panelRect.anchoredPosition;
        hasInitialPosition = true;
    }

    private void HideImmediate()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (panelRect != null && hasInitialPosition)
            panelRect.anchoredPosition = initialAnchoredPosition;
    }
}
