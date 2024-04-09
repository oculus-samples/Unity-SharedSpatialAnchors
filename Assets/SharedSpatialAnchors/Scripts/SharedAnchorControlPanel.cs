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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using PhotonPun = Photon.Pun;
using PhotonRealtime = Photon.Realtime;

public class SharedAnchorControlPanel : MonoBehaviour
{
    [SerializeField]
    private Transform referencePoint;

    [SerializeField]
    private GameObject cubePrefab;
    [SerializeField]
    private GameObject roomLayoutPanelRowPrefab;

    [SerializeField]
    private Transform spawnPoint;

    [SerializeField]
    protected GameObject menuPanel;

    [SerializeField]
    protected GameObject lobbyPanel;

    [SerializeField]
    private GameObject roomLayoutPanel;
    [SerializeField]
    private Button createRoomButton;

    [SerializeField]
    private Button joinRoomButton;

    [SerializeField]
    private Image anchorIcon;

    [SerializeField]
    private TextMeshProUGUI pageText;

    [SerializeField]
    private TextMeshProUGUI statusText;

    public TextMeshProUGUI StatusText
    {
        get { return statusText; }
    }

    [SerializeField]
    private TextMeshProUGUI renderStyleText;

    [SerializeField]
    private TextMeshProUGUI roomText;

    List<GameObject> lobbyRowList = new List<GameObject>();

    public TextMeshProUGUI RoomText
    {
        get { return roomText; }
    }

    [SerializeField]
    private TextMeshProUGUI userText;

    public TextMeshProUGUI UserText
    {
        get { return userText; }
    }

    private bool _isCreateMode;

    private void Start()
    {
        transform.parent = referencePoint;
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        if (renderStyleText != null)
        {
            renderStyleText.text = "Render: " + CoLocatedPassthroughManager.Instance.visualization.ToString();
        }
        ToggleRoomButtons(false);
    }

    public void OnCreateModeButtonPressed()
    {
        SampleController.Instance.Log("OnCreateModeButtonPressed");

        if (!_isCreateMode)
        {
            SampleController.Instance.StartPlacementMode();
            anchorIcon.color = Color.green;
            _isCreateMode = true;
        }
        else
        {
            SampleController.Instance.EndPlacementMode();
            anchorIcon.color = Color.white;
            _isCreateMode = false;
        }
    }

    public void OnLoadLocalAnchorsButtonPressed()
    {
        if (SampleController.Instance.cachedAnchorSample)
        {
            SharedAnchorLoader.Instance.LoadLastUsedCachedAnchor();
        }
        else
        {
            SharedAnchorLoader.Instance.LoadLocalAnchors();
        }
    }

    public void OnLoadSharedAnchorsButtonPressed()
    {
        SharedAnchorLoader.Instance.LoadSharedAnchors();
    }

    public void OnSpawnCubeButtonPressed()
    {
        SampleController.Instance.Log("OnSpawnCubeButtonPressed");

        SpawnCube();
    }

    public void LogNext()
    {
        if (SampleController.Instance.logText.pageToDisplay >= SampleController.Instance.logText.textInfo.pageCount)
        {
            return;
        }

        SampleController.Instance.logText.pageToDisplay++;
        if(pageText)
            pageText.text = SampleController.Instance.logText.pageToDisplay + "/" + SampleController.Instance.logText.textInfo.pageCount;
    }

    public void LogPrev()
    {
        if (SampleController.Instance.logText.pageToDisplay <= 1)
        {
            return;
        }

        SampleController.Instance.logText.pageToDisplay--;
        if(pageText)
            pageText.text = SampleController.Instance.logText.pageToDisplay + "/" + SampleController.Instance.logText.textInfo.pageCount;
    }

    private void SpawnCube()
    {
        var networkedCube = PhotonPun.PhotonNetwork.Instantiate(cubePrefab.name, spawnPoint.position, spawnPoint.rotation);
        var photonGrabbable = networkedCube.GetComponent<PhotonGrabbableObject>();
        photonGrabbable.TransferOwnershipToLocalPlayer();
    }

    public void ChangeUserPassthroughVisualization()
    {
        CoLocatedPassthroughManager.Instance.NextVisualization();
        if (renderStyleText)
        {
            renderStyleText.text = "Render: " + CoLocatedPassthroughManager.Instance.visualization.ToString();
        }
    }

    public void DisplayMenuPanel()
    {
        menuPanel.SetActive(true);
        lobbyPanel.SetActive(false);
    }

    public void DisplayLobbyPanel()
    {
        lobbyPanel.SetActive(true);
        menuPanel.SetActive(false);
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

    public void SetRoomList(List<PhotonRealtime.RoomInfo> roomList)
    {
        foreach (Transform roomTransform in roomLayoutPanel.transform)
        {
            if (roomTransform.gameObject != roomLayoutPanelRowPrefab)
                GameObject.Destroy(roomTransform.gameObject);
        }
        lobbyRowList.Clear();

        if (roomList.Count > 0)
        {
            for (int i = 0; i < roomList.Count; i++)
            {
                if (roomList[i].PlayerCount == 0)
                    continue;

                GameObject newLobbyRow = GameObject.Instantiate(roomLayoutPanelRowPrefab, roomLayoutPanel.transform);
                newLobbyRow.SetActive(true);
                newLobbyRow.GetComponentInChildren<TextMeshProUGUI>().text = roomList[i].Name;
                lobbyRowList.Add(newLobbyRow);
            }
        }
    }

    public void OnReturnToMenuButtonPressed()
    {
        if (PhotonPun.PhotonNetwork.IsConnected)
        {
            PhotonPun.PhotonNetwork.Disconnect();
        }
        else
        {
            SceneManager.LoadScene(0);
        }
    }
}
