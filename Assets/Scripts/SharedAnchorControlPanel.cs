// Copyright (c) Meta Platforms, Inc. and affiliates.
// This code is licensed under the MIT license (see LICENSE for details).

using Photon.Pun;
using Photon.Realtime;

using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using TMPro;

using Sampleton = SampleController; // only transitional

public class SharedAnchorControlPanel : MonoBehaviour
{
    //
    // public interface

    #region Serialized Fields

    [SerializeField]
    Transform referencePoint;

    [SerializeField]
    GameObject roomLayoutPanelRowPrefab;

    [SerializeField]
    Transform spawnPoint;

    [SerializeField]
    GameObject menuPanel;

    [SerializeField]
    GameObject lobbyPanel;

    [SerializeField]
    GameObject roomLayoutPanel;

    [SerializeField]
    Button createRoomButton;

    [SerializeField]
    Button joinRoomButton;

    [SerializeField]
    Graphic anchorIcon;

    [SerializeField]
    TMP_Text logText; // TODO

    [SerializeField]
    TMP_Text pageText;

    [SerializeField]
    TMP_Text statusText;

    [SerializeField]
    TMP_Text roomText;


    [SerializeField]
    TMP_Text userText;

    bool m_IsCreateMode;

    #endregion Serialized Fields


    #region UnityEvent (UI) Listeners

    public void OnReturnToMenuButtonPressed()
    {
        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom(becomeInactive: false);
        SceneManager.LoadScene(0);
    }

    public void OnCreateModeButtonPressed()
    {
        if (!m_IsCreateMode)
        {
            SampleController.Instance.StartPlacementMode();
            anchorIcon.color = Color.green;
            m_IsCreateMode = true;
        }
        else
        {
            SampleController.Instance.EndPlacementMode();
            anchorIcon.color = Color.white;
            m_IsCreateMode = false;
        }
    }

    public void OnLoadSavedAnchorsButtonPressed()
    {
        Sampleton.Log(nameof(OnLoadSavedAnchorsButtonPressed));
        SharedAnchorLoader.LoadSavedAnchors();
    }

    public void OnLoadSharedAnchorsButtonPressed()
    {
        Sampleton.Log(nameof(OnLoadSharedAnchorsButtonPressed));
        SharedAnchorLoader.ReloadSharedAnchors();
    }

    public void OnSpawnCubeButtonPressed()
    {
        Sampleton.Log(nameof(OnSpawnCubeButtonPressed));

        var networkedCube = PhotonNetwork.Instantiate("PhotonGrabbableCube", spawnPoint.position, spawnPoint.rotation);
        var photonGrabbable = networkedCube.GetComponent<PhotonGrabbableObject>();
        photonGrabbable.TransferOwnershipToLocalPlayer();
    }

    public void LogNext()
    {
        if (logText.pageToDisplay >= logText.textInfo.pageCount)
            return;

        logText.pageToDisplay++;
        if (pageText)
            pageText.text = $"{logText.pageToDisplay}/{logText.textInfo.pageCount}";
    }

    public void LogPrev()
    {
        if (logText.pageToDisplay <= 1)
        {
            logText.pageToDisplay = 1;
            return;
        }

        logText.pageToDisplay--;
        if (pageText)
            pageText.text = $"{logText.pageToDisplay}/{logText.textInfo.pageCount}";
    }

    #endregion UnityEvent (UI) Listeners


    #region Public Methods

    public void DisplayMenuPanel()
    {
        lobbyPanel.SetActive(false);
        menuPanel.SetActive(true);
    }

    public void DisplayLobbyPanel()
    {
        menuPanel.SetActive(false);
        lobbyPanel.SetActive(true);
    }

    public void ToggleRoomLayoutPanel(bool active)
    {
        roomLayoutPanel.SetActive(active);
    }

    public void ToggleRoomButtons(bool active)
    {
        if (createRoomButton)
            createRoomButton.interactable = active;

        if (joinRoomButton)
            joinRoomButton.interactable = active;
    }

    public void SetRoomList(List<RoomInfo> roomList)
    {
        foreach (Transform roomTransform in roomLayoutPanel.transform)
        {
            if (roomTransform.gameObject != roomLayoutPanelRowPrefab)
                Destroy(roomTransform.gameObject);
        }

        if (roomList.Count == 0)
            return;

        foreach (var room in roomList)
        {
            var newLobbyRow = Instantiate(roomLayoutPanelRowPrefab, roomLayoutPanel.transform);
            newLobbyRow.GetComponentInChildren<TextMeshProUGUI>().text = room.Name;
            newLobbyRow.SetActive(true);
        }
    }

    public void SetStatusText(string text)
    {
        statusText.text = text;
    }

    public void SetUserText(string text)
    {
        userText.text = text;
    }

    public void SetRoomText(string text)
    {
        roomText.text = text;
    }

    #endregion Public Methods

    //
    // private impl.

    void Start()
    {
        transform.parent = referencePoint;
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        if (roomLayoutPanelRowPrefab.scene == gameObject.scene)
        {
            roomLayoutPanelRowPrefab.SetActive(false);
        }

        ToggleRoomButtons(PhotonNetwork.IsConnected);
        ToggleRoomLayoutPanel(false);
        DisplayLobbyPanel();

        Sampleton.Log("System version: " + OVRPlugin.version);
    }

}
