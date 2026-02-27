// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;

using Oculus.Platform;

using Photon.Pun;
using Photon.Realtime;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.Assertions;

using Hashtable = ExitGames.Client.Photon.Hashtable;
using Sampleton = SampleController; // only transitional

/// <summary>
/// Manages Photon Room creation and maintenance, and tracks anchor sharing information.
/// </summary>
[MetaCodeSample("SharedSpatialAnchors")]
public class PhotonAnchorManager : MonoBehaviourPunCallbacks
{
    //
    // Static interface

    /// <remarks>
    ///     This collection may contain anchor Guids shared by multiple users.
    /// </remarks>
    public static IReadOnlyCollection<Guid> AnchorsSharedWithMe
    {
        get
        {
            var room = PhotonNetwork.CurrentRoom;
            if (room is null ||
                !PhotonNetwork.LocalPlayer.TryGetPlatformID(out ulong uid) ||
                !room.CustomProperties.TryGetValue($"{k_PropPrefixUser}{uid}", out var box) ||
                box is not Guid[] anchors)
            {
                return Array.Empty<Guid>();
            }
            return anchors;
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
            if (room is null)
                return Array.Empty<ulong>();

            var userIds = new List<ulong>(capacity: room.PlayerCount);

            foreach (var player in room.Players.Values)
            {
                if (player.TryGetPlatformID(out ulong uid))
                    userIds.Add(uid);
            }

            return userIds;
        }
    }


    public static void PublishAnchorToUsers(Guid oneAnchor, IReadOnlyCollection<ulong> userIds)
    {
        if (!PhotonNetwork.InRoom)
            return;

        PublishAnchorsToUsers(new HashSet<Guid> { oneAnchor }, userIds);
    }

    public static void PublishAnchorsToUsers(IEnumerable<Guid> anchorUuids, IReadOnlyCollection<ulong> userIds)
    {
        if (!PhotonNetwork.InRoom)
            return;

        if (anchorUuids is not HashSet<Guid> uuidSet)
            uuidSet = new HashSet<Guid>(anchorUuids);

        PublishAnchorsToUsers(uuidSet, userIds);
    }

    public static void PublishAlignmentAnchor(Guid anchorUuid, Pose anchorPoseOnHost)
    {
        if (!PhotonNetwork.InRoom)
            return;

        PrepareRoomPropertyUpdate(out var room, out var newProps, out var expected);

        newProps[k_PropKeyAlignId] = anchorUuid;
        newProps[k_PropKeyHostOff] = anchorPoseOnHost;

        if (!room.SetCustomProperties(newProps, expected))
            Sampleton.LogError("- ERR: room.SetCustomProperties failed! (possible concurrency failure)");
    }

    public static bool CheckIsAlignmentAnchor(SharedAnchor anchor, out Pose anchorPoseOnHost)
    {
        anchorPoseOnHost = default;

        if (!anchor || anchor.Uuid == Guid.Empty || PhotonNetwork.CurrentRoom is null)
            return false;

        var props = PhotonNetwork.CurrentRoom.CustomProperties;
        if (!props.TryGetValue(k_PropKeyAlignId, out var box) || box is not Guid alignId ||
            alignId != anchor.Uuid ||
            !props.TryGetValue(k_PropKeyHostOff, out box) || box is not Pose poseOnHost)
        {
            return false;
        }

        anchorPoseOnHost = poseOnHost;
        // let caller call Alignment.SetMRUKOrigin(...)
        return true;
    }


    // static impl.

    const string k_PropKeyVersion = "ver";
    const string k_PropKeySender = "src";
    const string k_PropPrefixUser = "u:";
    const string k_PropKeyAlignId = "aa";
    const string k_PropKeyHostOff = "ho";

    // Keeping this block to illustrate each user's contribution to the Room.CustomProperties layout:
    // static readonly Hashtable s_RoomPropLayout = new()
    // {
    //     [k_PropKeyVersion] = 0,                              // increments with each successful [re]share to the room
    //     [k_PropKeySender]  = "#01 'userface'",               // unique sender name of the last property update
    //     [$"{k_PropPrefixUser}{ocid}"] = Array.Empty<Guid>(), // ocid = Platform User ID, vals = loadable anchor IDs
    //     [k_PropKeyAlignId] = Guid.Empty,                     // alignment anchor uuid
    //     [k_PropKeyHostOff] = Pose.identity,                  // alignment anchor pose in the host's space
    // };


    static void PrepareRoomPropertyUpdate(out Room room, out Hashtable newProps, out Hashtable expectedProps)
    {
        room = PhotonNetwork.CurrentRoom;
        Assert.IsNotNull(room, "PhotonNetwork.CurrentRoom");

        expectedProps = null;

        int currentVersion = 0;
        if (room.CustomProperties.TryGetValue(k_PropKeyVersion, out var box) && box is int v)
        {
            expectedProps = new Hashtable
            {
                [k_PropKeyVersion] = currentVersion = v,
            };
        }

        newProps = new Hashtable
        {
            [k_PropKeyVersion] = currentVersion + 1,
            [k_PropKeySender] = $"{PhotonNetwork.LocalPlayer}",
        };
    }

    static void PublishAnchorsToUsers(HashSet<Guid> uuidSet, IReadOnlyCollection<ulong> userIds)
    {
        PrepareRoomPropertyUpdate(out var room, out var newProps, out var expected);

        var curProps = room.CustomProperties;
        var uuidArr = uuidSet.ToArray();
        var scratchUuidSet = new HashSet<Guid>(uuidSet.Count);
        foreach (ulong uid in userIds)
        {
            string key = $"{k_PropPrefixUser}{uid}";

            // (note: setting Guid[] values directly only works here thanks to our PhotonExtensions.cs)
            if (curProps.TryGetValue(key, out var box) && box is Guid[] alreadyShared)
            {
                scratchUuidSet.Clear();
                scratchUuidSet.UnionWith(alreadyShared);
                int precount = scratchUuidSet.Count;
                scratchUuidSet.UnionWith(uuidSet);
                if (scratchUuidSet.Count > precount)
                    newProps[key] = scratchUuidSet.ToArray();
            }
            else
            {
                newProps[key] = uuidArr;
            }
        }

        if (!room.SetCustomProperties(newProps, expected))
            Sampleton.LogError("- ERR: room.SetCustomProperties failed! (possible concurrency failure)");
    }

    static void SynchronizeRoomAlignment(Hashtable roomProperties)
    {
        object box;
        if (!roomProperties.TryGetValue(k_PropKeyAlignId, out box) || box is not Guid alignId ||
            !roomProperties.TryGetValue(k_PropKeyHostOff, out box) || box is not Pose anchorPoseOnHost)
        {
            Sampleton.Log("  ~ room alignment unchanged.");
            return;
        }

        if (!SharedAnchor.Find(alignId, out var alignmentAnchor))
        {
            Sampleton.Log($"  ~ deferring alignment; anchor {alignId.Brief()} not ready.");
            return;
        }

        Alignment.SetMRUKOrigin(alignmentAnchor, anchorPoseOnHost);
    }

    static IEnumerator DelayCall(Action call, float sec)
    {
        yield return new WaitForSecondsRealtime(sec);
        call();
    }


    //
    // instance impl.

    [SerializeField]
    SharedAnchorControlPanel controlPanel;

    Coroutine m_OnDisconnectDelayCall;


    #region [Monobehaviour Methods]

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
            // "UnityException: Update your app id by selecting 'Meta' > 'Platform' > 'Edit Settings'"
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

        if (controlPanel)
        {
            controlPanel.SetRoomText($"Photon Room: {room.Name}");

            UpdateUserListUI();

            controlPanel.DisplayMenuPanel();
        }
    }

    public override void OnLeftRoom()
    {
        Sampleton.Log($"Photon::OnLeftRoom");

        if (Alignment.IsSet)
            Alignment.ResetMRUKOrigin();

        foreach (var anchor in SharedAnchor.All)
        {
            if (!anchor)
                continue;

            Sampleton.Log($"  - unloading {anchor.Uuid.Brief()}");

            Destroy(anchor.gameObject);
        }

        if (controlPanel)
            controlPanel.DisplayLobbyPanel();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Sampleton.Log($"Photon::OnPlayerEnteredRoom: {newPlayer}");

        if (newPlayer.TryGetPlatformID(out ulong ocid))
        {
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
        var keysForLogging = changedProps.Keys
            .Select(k => k.ToString())
            .Where(k => k != k_PropKeyVersion && k != k_PropKeySender);

        Sampleton.Log($"Photon::OnRoomPropertiesUpdate: {string.Join(", ", keysForLogging)}");

        var player = PhotonNetwork.LocalPlayer;

        if (changedProps.TryGetValue(k_PropKeySender, out var box) && box is string sender)
        {
            if (sender == $"{player}")
            {
                Sampleton.Log("  x skipped (you are the sender)");
                return;
            }

            Sampleton.Log($"  + sender: '{sender}'");
        }

        /* update alignment anchor (iff it's already loaded and done w/ Awake) */

        SynchronizeRoomAlignment(changedProps);
        // note: We do want to call this before loading any new anchors, since new anchors will check themselves if
        //       they are the alignment anchor asynchronously (in SharedAnchor::Awake).  This is the way because
        //       it isn't a good idea to align to an anchor before it has finished asynchronously localizing.

        /* update & load anchors */

        if (!player.TryGetPlatformID(out ulong ocid))
        {
            Sampleton.Log("  - WARN: not logged in to Platform.", LogType.Warning);
            return;
        }

        if (!changedProps.TryGetValue($"{k_PropPrefixUser}{ocid}", out box))
        {
            Sampleton.Log("  ~ no new anchors to load;");
            return;
        }

        if (box is not Guid[] anchors)
        {
            Sampleton.LogError("  - ERR: expected CustomProperties[ocid] to be type Guid[]");
            return;
        }

        SharedAnchorLoader.LoadSharedAnchors(anchors);
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
            bob.Append('\n');
            if (player.TryGetPlatformID(out ulong ocid))
                bob.Append($"{player} {{ocid={ocid}}}");
            else
                bob.Append($"{player} {{ocid=None}}");
        }

        controlPanel.SetUserText(bob.ToString());
    }

    #endregion

}
