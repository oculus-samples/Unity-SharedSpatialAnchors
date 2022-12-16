﻿using System.Collections;
/*
* Copyright (c) Meta Platforms, Inc. and affiliates.
* All rights reserved.
*
* Licensed under the Oculus SDK License Agreement (the "License");
* you may not use the Oculus SDK except in compliance with the License,
* which is provided at the time of installation or download, or which
* otherwise accompanies this software in either electronic or hard copy form.
*
* You may obtain a copy of the License at
*
* https://developer.oculus.com/licenses/oculussdk/
*
* Unless required by applicable law or agreed to in writing, the Oculus SDK
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using Common;
using Oculus.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using PhotonPun = Photon.Pun;
using PhotonRealtime = Photon.Realtime;

/// <summary>
/// Manages Photon Room creation and maintenence, and tracks anchor sharing information.
/// </summary>
public class PhotonAnchorManager : PhotonPun.MonoBehaviourPunCallbacks
{
    [SerializeField]
    private SharedAnchorControlPanel controlPanel;

    [SerializeField]
    private PhotonLobbyPanel lobbyPanel;

    public static PhotonAnchorManager Instance;

    private const string UserIdsKey = "userids";
    private const char Separator = ',';
    private const byte PacketFormat = 0;

    // The size of the packet we are sending and receiving
    private const int UuidSize = 16;

    // Reusable buffer to serialize the data into
    private byte[] _sendUuidBuffer = new byte[1];
    private byte[] _getUuidBuffer = new byte[UuidSize];
    private byte[] _fakePacket = new byte[1];
    private string _oculusUsername;
    private ulong _oculusUserId;
    private Guid _fakeUuid;

    private readonly HashSet<string> _usernameList = new HashSet<string>();
    private readonly HashSet<Guid> _uuidsSharedWithMe = new HashSet<Guid>();

    #region [Monobehaviour Methods]

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    private IEnumerator Start()
    {
        SampleController.Instance.Log("System version: " + OVRPlugin.version);
        PhotonPun.PhotonNetwork.ConnectUsingSettings();
        Core.Initialize();
        
        while(Core.IsInitialized())
        yield return null;

        Oculus.Platform.Entitlements.IsUserEntitledToApplication().OnComplete(OnEntitlementCallback);
    }

    void OnEntitlementCallback(Message msg)
    {
        if(msg.IsError)
        {
            SampleController.Instance.Log("Entitlement Failed: failed with error: " + msg.GetError());
            return;
        }
        
        Users.GetLoggedInUser().OnComplete(GetLoggedInUserCallback);

        Array.Resize(ref _fakePacket, 1 + UuidSize);
        _fakePacket[0] = PacketFormat;

        var offset = 1;
        var fakeBytes = new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff };

        _fakeUuid = new Guid(fakeBytes);
        PackUuid(_fakeUuid, _fakePacket, ref offset);
    }

    #endregion

    private void GetLoggedInUserCallback(Message msg)
    {
        if (msg.IsError)
        {
            SampleController.Instance.Log("GetLoggedInUserCallback: failed with error: " + msg.GetError());
            return;
        }

        SampleController.Instance.Log("GetLoggedInUserCallback: success with message: " + msg + " type: " + msg.Type);

        var isLoggedInUserMessage = msg.Type == Message.MessageType.User_GetLoggedInUser;

        if (!isLoggedInUserMessage)
        {
            return;
        }

        _oculusUsername = msg.GetUser().OculusID;
        _oculusUserId = msg.GetUser().ID;

        SampleController.Instance.Log("GetLoggedInUserCallback: oculus user name: " + _oculusUsername + " oculus id: " + _oculusUserId);

        if (_oculusUserId == 0)
            SampleController.Instance.Log("You are not authenticated to use this app. Shared Spatial Anchors will not work.");

        PhotonPun.PhotonNetwork.LocalPlayer.NickName = _oculusUsername;
    }

    private void OnApplicationPause(bool pause)
    {
        if (!pause)
        {
            if (PhotonPun.PhotonNetwork.IsConnected)
            {
                SampleController.Instance.Log("Application Un-paused: Attempting to reconnect and rejoin a Photon room");
                PhotonPun.PhotonNetwork.ReconnectAndRejoin();
            }
            else
            {
                SampleController.Instance.Log("Application Un-paused: Connecting to a Photon server. Please join or create a room.");
                PhotonPun.PhotonNetwork.ConnectUsingSettings();
            }
        }
    }

    #region [Photon Callbacks]

    public override void OnConnectedToMaster()
    {
        SampleController.Instance.Log("Photon::OnConnectedToMaster: successfully connected to photon: " + PhotonPun.PhotonNetwork.CloudRegion);

        PhotonPun.PhotonNetwork.JoinLobby();

        if (lobbyPanel)
            lobbyPanel.EnableRoomButtons();
    }

    public override void OnDisconnected(PhotonRealtime.DisconnectCause cause)
    {
        SampleController.Instance.Log("Photon::OnDisconnected: failed to connect: " + cause);

        if (cause != PhotonRealtime.DisconnectCause.DisconnectByClientLogic)
        {
            Photon.Pun.PhotonNetwork.ReconnectAndRejoin();
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        SampleController.Instance.Log("Photon::OnJoinRoomFailed: " + message);

        if (lobbyPanel)
        {
            lobbyPanel.DisplayLobbyPanel();
        }
    }

    public override void OnJoinedRoom()
    {
        SampleController.Instance.Log("Photon::OnJoinedRoom: joined room: " + PhotonPun.PhotonNetwork.CurrentRoom.Name);

        controlPanel.RoomText.text = "Room: " + PhotonPun.PhotonNetwork.CurrentRoom.Name;

        AddUserToUserListState(_oculusUserId);

        foreach (var player in PhotonPun.PhotonNetwork.CurrentRoom.Players.Values)
        {
            AddToUsernameList(player.NickName);
        }

        if (SampleController.Instance.automaticCoLocation)
        {
            Photon.Pun.PhotonNetwork.Instantiate("PassthroughAvatarPhoton", Vector3.zero, Quaternion.identity);
        }

        if (lobbyPanel)
        {
            lobbyPanel.gameObject.SetActive(false);
        }

        GameObject sceneCaptureController = GameObject.Find("SceneCaptureController");
        if (sceneCaptureController)
        {
            if (Photon.Pun.PhotonNetwork.IsMasterClient)
            {
                sceneCaptureController.GetComponent<SceneApiSceneCaptureStrategy>().InitSceneCapture();
                sceneCaptureController.GetComponent<SceneApiSceneCaptureStrategy>().BeginCaptureScene();
            }
            else
            {
                LoadRoomFromProperties();
            }
        }
    }

    public override void OnPlayerEnteredRoom(PhotonRealtime.Player newPlayer)
    {
        SampleController.Instance.Log("Photon::OnPlayerEnteredRoom: new player joined room: " + newPlayer.NickName);

        AddToUsernameList(newPlayer.NickName);

        if (SampleController.Instance.automaticCoLocation)
        {
            Invoke(nameof(WaitToSendAnchor), 1);
        }
    }

    private void WaitToSendAnchor()
    {
        SampleController.Instance.colocationAnchor.OnShareButtonPressed();
    }

    public override void OnPlayerLeftRoom(PhotonRealtime.Player otherPlayer)
    {
        SampleController.Instance.Log("Photon::OnPlayerLeftRoom: player left room: " + otherPlayer.NickName);

        RemoveFromUsernameList(otherPlayer.NickName);
    }

    public override void OnCreatedRoom()
    {
        SampleController.Instance.Log("Photon::OnCreatedRoom: created room: " + PhotonPun.PhotonNetwork.CurrentRoom.Name);
    }

    public override void OnRoomListUpdate(List<PhotonRealtime.RoomInfo> roomList)
    {
        if (lobbyPanel)
        {
            lobbyPanel.SetRoomList(roomList);
        }
    }

    #endregion

    #region [Room creation, P2P connection, and inviting others]
    public void CreateNewRoomForLobby(string roomToJoin)
    {
        var isValidRoomToJoin = !string.IsNullOrEmpty(roomToJoin);

        if (!isValidRoomToJoin)
        {
            return;
        }

        SampleController.Instance.Log("JoinRoomFromLobby: attempting to create room: " + roomToJoin);

        var roomOptions = new PhotonRealtime.RoomOptions { IsVisible = true, MaxPlayers = 16, EmptyRoomTtl = 0, PlayerTtl = 300000 };

        PhotonPun.PhotonNetwork.JoinOrCreateRoom(roomToJoin, roomOptions, PhotonRealtime.TypedLobby.Default);
    }

    public void JoinRoomFromLobby(string roomToJoin)
    {
        var isValidRoomToJoin = !string.IsNullOrEmpty(roomToJoin);

        if (!isValidRoomToJoin)
        {
            return;
        }

        SampleController.Instance.Log("JoinRoomFromLobby: attempting to join room: " + roomToJoin);

        var roomOptions = new PhotonRealtime.RoomOptions { IsVisible = true, MaxPlayers = 16, EmptyRoomTtl = 0, PlayerTtl = 300000 };

        PhotonPun.PhotonNetwork.JoinOrCreateRoom(roomToJoin, roomOptions, PhotonRealtime.TypedLobby.Default);
    }

    #endregion

    #region [Send and read room data]

    public void PublishAnchorUuids(Guid[] uuids, uint numUuids)
    {
        SampleController.Instance.Log("PublishAnchorUuids: numUuids: " + numUuids);

        Array.Resize(ref _sendUuidBuffer, 1 + UuidSize * (int)numUuids);
        _sendUuidBuffer[0] = PacketFormat;

        var offset = 1;
        for (var i = 0; i < numUuids; i++)
        {
            PackUuid(uuids[i], _sendUuidBuffer, ref offset);
        }

        photonView.RPC(nameof(CheckForAnchorsShared), PhotonPun.RpcTarget.OthersBuffered, _sendUuidBuffer);
    }

    private static void PackUuid(Guid uuid, byte[] buf, ref int offset)
    {
        SampleController.Instance.Log("PackUuid: packing uuid: " + uuid);

        Buffer.BlockCopy(uuid.ToByteArray(), 0, buf, offset, UuidSize);
        offset += 16;
    }

    [PhotonPun.PunRPC]
    private void CheckForAnchorsShared(byte[] uuidsPacket)
    {
        Debug.Log(nameof(CheckForAnchorsShared) + " : found a packet...");

        var isInvalidPacketSize = uuidsPacket.Length % UuidSize != 1;

        if (isInvalidPacketSize)
        {
            SampleController.Instance.Log($"{nameof(CheckForAnchorsShared)}: invalid packet size: {uuidsPacket.Length} should be 1+{UuidSize}*numUuidsShared");
            return;
        }

        var isInvalidPacketType = uuidsPacket[0] != PacketFormat;

        if (isInvalidPacketType)
        {
            SampleController.Instance.Log(nameof(CheckForAnchorsShared) + " : invalid packet type: " + uuidsPacket.Length);
            return;
        }

        var numUuidsShared = (uuidsPacket.Length - 1) / UuidSize;
        var isEmptyUuids = numUuidsShared == 0;

        if (isEmptyUuids)
        {
            SampleController.Instance.Log(nameof(CheckForAnchorsShared) + " : we received a no-op packet");
            return;
        }

        SampleController.Instance.Log(nameof(CheckForAnchorsShared) + " : we received a valid uuid packet");

        var uuids = new HashSet<Guid>();
        var offset = 1;

        for (var i = 0; i < numUuidsShared; i++)
        {
            // We need to copy exactly 16 bytes here because Guid() expects a byte buffer sized to exactly 16 bytes

            Buffer.BlockCopy(uuidsPacket, offset, _getUuidBuffer, 0, UuidSize);
            offset += UuidSize;

            var uuid = new Guid(_getUuidBuffer);

            Debug.Log(nameof(CheckForAnchorsShared) + " : unpacked uuid: " + uuid);

            var shouldExit = uuid == _fakeUuid;

            if (shouldExit)
            {
                SampleController.Instance.Log(nameof(CheckForAnchorsShared) + " : received the fakeUuid/noop... exiting");
                return;
            }

            uuids.Add(uuid);
        }

        ReceivedUuids(uuids);
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(UserIdsKey))
        {
            foreach (SharedAnchor anchor in SampleController.Instance.GetLocalPlayerSharedAnchors())
            {
                anchor.ReshareAnchor();
            }
        }

        object data;
        if (propertiesThatChanged.TryGetValue("roomData", out data))
        {
            SampleController.Instance.Log("Room data recieved from master client.");
            DeserializeToScene((byte[])data);
        }
    }

    private void LoadRoomFromProperties()
    {
        SampleController.Instance.Log(nameof(LoadRoomFromProperties));

        if (Photon.Pun.PhotonNetwork.CurrentRoom == null)
        {
            SampleController.Instance.Log("no ROOm?");
            return;
        }

        object data;
        if (Photon.Pun.PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("roomData", out data))
        {
            DeserializeToScene((byte[])data);
        }
    }

    void DeserializeToScene(byte[] byteData)
    {
        string jsonData = System.Text.Encoding.UTF8.GetString((byte[])byteData);
        Scene deserializedScene = JsonUtility.FromJson<Scene>(jsonData);
        if (deserializedScene != null)
            SampleController.Instance.Log("deserializedScene num walls: " + deserializedScene.walls.Length);
        else
            SampleController.Instance.Log("deserializedScene is NULL");

        GameObject worldGenerationController = GameObject.Find("WorldGenerationController");
        if (worldGenerationController)
            worldGenerationController.GetComponent<WorldGenerationController>().GenerateWorld(deserializedScene);
    }
    #endregion


    #region [User list state handling]

    public static HashSet<ulong> GetUserList()
    {
        if (PhotonPun.PhotonNetwork.CurrentRoom == null || !PhotonPun.PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(UserIdsKey))
        {
            return new HashSet<ulong>();
        }

        var userListAsString = (string)PhotonPun.PhotonNetwork.CurrentRoom.CustomProperties[UserIdsKey];
        var parsedList = userListAsString.Split(Separator).Select(ulong.Parse);

        return new HashSet<ulong>(parsedList);
    }

    private void AddUserToUserListState(ulong userId)
    {
        var userList = GetUserList();
        var isKnownUserId = userList.Contains(userId);

        if (isKnownUserId)
        {
            return;
        }

        userList.Add(userId);
        SaveUserList(userList);
    }

    public void RemoveUserFromUserListState(ulong userId)
    {
        var userList = GetUserList();
        var isUnknownUserId = !userList.Contains(userId);

        if (isUnknownUserId)
        {
            return;
        }

        userList.Remove(userId);
        SaveUserList(userList);
    }

    private static void SaveUserList(HashSet<ulong> userList)
    {
        var userListAsString = string.Join(Separator.ToString(), userList);
        var setValue = new ExitGames.Client.Photon.Hashtable { { UserIdsKey, userListAsString } };

        PhotonPun.PhotonNetwork.CurrentRoom.SetCustomProperties(setValue);
    }

    private void AddToUsernameList(string username)
    {
        var isKnownUserName = _usernameList.Contains(username);

        if (isKnownUserName)
        {
            return;
        }

        _usernameList.Add(username);
        UpdateUsernameListDebug();
    }

    private void RemoveFromUsernameList(string username)
    {
        var isUnknownUserName = !_usernameList.Contains(username);

        if (isUnknownUserName)
        {
            return;
        }

        _usernameList.Remove(username);
        UpdateUsernameListDebug();
    }

    private void UpdateUsernameListDebug()
    {
        controlPanel.UserText.text = "Users:";

        var usernameListAsString = string.Join(Separator.ToString(), _usernameList);
        var usernames = usernameListAsString.Split(',');

        foreach (var username in usernames)
        {
            controlPanel.UserText.text += "\n" + "- " + username;
        }
    }

    public static string[] GetUsers()
    {
        var userIdsProperty = (string)PhotonPun.PhotonNetwork.CurrentRoom.CustomProperties[UserIdsKey];

        Debug.Log("GetUsers: " + userIdsProperty);

        var userIds = userIdsProperty.Split(',');
        return userIds;
    }

    private void ReceivedUuids(HashSet<Guid> uuids)
    {
        Debug.Log("ReceivedUuids: set of uuids shared: " + uuids.Count);

        uuids.ExceptWith(_uuidsSharedWithMe);

        Debug.Log("ReceivedUuids: number of unique new uuids shared: " + uuids.Count);

        SharedAnchorLoader.Instance.LoadCloudAnchors(uuids);
        _uuidsSharedWithMe.UnionWith(uuids);
    }

    //Two users are now confirmed to be on the same anchor
    public void SessionStart()
    {
        photonView.RPC("SendSessionStart", PhotonPun.RpcTarget.Others);
        SendSessionStart();
    }


    [PhotonPun.PunRPC]
    public void SendSessionStart()
    {
        CoLocatedPassthroughManager.Instance.SessionStart();
        if (SampleController.Instance.automaticCoLocation)
        {
            Photon.Pun.PhotonNetwork.Instantiate("PassthroughAvatarPhoton", Vector3.zero, Quaternion.identity);
        }
    }

    #endregion
}
