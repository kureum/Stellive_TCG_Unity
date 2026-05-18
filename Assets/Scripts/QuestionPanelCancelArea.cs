using UnityEngine;
using UnityEngine.EventSystems;

public class QuestionPanelCancelArea : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private QuestionPanel questionPanel;

    private void Awake()
    {
        if (questionPanel == null)
            questionPanel = GetComponentInParent<QuestionPanel>();
    }

    public void SetQuestionPanel(QuestionPanel panel)
    {
        questionPanel = panel;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        if (questionPanel == null)
            return;

        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        questionPanel.CancelByRightClick();
    }
}