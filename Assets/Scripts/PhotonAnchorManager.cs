// Copyright (c) Meta Platforms, Inc. and affiliates.

using Oculus.Platform;

using Photon.Pun;
using Photon.Realtime;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using Hashtable = ExitGames.Client.Photon.Hashtable;
using Sampleton = SampleController; // only transitional

/// <summary>
/// Manages Photon Room creation and maintenance, and tracks anchor sharing information.
/// </summary>
public class PhotonAnchorManager : MonoBehaviourPunCallbacks
{
    //
    // Static interface

    /// <remarks>
    ///     This collection may contain anchor Guids shared by multiple users.
    /// </remarks>
    public static IReadOnlyCollection<Guid> AllPublishedAnchors
    {
        get
        {
            var room = PhotonNetwork.CurrentRoom;
            if (room is null)
                return Array.Empty<Guid>();

            int myId = PhotonNetwork.LocalPlayer.ActorNumber;
            var anchors = new HashSet<Guid>();

            // looping through this way allows us to reload shared anchors even
            // from players who have left the room (after sharing of course)
            foreach (var (boxKey, boxVal) in room.CustomProperties)
            {
                if (boxKey is not string key || !key.EndsWith(".sharees"))
                    continue;

                if (boxVal is not int[] actorIds || !actorIds.Contains(myId))
                    continue;

                if (!room.CustomProperties.TryGetValue(key.Replace(".sharees", ".anchors"), out var box))
                    continue;

                if (box is Guid[] uuids)
                    anchors.UnionWith(uuids);
            }

            return anchors;
        }
    }

    /// <summary>
    ///     Anchors published to the current room by this local player.
    /// </summary>
    public static IReadOnlyCollection<Guid> PublishedAnchors
    {
        get
        {
            var room = PhotonNetwork.CurrentRoom;
            if (room is null ||
                !PhotonNetwork.LocalPlayer.TryGetPlatformID(out ulong myId) ||
                !room.CustomProperties.TryGetValue($"{myId}.anchors", out var box))
            {
                return Array.Empty<Guid>();
            }

            if (box is not Guid[] uuids)
            {
                Sampleton.LogError("- ERR: Expected CustomProperties[\"{ocid}.anchors\"] to be type Guid[]");
                return Array.Empty<Guid>();
            }

            return uuids;
        }
    }

    /// <remarks>
    ///     Photon won't have any valid Platform (Oculus) IDs if your app hasn't gone through
    ///     the DUC & Horizon store entitlement steps yet!
    /// </remarks>
    public static IReadOnlyCollection<ulong> RoomUserIds
    {
        get
        {
            var room = PhotonNetwork.CurrentRoom;
            if (room is null || !s_Instance)
                return Array.Empty<ulong>();

            return s_Instance.m_RoomUserIds.Keys;
        }
    }

    public static IReadOnlyCollection<int> ShareeActorIds
    {
        get
        {
            var room = PhotonNetwork.CurrentRoom;
            if (room is null ||
                !PhotonNetwork.LocalPlayer.TryGetPlatformID(out ulong ocid) ||
                !room.CustomProperties.TryGetValue($"{ocid}.sharees", out var box))
            {
                return Array.Empty<int>();
            }

            if (box is not int[] actorIds)
            {
                Sampleton.LogError("- ERR: Expected CustomProperties[\"{ocid}.sharees\"] to be type int[]");
                return Array.Empty<int>();
            }

            return actorIds;
        }
    }


    public static void PublishAnchorToUsers(Guid oneAnchor, IReadOnlyCollection<ulong> userIds)
    {
        if (!s_Instance || !PhotonNetwork.InRoom)
            return;

        s_Instance.PublishAnchorsToUsers(new HashSet<Guid> { oneAnchor }, userIds);
    }

    public static void PublishAnchorsToUsers(IEnumerable<Guid> anchorUuids, IReadOnlyCollection<ulong> userIds)
    {
        if (!s_Instance || !PhotonNetwork.InRoom)
            return;

        if (anchorUuids is not HashSet<Guid> uuidSet)
            uuidSet = new HashSet<Guid>(anchorUuids);

        s_Instance.PublishAnchorsToUsers(uuidSet, userIds);
    }


    // static impl.

    static PhotonAnchorManager s_Instance;

    // Keeping this block to illustrate each user's contribution to the Room.CustomProperties layout:
    // static readonly Hashtable s_RoomPropLayout = new()
    // {
    //     ["{ocid}.anchors"] = Array.Empty<Guid>(),
    //     ["{ocid}.sharees"] = Array.Empty<int>(), // actor numbers
    // };

    // This arch utilizes actor numbers to identify sharees, so that when a
    // player leaves and returns to a Room, they will be assigned a new
    // ActorNumber and will therefore be seen as requiring any shared anchors
    // from other room members to be shared again (even if superfluously)
    // before they will be loaded.
    //
    // This also prevents them from trying to load anchors that were shared to
    // the room while they were absent, which would be an error UNTIL the owner
    // of these shared anchors completes SharedAnchor.ShareAllMineTo(saidUser)
    // - see OnPlayerEnteredRoom.


    //
    // instance impl.

    [SerializeField]
    SharedAnchorControlPanel controlPanel;


    readonly Dictionary<ulong, int> m_RoomUserIds = new();

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
        PhotonNetwork.ConnectUsingSettings();

        // init Oculus.Platform.Core + GetLoggedInUser (for username, scoped OC_ID)
        Sampleton.Log($"Oculus.Platform.Core.Initialize()...");
        try
        {
            Core.LogMessages = true; // uses a lot of heap memory, but helpful for debugging platform connection issues.

            // (maybe a) KEY API CALL: static Oculus.Platform.Core.Initialize()
            Core.Initialize();
        }
        catch (UnityException e)
        {
            Sampleton.LogError($"Oculus.Platform.Core.Initialize FAILED: {e.Message}");
            // "UnityException: Update your app id by selecting 'Oculus Platform' -> 'Edit Settings'"
            //  (   Although note, this error message is outdated.
            //      The modern menu path is 'Meta' > 'Platform' > 'Edit Settings'.  )
        }

        if (Core.IsInitialized())
        {
            // (maybe a) KEY API CALL: static Oculus.Platform.Users.GetLoggedInUser()
            Users.GetLoggedInUser().OnComplete(GetLoggedInUserCallback);
        }

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
        if (pause)
            return;

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
            var err = msg.GetError();
            string codeStr = err.Code switch
            {
                2 => $"AUTHENTICATION_ERROR({err.Code})",
                3 => $"NETWORK_ERROR({err.Code})",
                4 => $"STORE_INSTALLATION_ERROR({err.Code})",
                5 => $"CALLER_NOT_SIGNED({err.Code})",
                6 => $"UNKNOWN_SERVER_ERROR({err.Code})",
                7 => $"PERMISSIONS_FAILURE({err.Code})",
                _ => $"UNKNOWN_ERROR({err.Code})"
            };
            Sampleton.LogError($"{nameof(GetLoggedInUserCallback)}: FAILED: {codeStr}");
            return;
        }

        Sampleton.Log($"{nameof(GetLoggedInUserCallback)}: received {msg.Type}");

        if (msg.Type != Message.MessageType.User_GetLoggedInUser)
            return;

        var ocid = msg.GetUser().ID;
        var username = msg.GetUser().OculusID;

        Sampleton.Log($"  + oculus user name: '{username}'");
        Sampleton.Log($"  + oculus id: {ocid}");

        PhotonNetwork.LocalPlayer.SetPlatformID(ocid);

        if (ocid == 0)
        {
            Sampleton.LogError(
                "You are not authenticated to use this app. User-based anchor sharing in this sample scene will not work."
            );
            return;
        }

        PhotonNetwork.NickName = username;
    }

    #region [Photon Callbacks]

    public override void OnConnectedToMaster()
    {
        Sampleton.Log($"Photon::OnConnectedToMaster: CloudRegion='{PhotonNetwork.CloudRegion}'");

        if (controlPanel)
            controlPanel.ToggleRoomButtons(true);

        PhotonNetwork.JoinLobby();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        const float kRetryDelaySec = 2f;

        var reachability = UnityEngine.Application.internetReachability;
        if (reachability == NetworkReachability.NotReachable)
        {
            // cause is typically DnsExceptionOnConnect
            Sampleton.Log($"Photon::OnDisconnected: {cause}\n- NetworkReachability.<b>{reachability} (check your wifi)</b>", LogType.Error);
            return;
        }

        switch (cause)
        {
            // unexpected (error) cases:
            case DisconnectCause.DisconnectByServerLogic: // error since the sample code never sends this signal
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

            // warning/retry cases:
            case DisconnectCause.Exception:
            case DisconnectCause.ExceptionOnConnect:
            case DisconnectCause.DnsExceptionOnConnect:
            case DisconnectCause.ClientTimeout:
            case DisconnectCause.ServerTimeout:
            case DisconnectCause.DisconnectByDisconnectMessage: // Photon's server can send this; logcat has details
            case DisconnectCause.DisconnectByServerReasonUnknown:
                Sampleton.Log($"Photon::OnDisconnected: {cause}", LogType.Warning);
                Sampleton.Log($"+ Attempting auto ReconnectAndRejoin() in {kRetryDelaySec:0} secs..");
                if (m_OnDisconnectDelayCall is not null)
                    StopCoroutine(m_OnDisconnectDelayCall);
                m_OnDisconnectDelayCall = StartCoroutine(DelayCall(() => PhotonNetwork.ReconnectAndRejoin(), kRetryDelaySec));
                return;

            // expected cases:
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

    public override void OnCreatedRoom()
    {
        Sampleton.Log($"Photon::OnCreatedRoom:");
    }

    public override void OnJoinedRoom()
    {
        var room = PhotonNetwork.CurrentRoom;
        Sampleton.Log($"Photon::OnJoinedRoom: \"{room.Name}\"");

        m_RoomUserIds.Clear();
        foreach (var (actorId, player) in room.Players)
        {
            if (player.TryGetPlatformID(out ulong ocid))
                m_RoomUserIds[ocid] = actorId;
        }

        if (controlPanel)
            controlPanel.SetRoomText($"Photon Room: {room.Name}");

        UpdateUserListUI();

        if (controlPanel)
            controlPanel.DisplayMenuPanel();
    }

    public override void OnLeftRoom()
    {
        Sampleton.Log($"Photon::OnLeftRoom");
        m_RoomUserIds.Clear();
        if (controlPanel)
            controlPanel.DisplayLobbyPanel();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Sampleton.Log($"Photon::OnPlayerEnteredRoom: {newPlayer}");

        if (newPlayer.TryGetPlatformID(out ulong ocid))
        {
            m_RoomUserIds[ocid] = newPlayer.ActorNumber;

            Sampleton.Log("  * (auto-sharing your pre-shared anchors to them...)");

            SharedAnchor.ShareAllMineTo(ocid, reshareOnly: true);
        }
        else
        {
            Sampleton.Log($"  - (player #{newPlayer.ActorNumber:00} lacks a valid Platform ID.)");
        }

        UpdateUserListUI();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdateUserListUI();

        if (otherPlayer.IsInactive)
        {
            Sampleton.Log($"Photon::OnPlayerLeftRoom: {otherPlayer} has gone inactive.");
            return;
        }

        Sampleton.Log($"Photon::OnPlayerLeftRoom: {otherPlayer} has left.");
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        if (controlPanel)
            controlPanel.SetRoomList(roomList);
    }

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        Sampleton.Log($"Photon::OnRoomPropertiesUpdate: n={changedProps.Count}");

        var player = PhotonNetwork.LocalPlayer;
        if (!player.TryGetPlatformID(out ulong ocid))
        {
            Sampleton.Log("  - SKIPPED: not logged in to Platform.");
            return;
        }

        foreach (var (boxKey, boxVal) in changedProps)
        {
            if (boxKey is not string key || !key.EndsWith(".sharees") || key.StartsWith($"{ocid}."))
                continue;

            if (boxVal is int[] sharees && sharees.Contains(player.ActorNumber))
            {
                SharedAnchorLoader.ReloadSharedAnchors();
                return;
            }
        }
    }

    #endregion

    #region [Room creation, P2P connection, and inviting others]

    public void OnCreateRoomButtonPressed()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Sampleton.Log($"{nameof(OnCreateRoomButtonPressed)}: Photon connection not ready!", LogType.Warning);
            if (!PhotonNetwork.IsConnected)
                tryNewConnection();
            return;
        }

        Sampleton.Log($"{nameof(OnCreateRoomButtonPressed)}: Photon is connected.");

        string newRoomName = $"{PhotonNetwork.NickName}'s room";

        Sampleton.Log($"+ Attempting to host a new room named \"{newRoomName}\"...");

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

        tryNewConnection();

        return;

        static void tryNewConnection()
        {
            Sampleton.LogError($"ERR: Room creation request not sent to server!");
            Sampleton.Log($"  - Attempting PhotonNetwork.ConnectUsingSettings()...", LogType.Warning);
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public void OnJoinRoomButtonPressed(TMPro.TextMeshProUGUI roomName)
    {
        if (!roomName)
        {
            Sampleton.LogError($"{nameof(OnJoinRoomButtonPressed)}: Missing reference to room name component!");
            return;
        }

        if (string.IsNullOrEmpty(roomName.text))
        {
            Sampleton.LogError($"{nameof(OnJoinRoomButtonPressed)}: given room name is empty!");
            return;
        }

        Sampleton.Log($"{nameof(OnJoinRoomButtonPressed)}: \"{roomName.text}\"");

        PhotonNetwork.JoinRoom(roomName.text);
    }

    public void OnFindRoomButtonPressed()
    {
        if (!PhotonNetwork.IsConnected)
        {
            if (PhotonNetwork.ConnectUsingSettings())
                Sampleton.Log("PhotonNetwork disconnected. Try again after this attempt to reconnect...", LogType.Warning);
            else
                Sampleton.Log("PhotonNetwork disconnected, and cannot attempt to reconnect at this time.", LogType.Error);
            return;
        }

        if (controlPanel)
            controlPanel.ToggleRoomLayoutPanel(true);
    }

    #endregion

    #region [Send and read room data]

    void PublishAnchorsToUsers(HashSet<Guid> uuidSet, IReadOnlyCollection<ulong> userIds)
    {
        if (!PhotonNetwork.LocalPlayer.TryGetPlatformID(out ulong myId))
        {
            Sampleton.LogError($"{nameof(PublishAnchorsToUsers)}: FAILED: a working platform (oculus) login is required.");
            return;
        }

        var room = PhotonNetwork.CurrentRoom;
        var alreadyShared = PublishedAnchors;
        var sharedToActors = new HashSet<int>(ShareeActorIds);

        uuidSet.UnionWith(alreadyShared);

        int nNewIds = uuidSet.Count - alreadyShared.Count;
        nNewIds -= sharedToActors.Count;
        sharedToActors.UnionWith(userIds.Select(id => m_RoomUserIds[id]));
        nNewIds += sharedToActors.Count;

        if (nNewIds == 0)
        {
            Sampleton.Log($"{nameof(PublishAnchorsToUsers)}: SKIP: zero new anchor/sharee ids.");
            return;
        }

        Sampleton.Log($"{nameof(PublishAnchorsToUsers)}: {nNewIds}/{uuidSet.Count + sharedToActors.Count} new ids");

        var uuidArr = new Guid[uuidSet.Count];
        uuidSet.CopyTo(uuidArr);

        var actorIdArr = new int[sharedToActors.Count];
        sharedToActors.CopyTo(actorIdArr);

        var props = new Hashtable
        {
            [$"{myId}.anchors"] = uuidArr,    // (this only works thanks to PhotonExtensions.cs)
            [$"{myId}.sharees"] = actorIdArr,
        };

        if (!room.SetCustomProperties(props))
            Sampleton.LogError("- ERR: room.SetCustomProperties failed!");
    }

    #endregion

    #region [User list state handling]

    void UpdateUserListUI()
    {
        if (!controlPanel || !PhotonNetwork.InRoom)
            return;

        var bob = new System.Text.StringBuilder("Users:");

        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.IsInactive)
                continue;
            if (player.TryGetPlatformID(out ulong ocid))
                bob.Append($"{player} {{ocid={ocid}}}");
            else
                bob.Append($"{player} {{ocid=None}}");
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
