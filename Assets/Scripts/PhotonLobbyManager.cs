using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PhotonLobbyManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const byte HelloEventCode = 1;
    private const int RoomCodeLength = 6;
    private const int MaxCreateRoomAttempts = 5;

    [Header("Photon")]
    [SerializeField] private string gameVersion = "stellive-tcg-lobby-v1";
    [SerializeField] private string fallbackNickNamePrefix = "Player";
    [SerializeField] private bool connectOnStart = true;

    [Header("Existing Room Code UI")]
    [SerializeField] private Button codeInputButton;
    [SerializeField] private GameObject roomNumberPanel;
    [SerializeField] private TMP_InputField roomNumberInputField;
    [SerializeField] private Button numberEnterButton;
    [SerializeField] private Button roomCancelButton;
    [SerializeField] private TMP_Text roomGuideText;
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text networkLogText;

    [Header("Online Test Buttons")]
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button sendHelloButton;
    [SerializeField] private bool createMissingOnlineButtonsAtRuntime = true;

    [Header("Lobby Buttons To Lock In Room")]
    [SerializeField] private Button[] lobbyButtonsToLock;

    private readonly List<string> logLines = new List<string>();
    private string pendingCreateRoomCode = "";
    private int createRoomAttemptCount;
    private bool isJoiningRoom;

    private void Awake()
    {
        ResolveReferencesIfNeeded();
        CreateMissingOnlineButtonsIfNeeded();
        ConfigureInputField();
        WireButtons();
        SetRoomPanelActive(false);
        SetRoomControls(false, false, true);
        SetLobbyButtonsInteractable(true);
        UpdateLobbyStatusText("Photon 연결 준비 중...");
    }

    private void Start()
    {
        if (connectOnStart)
            ConnectToPhoton();
    }

    public override void OnEnable()
    {
        base.OnEnable();
    }

    public override void OnDisable()
    {
        base.OnDisable();
    }

    private void OnDestroy()
    {
        UnwireButtons();
    }

    public void ConnectToPhoton()
    {
        EnsureNickName();

        PhotonNetwork.AutomaticallySyncScene = false;
        PhotonNetwork.GameVersion = gameVersion;

        if (PhotonNetwork.IsConnectedAndReady)
        {
            JoinLobbyIfNeeded();
            return;
        }

        UpdateLobbyStatusText("Photon 서버에 연결 중...");
        PhotonNetwork.ConnectUsingSettings();
    }

    public void CreateRoom()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            UpdateLobbyStatusText("Photon 연결이 아직 준비되지 않았습니다.");
            ConnectToPhoton();
            return;
        }

        if (PhotonNetwork.InRoom)
        {
            UpdateRoomStateText("이미 방에 입장해 있습니다.");
            return;
        }

        createRoomAttemptCount = 0;
        TryCreateRoomWithNewCode();
    }

    public void JoinRoom()
    {
        string roomCode = roomNumberInputField != null ? roomNumberInputField.text.Trim() : "";

        if (!IsValidRoomCode(roomCode))
        {
            UpdateLobbyStatusText("6자리 숫자 RoomCode를 입력하세요.");
            return;
        }

        if (!PhotonNetwork.IsConnectedAndReady)
        {
            UpdateLobbyStatusText("Photon 연결이 아직 준비되지 않았습니다.");
            ConnectToPhoton();
            return;
        }

        if (PhotonNetwork.InRoom)
        {
            UpdateRoomStateText("이미 방에 입장해 있습니다.");
            return;
        }

        isJoiningRoom = true;
        SetRoomControls(false, false, true);
        UpdateLobbyStatusText($"방 {roomCode}에 입장 중...");
        PhotonNetwork.JoinRoom(roomCode);
    }

    public void LeaveRoom()
    {
        if (PhotonNetwork.InRoom)
        {
            UpdateRoomStateText("방에서 나가는 중...");
            PhotonNetwork.LeaveRoom();
            return;
        }

        ResetToLobbyUi("로비 대기 중입니다.");
    }

    public void SendHello()
    {
        if (!PhotonNetwork.InRoom)
        {
            UpdateLobbyStatusText("hello를 보내려면 먼저 방에 입장해야 합니다.");
            return;
        }

        string message = $"{PhotonNetwork.NickName}: hello";
        RaiseEventOptions eventOptions = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.All
        };

        bool sent = PhotonNetwork.RaiseEvent(
            HelloEventCode,
            message,
            eventOptions,
            SendOptions.SendReliable);

        AppendNetworkLog(sent ? $"Sent {message}" : "hello 전송 실패");
    }

    public void UpdateLobbyStatusText(string message)
    {
        SetStatusText(message);
    }

    public void UpdateRoomStateText()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
        {
            UpdateLobbyStatusText("로비 대기 중입니다.");
            return;
        }

        string role = PhotonNetwork.IsMasterClient ? "Host" : "Client";
        string roomCode = PhotonNetwork.CurrentRoom.Name;
        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
        int maxPlayers = PhotonNetwork.CurrentRoom.MaxPlayers;

        if (roomCodeText != null)
            roomCodeText.text = roomCode;

        if (roleText != null)
            roleText.text = role;

        if (playerCountText != null)
            playerCountText.text = $"{playerCount}/{maxPlayers}";

        UpdateRoomStateText($"RoomCode {roomCode} | {role} | {playerCount}/{maxPlayers}");
    }

    public void UpdateRoomStateText(string message)
    {
        SetStatusText(message);
    }

    public string GenerateRoomCode()
    {
        return UnityEngine.Random.Range(0, 1000000).ToString("D6");
    }

    public override void OnConnectedToMaster()
    {
        UpdateLobbyStatusText("Photon 서버 연결 성공. 로비 입장 중...");
        JoinLobbyIfNeeded();
    }

    public override void OnJoinedLobby()
    {
        ResetToLobbyUi("Photon 로비 입장 완료. 방 생성 또는 RoomCode 입장이 가능합니다.");
    }

    public override void OnCreatedRoom()
    {
        UpdateRoomStateText($"방 생성 완료. RoomCode {PhotonNetwork.CurrentRoom.Name}. 상대 입장 대기 중...");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        if (createRoomAttemptCount < MaxCreateRoomAttempts)
        {
            AppendNetworkLog($"RoomCode 중복 또는 생성 실패. 재시도 중... ({returnCode}: {message})");
            TryCreateRoomWithNewCode();
            return;
        }

        pendingCreateRoomCode = "";
        SetLobbyButtonsInteractable(true);
        UpdateLobbyStatusText($"방 생성 실패: {message} ({returnCode})");
    }

    public override void OnJoinedRoom()
    {
        isJoiningRoom = false;

        SetRoomPanelActive(true);
        SetRoomControls(false, false, true);
        SetLobbyButtonsInteractable(false);

        if (PhotonNetwork.IsMasterClient)
            BattleStartSettings.SetOnlineHostMode(PhotonNetwork.CurrentRoom.Name);
        else
            BattleStartSettings.SetOnlineClientMode(PhotonNetwork.CurrentRoom.Name);

        AppendNetworkLog($"Joined room {PhotonNetwork.CurrentRoom.Name}");
        UpdateRoomStateText();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        isJoiningRoom = false;
        SetRoomControls(true, true, true);
        UpdateLobbyStatusText($"방 입장 실패: {message} ({returnCode})");
    }

    public override void OnLeftRoom()
    {
        BattleStartSettings.ClearOnlineSettings();
        pendingCreateRoomCode = "";
        isJoiningRoom = false;
        ResetToLobbyUi("방에서 나왔습니다. 로비 대기 중입니다.");
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        AppendNetworkLog($"{newPlayer.NickName} entered room");
        UpdateRoomStateText();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        AppendNetworkLog($"{otherPlayer.NickName} left room");
        UpdateRoomStateText();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        AppendNetworkLog($"MasterClient switched to {newMasterClient.NickName}");
        UpdateRoomStateText();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        BattleStartSettings.ClearOnlineSettings();
        SetLobbyButtonsInteractable(true);
        SetRoomControls(false, false, true);
        UpdateLobbyStatusText($"Photon 연결 해제: {cause}");
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != HelloEventCode)
            return;

        string message = photonEvent.CustomData as string ?? photonEvent.CustomData?.ToString() ?? "";
        AppendNetworkLog($"Received {message}");
    }

    private void TryCreateRoomWithNewCode()
    {
        createRoomAttemptCount++;
        pendingCreateRoomCode = GenerateRoomCode();

        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = 2,
            IsVisible = false,
            IsOpen = true
        };

        SetLobbyButtonsInteractable(false);
        SetRoomPanelActive(true);
        SetRoomControls(false, false, true);
        UpdateLobbyStatusText($"RoomCode {pendingCreateRoomCode} 생성 중...");
        PhotonNetwork.CreateRoom(pendingCreateRoomCode, roomOptions);
    }

    private void JoinLobbyIfNeeded()
    {
        if (PhotonNetwork.InLobby)
        {
            ResetToLobbyUi("Photon 로비 입장 완료. 방 생성 또는 RoomCode 입장이 가능합니다.");
            return;
        }

        PhotonNetwork.JoinLobby();
    }

    private void OpenJoinRoomPanel()
    {
        SetRoomPanelActive(true);
        SetRoomControls(true, true, true);

        if (roomNumberInputField != null)
        {
            roomNumberInputField.text = "";
            roomNumberInputField.ActivateInputField();
            roomNumberInputField.Select();
        }

        UpdateLobbyStatusText("입장할 6자리 RoomCode를 입력하세요.");
    }

    private void ResetToLobbyUi(string message)
    {
        SetRoomPanelActive(false);
        SetRoomControls(true, true, true);
        SetLobbyButtonsInteractable(true);

        if (roomCodeText != null)
            roomCodeText.text = "";

        if (roleText != null)
            roleText.text = "";

        if (playerCountText != null)
            playerCountText.text = "";

        UpdateLobbyStatusText(message);
    }

    private void ResolveReferencesIfNeeded()
    {
        if (codeInputButton == null)
            codeInputButton = FindSceneComponent<Button>("CodeInputButton");

        if (roomNumberPanel == null)
            roomNumberPanel = FindSceneObject("RoomNumberPanel");

        if (roomNumberInputField == null)
            roomNumberInputField = FindSceneComponent<TMP_InputField>("RoomNumberInputField")
                ?? FindSceneComponent<TMP_InputField>("RoomNunberInputField (TMP)")
                ?? FindSceneComponent<TMP_InputField>("RoomNumberInputField (TMP)");

        if (numberEnterButton == null)
            numberEnterButton = FindSceneComponent<Button>("NumberEnterButton");

        if (roomCancelButton == null)
            roomCancelButton = FindSceneComponent<Button>("RoomCancelButton");

        if (roomGuideText == null)
            roomGuideText = FindSceneComponent<TMP_Text>("RoomGuideText")
                ?? FindSceneComponent<TMP_Text>("RoomGuideText (TMP)");

        if (networkLogText == null)
            networkLogText = FindSceneComponent<TMP_Text>("NetworkLogText");

        if (roomCodeText == null)
            roomCodeText = FindSceneComponent<TMP_Text>("RoomCodeText");

        if (roleText == null)
            roleText = FindSceneComponent<TMP_Text>("RoleText");

        if (playerCountText == null)
            playerCountText = FindSceneComponent<TMP_Text>("PlayerCountText");

        if (createRoomButton == null)
            createRoomButton = FindSceneComponent<Button>("CreateRoomButton");

        if (sendHelloButton == null)
            sendHelloButton = FindSceneComponent<Button>("SendHelloButton");

        if (lobbyButtonsToLock == null || lobbyButtonsToLock.Length == 0)
        {
            lobbyButtonsToLock = new[]
            {
                FindSceneComponent<Button>("DeckPreset1"),
                FindSceneComponent<Button>("DeckPreset2"),
                FindSceneComponent<Button>("DeckPreset3"),
                FindSceneComponent<Button>("DeckPreset4"),
                FindSceneComponent<Button>("DeckPreset5"),
                createRoomButton,
                codeInputButton
            };
        }
    }

    private void CreateMissingOnlineButtonsIfNeeded()
    {
        if (!createMissingOnlineButtonsAtRuntime)
            return;

        Transform gameRoomPanel = FindSceneTransform("GameRoomPanel");
        Transform roomPanel = roomNumberPanel != null ? roomNumberPanel.transform : null;

        if (createRoomButton == null && gameRoomPanel != null)
        {
            createRoomButton = CreateRuntimeButton(
                gameRoomPanel,
                "CreateRoomButton",
                "방 만들기",
                new Vector2(0f, -180f),
                new Vector2(333f, 70f));
        }

        if (sendHelloButton == null && roomPanel != null)
        {
            sendHelloButton = CreateRuntimeButton(
                roomPanel,
                "SendHelloButton",
                "hello",
                new Vector2(0f, -108f),
                new Vector2(180f, 44f));
        }

        if (networkLogText == null && roomPanel != null)
        {
            networkLogText = CreateRuntimeText(
                roomPanel,
                "NetworkLogText",
                "",
                new Vector2(0f, -154f),
                new Vector2(900f, 64f),
                22f);
        }
    }

    private void ConfigureInputField()
    {
        if (roomNumberInputField == null)
            return;

        roomNumberInputField.characterLimit = RoomCodeLength;
        roomNumberInputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        roomNumberInputField.lineType = TMP_InputField.LineType.SingleLine;
        roomNumberInputField.onValueChanged.RemoveListener(OnRoomCodeInputValueChanged);
        roomNumberInputField.onValueChanged.AddListener(OnRoomCodeInputValueChanged);
    }

    private void WireButtons()
    {
        if (createRoomButton != null)
        {
            createRoomButton.onClick.RemoveListener(CreateRoom);
            createRoomButton.onClick.AddListener(CreateRoom);
        }

        if (codeInputButton != null)
        {
            codeInputButton.onClick.RemoveListener(OpenJoinRoomPanel);
            codeInputButton.onClick.AddListener(OpenJoinRoomPanel);
        }

        if (numberEnterButton != null)
        {
            numberEnterButton.onClick.RemoveListener(JoinRoom);
            numberEnterButton.onClick.AddListener(JoinRoom);
        }

        if (roomCancelButton != null)
        {
            roomCancelButton.onClick.RemoveListener(LeaveRoom);
            roomCancelButton.onClick.AddListener(LeaveRoom);
        }

        if (sendHelloButton != null)
        {
            sendHelloButton.onClick.RemoveListener(SendHello);
            sendHelloButton.onClick.AddListener(SendHello);
        }
    }

    private void UnwireButtons()
    {
        if (createRoomButton != null)
            createRoomButton.onClick.RemoveListener(CreateRoom);

        if (codeInputButton != null)
            codeInputButton.onClick.RemoveListener(OpenJoinRoomPanel);

        if (numberEnterButton != null)
            numberEnterButton.onClick.RemoveListener(JoinRoom);

        if (roomCancelButton != null)
            roomCancelButton.onClick.RemoveListener(LeaveRoom);

        if (sendHelloButton != null)
            sendHelloButton.onClick.RemoveListener(SendHello);

        if (roomNumberInputField != null)
            roomNumberInputField.onValueChanged.RemoveListener(OnRoomCodeInputValueChanged);
    }

    private void OnRoomCodeInputValueChanged(string value)
    {
        if (roomNumberInputField == null)
            return;

        string filtered = FilterRoomCode(value);
        if (filtered == value)
            return;

        roomNumberInputField.text = filtered;
        roomNumberInputField.caretPosition = filtered.Length;
    }

    private string FilterRoomCode(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        char[] buffer = new char[Mathf.Min(RoomCodeLength, value.Length)];
        int count = 0;

        foreach (char c in value)
        {
            if (!char.IsDigit(c))
                continue;

            buffer[count++] = c;
            if (count >= RoomCodeLength)
                break;
        }

        return new string(buffer, 0, count);
    }

    private bool IsValidRoomCode(string roomCode)
    {
        if (string.IsNullOrWhiteSpace(roomCode) || roomCode.Length != RoomCodeLength)
            return false;

        foreach (char c in roomCode)
        {
            if (!char.IsDigit(c))
                return false;
        }

        return true;
    }

    private void EnsureNickName()
    {
        if (!string.IsNullOrWhiteSpace(PhotonNetwork.NickName))
            return;

        string deviceName = SystemInfo.deviceName;
        if (string.IsNullOrWhiteSpace(deviceName))
            deviceName = $"{fallbackNickNamePrefix}-{UnityEngine.Random.Range(1000, 10000)}";

        PhotonNetwork.NickName = deviceName;
    }

    private void SetRoomPanelActive(bool active)
    {
        if (roomNumberPanel != null)
            roomNumberPanel.SetActive(active);
    }

    private void SetRoomControls(bool inputInteractable, bool enterInteractable, bool cancelInteractable)
    {
        if (roomNumberInputField != null)
            roomNumberInputField.interactable = inputInteractable;

        if (numberEnterButton != null)
            numberEnterButton.interactable = enterInteractable && !isJoiningRoom;

        if (roomCancelButton != null)
            roomCancelButton.interactable = cancelInteractable;

        if (sendHelloButton != null)
            sendHelloButton.interactable = PhotonNetwork.InRoom;
    }

    private void SetLobbyButtonsInteractable(bool interactable)
    {
        if (createRoomButton != null)
            createRoomButton.interactable = interactable && PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InRoom;

        if (codeInputButton != null)
            codeInputButton.interactable = interactable && PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InRoom;

        if (lobbyButtonsToLock == null)
            return;

        foreach (Button button in lobbyButtonsToLock)
        {
            if (button == null || button == createRoomButton || button == codeInputButton)
                continue;

            button.interactable = interactable;
        }
    }

    private void SetStatusText(string message)
    {
        if (roomGuideText != null)
            roomGuideText.text = message;

        Debug.Log($"[PhotonLobby] {message}");
    }

    private void AppendNetworkLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        logLines.Add(message);
        while (logLines.Count > 5)
            logLines.RemoveAt(0);

        if (networkLogText != null)
            networkLogText.text = string.Join("\n", logLines);
        else
            SetStatusText(message);

        Debug.Log($"[PhotonLobby] {message}");
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
        image.color = new Color(0f, 0.36862746f, 0.72156864f, 1f);

        TMP_Text labelText = CreateRuntimeText(buttonObject.transform, $"{objectName}_Text", label, Vector2.zero, size, 28f);
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.white;

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
        tmpText.enableAutoSizing = true;
        tmpText.fontSizeMin = 14f;
        tmpText.fontSizeMax = fontSize;
        tmpText.textWrappingMode = TextWrappingModes.Normal;

        return tmpText;
    }

    private GameObject FindSceneObject(string objectName)
    {
        Transform transform = FindSceneTransform(objectName);
        return transform != null ? transform.gameObject : null;
    }

    private Transform FindSceneTransform(string objectName)
    {
        foreach (Transform transform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (!IsLoadedSceneObject(transform.gameObject))
                continue;

            if (string.Equals(transform.name, objectName, StringComparison.Ordinal))
                return transform;
        }

        return null;
    }

    private T FindSceneComponent<T>(string objectName) where T : Component
    {
        GameObject sceneObject = FindSceneObject(objectName);
        return sceneObject != null ? sceneObject.GetComponent<T>() : null;
    }

    private bool IsLoadedSceneObject(GameObject sceneObject)
    {
        if (sceneObject == null)
            return false;

        Scene scene = sceneObject.scene;
        return scene.IsValid() && scene.isLoaded;
    }
}
