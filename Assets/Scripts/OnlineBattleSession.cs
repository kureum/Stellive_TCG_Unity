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
    public static OnlineBattleSession Instance { get; private set; }

    public const byte DeckInfoExchangeEventCode = 20;

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
    public bool IsOnlineBattleActive => isOnlineBattleActive;
    public bool WasOnlineBattleSession { get; private set; }

    private readonly Dictionary<int, NetworkDeckInfoDto> deckInfoByActorNumber =
        new Dictionary<int, NetworkDeckInfoDto>();

    private Coroutine exchangeRoutine;
    private bool hasAppliedIdolSlots;
    private bool isApplyingIdolSlots;
    private bool hasRespondedToRemoteDeckInfo;
    private BattleActionResolver actionResolver;
    private BattleActionResultApplier resultApplier;
    private bool isOnlineBattleActive;
    private bool isEndingOnlineBattle;
    private bool hasBroadcastStartMainGameResult;
    private int authoritativeBroadcastSetupFirstActorNumber;

    private string SaveFilePath =>
        Path.Combine(Application.persistentDataPath, "deck_presets.json");

    private void Awake()
    {
        Instance = this;
        WasOnlineBattleSession = BattleStartSettings.IsOnlineBattle;
    }

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

        isOnlineBattleActive = true;

        if (battleManager == null)
            battleManager = GetComponent<BattleManager>();

        if (battleManager == null)
            battleManager = FindAnyObjectByType<BattleManager>();

        actionResolver = new BattleActionResolver(battleManager);
        resultApplier = new BattleActionResultApplier(battleManager);

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
        EndOnlineBattleSession("OnlineBattleSession disabled", true);

        if (exchangeRoutine != null)
        {
            StopCoroutine(exchangeRoutine);
            exchangeRoutine = null;
        }

        base.OnDisable();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (!isOnlineBattleActive)
            return;

        RefreshActorMapping();

        if (LocalDeckInfo != null)
            SendLocalDeckInfo();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!isOnlineBattleActive)
            return;

        deckInfoByActorNumber.Remove(otherPlayer.ActorNumber);
        RemoteDeckInfo = null;
        hasAppliedIdolSlots = false;
        isApplyingIdolSlots = false;
        hasRespondedToRemoteDeckInfo = false;
        RefreshActorMapping();

        if (battleManager != null)
            battleManager.EndOnlineBattleFromExternal("상대 플레이어가 온라인 배틀에서 퇴장했습니다.");

        EndOnlineBattleSession("Remote player left BattleScene", true);
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (!isOnlineBattleActive)
            return;

        RefreshActorMapping();
    }

    public override void OnLeftRoom()
    {
        isOnlineBattleActive = false;
        isEndingOnlineBattle = false;
        BattleStartSettings.ClearOnlineSettings();
    }

    public void OnEvent(EventData photonEvent)
    {
        if (!isOnlineBattleActive)
            return;

        switch (photonEvent.Code)
        {
            case DeckInfoExchangeEventCode:
                HandleDeckInfoExchange(photonEvent);
                break;
            case BattleActionRequestEventCode:
                HandleBattleActionRequest(photonEvent);
                break;
            case BattleActionResultEventCode:
                HandleBattleActionResult(photonEvent);
                break;
        }
    }

    private void HandleDeckInfoExchange(EventData photonEvent)
    {
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

    private void HandleBattleActionRequest(EventData photonEvent)
    {
        if (!IsHost || actionResolver == null)
            return;

        BattleAction action = BattleActionSerializer.FromJson(photonEvent.CustomData as string);
        if (action == null)
            return;

        action.actor = photonEvent.Sender == LocalActorNumber
            ? BattleSlotOwner.My
            : BattleSlotOwner.Enemy;
        string originalSourceSlotId = action.sourceSlotId;
        string originalTargetSlotId = action.targetSlotId;
        action.targetSlotId = ConvertSlotIdFromSenderPerspective(action.targetSlotId, action.actor);
        action.sourceSlotId = ConvertSlotIdFromSenderPerspective(action.sourceSlotId, action.actor);

        Debug.Log(
            $"[OnlineBattle] Host received BattleActionRequest: {action.actionType}, " +
            $"actor={action.actor}, currentTurn={battleManager?.CurrentActionSideFromExternal}, " +
            $"turnCount={battleManager?.GetCurrentTurnCountFromExternal()}, " +
            $"sourceSlot={originalSourceSlotId}->{action.sourceSlotId}, " +
            $"targetSlot={originalTargetSlotId}->{action.targetSlotId}");

        battleManager?.LogOnlineZoneAuthorityBeforeResultFromExternal(action);

        BattleActionResult result = actionResolver.ResolveActionAsHost(action);
        if (!result.isAccepted)
        {
            Debug.LogWarning(
                $"[OnlineBattle] {action.actionType} rejected: reason={result.rejectReason}");
        }
        else
        {
            Debug.Log(
                $"[OnlineBattle] Host accepted {action.actionType}. actor={result.actor}");
        }

        DispatchBattleActionResult(result);
    }

    private void DispatchBattleActionResult(BattleActionResult result)
    {
        if (TryDispatchSelectionRequestResult(result))
            return;

        if (TryDispatchPrivateOwnerResult(result))
            return;

        if (result != null &&
            string.Equals(result.resolvedEffectRef, "content.moveOwnCharToEmptyOrBattleIfTagged", StringComparison.OrdinalIgnoreCase))
        {
            CardZoneMoveDelta fieldMove = null;
            if (result.cardZoneMoveDeltas != null)
            {
                foreach (CardZoneMoveDelta delta in result.cardZoneMoveDeltas)
                {
                    if (delta != null &&
                        string.Equals(delta.fromZone, "FieldCharacter", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(delta.toZone, "FieldCharacter", StringComparison.OrdinalIgnoreCase))
                    {
                        fieldMove = delta;
                        break;
                    }
                }
            }

            Debug.Log(
                $"[OnlineBattleSession] Broadcast moveTagged payload. " +
                $"zoneMoves={result.cardZoneMoveDeltas?.Count ?? 0}, " +
                $"from={fieldMove?.fromSlotId ?? ""}, to={fieldMove?.toSlotId ?? ""}, " +
                $"cardInstanceId={fieldMove?.cardInstanceId ?? ""}, cardId={fieldMove?.cardId ?? ""}, " +
                $"owner={fieldMove?.owner.ToString() ?? ""}, faceDown={fieldMove?.faceDown.ToString() ?? ""}");
        }

        BroadcastBattleActionResult(BattleActionResultSerializer.ToJson(result));
    }

    private bool TryDispatchPrivateOwnerResult(BattleActionResult result)
    {
        if (result == null ||
            !result.isAccepted ||
            result.cardDrawDeltas == null ||
            result.cardDrawDeltas.Count == 0)
        {
            return false;
        }

        CardDrawDelta privateDraw = null;
        foreach (CardDrawDelta delta in result.cardDrawDeltas)
        {
            if (delta != null && delta.visibleToOwnerOnly)
            {
                privateDraw = delta;
                break;
            }
        }

        if (privateDraw == null)
            return false;

        int ownerActorNumber = ResolveActorNumberForOwner(privateDraw.owner);
        int otherActorNumber = ownerActorNumber == LocalActorNumber
            ? RemoteActorNumber
            : LocalActorNumber;

        BattleActionResult publicResult = CloneResult(result);
        SanitizePrivateOwnerPayloadForNonOwner(publicResult, privateDraw.owner, $"actor:{otherActorNumber}");

        if (ownerActorNumber == LocalActorNumber)
            ApplyHostLocalResult(result);
        else
            SendBattleActionResultToActor(BattleActionResultSerializer.ToJson(result), ownerActorNumber);

        if (otherActorNumber == LocalActorNumber)
            ApplyHostLocalResult(publicResult);
        else
            SendBattleActionResultToActor(BattleActionResultSerializer.ToJson(publicResult), otherActorNumber);

        Debug.Log(
            $"[OnlineBattle] Dispatched private owner result. " +
            $"owner={privateDraw.owner}, ownerActor={ownerActorNumber}, otherActor={otherActorNumber}, " +
            $"actionType={result.requestActionType}, effectRef={result.resolvedEffectRef}");
        return true;
    }

    private bool TryDispatchSelectionRequestResult(BattleActionResult result)
    {
        if (result == null ||
            !result.isAccepted ||
            result.selectionRequests == null ||
            result.selectionRequests.Count == 0)
        {
            return false;
        }

        SelectionRequestDelta request = result.selectionRequests[0];
        if (request == null || string.IsNullOrWhiteSpace(request.requestId))
            return false;

        int requestedActorNumber = ResolveActorNumberForOwner(request.requestedPlayer);
        int otherActorNumber = requestedActorNumber == LocalActorNumber
            ? RemoteActorNumber
            : LocalActorNumber;

        BattleActionResult publicResult = CloneResult(result);
        string otherRecipient = $"actor:{otherActorNumber}";
        int privateCandidatesRemoved = SanitizeSelectionRequestsForNonRequestedPlayer(publicResult, otherRecipient);
        if (HasOwnerOnlyPrivatePayload(publicResult, request.requestedPlayer))
        {
            SanitizePrivateOwnerPayloadForNonOwner(
                publicResult,
                request.requestedPlayer,
                otherRecipient,
                privateCandidatesRemoved);
        }

        Debug.Log(
            $"[OnlineBattleSession] Dispatch owner result selectionRequests={result.selectionRequests?.Count ?? 0}, " +
            $"requestId={request.requestId}, requestedActor={requestedActorNumber}, localActor={LocalActorNumber}");

        if (requestedActorNumber == LocalActorNumber)
            ApplyHostLocalResult(result);
        else
        {
            SendBattleActionResultToActor(BattleActionResultSerializer.ToJson(result), requestedActorNumber);
        }

        if (otherActorNumber == LocalActorNumber)
            ApplyHostLocalResult(publicResult);
        else
            SendBattleActionResultToActor(BattleActionResultSerializer.ToJson(publicResult), otherActorNumber);

        Debug.Log(
            $"[OnlineBattle] Dispatched selection request result. " +
            $"requestId={request.requestId}, requestedOwner={request.requestedPlayer}, " +
            $"requestedActor={requestedActorNumber}, otherActor={otherActorNumber}");
        return true;
    }

    private static bool HasOwnerOnlyPrivatePayload(BattleActionResult result, BattleSlotOwner privateOwner)
    {
        if (result == null || result.cardDrawDeltas == null)
            return false;

        foreach (CardDrawDelta delta in result.cardDrawDeltas)
        {
            if (delta != null && delta.owner == privateOwner && delta.visibleToOwnerOnly)
                return true;
        }

        return false;
    }

    private void ApplyHostLocalResult(BattleActionResult result)
    {
        if (result == null || resultApplier == null)
            return;

        resultApplier.Apply(result);
    }

    private bool SendBattleActionResultToActor(string resultJson, int actorNumber)
    {
        if (string.IsNullOrWhiteSpace(resultJson) || actorNumber <= 0)
            return false;

        bool sent = PhotonNetwork.RaiseEvent(
            BattleActionResultEventCode,
            resultJson,
            new RaiseEventOptions { TargetActors = new[] { actorNumber } },
            SendOptions.SendReliable);

        Debug.Log($"[OnlineBattle] Send BattleActionResult to actor={actorNumber}, sent={sent}");
        return sent;
    }

    private int ResolveActorNumberForOwner(BattleSlotOwner owner)
    {
        return owner == BattleSlotOwner.My
            ? LocalActorNumber
            : RemoteActorNumber;
    }

    private static BattleActionResult CloneResult(BattleActionResult result)
    {
        return BattleActionResultSerializer.FromJson(BattleActionResultSerializer.ToJson(result));
    }

    private static int SanitizeSelectionRequestsForNonRequestedPlayer(BattleActionResult result, string recipient)
    {
        if (result == null || result.selectionRequests == null)
            return 0;

        int privateCandidatesRemoved = 0;
        foreach (SelectionRequestDelta request in result.selectionRequests)
        {
            if (request == null)
                continue;

            if (request.candidateTargets != null)
                request.candidateTargets.Clear();
            if (request.candidatePublicIds != null)
                request.candidatePublicIds.Clear();
            if (request.candidatePrivateIdsForOwnerOnly != null)
            {
                privateCandidatesRemoved += request.candidatePrivateIdsForOwnerOnly.Count;
                request.candidatePrivateIdsForOwnerOnly.Clear();
            }
        }

        if (privateCandidatesRemoved > 0)
        {
            Debug.Log(
                $"[OnlinePrivateSanitize] recipient={recipient}, actor={result.actor}, " +
                $"publicZoneMoves={CountPublicZoneMoves(result)}, privateDrawsRemoved=0, " +
                $"privateCandidatesRemoved={privateCandidatesRemoved}, " +
                $"publicAffectedIdsKept={(result.affectedCardIds != null ? result.affectedCardIds.Count : 0)}");
        }

        return privateCandidatesRemoved;
    }

    private static void SanitizePrivateOwnerPayloadForNonOwner(
        BattleActionResult result,
        BattleSlotOwner privateOwner,
        string recipient,
        int privateCandidatesRemoved = 0)
    {
        if (result == null)
            return;

        int privateDrawsRemoved = 0;
        int publicZoneMoves = 0;
        if (result.drawnCardInstanceIds != null)
            result.drawnCardInstanceIds.Clear();
        if (result.resolvedRandomCardIds != null)
            result.resolvedRandomCardIds.Clear();

        if (result.cardDrawDeltas != null)
        {
            foreach (CardDrawDelta delta in result.cardDrawDeltas)
            {
                if (delta == null || delta.owner != privateOwner || !delta.visibleToOwnerOnly)
                    continue;

                delta.cardInstanceId = "";
                delta.cardId = "";
                delta.fromDeckIndex = -1;
                delta.publicCardIdForOpponent = "";
                privateDrawsRemoved++;
            }
        }

        if (result.cardZoneMoveDeltas != null)
        {
            foreach (CardZoneMoveDelta delta in result.cardZoneMoveDeltas)
            {
                if (delta == null ||
                    delta.owner != privateOwner ||
                    !string.Equals(delta.fromZone, "Deck", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(delta.toZone, "Hand", StringComparison.OrdinalIgnoreCase))
                {
                    if (IsPublicZoneMove(delta))
                        publicZoneMoves++;
                    continue;
                }

                delta.cardInstanceId = "";
                delta.cardId = "";
                delta.isPublic = false;
            }
        }

        if (result.messageDeltas != null)
        {
            foreach (MessageDelta delta in result.messageDeltas)
            {
                if (delta == null || !string.Equals(delta.audience, "OwnerOnly", StringComparison.OrdinalIgnoreCase))
                    continue;

                delta.relatedCardId = "";
                delta.relatedInstanceId = "";
            }
        }

        Debug.Log(
            $"[OnlinePrivateSanitize] recipient={recipient}, actor={result.actor}, " +
            $"publicZoneMoves={publicZoneMoves}, privateDrawsRemoved={privateDrawsRemoved}, " +
            $"privateCandidatesRemoved={privateCandidatesRemoved}, " +
            $"publicAffectedIdsKept={(result.affectedCardIds != null ? result.affectedCardIds.Count : 0)}");
    }

    private static bool IsPublicZoneMove(CardZoneMoveDelta delta)
    {
        if (delta == null)
            return false;

        if (delta.isPublic)
            return true;

        return IsPublicZone(delta.fromZone) || IsPublicZone(delta.toZone);
    }

    private static int CountPublicZoneMoves(BattleActionResult result)
    {
        if (result == null || result.cardZoneMoveDeltas == null)
            return 0;

        int count = 0;
        foreach (CardZoneMoveDelta delta in result.cardZoneMoveDeltas)
        {
            if (IsPublicZoneMove(delta))
                count++;
        }

        return count;
    }

    private static bool IsPublicZone(string zone)
    {
        return string.Equals(zone, "FieldCharacter", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(zone, "FieldContent", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(zone, "RestZone", StringComparison.OrdinalIgnoreCase);
    }

    private void HandleBattleActionResult(EventData photonEvent)
    {
        BattleActionResult result =
            BattleActionResultSerializer.FromJson(photonEvent.CustomData as string);
        if (result == null || resultApplier == null)
            return;

        if (!IsHost)
        {
            result.actor = InvertOwner(result.actor);
            if (result.requestActionType == BattleActionType.StartMainGame ||
                result.requestActionType == BattleActionType.EndTurn ||
                result.requestActionType == BattleActionType.SummonFaceDown ||
                result.requestActionType == BattleActionType.SummonFaceUp ||
                result.requestActionType == BattleActionType.FlipSummon ||
                result.requestActionType == BattleActionType.MoveCharacter ||
                result.requestActionType == BattleActionType.StartCollab ||
                result.requestActionType == BattleActionType.UseContent ||
                result.requestActionType == BattleActionType.UseIdolActive ||
                result.requestActionType == BattleActionType.SelectEffectTarget ||
                result.requestActionType == BattleActionType.SelectCardOption ||
                result.requestActionType == BattleActionType.SelectEffectChoice)
            {
                result.currentTurnPlayer = InvertOwner(result.currentTurnPlayer);
            }

            if (result.requestActionType == BattleActionType.EndTurn &&
                result.didAdvanceTurn &&
                result.drawnCardInstanceIds != null &&
                result.drawnCardInstanceIds.Count > 0)
            {
                result.drawnPlayer = InvertOwner(result.drawnPlayer);
            }

            if (result.requestActionType == BattleActionType.MoveCharacter)
                result.characterOwner = InvertOwner(result.characterOwner);

            if (result.requestActionType == BattleActionType.StartCollab)
            {
                result.attackerOwner = InvertOwner(result.attackerOwner);
                result.defenderOwner = InvertOwner(result.defenderOwner);
            }

            ConvertResultSlotIdsToLocalPerspective(result, "Client");
        }

        if (result.requestActionType == BattleActionType.StartMainGame ||
            result.requestActionType == BattleActionType.EndTurn ||
            result.requestActionType == BattleActionType.SummonFaceDown ||
            result.requestActionType == BattleActionType.SummonFaceUp ||
            result.requestActionType == BattleActionType.FlipSummon ||
            result.requestActionType == BattleActionType.MoveCharacter ||
            result.requestActionType == BattleActionType.StartCollab ||
            result.requestActionType == BattleActionType.UseContent ||
            result.requestActionType == BattleActionType.UseIdolActive ||
            result.requestActionType == BattleActionType.SelectEffectTarget ||
            result.requestActionType == BattleActionType.SelectCardOption ||
            result.requestActionType == BattleActionType.SelectEffectChoice)
        {
            Debug.Log($"[OnlineBattle] Received {result.requestActionType}Result.");
        }

        resultApplier.Apply(result);

        if (IsHost &&
            result.isAccepted &&
            result.requestActionType == BattleActionType.PlaceBroadcast)
        {
            TryBroadcastStartMainGameResult();
        }
    }

    private void TryBroadcastStartMainGameResult()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.Log(
                "[OnlineBattle] Skip StartMainGame completion check because this client is not Host.");
            return;
        }

        if (hasBroadcastStartMainGameResult || battleManager == null)
            return;

        bool isComplete =
            battleManager.IsBroadcastSetupCompleteForBothPlayersFromExternal(
                out int hostPlaced,
                out int hostRequired,
                out int clientPlaced,
                out int clientRequired);

        Debug.Log(
            $"[OnlineBattle] Checking broadcast setup completion: " +
            $"host={hostPlaced}/{hostRequired} client={clientPlaced}/{clientRequired}");

        if (!isComplete)
            return;

        Debug.Log("[OnlineBattle] Broadcast setup complete for both players.");

        BattleActionResult startResult =
            battleManager.CreateStartMainGameResultFromExternal();
        string json = BattleActionResultSerializer.ToJson(startResult);
        if (string.IsNullOrWhiteSpace(json))
            return;

        hasBroadcastStartMainGameResult = true;
        Debug.Log("[OnlineBattle] Broadcasting StartMainGameResult.");

        if (!BroadcastBattleActionResult(json))
            hasBroadcastStartMainGameResult = false;
    }

    public bool SendBattleActionRequest(string actionJson)
    {
        if (!isOnlineBattleActive ||
            !PhotonNetwork.InRoom ||
            string.IsNullOrWhiteSpace(actionJson))
            return false;

        bool sent = PhotonNetwork.RaiseEvent(
            BattleActionRequestEventCode,
            actionJson,
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
            SendOptions.SendReliable);

        if (sent)
        {
            BattleAction action = BattleActionSerializer.FromJson(actionJson);
            Debug.Log(
                $"[OnlineBattle] Sent BattleActionRequest: " +
                $"{(action != null ? action.actionType.ToString() : "Unknown")}");
        }
        return sent;
    }

    public bool BroadcastBattleActionResult(string resultJson)
    {
        if (!isOnlineBattleActive ||
            !IsHost ||
            !PhotonNetwork.InRoom ||
            string.IsNullOrWhiteSpace(resultJson))
            return false;

        BattleActionResult result = BattleActionResultSerializer.FromJson(resultJson);
        bool sent = PhotonNetwork.RaiseEvent(
            BattleActionResultEventCode,
            resultJson,
            new RaiseEventOptions { Receivers = ReceiverGroup.All },
            SendOptions.SendReliable);

        if (sent && result != null)
        {
            Debug.Log(
                $"[OnlineBattle] Broadcast BattleActionResult: " +
                $"{result.requestActionType} accepted={result.isAccepted}");
        }

        return sent;
    }

    public void EndOnlineBattleSession(string reason, bool leaveRoom)
    {
        if (!WasOnlineBattleSession)
            return;

        if (isEndingOnlineBattle)
            return;

        isEndingOnlineBattle = true;
        isOnlineBattleActive = false;

        if (exchangeRoutine != null)
        {
            StopCoroutine(exchangeRoutine);
            exchangeRoutine = null;
        }

        Debug.Log($"[OnlineBattleSession] Session ended. reason={reason}");
        if (battleManager != null)
            battleManager.ClearOnlinePersistentStatusesFromExternal(reason);

        BattleStartSettings.ClearOnlineSettings();

        if (leaveRoom && PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
    }

    public static void EndActiveSessionBeforeSceneChange(string reason)
    {
        if (Instance != null)
        {
            Instance.EndOnlineBattleSession(reason, true);
            return;
        }

        if (BattleStartSettings.IsOnlineBattle)
        {
            BattleStartSettings.ClearOnlineSettings();

            if (PhotonNetwork.InRoom)
                PhotonNetwork.LeaveRoom();
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

        if (IsHost)
        {
            if (authoritativeBroadcastSetupFirstActorNumber <= 0)
            {
                authoritativeBroadcastSetupFirstActorNumber =
                    ChooseBroadcastSetupFirstActorNumberAsHost();
            }

            LocalDeckInfo.broadcastSetupFirstActorNumber =
                authoritativeBroadcastSetupFirstActorNumber;
        }

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
            deckHash = ComputeDeckHash(selectedDeckId, preset.cardIds),
            broadcastSetupFirstActorNumber = IsHost
                ? ChooseBroadcastSetupFirstActorNumberAsHost()
                : 0
        };

        if (IsHost)
        {
            authoritativeBroadcastSetupFirstActorNumber =
                deckInfo.broadcastSetupFirstActorNumber;
        }

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

        if (!battleManager.TryInitializeOnlineBattleRuntimeFromExternal(
                LocalActorNumber,
                LocalDeckInfo,
                RemoteActorNumber,
                RemoteDeckInfo))
        {
            isApplyingIdolSlots = true;
            StartCoroutine(RetryApplySynchronizedIdolSlots());
            return;
        }

        hasAppliedIdolSlots = true;
        ApplyHostBroadcastSetupFirstActor();
        Debug.Log(
            $"[OnlineBattleSession] IdolSlot 동기화 완료. " +
            $"my={LocalDeckInfo.idolCardId}, opponent={RemoteDeckInfo.idolCardId}");
    }

    private IEnumerator RetryApplySynchronizedIdolSlots()
    {
        for (int attempt = 0;
             attempt < 120 && isOnlineBattleActive && !hasAppliedIdolSlots;
             attempt++)
        {
            yield return null;

            if (battleManager != null &&
                battleManager.TryInitializeOnlineBattleRuntimeFromExternal(
                    LocalActorNumber,
                    LocalDeckInfo,
                    RemoteActorNumber,
                    RemoteDeckInfo))
            {
                hasAppliedIdolSlots = true;
                ApplyHostBroadcastSetupFirstActor();
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

    private void ApplyHostBroadcastSetupFirstActor()
    {
        NetworkDeckInfoDto hostInfo = IsHost ? LocalDeckInfo : RemoteDeckInfo;
        if (battleManager == null || hostInfo == null)
        {
            return;
        }

        int firstActorNumber = hostInfo.broadcastSetupFirstActorNumber;
        if (firstActorNumber <= 0)
        {
            Debug.LogWarning(
                "[OnlineBattle] Host deck info has no broadcast setup first actorNumber.");
            return;
        }

        if (!TryConvertActorNumberToLocalOwner(
                firstActorNumber,
                out BattleSlotOwner localOwner))
        {
            return;
        }

        Debug.Log(
            $"[OnlineBattle] Apply broadcast setup first actor: " +
            $"firstActorNumber={firstActorNumber}, localActor={LocalActorNumber}, " +
            $"remoteActor={RemoteActorNumber}, localOwner={localOwner}, isHost={IsHost}");

        battleManager.ApplyOnlineBroadcastSetupFirstActorFromExternal(localOwner);
    }

    private int ChooseBroadcastSetupFirstActorNumberAsHost()
    {
        if (!IsHost || LocalActorNumber <= 0)
            return 0;

        int selectedActorNumber = RemoteActorNumber > 0 &&
            UnityEngine.Random.Range(0, 2) == 1
                ? RemoteActorNumber
                : LocalActorNumber;

        Debug.Log(
            $"[OnlineBattle] Host selected broadcast setup first actorNumber=" +
            $"{selectedActorNumber}. hostActor={LocalActorNumber}, " +
            $"remoteActor={RemoteActorNumber}");
        return selectedActorNumber;
    }

    private bool TryConvertActorNumberToLocalOwner(
        int actorNumber,
        out BattleSlotOwner owner)
    {
        if (actorNumber == LocalActorNumber)
        {
            owner = BattleSlotOwner.My;
            return true;
        }

        if (actorNumber == RemoteActorNumber)
        {
            owner = BattleSlotOwner.Enemy;
            return true;
        }

        owner = BattleSlotOwner.Enemy;
        Debug.LogWarning(
            $"[OnlineBattle] Unknown actorNumber={actorNumber}, " +
            $"local={LocalActorNumber}, remote={RemoteActorNumber}");
        return false;
    }

    private static BattleSlotOwner InvertOwner(BattleSlotOwner owner)
    {
        return owner == BattleSlotOwner.My
            ? BattleSlotOwner.Enemy
            : BattleSlotOwner.My;
    }

    private static string ConvertSlotIdToOwner(string slotId, BattleSlotOwner owner)
    {
        if (string.IsNullOrWhiteSpace(slotId))
            return slotId;

        string[] parts = slotId.Split('_');
        if (parts.Length != 3)
            return slotId;

        return $"{owner}_{parts[1]}_{parts[2]}";
    }

    private static string ConvertSlotIdFromSenderPerspective(string slotId, BattleSlotOwner senderOwnerOnHost)
    {
        if (string.IsNullOrWhiteSpace(slotId))
            return slotId;

        string[] parts = slotId.Split('_');
        if (parts.Length != 3)
            return slotId;

        if (!Enum.TryParse(parts[0], out BattleSlotOwner slotOwnerInSenderPerspective))
            return slotId;

        BattleSlotOwner hostOwner =
            senderOwnerOnHost == BattleSlotOwner.My
                ? slotOwnerInSenderPerspective
                : InvertOwner(slotOwnerInSenderPerspective);

        return $"{hostOwner}_{parts[1]}_{parts[2]}";
    }

    private static string InvertSlotIdOwner(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId))
            return slotId;

        string[] parts = slotId.Split('_');
        if (parts.Length != 3)
            return slotId;

        if (!Enum.TryParse(parts[0], out BattleSlotOwner owner))
            return slotId;

        return $"{InvertOwner(owner)}_{parts[1]}_{parts[2]}";
    }

    private static void ConvertResultSlotIdsToLocalPerspective(BattleActionResult result, string recipient)
    {
        if (result.affectedSlotIds != null)
        {
            for (int i = 0; i < result.affectedSlotIds.Count; i++)
                result.affectedSlotIds[i] = InvertSlotIdOwner(result.affectedSlotIds[i]);
        }

        if (result.resolvedTargetSlotIds != null)
        {
            for (int i = 0; i < result.resolvedTargetSlotIds.Count; i++)
                result.resolvedTargetSlotIds[i] = InvertSlotIdOwner(result.resolvedTargetSlotIds[i]);
        }

        if (result.viewerDeltas != null)
        {
            for (int i = 0; i < result.viewerDeltas.Count; i++)
            {
                if (result.viewerDeltas[i] != null)
                    result.viewerDeltas[i].owner = InvertOwner(result.viewerDeltas[i].owner);
            }
        }

        if (result.fieldStatDeltas != null)
        {
            for (int i = 0; i < result.fieldStatDeltas.Count; i++)
            {
                if (result.fieldStatDeltas[i] != null)
                    result.fieldStatDeltas[i].slotId = InvertSlotIdOwner(result.fieldStatDeltas[i].slotId);
            }
        }

        if (result.selectionRequests != null)
        {
            for (int i = 0; i < result.selectionRequests.Count; i++)
            {
                SelectionRequestDelta request = result.selectionRequests[i];
                if (request == null)
                    continue;

                request.requestingPlayer = InvertOwner(request.requestingPlayer);
                request.requestedPlayer = InvertOwner(request.requestedPlayer);

                if (request.candidatePublicIds != null)
                {
                    for (int j = 0; j < request.candidatePublicIds.Count; j++)
                        request.candidatePublicIds[j] = InvertSlotIdOwner(request.candidatePublicIds[j]);
                }

                if (request.candidateTargets != null)
                {
                    for (int j = 0; j < request.candidateTargets.Count; j++)
                    {
                        SelectionRequestTarget target = request.candidateTargets[j];
                        if (target == null)
                            continue;

                        target.slotId = InvertSlotIdOwner(target.slotId);
                        target.slotOwner = InvertOwner(target.slotOwner);
                    }
                }
            }
        }

        if (result.statusDeltas != null)
        {
            for (int i = 0; i < result.statusDeltas.Count; i++)
            {
                StatusDelta delta = result.statusDeltas[i];
                if (delta == null)
                    continue;

                delta.owner = InvertOwner(delta.owner);
                delta.sourceActor = InvertOwner(delta.sourceActor);
                delta.targetOwner = InvertOwner(delta.targetOwner);
                delta.targetSlotId = InvertSlotIdOwner(delta.targetSlotId);
            }
        }

        if (result.actionStateDeltas != null)
        {
            for (int i = 0; i < result.actionStateDeltas.Count; i++)
            {
                ActionStateDelta delta = result.actionStateDeltas[i];
                if (delta == null)
                    continue;

                string originalSlot = delta.slotId;
                delta.owner = InvertOwner(delta.owner);
                if (!string.IsNullOrWhiteSpace(delta.slotId))
                    delta.slotId = InvertSlotIdOwner(delta.slotId);

                Debug.Log(
                    $"[OnlineRemapAudit][ActionState] recipient={recipient}, actor={result.actor}, " +
                    $"type={delta.actionStateType}, originalSlot={originalSlot}, " +
                    $"remappedSlot={delta.slotId}, cardInstanceId={delta.cardInstanceId}");
            }
        }

        if (result.cardZoneMoveDeltas != null)
        {
            for (int i = 0; i < result.cardZoneMoveDeltas.Count; i++)
            {
                CardZoneMoveDelta delta = result.cardZoneMoveDeltas[i];
                if (delta == null)
                    continue;

                delta.owner = InvertOwner(delta.owner);
                delta.fromSlotId = InvertSlotIdOwner(delta.fromSlotId);
                delta.toSlotId = InvertSlotIdOwner(delta.toSlotId);
            }
        }

        if (result.fieldContentDeltas != null)
        {
            for (int i = 0; i < result.fieldContentDeltas.Count; i++)
            {
                FieldContentDelta delta = result.fieldContentDeltas[i];
                if (delta == null)
                    continue;

                delta.slotId = InvertSlotIdOwner(delta.slotId);
                delta.contentOwner = InvertOwner(delta.contentOwner);
            }
        }

        if (result.cardRevealDeltas != null)
        {
            for (int i = 0; i < result.cardRevealDeltas.Count; i++)
            {
                CardRevealDelta delta = result.cardRevealDeltas[i];
                if (delta == null)
                    continue;

                delta.owner = InvertOwner(delta.owner);
                delta.actor = InvertOwner(delta.actor);
                delta.revealedCardOwner = InvertOwner(delta.revealedCardOwner);
                delta.slotId = InvertSlotIdOwner(delta.slotId);
                delta.revealTo = InvertAudience(delta.revealTo);
            }
        }

        if (result.cardDrawDeltas != null)
        {
            for (int i = 0; i < result.cardDrawDeltas.Count; i++)
            {
                if (result.cardDrawDeltas[i] != null)
                    result.cardDrawDeltas[i].owner = InvertOwner(result.cardDrawDeltas[i].owner);
            }
        }

        if (result.deckOrderDeltas != null)
        {
            for (int i = 0; i < result.deckOrderDeltas.Count; i++)
            {
                if (result.deckOrderDeltas[i] != null)
                    result.deckOrderDeltas[i].owner = InvertOwner(result.deckOrderDeltas[i].owner);
            }
        }

    }

    private static string InvertAudience(string audience)
    {
        if (string.IsNullOrWhiteSpace(audience))
            return audience;

        if (string.Equals(audience, "My", StringComparison.OrdinalIgnoreCase))
            return "Enemy";

        if (string.Equals(audience, "Enemy", StringComparison.OrdinalIgnoreCase))
            return "My";

        return audience;
    }
}
