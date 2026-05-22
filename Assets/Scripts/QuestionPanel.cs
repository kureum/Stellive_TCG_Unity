using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class QuestionPanel : MonoBehaviour, IPointerClickHandler
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Text")]
    [SerializeField] private TMP_Text requestText;

    [Header("Summon Question Panel")]
    [SerializeField] private GameObject askSummonPanel;
    [SerializeField] private Button frontButton;
    [SerializeField] private Button backsideButton;

    [Header("Yes / No Question Panel")]
    [SerializeField] private GameObject askYesNoPanel;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private Action onFrontAction;
    private Action onBacksideAction;
    private Action onYesAction;
    private Action onNoAction;
    private Action onCancelAction;

    private bool isOpen;
    private bool enableRightClickCancel;

    private void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        Hide();
    }

    public bool IsOpen()
    {
        return isOpen;
    }

    public bool CanOpen()
    {
        return !isOpen;
    }

    public bool TryShowSummonQuestion(
        string message,
        bool canFront,
        bool canBackside,
        Action onFront,
        Action onBackside,
        Action onCancel)
    {
        if (isOpen)
            return false;

        isOpen = true;
        enableRightClickCancel = true;

        onFrontAction = onFront;
        onBacksideAction = onBackside;
        onYesAction = null;
        onNoAction = null;
        onCancelAction = onCancel;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (requestText != null)
            requestText.text = message;

        if (askSummonPanel != null)
            askSummonPanel.SetActive(true);

        if (askYesNoPanel != null)
            askYesNoPanel.SetActive(false);

        SetupButton(frontButton, canFront, OnClickFront);
        SetupButton(backsideButton, canBackside, OnClickBackside);

        return true;
    }

    public void ShowSummonQuestion(
        string message,
        bool canFront,
        bool canBackside,
        Action onFront,
        Action onBackside,
        Action onCancel)
    {
        TryShowSummonQuestion(
            message,
            canFront,
            canBackside,
            onFront,
            onBackside,
            onCancel
        );
    }

    public bool TryShowYesNoQuestion(
        string message,
        Action onYes,
        Action onNo,
        Action onCancel)
    {
        if (isOpen)
            return false;

        isOpen = true;
        enableRightClickCancel = true;

        onFrontAction = null;
        onBacksideAction = null;
        onYesAction = onYes;
        onNoAction = onNo;
        onCancelAction = onCancel;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (requestText != null)
            requestText.text = message;

        if (askSummonPanel != null)
            askSummonPanel.SetActive(false);

        if (askYesNoPanel != null)
            askYesNoPanel.SetActive(true);

        SetupButton(yesButton, true, OnClickYes);
        SetupButton(noButton, true, OnClickNo);

        return true;
    }

    public void ShowYesNoQuestion(
        string message,
        Action onYes,
        Action onNo,
        Action onCancel)
    {
        TryShowYesNoQuestion(message, onYes, onNo, onCancel);
    }

    public void Hide()
    {
        isOpen = false;
        enableRightClickCancel = false;

        onFrontAction = null;
        onBacksideAction = null;
        onYesAction = null;
        onNoAction = null;
        onCancelAction = null;

        if (askSummonPanel != null)
            askSummonPanel.SetActive(false);

        if (askYesNoPanel != null)
            askYesNoPanel.SetActive(false);

        ClearButton(frontButton);
        ClearButton(backsideButton);
        ClearButton(yesButton);
        ClearButton(noButton);

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    public void CancelByRightClick()
    {
        if (!isOpen)
            return;

        if (!enableRightClickCancel)
            return;

        Cancel();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        CancelByRightClick();
    }

    private void SetupButton(Button button, bool interactable, Action clickAction)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.interactable = interactable;

        if (clickAction != null)
            button.onClick.AddListener(() => clickAction());
    }

    private void ClearButton(Button button)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.interactable = true;
    }

    private void OnClickFront()
    {
        Action action = onFrontAction;
        Hide();
        action?.Invoke();
    }

    private void OnClickBackside()
    {
        Action action = onBacksideAction;
        Hide();
        action?.Invoke();
    }

    private void OnClickYes()
    {
        Action action = onYesAction;
        Hide();
        action?.Invoke();
    }

    private void OnClickNo()
    {
        Action action = onNoAction;
        Hide();
        action?.Invoke();
    }

    private void Cancel()
    {
        Action action = onCancelAction;
        Hide();
        action?.Invoke();
    }
}
