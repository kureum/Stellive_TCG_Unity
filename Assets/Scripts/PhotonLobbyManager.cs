using System;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PhotonLobbyManager : MonoBehaviourPunCallbacks
{
    private const int RoomCodeLength = 6;
    private const int MaxCreateRoomAttempts = 5;
    private const string BattleSceneName = "BattleScene";
    private const string HostWaitingMessage = "상대의 입장을 기다리고 있습니다.";
    private const string OpponentEnteredMessage = "상대가 입장했습니다.";
    private const string StartingBattleMessage = "배틀을 시작합니다.";
    private const float BattleStartMessageDuration = 0.5f;

    private enum RoomPanelMode
    {
        Closed,
        Join,
        HostWaiting,
        GuestInRoom
    }

    [Header("Photon")]
    [Tooltip("Photon Cloud Region code to force for local online tests. If this does not match the project, compare logs and try asia, jp, or kr.")]
    [SerializeField] private string fixedPhotonRegion = "kr";
    [SerializeField] private string fixedGameVersion = "0.1.0";
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

    [Header("Online Room Buttons")]
    [SerializeField] private Button createRoomButton;
    [SerializeField] private bool createMissingOnlineButtonsAtRuntime = true;

    [Header("Lobby Buttons To Lock In Room")]
    [SerializeField] private Button[] lobbyButtonsToLock;

    private string pendingCreateRoomCode = "";
    private int createRoomAttemptCount;
    private bool isJoiningRoom;
    private bool isCreateRoomPending;
    private bool cancelCreateRoomRequested;
    private bool isLoadingBattleScene;
    private bool hasStartedBattleLoadAsHost;
    private RoomPanelMode roomPanelMode = RoomPanelMode.Closed;

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        ResolveReferencesIfNeeded();
        CreateMissingOnlineButtonsIfNeeded();
        ConfigureInputField();
        WireButtons();
        ResetRoomPanel();
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
        PhotonNetwork.AutomaticallySyncScene = true;

        if (PhotonNetwork.IsConnectedAndReady)
        {
            LogNetworkState("ConnectToPhoton already connected and ready");
            JoinLobbyIfNeeded();
            return;
        }

        if (PhotonNetwork.IsConnected)
        {
            LogNetworkState("ConnectToPhoton already connecting or connected");
            UpdateLobbyStatusText("Photon 서버 연결을 확인 중입니다...");
            return;
        }

        ApplyPhotonConnectionSettings();
        UpdateLobbyStatusText("Photon 서버에 연결 중...");
        LogNetworkState("Before ConnectUsingSettings");
        PhotonNetwork.ConnectUsingSettings();
    }

    public void CreateRoom()
    {
        if (isCreateRoomPending)
        {
            UpdateLobbyStatusText(
                cancelCreateRoomRequested
                    ? "이전 방 생성 취소를 처리 중입니다."
                    : "방을 생성 중입니다.");
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

        createRoomAttemptCount = 0;
        cancelCreateRoomRequested = false;
        TryCreateRoomWithNewCode();
    }

    public void JoinRoom()
    {
        if (roomPanelMode != RoomPanelMode.Join)
            return;

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
        Debug.Log($"[PhotonLobby] Try JoinRoom. roomCode={roomCode}");
        LogNetworkState("Before JoinRoom");
        PhotonNetwork.JoinRoom(roomCode);
    }

    public void LeaveRoom()
    {
        if (isLoadingBattleScene)
            return;

        if (PhotonNetwork.InRoom)
        {
            UpdateRoomStateText("방에서 나가는 중...");
            PhotonNetwork.LeaveRoom();
            return;
        }

        if (isCreateRoomPending)
        {
            cancelCreateRoomRequested = true;
            ResetRoomPanel();
            SetLobbyButtonsInteractable(true);
            UpdateLobbyStatusText("방 생성을 취소하고 로비로 돌아갑니다.");
            return;
        }

        ResetToLobbyUi("로비 대기 중입니다.");
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

        if (isLoadingBattleScene)
        {
            SetRoomControls(false, false, false);
            SetStatusText(StartingBattleMessage);
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            ShowHostWaitingPanel(roomCode);
            SetStatusText(playerCount >= maxPlayers ? OpponentEnteredMessage : HostWaitingMessage);
        }
        else
        {
            roomPanelMode = RoomPanelMode.GuestInRoom;
            SetRoomPanelActive(true);
            SetRoomControls(false, false, true);
            UpdateRoomStateText($"RoomCode {roomCode} | {role} | {playerCount}/{maxPlayers}");
        }
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
        LogNetworkState("OnConnectedToMaster");
        UpdateLobbyStatusText("Photon 서버 연결 성공. 로비 입장 중...");
        JoinLobbyIfNeeded();
    }

    public override void OnJoinedLobby()
    {
        LogNetworkState("OnJoinedLobby");
        ResetToLobbyUi("Photon 로비 입장 완료. 방 생성 또는 RoomCode 입장이 가능합니다.");
    }

    public override void OnCreatedRoom()
    {
        LogNetworkState("OnCreatedRoom");
        isCreateRoomPending = false;

        if (cancelCreateRoomRequested)
        {
            if (PhotonNetwork.InRoom)
                PhotonNetwork.LeaveRoom();
            return;
        }

        string roomCode = PhotonNetwork.CurrentRoom != null
            ? PhotonNetwork.CurrentRoom.Name
            : pendingCreateRoomCode;

        ShowHostWaitingPanel(roomCode);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[PhotonLobby] CreateRoom failed. code={returnCode}, message={message}");
        LogNetworkState("OnCreateRoomFailed");

        if (cancelCreateRoomRequested)
        {
            isCreateRoomPending = false;
            pendingCreateRoomCode = "";
            ResetToLobbyUi("방 생성을 취소했습니다. 로비 대기 중입니다.");
            return;
        }

        if (createRoomAttemptCount < MaxCreateRoomAttempts)
        {
            LogNetworkState($"RoomCode 중복 또는 생성 실패. 재시도 중... ({returnCode}: {message})");
            TryCreateRoomWithNewCode();
            return;
        }

        isCreateRoomPending = false;
        pendingCreateRoomCode = "";
        ResetToLobbyUi($"방 생성 실패: {message} ({returnCode})");
    }

    public override void OnJoinedRoom()
    {
        LogNetworkState("OnJoinedRoom");
        isJoiningRoom = false;
        isCreateRoomPending = false;

        if (cancelCreateRoomRequested)
        {
            if (PhotonNetwork.InRoom)
                PhotonNetwork.LeaveRoom();
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            BattleStartSettings.SetOnlineHostMode(PhotonNetwork.CurrentRoom.Name);
            ShowHostWaitingPanel(PhotonNetwork.CurrentRoom.Name);
        }
        else
        {
            BattleStartSettings.SetOnlineClientMode(PhotonNetwork.CurrentRoom.Name);
            roomPanelMode = RoomPanelMode.GuestInRoom;
            SetRoomPanelActive(true);
            SetRoomControls(false, false, true);
        }

        SetLobbyButtonsInteractable(false);
        UpdateRoomStateText();
        TryStartBattleWhenRoomReady();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[PhotonLobby] JoinRoom failed. code={returnCode}, message={message}");
        LogNetworkState("OnJoinRoomFailed");
        isJoiningRoom = false;
        SetRoomPanelModeForJoin(false);
        UpdateLobbyStatusText($"방 입장 실패: {message} ({returnCode})");
    }

    public override void OnLeftRoom()
    {
        Debug.LogWarning("[PhotonLobby] OnLeftRoom called.");
        LogNetworkState("OnLeftRoom");
        BattleStartSettings.ClearOnlineSettings();
        pendingCreateRoomCode = "";
        isJoiningRoom = false;
        isCreateRoomPending = false;
        cancelCreateRoomRequested = false;
        isLoadingBattleScene = false;
        hasStartedBattleLoadAsHost = false;
        ResetToLobbyUi("방에서 나왔습니다. 로비 대기 중입니다.");
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        LogNetworkState($"{newPlayer.NickName} entered room");
        UpdateRoomStateText();
        TryStartBattleWhenRoomReady();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        LogNetworkState($"{otherPlayer.NickName} left room");

        if (PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.PlayerCount < 2)
        {
            SetLoadingBattleSceneState(false);
            hasStartedBattleLoadAsHost = false;

            if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom != null)
                PhotonNetwork.CurrentRoom.IsOpen = true;
        }

        UpdateRoomStateText();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        LogNetworkState($"MasterClient switched to {newMasterClient.NickName}");
        UpdateRoomStateText();
        TryStartBattleWhenRoomReady();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogError($"[PhotonLobby] Disconnected. cause={cause}");
        LogNetworkState("OnDisconnected");
        BattleStartSettings.ClearOnlineSettings();
        pendingCreateRoomCode = "";
        isJoiningRoom = false;
        isCreateRoomPending = false;
        cancelCreateRoomRequested = false;
        isLoadingBattleScene = false;
        hasStartedBattleLoadAsHost = false;
        ResetRoomPanel();
        SetLobbyButtonsInteractable(true);
        UpdateLobbyStatusText($"Photon 연결 해제: {cause}");
    }

    private void TryStartBattleWhenRoomReady()
    {
        if (!PhotonNetwork.InRoom ||
            PhotonNetwork.CurrentRoom == null ||
            PhotonNetwork.CurrentRoom.PlayerCount != 2)
        {
            return;
        }

        bool enteredLoadingState = !isLoadingBattleScene;
        if (enteredLoadingState)
        {
            SetStatusText(OpponentEnteredMessage);
            SetLoadingBattleSceneState(true);
        }

        if (PhotonNetwork.IsMasterClient)
            StartOnlineBattleAsHost();
        else if (enteredLoadingState)
            StartCoroutine(ShowStartingBattleMessageAfterDelay());
    }

    private void StartOnlineBattleAsHost()
    {
        if (!PhotonNetwork.IsMasterClient ||
            !PhotonNetwork.InRoom ||
            PhotonNetwork.CurrentRoom == null ||
            hasStartedBattleLoadAsHost)
        {
            return;
        }

        hasStartedBattleLoadAsHost = true;
        SetRoomClosedForBattleStart();
        StartCoroutine(LoadBattleSceneAfterStatusUpdate());
    }

    private void SetRoomClosedForBattleStart()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return;

        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;
    }

    private void SetLoadingBattleSceneState(bool loading)
    {
        isLoadingBattleScene = loading;
        SetRoomControls(false, false, !loading);
        SetLobbyButtonsInteractable(false);
    }

    private IEnumerator LoadBattleSceneAfterStatusUpdate()
    {
        yield return new WaitForSecondsRealtime(BattleStartMessageDuration);

        if (!PhotonNetwork.IsMasterClient ||
            !PhotonNetwork.InRoom ||
            PhotonNetwork.CurrentRoom == null ||
            PhotonNetwork.CurrentRoom.PlayerCount != 2)
        {
            SetLoadingBattleSceneState(false);
            hasStartedBattleLoadAsHost = false;

            if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom != null)
                PhotonNetwork.CurrentRoom.IsOpen = true;

            UpdateRoomStateText();
            yield break;
        }

        SetStatusText(StartingBattleMessage);
        yield return null;
        PhotonNetwork.LoadLevel(BattleSceneName);
    }

    private IEnumerator ShowStartingBattleMessageAfterDelay()
    {
        yield return new WaitForSecondsRealtime(BattleStartMessageDuration);

        if (isLoadingBattleScene && PhotonNetwork.InRoom)
            SetStatusText(StartingBattleMessage);
    }

    private void TryCreateRoomWithNewCode()
    {
        createRoomAttemptCount++;
        pendingCreateRoomCode = GenerateRoomCode();
        isCreateRoomPending = true;

        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = 2,
            IsVisible = false,
            IsOpen = true
        };

        SetLobbyButtonsInteractable(false);
        ShowHostWaitingPanel(pendingCreateRoomCode);
        Debug.Log($"[PhotonLobby] Try CreateRoom. roomCode={pendingCreateRoomCode}");
        LogNetworkState("Before CreateRoom");
        PhotonNetwork.CreateRoom(pendingCreateRoomCode, roomOptions);
    }

    private void ApplyPhotonConnectionSettings()
    {
        string version = string.IsNullOrWhiteSpace(fixedGameVersion)
            ? "0.1.0"
            : fixedGameVersion.Trim();

        PhotonNetwork.GameVersion = version;

        if (PhotonNetwork.PhotonServerSettings != null &&
            PhotonNetwork.PhotonServerSettings.AppSettings != null)
        {
            PhotonNetwork.PhotonServerSettings.AppSettings.AppVersion = version;

            if (!string.IsNullOrWhiteSpace(fixedPhotonRegion))
                PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = fixedPhotonRegion.Trim();
        }
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
        ShowJoinRoomPanel();
    }

    private void ResetToLobbyUi(string message)
    {
        isLoadingBattleScene = false;
        hasStartedBattleLoadAsHost = false;
        ResetRoomPanel();
        SetLobbyButtonsInteractable(true);

        if (roomCodeText != null)
            roomCodeText.text = "";

        if (roleText != null)
            roleText.text = "";

        if (playerCountText != null)
            playerCountText.text = "";

        UpdateLobbyStatusText(message);
    }

    private void ShowHostWaitingPanel(string roomCode)
    {
        SetRoomPanelModeForHostWaiting(roomCode);
        SetRoomPanelActive(true);
    }

    private void ShowJoinRoomPanel()
    {
        SetRoomPanelActive(true);
        SetRoomPanelModeForJoin(true);
        UpdateLobbyStatusText("입장할 6자리 RoomCode를 입력하세요.");
    }

    private void ResetRoomPanel()
    {
        roomPanelMode = RoomPanelMode.Closed;

        if (roomNumberInputField != null)
            roomNumberInputField.text = "";

        SetRoomControls(true, true, true);
        SetRoomPanelActive(false);
    }

    private void SetRoomPanelModeForHostWaiting(string roomCode)
    {
        roomPanelMode = RoomPanelMode.HostWaiting;

        if (roomNumberInputField != null)
            roomNumberInputField.text = roomCode ?? "";

        SetRoomControls(false, false, true);
        SetStatusText(HostWaitingMessage);
    }

    private void SetRoomPanelModeForJoin(bool clearInput)
    {
        roomPanelMode = RoomPanelMode.Join;

        if (roomNumberInputField != null)
        {
            if (clearInput)
                roomNumberInputField.text = "";

            roomNumberInputField.interactable = true;

            if (clearInput)
            {
                roomNumberInputField.ActivateInputField();
                roomNumberInputField.Select();
            }
        }

        SetRoomControls(true, true, true);
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

        if (roomCodeText == null)
            roomCodeText = FindSceneComponent<TMP_Text>("RoomCodeText");

        if (roleText == null)
            roleText = FindSceneComponent<TMP_Text>("RoleText");

        if (playerCountText == null)
            playerCountText = FindSceneComponent<TMP_Text>("PlayerCountText");

        if (createRoomButton == null)
            createRoomButton = FindSceneComponent<Button>("CreateRoomButton");

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
        if (createRoomButton == null && gameRoomPanel != null)
        {
            createRoomButton = CreateRuntimeButton(
                gameRoomPanel,
                "CreateRoomButton",
                "방 만들기",
                new Vector2(0f, -180f),
                new Vector2(333f, 70f));
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
            roomCancelButton.interactable = cancelInteractable && !isLoadingBattleScene;
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

    private void LogNetworkState(string context)
    {
        if (string.IsNullOrWhiteSpace(context))
            return;

        string fixedRegion = PhotonNetwork.PhotonServerSettings != null &&
            PhotonNetwork.PhotonServerSettings.AppSettings != null
                ? PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion
                : "";

        Debug.Log(
            $"[PhotonLobby:{context}] " +
            $"IsConnected={PhotonNetwork.IsConnected}, " +
            $"IsConnectedAndReady={PhotonNetwork.IsConnectedAndReady}, " +
            $"Server={PhotonNetwork.Server}, " +
            $"CloudRegion={PhotonNetwork.CloudRegion}, " +
            $"FixedRegion={fixedRegion}, " +
            $"InLobby={PhotonNetwork.InLobby}, " +
            $"InRoom={PhotonNetwork.InRoom}, " +
            $"CurrentRoom={PhotonNetwork.CurrentRoom?.Name}, " +
            $"GameVersion={PhotonNetwork.GameVersion}, " +
            $"UserId={PhotonNetwork.LocalPlayer?.UserId}, " +
            $"NickName={PhotonNetwork.NickName}");
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
