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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls anchor data and anchor control panel behavior.
/// </summary>
public class SharedAnchor : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI anchorName;

    [SerializeField]
    private Image saveIcon;

    [SerializeField]
    private GameObject shareButton;

    [SerializeField]
    private Image shareIcon;

    [SerializeField]
    private Image alignIcon;

    [SerializeField]
    private Color grayColor;

    [SerializeField]
    private Color greenColor;

    private OVRSpatialAnchor _spatialAnchor;

    #if OVR_INTERNAL_CODE
    private LocalGroupController localGroupControllerRef;
    #endif

    public const string SHARE_POINT_CLOUD_DATA_ERROR = "Share Point Cloud Data is disabled on your device. Settings -> Privacy & Safety -> Device Permissions";
    public const string SHARE_POINT_CLOUD_DATA_INFO_URL = "https://www.meta.com/help/quest/articles/in-vr-experiences/oculus-features/point-cloud/";
    private const uint MAX_ANCHOR_SAVE_ATTEMPTS = 3;
    private uint numAnchorSaveAttempts = 0;

    public bool IsSavedLocally
    {
        set
        {
            if (saveIcon != null)
            {
                saveIcon.color = value ? greenColor : grayColor;
            }
        }
    }

    private bool IsSelectedForShare
    {
        set
        {
            if (shareIcon != null)
            {
                shareIcon.color = value ? greenColor : grayColor;
            }
        }
    }

    public bool IsSelectedForAlign
    {
        set
        {
            if (alignIcon != null)
            {
                alignIcon.color = value ? greenColor : grayColor;
            }
        }
    }

    private void Awake()
    {
        _spatialAnchor = GetComponent<OVRSpatialAnchor>();
    }

    private IEnumerator Start()
    {
        while (_spatialAnchor && !_spatialAnchor.Created)
        {
            yield return null;
        }

        if (_spatialAnchor != null)
        {
            anchorName.text = _spatialAnchor.Uuid.ToString("D");
        }
        else
        {
            Destroy(gameObject);
        }

        if (SampleController.Instance.automaticCoLocation)
            transform.Find("Canvas").gameObject.SetActive(false);

#if OVR_INTERNAL_CODE
        if (SampleController.Instance.localGroupSample)
            localGroupControllerRef = FindObjectOfType<LocalGroupController>();
#endif
    }

    public void OnSaveLocalButtonPressed()
    {
        if (_spatialAnchor == null)
        {
            return;
        }

        _spatialAnchor.SaveAsync().ContinueWith((isSuccessful) =>
        {
            if (isSuccessful)
            {
                SampleController.Instance.Log($"Successfully Saved Spatial Anchor");

                IsSavedLocally = true;
                SharedAnchorLoader.Instance?.AddLocallySavedAnchor(_spatialAnchor);
            }
            else
            {
                SampleController.Instance.LogError($"Failed to save spatial anchor to local storage");
            }
        });
    }

    public void OnHideButtonPressed()
    {
        SampleController.Instance.Log($"{nameof(OnHideButtonPressed)}: Hiding Spatial Anchor");
        Destroy(gameObject);
    }

    public void OnEraseButtonPressed()
    {
        if (_spatialAnchor == null)
        {
            return;
        }

        _spatialAnchor.EraseAsync().ContinueWith((isSuccessful) =>
        {
            SampleController.Instance.Log($"Successfully Erased Spatial Anchor : {isSuccessful}");

            if (isSuccessful)
            {
                SharedAnchorLoader.Instance?.RemoveLocallySavedAnchor(_spatialAnchor);
                Destroy(gameObject);
            }
        });
    }

    private bool IsReadyToShare()
    {
        if (!Photon.Pun.PhotonNetwork.IsConnected)
        {
            SampleController.Instance.Log("Can't share - no users to share with because you are no longer connected to the Photon network");
            return false;
        }

        var userIds = PhotonAnchorManager.GetUserList().Select(userId => userId.ToString()).ToArray();
        if (userIds.Length == 0)
        {
            SampleController.Instance.Log("Can't share - no users to share with or can't get the user ids through photon custom properties");
            return false;
        }

        if (_spatialAnchor == null)
        {
            SampleController.Instance.Log("Can't share - no associated spatial anchor");
            return false;
        }
        return true;
    }

    public void OnShareButtonPressed()
    {
        SampleController.Instance.Log(nameof(OnShareButtonPressed));

#if OVR_INTERNAL_CODE
        if (SampleController.Instance.localGroupSample)
        {
            IsSelectedForShare = true;

            if (localGroupControllerRef.peerNetworkingEnabled)
            {
                SampleController.Instance.Log("Sharing anchor over p2p Local Group");
                localGroupControllerRef.GetComponent<LocalGroupController>().ShareAnchorsWithLocalGroup(new List<OVRSpatialAnchor> { GetComponent<OVRSpatialAnchor>() });
            }
            else
            {
                SampleController.Instance.Log("Sharing anchor over cloud Local Group");
                numAnchorSaveAttempts = 0;
                SaveToCloudThenShare();
            }
        }
        else
#endif
        {
            if (!IsReadyToShare())
            {
                return;
            }

            IsSelectedForShare = true;
            numAnchorSaveAttempts = 0;
            SaveToCloudThenShare();
        }
    }

    private void SaveToCloudThenShare()
    {
        OVRSpatialAnchor.SaveOptions saveOptions;
        saveOptions.Storage = OVRSpace.StorageLocation.Cloud;
        _spatialAnchor.SaveAsync(saveOptions).ContinueWith((isSuccessful) =>
        {
            if (isSuccessful)
            {
                SampleController.Instance.Log("Successfully saved anchor(s) to the cloud");

#if OVR_INTERNAL_CODE
                if (SampleController.Instance.localGroupSample && localGroupControllerRef)
                {
                    localGroupControllerRef.GetComponent<LocalGroupController>().ShareAnchorsWithLocalGroup(new List<OVRSpatialAnchor> { GetComponent<OVRSpatialAnchor>() });
                }
                else
#endif
                {
                    var userIds = PhotonAnchorManager.GetUserList().Select(userId => userId.ToString()).ToArray();
                    ICollection<OVRSpaceUser> spaceUserList = new List<OVRSpaceUser>();
                    foreach (string strUsername in userIds)
                    {
                        SampleController.Instance.Log($"Sharing Anchor with {strUsername}");
                        spaceUserList.Add(new OVRSpaceUser(ulong.Parse(strUsername)));
                    }

                    _spatialAnchor.ShareAsync(spaceUserList).ContinueWith(OnShareComplete);
                }

                SampleController.Instance.AddSharedAnchorToLocalPlayer(this);
            }
            else
            {
                SampleController.Instance.Log($"Saving Spatial Anchor Failed");
                numAnchorSaveAttempts++;
                if (numAnchorSaveAttempts < MAX_ANCHOR_SAVE_ATTEMPTS)
                {
                    SampleController.Instance.Log("Retrying anchor save to cloud...");
                    SaveToCloudThenShare();
                }
            }
        });
    }

    public void ReshareAnchor()
    {
        if (!IsReadyToShare())
        {
            return;
        }

        SampleController.Instance.Log("ReshareAnchor: re-sharing anchor with all users in the room");

        IsSelectedForShare = true;

        OVRSpatialAnchor.SaveOptions saveOptions;
        saveOptions.Storage = OVRSpace.StorageLocation.Cloud;
        ICollection<OVRSpaceUser> spaceUserList = new List<OVRSpaceUser>();
        foreach (string strUsername in PhotonAnchorManager.GetUsers())
        {
            spaceUserList.Add(new OVRSpaceUser(ulong.Parse(strUsername)));
        }
        _spatialAnchor.ShareAsync(spaceUserList).ContinueWith(OnShareComplete);
    }

    private void OnShareComplete(OVRSpatialAnchor.OperationResult result)
    {
        SampleController.Instance.Log(nameof(OnShareComplete) + " Result: " + result);

        if (result != OVRSpatialAnchor.OperationResult.Success)
        {
            shareIcon.color = Color.red;

            if (result == OVRSpatialAnchor.OperationResult.Failure_SpaceCloudStorageDisabled)
            {
                SampleController.Instance.Log(SHARE_POINT_CLOUD_DATA_ERROR);
                Application.OpenURL(SHARE_POINT_CLOUD_DATA_INFO_URL);
            }

            return;
        }

        SampleController.Instance.Log($"{nameof(OnShareComplete)} - UUID: {_spatialAnchor.Uuid}");
        PhotonAnchorManager.Instance.PublishAnchorUuids(new Guid[] { _spatialAnchor.Uuid }, 1, true);
    }

    public void OnAlignButtonPressed()
    {
        SampleController.Instance.Log("OnAlignButtonPressed: aligning to anchor");

        AlignPlayer.Instance.SetAlignmentAnchor(this);
    }

    public void DisableShareIcon()
    {
        if (shareButton)
        {
            shareButton.SetActive(false);
        }
    }
}
