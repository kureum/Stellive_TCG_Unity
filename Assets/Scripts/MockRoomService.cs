using System.Collections.Generic;

public enum LobbyRoomState
{
    Idle,
    RoomPanelOpen,
    HostingWaiting,
    Joining,
    Matched,
    OccupiedFailed
}

public enum RoomCodeState
{
    Empty,
    HostWaiting,
    Occupied
}

public enum RoomEnterResult
{
    BecameHost,
    MatchedAsGuest,
    Occupied,
    AlreadyHosting,
    Invalid
}

public class MockRoomInfo
{
    public string roomCode;
    public RoomCodeState state;
    public string hostId;
    public string guestId;
}

public static class MockRoomService
{
    // Temporary local-only room code store. Replace this with a server/network
    // room service when real online transport is introduced.
    private static readonly Dictionary<string, MockRoomInfo> rooms =
        new Dictionary<string, MockRoomInfo>();

    public static bool TryEnterRoom(
        string roomCode,
        string localUserId,
        out RoomEnterResult result)
    {
        result = RoomEnterResult.Invalid;

        if (string.IsNullOrWhiteSpace(roomCode) || string.IsNullOrWhiteSpace(localUserId))
            return false;

        if (!rooms.TryGetValue(roomCode, out MockRoomInfo room) ||
            room == null ||
            room.state == RoomCodeState.Empty)
        {
            rooms[roomCode] = new MockRoomInfo
            {
                roomCode = roomCode,
                state = RoomCodeState.HostWaiting,
                hostId = localUserId,
                guestId = ""
            };

            result = RoomEnterResult.BecameHost;
            return true;
        }

        if (room.hostId == localUserId)
        {
            result = RoomEnterResult.AlreadyHosting;
            return true;
        }

        if (room.state == RoomCodeState.HostWaiting)
        {
            room.guestId = localUserId;
            room.state = RoomCodeState.Occupied;
            result = RoomEnterResult.MatchedAsGuest;
            return true;
        }

        if (room.state == RoomCodeState.Occupied)
        {
            result = RoomEnterResult.Occupied;
            return true;
        }

        return false;
    }

    public static bool CancelHostRoom(string roomCode, string localUserId)
    {
        if (string.IsNullOrWhiteSpace(roomCode) || string.IsNullOrWhiteSpace(localUserId))
            return false;

        if (!rooms.TryGetValue(roomCode, out MockRoomInfo room) || room == null)
            return false;

        if (room.hostId != localUserId)
            return false;

        rooms.Remove(roomCode);
        return true;
    }

    public static RoomCodeState GetRoomState(string roomCode)
    {
        if (string.IsNullOrWhiteSpace(roomCode))
            return RoomCodeState.Empty;

        if (!rooms.TryGetValue(roomCode, out MockRoomInfo room) || room == null)
            return RoomCodeState.Empty;

        return room.state;
    }

    public static bool SimulateGuestJoin(string roomCode, string guestId)
    {
        if (string.IsNullOrWhiteSpace(roomCode) || string.IsNullOrWhiteSpace(guestId))
            return false;

        if (!rooms.TryGetValue(roomCode, out MockRoomInfo room) ||
            room == null ||
            room.state != RoomCodeState.HostWaiting)
        {
            return false;
        }

        if (room.hostId == guestId)
            guestId = guestId + "_guest";

        room.guestId = guestId;
        room.state = RoomCodeState.Occupied;
        return true;
    }
}
