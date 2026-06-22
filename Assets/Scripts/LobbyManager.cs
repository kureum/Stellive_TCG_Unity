using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    [Header("Preset Buttons")]
    [SerializeField] private Button[] presetButtons = new Button[5];

    [Header("Room Code UI")]
    [SerializeField] private bool createMissingRoomUiAtRuntime = true;
    [SerializeField] private bool handOffRoomUiToPhotonLobbyManager = true;
    [SerializeField] private Button codeInputButton;
    [SerializeField] private GameObject roomNumberPanel;
    [SerializeField] private TMP_InputField roomNumberInputField;
    [SerializeField] private Button numberEnterButton;
    [SerializeField] private Button roomCancelButton;
    [SerializeField] private TMP_Text roomGuideText;
    [SerializeField] private Button battleStartButton;
    [SerializeField] private Button[] additionalLobbyInputButtons;

    [Header("Preset Highlight")]
    [SerializeField] private Color normalPresetButtonColor = Color.white;
    [SerializeField] private Color selectedPresetButtonColor = new Color(1f, 0.86f, 0.2f, 1f);
    [SerializeField] private Color selectedPresetHighlightedColor = new Color(1f, 0.92f, 0.45f, 1f);
    [SerializeField] private Color selectedPresetPressedColor = new Color(0.95f, 0.72f, 0.1f, 1f);

    [Header("Room State")]
    [SerializeField] private LobbyRoomState currentRoomState = LobbyRoomState.Idle;
    [SerializeField] private string currentRoomCode = "";
    [SerializeField] private bool isHostWaiting = false;
    [SerializeField] private bool isMatched = false;

    private string localUserId;

    public LobbyRoomState CurrentRoomState => currentRoomState;
    public string CurrentRoomCode => currentRoomCode;
    public bool IsHostWaiting => isHostWaiting;
    public bool IsMatched => isMatched;

    private void Awake()
    {
        localUserId = $"local-{SystemInfo.deviceUniqueIdentifier}";
        bool photonRoomFlowActive =
            handOffRoomUiToPhotonLobbyManager &&
            GetComponent("PhotonLobbyManager") != null;

        ResolvePresetButtonsIfNeeded();

        if (!photonRoomFlowActive)
        {
            ResolveRoomUiIfNeeded();
            SetupRoomUi();
            SetRoomState(LobbyRoomState.Idle);
        }

        BattleStartSettings.SetLocalTestMode();
        RefreshPresetButtonHighlights();
    }

    private void OnEnable()
    {
        RefreshPresetButtonHighlights();
    }

    private void OnDestroy()
    {
        if (codeInputButton != null)
            codeInputButton.onClick.RemoveListener(OpenRoomNumberPanel);

        if (numberEnterButton != null)
            numberEnterButton.onClick.RemoveListener(OnClickNumberEnterButton);

        if (roomCancelButton != null)
            roomCancelButton.onClick.RemoveListener(OnClickRoomCancelButton);

        if (roomNumberInputField != null)
            roomNumberInputField.onValueChanged.RemoveListener(OnRoomCodeInputValueChanged);
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
        if (currentRoomState == LobbyRoomState.HostingWaiting ||
            currentRoomState == LobbyRoomState.Joining ||
            currentRoomState == LobbyRoomState.Matched)
        {
            Debug.LogWarning($"[LobbyRoom] Preset selection blocked. state={currentRoomState}");
            return;
        }

        BattleStartSettings.SelectMyPreset(presetIndex);
        BattleStartSettings.SetLocalTestMode();
        RefreshPresetButtonHighlights();
        Debug.Log($"배틀 시작 프리셋 선택: Preset {presetIndex + 1}");
    }

    public void OpenRoomNumberPanel()
    {
        ResolveRoomUiIfNeeded();

        currentRoomCode = "";
        SetRoomState(LobbyRoomState.RoomPanelOpen);

        if (roomNumberInputField != null)
        {
            roomNumberInputField.text = "";
            roomNumberInputField.ActivateInputField();
            roomNumberInputField.Select();
        }

        SetRoomGuideText("6자리 방 번호를 입력하세요.");
        Debug.Log("[LobbyRoom] RoomNumberPanel opened");
    }

    private void OnClickNumberEnterButton()
    {
        string roomCode = roomNumberInputField != null
            ? roomNumberInputField.text.Trim()
            : "";

        if (!IsValidRoomCode(roomCode))
        {
            SetRoomGuideText("6자리 숫자를 입력하세요");
            Debug.LogWarning($"[LobbyRoom] Invalid room code: {roomCode}");
            return;
        }

        SetRoomState(LobbyRoomState.Joining);
        SetRoomGuideText("방에 입장 중입니다.");

        if (!MockRoomService.TryEnterRoom(roomCode, localUserId, out RoomEnterResult result))
            result = RoomEnterResult.Invalid;

        HandleRoomEnterResult(roomCode, result);
    }

    private void OnClickRoomCancelButton()
    {
        if (currentRoomState == LobbyRoomState.HostingWaiting)
        {
            bool cancelled = MockRoomService.CancelHostRoom(currentRoomCode, localUserId);
            Debug.Log($"[LobbyRoom] Cancel host. roomCode={currentRoomCode}, result={cancelled}");
        }

        currentRoomCode = "";
        isHostWaiting = false;
        isMatched = false;
        BattleStartSettings.ClearOnlineSettings();
        SetRoomState(LobbyRoomState.Idle);
    }

    public bool DebugMockGuestJoin(string roomCode)
    {
        string targetCode = !string.IsNullOrWhiteSpace(roomCode)
            ? roomCode.Trim()
            : currentRoomCode;

        if (string.IsNullOrWhiteSpace(targetCode))
        {
            Debug.LogWarning("[LobbyRoom] mockguestjoin failed: roomCode is empty.");
            return false;
        }

        bool joined = MockRoomService.SimulateGuestJoin(targetCode, $"{localUserId}-debugGuest");

        if (!joined)
        {
            Debug.LogWarning($"[LobbyRoom] mockguestjoin failed. roomCode={targetCode}");
            return false;
        }

        if (currentRoomState == LobbyRoomState.HostingWaiting &&
            string.Equals(currentRoomCode, targetCode, StringComparison.OrdinalIgnoreCase))
        {
            HandleMatchedAsHost(targetCode);
        }

        Debug.Log($"[LobbyRoom] Matched as host by mock guest. roomCode={targetCode}");
        return true;
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

    private void ResolveRoomUiIfNeeded()
    {
        if (codeInputButton == null)
            codeInputButton = FindButton("CodeInputButton");

        if (roomNumberPanel == null)
            roomNumberPanel = GameObject.Find("RoomNumberPanel");

        if (roomNumberInputField == null)
        {
            GameObject inputObject = GameObject.Find("RoomNumberInputField");
            if (inputObject != null)
                roomNumberInputField = inputObject.GetComponent<TMP_InputField>();
        }

        if (numberEnterButton == null)
            numberEnterButton = FindButton("NumberEnterButton");

        if (roomCancelButton == null)
            roomCancelButton = FindButton("RoomCancelButton");

        if (roomGuideText == null)
        {
            GameObject textObject = GameObject.Find("RoomGuideText");
            if (textObject != null)
                roomGuideText = textObject.GetComponent<TMP_Text>();
        }

        if (battleStartButton == null)
            battleStartButton = FindButton("GoToBattleButton") ?? FindButton("BattleStartButton");

        if (createMissingRoomUiAtRuntime)
            CreateMissingRoomUiIfNeeded();
    }

    private void CreateMissingRoomUiIfNeeded()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform parent = canvas.transform;

        if (codeInputButton == null)
            codeInputButton = CreateRuntimeButton(parent, "CodeInputButton", "방 코드", new Vector2(0f, -170f), new Vector2(150f, 46f));

        if (roomNumberPanel == null)
            roomNumberPanel = CreateRoomNumberPanel(parent);

        if (roomNumberPanel == null)
            return;

        Transform panelTransform = roomNumberPanel.transform;

        if (roomGuideText == null)
            roomGuideText = CreateRuntimeText(panelTransform, "RoomGuideText", "6자리 방 번호를 입력하세요.", new Vector2(0f, 70f), new Vector2(320f, 34f), 18f);

        if (roomNumberInputField == null)
            roomNumberInputField = CreateRuntimeInputField(panelTransform, "RoomNumberInputField", new Vector2(0f, 18f), new Vector2(210f, 42f));

        if (numberEnterButton == null)
            numberEnterButton = CreateRuntimeButton(panelTransform, "NumberEnterButton", "입장", new Vector2(-58f, -45f), new Vector2(96f, 38f));

        if (roomCancelButton == null)
            roomCancelButton = CreateRuntimeButton(panelTransform, "RoomCancelButton", "취소", new Vector2(58f, -45f), new Vector2(96f, 38f));
    }

    private GameObject CreateRoomNumberPanel(Transform parent)
    {
        GameObject panel = new GameObject("RoomNumberPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(380f, 210f);

        Image image = panel.GetComponent<Image>();
        image.color = new Color(0.08f, 0.09f, 0.12f, 0.94f);

        return panel;
    }

    private Button CreateRuntimeButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.36f, 0.72f, 1f);

        TMP_Text text = CreateRuntimeText(buttonObject.transform, $"{objectName}_Text", label, Vector2.zero, size, 18f);
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        return buttonObject.GetComponent<Button>();
    }

    private TMP_Text CreateRuntimeText(
        Transform parent,
        string objectName,
        string text,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TMP_Text tmpText = textObject.GetComponent<TMP_Text>();
        tmpText.text = text;
        tmpText.fontSize = fontSize;
        tmpText.color = Color.white;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.textWrappingMode = TextWrappingModes.NoWrap;

        return tmpText;
    }

    private TMP_InputField CreateRuntimeInputField(
        Transform parent,
        string objectName,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        GameObject inputObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputObject.transform.SetParent(parent, false);

        RectTransform rect = inputObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = inputObject.GetComponent<Image>();
        image.color = Color.white;

        TMP_Text text = CreateRuntimeText(inputObject.transform, "Text", "", Vector2.zero, new Vector2(size.x - 24f, size.y - 8f), 22f);
        text.color = Color.black;
        text.alignment = TextAlignmentOptions.Center;

        TMP_Text placeholder = CreateRuntimeText(inputObject.transform, "Placeholder", "000000", Vector2.zero, new Vector2(size.x - 24f, size.y - 8f), 18f);
        placeholder.color = new Color(0f, 0f, 0f, 0.35f);
        placeholder.alignment = TextAlignmentOptions.Center;

        TMP_InputField inputField = inputObject.GetComponent<TMP_InputField>();
        inputField.textComponent = text;
        inputField.placeholder = placeholder;
        inputField.characterLimit = 6;
        inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        inputField.lineType = TMP_InputField.LineType.SingleLine;

        return inputField;
    }

    private void SetupRoomUi()
    {
        if (codeInputButton != null)
        {
            codeInputButton.onClick.RemoveListener(OpenRoomNumberPanel);
            codeInputButton.onClick.AddListener(OpenRoomNumberPanel);
        }

        if (numberEnterButton != null)
        {
            numberEnterButton.onClick.RemoveListener(OnClickNumberEnterButton);
            numberEnterButton.onClick.AddListener(OnClickNumberEnterButton);
        }

        if (roomCancelButton != null)
        {
            roomCancelButton.onClick.RemoveListener(OnClickRoomCancelButton);
            roomCancelButton.onClick.AddListener(OnClickRoomCancelButton);
        }

        if (roomNumberInputField != null)
        {
            roomNumberInputField.characterLimit = 6;
            roomNumberInputField.contentType = TMP_InputField.ContentType.IntegerNumber;
            roomNumberInputField.onValueChanged.RemoveListener(OnRoomCodeInputValueChanged);
            roomNumberInputField.onValueChanged.AddListener(OnRoomCodeInputValueChanged);
        }
    }

    private Button FindButton(string objectName)
    {
        GameObject buttonObject = GameObject.Find(objectName);
        return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
    }

    private void OnRoomCodeInputValueChanged(string value)
    {
        if (roomNumberInputField == null)
            return;

        string filtered = FilterRoomCode(value);
        if (filtered != value)
        {
            roomNumberInputField.text = filtered;
            roomNumberInputField.caretPosition = filtered.Length;
        }
    }

    private string FilterRoomCode(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        char[] buffer = new char[Mathf.Min(6, value.Length)];
        int count = 0;

        foreach (char c in value)
        {
            if (!char.IsDigit(c))
                continue;

            buffer[count++] = c;
            if (count >= 6)
                break;
        }

        return new string(buffer, 0, count);
    }

    private bool IsValidRoomCode(string roomCode)
    {
        if (string.IsNullOrWhiteSpace(roomCode) || roomCode.Length != 6)
            return false;

        foreach (char c in roomCode)
        {
            if (!char.IsDigit(c))
                return false;
        }

        return true;
    }

    private void HandleRoomEnterResult(string roomCode, RoomEnterResult result)
    {
        switch (result)
        {
            case RoomEnterResult.BecameHost:
                currentRoomCode = roomCode;
                isHostWaiting = true;
                isMatched = false;
                SetRoomState(LobbyRoomState.HostingWaiting);
                SetRoomGuideText("방을 만들었습니다. 상대 유저를 기다리는 중입니다.");
                Debug.Log($"[LobbyRoom] Became host. roomCode={roomCode}");
                break;

            case RoomEnterResult.MatchedAsGuest:
                currentRoomCode = roomCode;
                isHostWaiting = false;
                isMatched = true;
                BattleStartSettings.SetOnlineClientMode(roomCode);
                SetRoomState(LobbyRoomState.Matched);
                SetRoomGuideText("방에 입장했습니다. 배틀을 시작합니다.");
                Debug.Log($"[LobbyRoom] Matched as guest. roomCode={roomCode}");
                break;

            case RoomEnterResult.Occupied:
                SetRoomState(LobbyRoomState.OccupiedFailed);
                SetRoomGuideText("다른 번호를 입력하세요");
                Debug.LogWarning($"[LobbyRoom] Room occupied. roomCode={roomCode}");
                break;

            case RoomEnterResult.AlreadyHosting:
                currentRoomCode = roomCode;
                isHostWaiting = true;
                SetRoomState(LobbyRoomState.HostingWaiting);
                SetRoomGuideText("이미 이 방에서 상대를 기다리는 중입니다.");
                Debug.Log($"[LobbyRoom] Already hosting. roomCode={roomCode}");
                break;

            default:
                SetRoomState(LobbyRoomState.RoomPanelOpen);
                SetRoomGuideText("6자리 숫자를 입력하세요");
                Debug.LogWarning($"[LobbyRoom] Invalid room enter result. roomCode={roomCode}, result={result}");
                break;
        }
    }

    private void HandleMatchedAsHost(string roomCode)
    {
        currentRoomCode = roomCode;
        isHostWaiting = false;
        isMatched = true;
        BattleStartSettings.SetOnlineHostMode(roomCode);
        SetRoomState(LobbyRoomState.Matched);
        SetRoomGuideText("상대와 연결되었습니다. 배틀을 시작합니다.");
        Debug.Log($"[LobbyRoom] Matched as host. roomCode={roomCode}");
    }

    private void SetRoomState(LobbyRoomState state)
    {
        currentRoomState = state;

        switch (state)
        {
            case LobbyRoomState.Idle:
                SetRoomPanelActive(false);
                SetLobbyInteractable(true);
                SetRoomPanelInputMode(true, true, true);
                break;

            case LobbyRoomState.RoomPanelOpen:
                SetRoomPanelActive(true);
                SetLobbyInteractable(true);
                SetRoomPanelInputMode(true, true, true);
                break;

            case LobbyRoomState.HostingWaiting:
                SetRoomPanelActive(true);
                SetLobbyInteractable(false);
                SetRoomPanelInputMode(false, false, true);
                break;

            case LobbyRoomState.Joining:
                SetRoomPanelActive(true);
                SetLobbyInteractable(false);
                SetRoomPanelInputMode(false, false, false);
                break;

            case LobbyRoomState.Matched:
                SetRoomPanelActive(true);
                SetLobbyInteractable(false);
                SetRoomPanelInputMode(false, false, false);
                break;

            case LobbyRoomState.OccupiedFailed:
                SetRoomPanelActive(true);
                SetLobbyInteractable(true);
                SetRoomPanelInputMode(true, true, true);
                break;
        }
    }

    private void SetRoomPanelActive(bool active)
    {
        if (roomNumberPanel != null)
            roomNumberPanel.SetActive(active);
    }

    private void SetRoomPanelInputMode(
        bool inputInteractable,
        bool enterInteractable,
        bool cancelInteractable)
    {
        if (roomNumberInputField != null)
            roomNumberInputField.interactable = inputInteractable;

        if (numberEnterButton != null)
            numberEnterButton.interactable = enterInteractable;

        if (roomCancelButton != null)
            roomCancelButton.interactable = cancelInteractable;
    }

    private void SetLobbyInteractable(bool interactable)
    {
        foreach (Button button in presetButtons)
        {
            if (button != null)
                button.interactable = interactable;
        }

        if (battleStartButton != null)
            battleStartButton.interactable = interactable;

        if (codeInputButton != null)
            codeInputButton.interactable = interactable;

        if (additionalLobbyInputButtons == null)
            return;

        foreach (Button button in additionalLobbyInputButtons)
        {
            if (button != null)
                button.interactable = interactable;
        }
    }

    private void SetRoomGuideText(string message)
    {
        if (roomGuideText != null)
            roomGuideText.text = message;
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
