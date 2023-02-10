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

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

public class SharedAnchorControlPanel : MonoBehaviour
{
    [SerializeField]
    private Transform referencePoint;

    [SerializeField]
    private GameObject cubePrefab;

    [SerializeField]
    private Transform spawnPoint;

    [SerializeField]
    private Color grayColor;

    [SerializeField]
    private Color greenColor;

    [SerializeField]
    private Image anchorIcon;

    [SerializeField]
    private TextMeshProUGUI pageText;

    [SerializeField]
    private TextMeshProUGUI statusText;

    [SerializeField]
    private TextMeshProUGUI renderStyleText;

    [SerializeField]
    private TextMeshProUGUI roomText;

    public TextMeshProUGUI RoomText
    {
        get {  return roomText; }
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
        if(renderStyleText != null)
        {
            renderStyleText.text = "Render: " + CoLocatedPassthroughManager.Instance.visualization.ToString();
        }
    }

    private void Update()
    {
        statusText.text = "Status: " + PhotonNetwork.NetworkClientState;
    }

    public void OnCreateModeButtonPressed()
    {
        SampleController.Instance.Log("OnCreateModeButtonPressed");

        if (!_isCreateMode)
        {
            SampleController.Instance.StartPlacementMode();
            anchorIcon.color = greenColor;
            _isCreateMode = true;
        }
        else
        {
            SampleController.Instance.EndPlacementMode();
            anchorIcon.color = grayColor;
            _isCreateMode = false;
        }
    }

    public void OnLoadAnchorsButtonPressed()
    {
        SampleController.Instance.Log("OnLoadAnchorsButtonPressed");

        if (SampleController.Instance.cachedAnchorSample) {
            SharedAnchorLoader.Instance.LoadLastUsedCachedAnchor();
        } else {
            SharedAnchorLoader.Instance.LoadLocalAnchors();
        }
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
        pageText.text = SampleController.Instance.logText.pageToDisplay + "/" + SampleController.Instance.logText.textInfo.pageCount;
    }

    public void LogPrev()
    {
        if (SampleController.Instance.logText.pageToDisplay <= 1)
        {
            return;
        }

        SampleController.Instance.logText.pageToDisplay--;
        pageText.text = SampleController.Instance.logText.pageToDisplay + "/" + SampleController.Instance.logText.textInfo.pageCount;
    }

    private void SpawnCube()
    {
        var networkedCube = PhotonNetwork.Instantiate(cubePrefab.name, spawnPoint.position, spawnPoint.rotation);
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
}
