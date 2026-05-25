using UnityEngine;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    [Header("Preset Buttons")]
    [SerializeField] private Button[] presetButtons = new Button[5];

    [Header("Preset Highlight")]
    [SerializeField] private Color normalPresetButtonColor = Color.white;
    [SerializeField] private Color selectedPresetButtonColor = new Color(1f, 0.86f, 0.2f, 1f);
    [SerializeField] private Color selectedPresetHighlightedColor = new Color(1f, 0.92f, 0.45f, 1f);
    [SerializeField] private Color selectedPresetPressedColor = new Color(0.95f, 0.72f, 0.1f, 1f);

    private void Awake()
    {
        ResolvePresetButtonsIfNeeded();
        RefreshPresetButtonHighlights();
    }

    private void OnEnable()
    {
        RefreshPresetButtonHighlights();
    }

    public void SelectPreset1()
    {
        SelectPreset(0);
    }

    public void SelectPreset2()
    {
        SelectPreset(1);
    }

    public void SelectPreset3()
    {
        SelectPreset(2);
    }

    public void SelectPreset4()
    {
        SelectPreset(3);
    }

    public void SelectPreset5()
    {
        SelectPreset(4);
    }

    public void SelectPreset(int presetIndex)
    {
        BattleStartSettings.SelectMyPreset(presetIndex);
        RefreshPresetButtonHighlights();
        Debug.Log($"배틀 시작 프리셋 선택: Preset {presetIndex + 1}");
    }

    private void ResolvePresetButtonsIfNeeded()
    {
        for (int i = 0; i < presetButtons.Length; i++)
        {
            if (presetButtons[i] != null)
                continue;

            GameObject buttonObject = GameObject.Find($"DeckPreset{i + 1}");

            if (buttonObject == null)
                continue;

            presetButtons[i] = buttonObject.GetComponent<Button>();
        }
    }

    private void RefreshPresetButtonHighlights()
    {
        ResolvePresetButtonsIfNeeded();

        int selectedIndex = BattleStartSettings.SelectedMyPresetIndex;

        for (int i = 0; i < presetButtons.Length; i++)
        {
            Button button = presetButtons[i];

            if (button == null)
                continue;

            ApplyPresetButtonColor(button, i == selectedIndex);
        }
    }

    private void ApplyPresetButtonColor(Button button, bool isSelected)
    {
        ColorBlock colors = button.colors;

        if (isSelected)
        {
            colors.normalColor = selectedPresetButtonColor;
            colors.selectedColor = selectedPresetButtonColor;
            colors.highlightedColor = selectedPresetHighlightedColor;
            colors.pressedColor = selectedPresetPressedColor;
        }
        else
        {
            colors.normalColor = normalPresetButtonColor;
            colors.selectedColor = normalPresetButtonColor;
        }

        button.colors = colors;

        Graphic targetGraphic = button.targetGraphic;

        if (targetGraphic != null)
            targetGraphic.color = isSelected ? selectedPresetButtonColor : normalPresetButtonColor;
    }
}
