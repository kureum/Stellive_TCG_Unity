using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class OnlineBattleSession : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public const byte DeckInfoExchangeEventCode = 20;

    // Reserved for the next online milestones. They are intentionally unused in this step.
    public const byte BattleActionRequestEventCode = 21;
    public const byte BattleActionResultEventCode = 22;

    private const float InitialExchangeDelaySeconds = 0.35f;
    private const float ExchangeRetryIntervalSeconds = 0.75f;
    private const int MaxExchangeAttempts = 8;

    [SerializeField] private BattleManager battleManager;

    public int LocalActorNumber { get; private set; }
    public int RemoteActorNumber { get; private set; }
    public string LocalPlayerSide { get; private set; } = "";
    public string RemotePlayerSide { get; private set; } = "";
    public bool IsHost { get; private set; }
    public NetworkDeckInfoDto LocalDeckInfo { get; private set; }
    public NetworkDeckInfoDto RemoteDeckInfo { get; private set; }
    public bool AreBothDeckInfosReady => LocalDeckInfo != null && RemoteDeckInfo != null;

    private readonly Dictionary<int, NetworkDeckInfoDto> deckInfoByActorNumber =
        new Dictionary<int, NetworkDeckInfoDto>();

    private Coroutine exchangeRoutine;
    private bool hasAppliedIdolSlots;
    private bool isApplyingIdolSlots;
    private bool hasRespondedToRemoteDeckInfo;

    private string SaveFilePath =>
        Path.Combine(Application.persistentDataPath, "deck_presets.json");

    private IEnumerator Start()
    {
        if (!BattleStartSettings.IsOnlineBattle)
        {
            enabled = false;
            yield break;
        }

        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || PhotonNetwork.LocalPlayer == null)
        {
            Debug.LogError("[OnlineBattleSession] 온라인 모드지만 Photon Room 연결이 없습니다.");
            enabled = false;
            yield break;
        }

        if (battleManager == null)
            battleManager = GetComponent<BattleManager>();

        if (battleManager == null)
            battleManager = FindAnyObjectByType<BattleManager>();

        RefreshActorMapping();

        if (!TryBuildLocalDeckInfo(out NetworkDeckInfoDto localDeckInfo, out string error))
        {
            Debug.LogError($"[OnlineBattleSession] 로컬 덱 정보 생성 실패: {error}");
            yield break;
        }

        StoreDeckInfo(localDeckInfo);

        Debug.Log(
            $"[OnlineBattleSession] Room 연결 확인. room={PhotonNetwork.CurrentRoom.Name}, " +
            $"localActor={LocalActorNumber}, remoteActor={RemoteActorNumber}, " +
            $"side={LocalPlayerSide}, deck={LocalDeckInfo.deckName}, idol={LocalDeckInfo.idolCardId}");

        yield return new WaitForSecondsRealtime(InitialExchangeDelaySeconds);
        exchangeRoutine = StartCoroutine(ExchangeDeckInfoUntilReady());
    }

    public override void OnDisable()
    {
        if (exchangeRoutine != null)
        {
            StopCoroutine(exchangeRoutine);
            exchangeRoutine = null;
        }

        base.OnDisable();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshActorMapping();

        if (LocalDeckInfo != null)
            SendLocalDeckInfo();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        deckInfoByActorNumber.Remove(otherPlayer.ActorNumber);
        RemoteDeckInfo = null;
        hasAppliedIdolSlots = false;
        isApplyingIdolSlots = false;
        hasRespondedToRemoteDeckInfo = false;
        RefreshActorMapping();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        RefreshActorMapping();
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != DeckInfoExchangeEventCode)
            return;

        string json = photonEvent.CustomData as string;
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[OnlineBattleSession] 빈 DeckInfoExchange payload를 무시했습니다.");
            return;
        }

        NetworkDeckInfoDto receivedInfo;

        try
        {
            receivedInfo = JsonUtility.FromJson<NetworkDeckInfoDto>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[OnlineBattleSession] DeckInfoExchange 역직렬화 실패: {exception.Message}");
            return;
        }

        if (receivedInfo == null)
            return;

        int senderActorNumber = photonEvent.Sender;
        if (senderActorNumber <= 0)
        {
            Debug.LogWarning("[OnlineBattleSession] sender actorNumber가 없는 덱 정보를 무시했습니다.");
            return;
        }

        // Photon sender is authoritative for identity; payload actorNumber is informational only.
        receivedInfo.actorNumber = senderActorNumber;
        StoreDeckInfo(receivedInfo);
        RefreshActorMapping();

        Debug.Log(
            $"[OnlineBattleSession] 상대 덱 정보 수신. actor={senderActorNumber}, " +
            $"side={receivedInfo.playerSide}, deck={receivedInfo.deckName}, idol={receivedInfo.idolCardId}");

        TryApplySynchronizedIdolSlots();

        if (!hasRespondedToRemoteDeckInfo && LocalDeckInfo != null)
        {
            hasRespondedToRemoteDeckInfo = true;
            SendLocalDeckInfo();
        }
    }

    private IEnumerator ExchangeDeckInfoUntilReady()
    {
        for (int attempt = 1; attempt <= MaxExchangeAttempts && !AreBothDeckInfosReady; attempt++)
        {
            SendLocalDeckInfo();
            yield return new WaitForSecondsRealtime(ExchangeRetryIntervalSeconds);
        }

        exchangeRoutine = null;

        if (!AreBothDeckInfosReady)
        {
            Debug.LogWarning(
                $"[OnlineBattleSession] 상대 덱 정보 대기 시간 초과. " +
                $"localActor={LocalActorNumber}, remoteActor={RemoteActorNumber}");
            yield break;
        }

        TryApplySynchronizedIdolSlots();
    }

    private void SendLocalDeckInfo()
    {
        if (LocalDeckInfo == null || !PhotonNetwork.InRoom)
            return;

        string json = JsonUtility.ToJson(LocalDeckInfo);
        RaiseEventOptions options = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.Others
        };

        bool sent = PhotonNetwork.RaiseEvent(
            DeckInfoExchangeEventCode,
            json,
            options,
            SendOptions.SendReliable);

        if (!sent)
            Debug.LogWarning("[OnlineBattleSession] 로컬 덱 정보 전송 요청에 실패했습니다.");
    }

    private void StoreDeckInfo(NetworkDeckInfoDto deckInfo)
    {
        if (deckInfo == null || deckInfo.actorNumber <= 0)
            return;

        deckInfo.broadcastCardIds = deckInfo.broadcastCardIds ?? Array.Empty<string>();
        deckInfo.mainDeckCardIds = deckInfo.mainDeckCardIds ?? Array.Empty<string>();
        deckInfoByActorNumber[deckInfo.actorNumber] = deckInfo;

        if (deckInfo.actorNumber == LocalActorNumber)
            LocalDeckInfo = deckInfo;
        else
            RemoteDeckInfo = deckInfo;
    }

    private void RefreshActorMapping()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
            return;

        LocalActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        IsHost = PhotonNetwork.IsMasterClient;
        LocalPlayerSide = IsHost ? "Host" : "Client";
        RemotePlayerSide = IsHost ? "Client" : "Host";
        RemoteActorNumber = 0;

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player == null || player.ActorNumber == LocalActorNumber)
                continue;

            RemoteActorNumber = player.ActorNumber;
            break;
        }

        deckInfoByActorNumber.TryGetValue(LocalActorNumber, out NetworkDeckInfoDto localInfo);
        LocalDeckInfo = localInfo;

        NetworkDeckInfoDto remoteInfo = null;
        if (RemoteActorNumber > 0)
            deckInfoByActorNumber.TryGetValue(RemoteActorNumber, out remoteInfo);

        RemoteDeckInfo = remoteInfo;
    }

    private bool TryBuildLocalDeckInfo(out NetworkDeckInfoDto deckInfo, out string error)
    {
        deckInfo = null;
        error = "";

        if (!TryLoadDeckPreset(out DeckPresetSaveData preset, out int presetIndex, out bool usedFallback, out error))
            return false;

        if (!TryLoadCardDatabase(out CardDatabase database, out error))
            return false;

        Dictionary<string, BaseCardData> cardsById = BuildCardLookup(database);
        List<string> broadcastCardIds = new List<string>();
        List<string> mainDeckCardIds = new List<string>();
        string idolCardId = "";

        foreach (string cardId in preset.cardIds ?? new List<string>())
        {
            if (string.IsNullOrWhiteSpace(cardId) || !cardsById.TryGetValue(cardId, out BaseCardData card))
                continue;

            switch (card.kind)
            {
                case "Idol":
                    if (string.IsNullOrEmpty(idolCardId))
                        idolCardId = card.id;
                    break;

                case "Broadcast":
                    broadcastCardIds.Add(card.id);
                    break;

                case "Character":
                case "Content":
                    mainDeckCardIds.Add(card.id);
                    break;
            }
        }

        if (string.IsNullOrEmpty(idolCardId))
        {
            error = $"Preset {presetIndex + 1}에 아이돌 카드가 없습니다.";
            return false;
        }

        string selectedDeckId = $"preset-{presetIndex + 1}";
        deckInfo = new NetworkDeckInfoDto
        {
            actorNumber = LocalActorNumber,
            playerSide = LocalPlayerSide,
            selectedDeckId = selectedDeckId,
            deckName = string.IsNullOrWhiteSpace(preset.deckName) ? selectedDeckId : preset.deckName,
            idolCardId = idolCardId,
            broadcastCardIds = broadcastCardIds.ToArray(),
            mainDeckCardIds = mainDeckCardIds.ToArray(),
            deckHash = ComputeDeckHash(selectedDeckId, preset.cardIds)
        };

        if (usedFallback)
        {
            Debug.LogWarning(
                $"[OnlineBattleSession] 임시 선택 덱 사용: 선택 프리셋을 사용할 수 없어 " +
                $"첫 번째 완성 덱 Preset {presetIndex + 1}을 사용합니다.");
        }

        return true;
    }

    private bool TryLoadDeckPreset(
        out DeckPresetSaveData preset,
        out int presetIndex,
        out bool usedFallback,
        out string error)
    {
        preset = null;
        presetIndex = BattleStartSettings.SelectedMyPresetIndex;
        usedFallback = false;
        error = "";

        if (!File.Exists(SaveFilePath))
        {
            error = $"덱 프리셋 파일이 없습니다: {SaveFilePath}";
            return false;
        }

        string json = File.ReadAllText(SaveFilePath);
        DeckPresetSaveFile saveFile = JsonUtility.FromJson<DeckPresetSaveFile>(json);

        if (saveFile == null || saveFile.presets == null)
        {
            error = "덱 프리셋 파일을 읽을 수 없습니다.";
            return false;
        }

        if (presetIndex >= 0)
        {
            int selectedPresetIndex = presetIndex;
            preset = saveFile.presets.FirstOrDefault(item =>
                item != null &&
                item.presetIndex == selectedPresetIndex &&
                item.isValidForPlay &&
                item.cardIds != null &&
                item.cardIds.Count > 0);
        }

        if (preset == null)
        {
            preset = saveFile.presets
                .Where(item =>
                    item != null &&
                    item.isValidForPlay &&
                    item.cardIds != null &&
                    item.cardIds.Count > 0)
                .OrderBy(item => item.presetIndex)
                .FirstOrDefault();

            usedFallback = preset != null;
            presetIndex = preset != null ? preset.presetIndex : -1;
        }

        if (preset == null)
        {
            error = "사용 가능한 완성 덱 프리셋이 없습니다.";
            return false;
        }

        return true;
    }

    private static bool TryLoadCardDatabase(out CardDatabase database, out string error)
    {
        database = null;
        error = "";
        TextAsset cardsJson = Resources.Load<TextAsset>("cards");

        if (cardsJson == null)
        {
            error = "Resources/cards.json을 찾을 수 없습니다.";
            return false;
        }

        database = JsonUtility.FromJson<CardDatabase>(cardsJson.text);
        if (database == null)
        {
            error = "Resources/cards.json 파싱에 실패했습니다.";
            return false;
        }

        return true;
    }

    private static Dictionary<string, BaseCardData> BuildCardLookup(CardDatabase database)
    {
        Dictionary<string, BaseCardData> result =
            new Dictionary<string, BaseCardData>(StringComparer.OrdinalIgnoreCase);

        AddCards(result, database.idols);
        AddCards(result, database.broadcasts);
        AddCards(result, database.characters);
        AddCards(result, database.contents);
        return result;
    }

    private static void AddCards<T>(
        IDictionary<string, BaseCardData> lookup,
        IEnumerable<T> cards) where T : BaseCardData
    {
        if (cards == null)
            return;

        foreach (T card in cards)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.id))
                continue;

            lookup[card.id] = card;
        }
    }

    private static string ComputeDeckHash(string selectedDeckId, IEnumerable<string> cardIds)
    {
        // The full main-deck list is exchanged for this first implementation only.
        // A later fairness/security pass can exchange only this hash and let the Host validate details.
        string canonical = $"{selectedDeckId}|{string.Join("|", cardIds ?? Enumerable.Empty<string>())}";

        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            StringBuilder builder = new StringBuilder(hash.Length * 2);

            foreach (byte value in hash)
                builder.Append(value.ToString("x2"));

            return builder.ToString();
        }
    }

    private void TryApplySynchronizedIdolSlots()
    {
        if (hasAppliedIdolSlots ||
            isApplyingIdolSlots ||
            !AreBothDeckInfosReady ||
            battleManager == null)
        {
            return;
        }

        if (!battleManager.TryApplyOnlineIdolCardsFromExternal(
                LocalDeckInfo.idolCardId,
                RemoteDeckInfo.idolCardId))
        {
            isApplyingIdolSlots = true;
            StartCoroutine(RetryApplySynchronizedIdolSlots());
            return;
        }

        hasAppliedIdolSlots = true;
        Debug.Log(
            $"[OnlineBattleSession] IdolSlot 동기화 완료. " +
            $"my={LocalDeckInfo.idolCardId}, opponent={RemoteDeckInfo.idolCardId}");
    }

    private IEnumerator RetryApplySynchronizedIdolSlots()
    {
        for (int attempt = 0; attempt < 10 && !hasAppliedIdolSlots; attempt++)
        {
            yield return null;

            if (battleManager != null &&
                battleManager.TryApplyOnlineIdolCardsFromExternal(
                    LocalDeckInfo.idolCardId,
                    RemoteDeckInfo.idolCardId))
            {
                hasAppliedIdolSlots = true;
                Debug.Log(
                    $"[OnlineBattleSession] 지연 후 IdolSlot 동기화 완료. " +
                    $"my={LocalDeckInfo.idolCardId}, opponent={RemoteDeckInfo.idolCardId}");
            }
        }

        isApplyingIdolSlots = false;

        if (!hasAppliedIdolSlots)
        {
            Debug.LogWarning(
                $"[OnlineBattleSession] BattleManager 준비 대기 후에도 IdolSlot을 적용하지 못했습니다. " +
                $"my={LocalDeckInfo?.idolCardId}, opponent={RemoteDeckInfo?.idolCardId}");
        }
    }
}
