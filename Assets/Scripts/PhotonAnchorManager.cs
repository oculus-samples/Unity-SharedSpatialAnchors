// Copyright (c) Meta Platforms, Inc. and affiliates.
// This code is licensed under the MIT license (see LICENSE for details).

using Oculus.Platform;

using Photon.Pun;
using Photon.Realtime;

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using Hashtable = ExitGames.Client.Photon.Hashtable;
using Sampleton = SampleController; // only transitional

/// <summary>
/// Manages Photon Room creation and maintenence, and tracks anchor sharing information.
/// </summary>
public class PhotonAnchorManager : MonoBehaviourPunCallbacks
{
    //
    // Static interface

    public static HashSet<Guid> PublishedAnchors
        => s_PublishedAnchors.TryGetValue(PhotonNetwork.CurrentRoom, out var anchorSet) ? anchorSet
         : s_PublishedAnchors[PhotonNetwork.CurrentRoom] = new();

    public static void PublishAnchorToRoom(Guid oneAnchor)
    {
        PublishAnchorsToRoom(new HashSet<Guid> { oneAnchor });
    }

    public static void PublishAnchorsToRoom(IEnumerable<Guid> anchorUuids)
    {
        if (!s_Instance || !PhotonNetwork.InRoom)
            return;

        if (anchorUuids is not HashSet<Guid> uuidSet)
            uuidSet = new HashSet<Guid>(anchorUuids);

        s_Instance.PublishAnchorUuids(uuidSet);
    }


    static PhotonAnchorManager s_Instance;

    static readonly Dictionary<RoomInfo, HashSet<Guid>> s_PublishedAnchors = new();
    static readonly HashSet<ulong> s_RoomUserIds = new();

    //
    // instance impl.

    [SerializeField]
    SharedAnchorControlPanel controlPanel;

    const string k_PubAnchorsKey = "anchors";
    const byte k_PacketFormat = 1;
    const int k_UuidSize = 16;

    string m_OculusUsername;
    ulong m_OculusUserID;

    Coroutine m_OnDisconnectDelayCall;

    #region [Monobehaviour Methods]

    public override void OnEnable()
    {
        if (s_Instance && s_Instance != this)
        {
            Destroy(this);
            return;
        }

        s_Instance = this;
        base.OnEnable();
    }

    IEnumerator Start()
    {
        if (!PhotonNetwork.IsConnected)
            PhotonNetwork.ConnectUsingSettings();

        Core.LogMessages = true; // uses a lot of heap memory, but helpful for debugging platform connection issues.

        // (maybe a) KEY API CALL: static Oculus.Platform.Core.Initialize()
        Core.Initialize();

        // (maybe a) KEY API CALL: static Oculus.Platform.Users.GetLoggedInUser()
        Users.GetLoggedInUser().OnComplete(GetLoggedInUserCallback);

        // ^ only key calls iff your app employs user-based anchor sharing instead of group-based.

        var interval = new WaitForSecondsRealtime(1f / 5);

        while (controlPanel)
        {
            controlPanel.SetStatusText($"Status: {PhotonNetwork.NetworkClientState}");
            // UpdateUserListUI(); // uncomment if you want live updates for player active/inactive state
            yield return interval;
        }
    }

    void OnApplicationPause(bool pause)
    {
        if (PhotonNetwork.IsConnected)
        {
            Sampleton.Log("Application Un-paused: Attempting to reconnect and rejoin a Photon room");
            PhotonNetwork.ReconnectAndRejoin();
        }
        else
        {
            Sampleton.Log("Application Un-paused: Connecting to a Photon server. Please join or create a room.");
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    #endregion

    void GetLoggedInUserCallback(Message msg)
    {
        if (msg.IsError)
        {
            Sampleton.LogError($"{nameof(GetLoggedInUserCallback)}: FAILED: {msg.GetError()}");
            return;
        }

        Sampleton.Log($"{nameof(GetLoggedInUserCallback)}: received {msg.Type}");

        if (msg.Type != Message.MessageType.User_GetLoggedInUser)
            return;

        m_OculusUsername = msg.GetUser().OculusID;
        m_OculusUserID = msg.GetUser().ID;

        Sampleton.Log($"+ oculus user name: '{m_OculusUsername}'\n+ oculus id: {m_OculusUserID}");

        var thisPlaya = PhotonNetwork.LocalPlayer;

        if (m_OculusUserID == 0)
        {
            m_OculusUsername = $"TestUser{UnityEngine.Random.Range(0, 10000):0000}";
            Sampleton.LogError("You are not authenticated to use this app. This sample scene will not work.");
        }
        else
        {
            thisPlaya.SetPlatformID(m_OculusUserID);
        }

        thisPlaya.NickName = m_OculusUsername;
    }

    #region [Photon Callbacks]

    public override void OnConnectedToMaster()
    {
        Sampleton.Log($"Photon::OnConnectedToMaster: successfully connected to region '{PhotonNetwork.CloudRegion}'");

        if (controlPanel)
            controlPanel.ToggleRoomButtons(true);

        PhotonNetwork.JoinLobby();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        s_RoomUserIds.Clear();

        switch (cause)
        {
            case DisconnectCause.DisconnectByServerLogic:
            case DisconnectCause.DisconnectByDisconnectMessage:
            case DisconnectCause.DnsExceptionOnConnect:
            case DisconnectCause.ServerAddressInvalid:
            case DisconnectCause.InvalidRegion:
            case DisconnectCause.InvalidAuthentication:
            case DisconnectCause.AuthenticationTicketExpired:
            case DisconnectCause.CustomAuthenticationFailed:
            case DisconnectCause.OperationNotAllowedInCurrentState:
            case DisconnectCause.DisconnectByOperationLimit:
            case DisconnectCause.MaxCcuReached:
                Sampleton.LogError($"Photon:OnDisconnected: {cause}\n- will NOT attempt to automatically ReconnectAndRejoin()");
                return;

            case DisconnectCause.Exception:
            case DisconnectCause.ExceptionOnConnect:
            case DisconnectCause.ClientTimeout:
            case DisconnectCause.ServerTimeout:
            case DisconnectCause.DisconnectByServerReasonUnknown:
                Sampleton.Log($"Photon::OnDisconnected: {cause}\n+ attempting auto ReconnectAndRejoin() next frame...");
                if (m_OnDisconnectDelayCall is not null)
                    StopCoroutine(m_OnDisconnectDelayCall);
                m_OnDisconnectDelayCall = StartCoroutine(DelayCall(() => PhotonNetwork.ReconnectAndRejoin(), 1e-2f));
                return;

            default:
            case DisconnectCause.None:
            case DisconnectCause.DisconnectByClientLogic:
                Sampleton.Log($"Photon::OnDisconnected: {cause}");
                return;
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Sampleton.Log($"Photon::OnJoinRoomFailed: \"{message}\" ({returnCode})\n\nCreate or Join a new room.");

        if (controlPanel)
            controlPanel.DisplayLobbyPanel();
    }

    public override void OnJoinedRoom()
    {
        PublishedAnchors.Clear();

        s_RoomUserIds.Clear(); // defers refresh for next time GetRoomUserIds is called.

        var room = PhotonNetwork.CurrentRoom;

        if (room.MasterClientId != PhotonNetwork.LocalPlayer.ActorNumber)
            Sampleton.Log($"Photon::OnJoinedRoom: {room.Name}");

        if (controlPanel)
            controlPanel.SetRoomText($"Photon Room: {room.Name}");

        UpdateUserListUI();

        if (controlPanel)
            controlPanel.DisplayMenuPanel();

        if (room.PlayerCount < 2) // nobody else to share with me
            return;

        // Wait half a sec, give time for existing room members to [re]share their anchors with me:
        _ = StartCoroutine(DelayCall(() => OnRoomPropertiesUpdate(room.CustomProperties), 0.5f));
    }

    public override void OnLeftRoom()
    {
        s_RoomUserIds.Clear();
        if (controlPanel)
            controlPanel.DisplayLobbyPanel();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Sampleton.Log($"Photon::OnPlayerEnteredRoom: {newPlayer}");

        if (newPlayer.TryGetPlatformID(out ulong uid))
        {
            SharedAnchor.ShareAllMineTo(uid, reshareOnly: true);
            if (s_RoomUserIds.Count > 0)
                s_RoomUserIds.Add(uid);
        }
        else
        {
            s_RoomUserIds.Clear(); // enqueues a lazy refresh, but realistically this user is probably not authenticated
        }

        UpdateUserListUI();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (otherPlayer.IsInactive)
        {
            Sampleton.Log($"Photon::OnPlayerLeftRoom: {otherPlayer} has gone inactive.");
            return;
        }

        Sampleton.Log($"Photon::OnPlayerLeftRoom: {otherPlayer} has left.");

        s_RoomUserIds.Clear(); // enqueues a lazy refresh

        UpdateUserListUI();
    }

    public override void OnCreatedRoom()
    {
        Sampleton.Log($"Photon::OnCreatedRoom: User {PhotonNetwork.LocalPlayer} created room \"{PhotonNetwork.CurrentRoom.Name}\"");
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        if (controlPanel)
            controlPanel.SetRoomList(roomList);
    }

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        Sampleton.Log(nameof(OnRoomPropertiesUpdate));

        var bytes = new List<byte> { k_PacketFormat };
        foreach (var (boxKey, boxVal) in changedProps)
        {
            string key = (string)boxKey;
            if (!key.EndsWith('.' + k_PubAnchorsKey) || key.StartsWith(PhotonNetwork.NickName + '.'))
                continue;
            if (boxVal is not byte[] rawBytes)
                continue;
            if (rawBytes.Length <= 1 || rawBytes[0] != k_PacketFormat)
                continue;
            bytes.AddRange(new ArraySegment<byte>(rawBytes, 1, rawBytes.Length - 1));
        }

        if (bytes.Count > k_UuidSize)
            CheckForAnchorsShared(bytes.ToArray());
    }

    #endregion

    #region [Room creation, P2P connection, and inviting others]

    public void OnCreateRoomButtonPressed()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Sampleton.Log($"{nameof(OnCreateRoomButtonPressed)}: No Photon connection!\nAttempting to reconnect...");
            PhotonNetwork.ConnectUsingSettings();
            return;
        }

        Sampleton.Log($"{nameof(OnCreateRoomButtonPressed)}: Photon is connected.");

        string username = PhotonNetwork.NickName;
        if (string.IsNullOrEmpty(username))
        {
            PhotonNetwork.NickName = username = $"TestUser{UnityEngine.Random.Range(0, 10000):0000}";
        }

        string newRoomName = $"{username}'s room";

        Sampleton.Log($"Attempting to host a new room named \"{newRoomName}\"...");

        var roomOptions = new RoomOptions
        {
            IsVisible = true,
            IsOpen = true,
            BroadcastPropsChangeToAll = true,
            MaxPlayers = 0,       // no defined limit
            EmptyRoomTtl = 0,     // rooms immediately teardown
            PlayerTtl = 300000    // 5 minutes
        };

        if (PhotonNetwork.JoinOrCreateRoom(newRoomName, roomOptions, TypedLobby.Default))
            return;

        Sampleton.LogError($"ERR: Room creation request not sent to server!");
    }

    public void OnJoinRoomButtonPressed(TMPro.TextMeshProUGUI roomName)
    {
        Sampleton.Log("OnJoinRoomButtonPressed");

        if (string.IsNullOrEmpty(PhotonNetwork.NickName))
        {
            PhotonNetwork.NickName = $"TestUser{UnityEngine.Random.Range(0, 10000):0000}";
        }

        JoinRoomFromLobby(roomName.text);
    }

    public void OnFindRoomButtonPressed()
    {
        if (PhotonNetwork.IsConnected)
        {
            if (controlPanel)
                controlPanel.ToggleRoomLayoutPanel(true);
        }
        else
        {
            Sampleton.Log("Attempting to reconnect and rejoin a room");
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public void JoinRoomFromLobby(string roomToJoin)
    {
        var isValidRoomToJoin = !string.IsNullOrEmpty(roomToJoin);

        if (!isValidRoomToJoin)
        {
            return;
        }

        Sampleton.Log($"{nameof(JoinRoomFromLobby)}: Room Name: " + roomToJoin);

        var roomOptions = new RoomOptions { IsVisible = true, MaxPlayers = 16, EmptyRoomTtl = 0, PlayerTtl = 300000 };

        PhotonNetwork.JoinOrCreateRoom(roomToJoin, roomOptions, TypedLobby.Default);
    }

    #endregion

    #region [Send and read room data]

    void PublishAnchorUuids(HashSet<Guid> uuidSet)
    {
        if (m_OculusUserID == 0 || string.IsNullOrEmpty(PhotonNetwork.NickName))
        {
            Sampleton.LogError($"{nameof(PublishAnchorUuids)}: FAILED: a working platform (oculus) login is required.");
            return;
        }

        var room = PhotonNetwork.CurrentRoom;
        var roomAnchorSet = PublishedAnchors;

        int nNewIds = roomAnchorSet.Count;
        roomAnchorSet.UnionWith(uuidSet);
        nNewIds = roomAnchorSet.Count - nNewIds;

        if (nNewIds == 0)
        {
            Sampleton.Log($"{nameof(PublishAnchorUuids)}: SKIP: zero new UUIDs.");
            return;
        }

        Sampleton.Log($"{nameof(PublishAnchorUuids)}: {nNewIds} new UUIDs ({uuidSet.Count} total)");

        var rawBytes = new byte[1 + k_UuidSize * uuidSet.Count];
        rawBytes[0] = k_PacketFormat;
        int offset = 1;
        foreach (var uuid in uuidSet)
        {
            Sampleton.Log($"  + anchor: {uuid}");

            var toSpan = new Span<byte>(rawBytes, offset, k_UuidSize);
            if (!uuid.TryWriteBytes(toSpan))
            {
                Sampleton.LogError($"ERR: Failed to write {uuid} at index {offset}. (length: {rawBytes.Length})");
                // dump any cached published anchors
                s_PublishedAnchors.Remove(room);
                return;
            }
            offset += k_UuidSize;
        }

        var props = new Hashtable
        {
            [$"{PhotonNetwork.NickName}.{k_PubAnchorsKey}"] = rawBytes
        };

        room.SetCustomProperties(props); // prefer room properties so that the data can be queried anytime after sync

        // photonView.RPC(nameof(CheckForAnchorsShared), RpcTarget.OthersBuffered, s_AnchorListByteBuffer);
    }

    // [PunRPC]
    void CheckForAnchorsShared(byte[] uuidsPacket)
    {
        Debug.Log($"{nameof(CheckForAnchorsShared)}: BEGIN RPC ({nameof(uuidsPacket)}[{(uuidsPacket is null ? "null" : uuidsPacket.Length)}])");

        if (uuidsPacket is null)
        {
            Sampleton.LogError($"  - ERR: {nameof(uuidsPacket)} was null!");
            return;
        }

        if (uuidsPacket.Length % k_UuidSize != 1)
        {
            Sampleton.LogError($"  - ERR: invalid packet size: {uuidsPacket.Length}");
            return;
        }

        if (uuidsPacket[0] != k_PacketFormat)
        {
            Sampleton.LogError($"  - ERR: invalid packet format: {uuidsPacket[0]}");
            return;
        }

        var nUuidsShared = (uuidsPacket.Length - 1) / k_UuidSize;
        if (nUuidsShared == 0)
        {
            Sampleton.Log($"  - SKIP: anchor packet is empty.");
            return;
        }

        Sampleton.Log($"  + valid anchor packet received!");

        var roomAnchorSet = PublishedAnchors;

        var uuids = new HashSet<Guid>();
        int offset = 1;
        for (var i = 0; i < nUuidsShared; i++)
        {
            // using a ReadOnlySpan is efficient because the packet array never needs to be copied into buffers
            var uuid = new Guid(new ReadOnlySpan<byte>(uuidsPacket, start: offset, length: k_UuidSize));
            offset += k_UuidSize;

            Debug.LogFormat(
                LogType.Log,
                LogOption.NoStacktrace,
                context: this,
                $"{nameof(CheckForAnchorsShared)}: unpacked {uuid}"
            );

            if (roomAnchorSet.Add(uuid))
                uuids.Add(uuid);
        }

        Sampleton.Log($"{nameof(CheckForAnchorsShared)}: RPC DONE. {uuids.Count} anchor IDs received.");

        if (uuids.Count > 0)
            SharedAnchorLoader.LoadAnchorsFromRemote(uuids);
    }

    #endregion

    #region [User list state handling]

    public static HashSet<ulong> GetRoomUserIds(bool refresh = false)
    {
        if (!refresh && s_RoomUserIds.Count > 0)
            return s_RoomUserIds;

        s_RoomUserIds.Clear();
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.TryGetPlatformID(out ulong uid))
                s_RoomUserIds.Add(uid);
        }

        return s_RoomUserIds;
    }

    void UpdateUserListUI()
    {
        if (!controlPanel || !PhotonNetwork.InRoom)
            return;

        var bob = new System.Text.StringBuilder("Users:");

        foreach (var player in PhotonNetwork.PlayerList)
        {
            bob.Append("\n- ").Append(player.ToStringFull());
        }

        controlPanel.SetUserText(bob.ToString());
    }

    #endregion


    static IEnumerator DelayCall(Action call, float sec)
    {
        yield return new WaitForSecondsRealtime(sec);
        call();
    }

}
