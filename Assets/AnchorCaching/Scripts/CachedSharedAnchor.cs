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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System;

public class CachedSharedAnchor : MonoBehaviour
{
    private OVRSpatialAnchor _spatialAnchor;

    public bool IsSavedLocally;
    public bool IsAutoAlign = true;

    bool anchorSuccessfullyShared = false;

    public bool IsAnchorShared
    {
        get { return anchorSuccessfullyShared; }
        set
        {
            anchorSuccessfullyShared = value;
            sharedAnchorImage.color = Color.green;
        }
    }

    [SerializeField]
    Image sharedAnchorImage;

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

        if (_spatialAnchor == null)
        {
            Destroy(gameObject);
        }

        yield return new WaitForSeconds(0.1f);

        if (!IsSavedLocally)
        {
            SaveLocal();
            yield return new WaitForEndOfFrame();
        }

        if (!IsAnchorShared)
        {
            yield return new WaitForSeconds(0.5f);
            sharedAnchorImage.color = Color.white;
            ShareAnchor();
        }

        if (IsAutoAlign) {
            AlignToAnchor();
        }
    }

    public void SaveLocal()
    {
        SampleController.Instance.Log(nameof(SaveLocal));

        if (_spatialAnchor == null)
        {
            return;
        }

        _spatialAnchor.Save((_, isSuccessful) =>
        {
            if (isSuccessful)
            {
                IsSavedLocally = true;

                // store the most recently used anchor uuid.
                // It will be used on subsequent sessions for faster colocation.
                string anchorUuid = _spatialAnchor.Uuid.ToString();
                PlayerPrefs.SetString("cached_anchor_uuid", anchorUuid);
                PlayerPrefs.Save();

                SampleController.Instance.Log("CachedSharedAnchor: SaveLocal: done, uuid: " + anchorUuid);
            }
        });
    }

    private bool IsReadyToShare() {
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

    public void ShareAnchor()
    {
        SampleController.Instance.Log(nameof(ShareAnchor));

        if (!IsReadyToShare()){
            return;
        }

        OVRSpatialAnchor.SaveOptions saveOptions;
        saveOptions.Storage = OVRSpace.StorageLocation.Cloud;

        _spatialAnchor.Save(saveOptions, (spatialAnchor, isSuccessful) =>
        {            
            if (isSuccessful)
            {
                SampleController.Instance.Log("Successfully saved anchor(s) to the cloud");
                SampleController.Instance.colocationCachedAnchor = this;

                var userIds = PhotonAnchorManager.GetUserList().Select(userId => userId.ToString()).ToArray();
                ICollection<OVRSpaceUser> spaceUserList = new List<OVRSpaceUser>();
                foreach (string strUsername in PhotonAnchorManager.GetUsers())
                {
                    spaceUserList.Add(new OVRSpaceUser(ulong.Parse(strUsername)));
                }

                OVRSpatialAnchor.Share(new List<OVRSpatialAnchor> { spatialAnchor }, spaceUserList, OnShareComplete);
            }
            else
            {
                SampleController.Instance.Log("Saving anchor(s) failed. Possible reasons include an unsupported device.");
            }
        });
    }

    public void ReshareAnchor()
    {
        SampleController.Instance.Log(nameof(ReshareAnchor));

        if (!IsReadyToShare()){
            return;
        }

        OVRSpatialAnchor.SaveOptions saveOptions;
        saveOptions.Storage = OVRSpace.StorageLocation.Cloud;
        ICollection<OVRSpaceUser> spaceUserList = new List<OVRSpaceUser>();
        foreach (string strUsername in PhotonAnchorManager.GetUsers())
        {
            spaceUserList.Add(new OVRSpaceUser(ulong.Parse(strUsername)));
        }
        OVRSpatialAnchor.Share(new List<OVRSpatialAnchor> { _spatialAnchor }, spaceUserList, OnShareComplete);
    }

    private static void OnShareComplete(ICollection<OVRSpatialAnchor> spatialAnchors, OVRSpatialAnchor.OperationResult result)
    {
        SampleController.Instance.Log(nameof(OnShareComplete) + " Result: " + result);

        if (result != OVRSpatialAnchor.OperationResult.Success)
        {
            foreach (var spatialAnchor in spatialAnchors)
            {
                spatialAnchor.GetComponent<CachedSharedAnchor>().sharedAnchorImage.color = Color.red;
            }
            return;
        }

        var uuids = new Guid[spatialAnchors.Count];
        var uuidIndex = 0;

        foreach (var spatialAnchor in spatialAnchors)
        {
            SampleController.Instance.Log("OnShareComplete: space: " + spatialAnchor.Space.Handle + ", uuid: " + spatialAnchor.Uuid);
            spatialAnchor.GetComponent<CachedSharedAnchor>().sharedAnchorImage.color = Color.green;

            uuids[uuidIndex] = spatialAnchor.Uuid;
            ++uuidIndex;
        }

        PhotonAnchorManager.Instance.PublishAnchorUuids(uuids, (uint)uuids.Length, true);
    }

    public void SendLocalAnchor() {
        SampleController.Instance.Log("SendLocalAnchor: uuid: " + _spatialAnchor);
        var uuids = new Guid[1];
        uuids[0] = _spatialAnchor.Uuid;
        PhotonAnchorManager.Instance.PublishAnchorUuids(uuids, (uint)uuids.Length, false);
    }

    public void AlignToAnchor()
    {
        SampleController.Instance.Log("AlignToAnchor: uuid: " + _spatialAnchor.Uuid);
        Invoke(nameof(WaitAlignToAnchor), 0.1f);
    }

    private void WaitAlignToAnchor() {
        SampleController.Instance.Log("WaitAlignToAnchor: uuid: " + _spatialAnchor.Uuid);
        AlignPlayer.Instance.AlignToCachedAnchor(this);
    }
}
