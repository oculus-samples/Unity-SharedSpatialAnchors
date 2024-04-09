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
using UnityEngine;
using UnityEngine.Assertions;
using static OVRSpatialAnchor;

public class SharedAnchorLoader : MonoBehaviour
{
    enum AnchorQueryMode
    {
        CLOUD, // query to load anchors from the cloud
        LOCAL, // query all local anchors available
        LOCAL_THEN_CLOUD, // query to load anchors from local device and then to retry from cloud if none are found.
        LOCAL_THEN_SHARE // query to load anchors form local device and then to share with them other.
    };

    public static SharedAnchorLoader Instance;

    private AnchorQueryMode queryMode;
    private readonly HashSet<Guid> _anchorsToRetryLoading = new HashSet<Guid>();
    private readonly HashSet<Guid> _loadedAnchorUuids = new HashSet<Guid>();
    private List<string> _locallySavedAnchorUuids = new List<string>();
    private List<string> _recievedSharedAnchorUuids = new List<string>();
    SharedAnchor colocationAnchor = null;


    private const int MAX_ANCHOR_LOAD_RETRY = 3;
    private int loadUnboundAnchorCount = 0;
    private Guid[] anchorsLoading;

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

        string anchorIds = PlayerPrefs.GetString("local_anchors");
        if (anchorIds != "")
        {
            string[] anchorIdList = anchorIds.Split('|');
            foreach(string anchorIDString in anchorIdList)
            {
                _locallySavedAnchorUuids.Add(anchorIDString);
            }
        }

        anchorIds = PlayerPrefs.GetString("shared_anchors");
        if (anchorIds != "")
        {
            string[] anchorIdList = anchorIds.Split('|');
            foreach (string anchorIDString in anchorIdList)
            {
                _recievedSharedAnchorUuids.Add(anchorIDString);
            }
        }
    }

    private System.Collections.IEnumerator WaitingForAnchorLocalization()
    {
        while (!colocationAnchor.GetComponent<OVRSpatialAnchor>().Localized)
        {
            SampleController.Instance.Log(nameof(WaitingForAnchorLocalization) + "...");
            yield return null;
        }

        SampleController.Instance.Log($"{nameof(WaitingForAnchorLocalization)}: Anchor Localized");
        colocationAnchor.OnAlignButtonPressed();
        PhotonAnchorManager.Instance.SessionStart();
    }

    private void RetryLoadingAnchors()
    {
        if (queryMode == AnchorQueryMode.LOCAL_THEN_CLOUD && _anchorsToRetryLoading.Count > 0)
        {
            // We have tried to look for a shared anchor on local device but it was not found
            // retry querying the shared anchors from cloud.
            // Querying on local device takes <100 ms, querying from cloud takes ~5sec
            // It is best practice to always save shared anchor to local device and always
            // first attempt to load them from local device to optimize latency.
            SampleController.Instance.Log(nameof(RetryLoadingAnchors));
            var uuids = new HashSet<Guid>(_anchorsToRetryLoading);
            _anchorsToRetryLoading.Clear();
            RetrieveAnchorsFromCloud(new HashSet<Guid>(uuids).ToArray(), AnchorQueryMode.LOCAL_THEN_CLOUD);
        }
    }

    public void LoadLocalAnchors()
    {
        string anchorIds = PlayerPrefs.GetString("local_anchors");
        if (anchorIds != "")
        {
            string[] anchorIdList = anchorIds.Split('|');
            Guid[] guids = new Guid[anchorIdList.Length];
            for (int i = 0; i < anchorIdList.Length; i++)
            {
                guids[i] = Guid.Parse(anchorIdList[i]);
            }
            RetrieveAnchorsFromLocal(guids, AnchorQueryMode.LOCAL);
        }
        else
        {
            SampleController.Instance.Log($"{nameof(LoadLocalAnchors)}: there are no local anchors saved");
        }
    }

    public void LoadSharedAnchors()
    {
        string anchorIds = PlayerPrefs.GetString("shared_anchors");
        if (anchorIds != "")
        {
            string[] anchorIdList = anchorIds.Split('|');
            Guid[] guids = new Guid[anchorIdList.Length];
            for (int i = 0; i < anchorIdList.Length; i++)
            {
                guids[i] = Guid.Parse(anchorIdList[i]);
                SampleController.Instance.Log($"{nameof(LoadSharedAnchors)} : UUID {guids[i]}");
            }
            RetrieveAnchorsFromCloud(guids, AnchorQueryMode.CLOUD);
        }
        else
        {
            SampleController.Instance.Log($"{nameof(LoadSharedAnchors)}: there are no shared anchors saved");
        }
    }

    public void LoadLastUsedCachedAnchor()
    {
        // Loads the last used shared anchor form local device.
        string uuid = PlayerPrefs.GetString("cached_anchor_uuid");
        if (uuid.Length == 0)
        {
            SampleController.Instance.Log($"{nameof(LoadLastUsedCachedAnchor)}: no cached anchor found");
            return;
        }

        SampleController.Instance.Log($"{nameof(LoadLastUsedCachedAnchor)} uuid: {uuid}");

        HashSet<Guid> uuids = new HashSet<Guid>();
        uuids.Add(new Guid(uuid));

        RetrieveAnchorsFromLocal(uuids.ToArray(), AnchorQueryMode.LOCAL_THEN_SHARE);
    }

    public void LoadAnchorsFromRemote(HashSet<Guid> uuids)
    {
        // Load anchors received from remote participant
        SampleController.Instance.Log($"{nameof(LoadAnchorsFromRemote)} uuids count: {uuids.Count}");

        // Filter out uuids that are already localized
        uuids.ExceptWith(_loadedAnchorUuids);

        if (uuids.Count == 0)
        {
            SampleController.Instance.Log($"{nameof(LoadAnchorsFromRemote)}: no new anchors to load");
            return;
        }

        foreach (Guid uuid in uuids)
        {
            SampleController.Instance.Log($"{nameof(LoadAnchorsFromRemote)} uuid: {uuid}");
        }

        loadUnboundAnchorCount = 0;

        if (SampleController.Instance.cachedAnchorSample)
        {
            // In the cached anchor sample, we first try to load anchors cached on local device
            // and only query the cloud if the shared anchors have not been found locally.
            // Querying on local device takes <100 ms while querying the cloud takes ~5 seconds.
            RetrieveAnchorsFromLocal(uuids.ToArray(), AnchorQueryMode.LOCAL_THEN_CLOUD);
        }
        else
        {
            RetrieveAnchorsFromCloud(uuids.ToArray(), AnchorQueryMode.CLOUD);
        }
    }

    private void RetrieveAnchorsFromLocal(Guid[] anchorIds, AnchorQueryMode newQueryMode)
    {
        queryMode = newQueryMode;
        Assert.IsTrue(anchorIds.Length <= OVRPlugin.SpaceFilterInfoIdsMaxSize, "SpaceFilterInfoIdsMaxSize exceeded.");
        SampleController.Instance.Log($"{nameof(RetrieveAnchorsFromLocal)}: {anchorIds.Length} anchors to load");

        OVRSpatialAnchor.LoadUnboundAnchorsAsync(new OVRSpatialAnchor.LoadOptions()
        {
            StorageLocation = OVRSpace.StorageLocation.Local,
            Timeout = 0,
            Uuids = anchorIds
        }).ContinueWith(OnLoadUnboundAnchorsComplete);
    }

    private void RetrieveAnchorsFromCloud(Guid[] anchorIds, AnchorQueryMode newQueryMode)
    {
        queryMode = newQueryMode;
        Assert.IsTrue(anchorIds.Length <= OVRPlugin.SpaceFilterInfoIdsMaxSize, "SpaceFilterInfoIdsMaxSize exceeded.");
        SampleController.Instance.Log($"Calling {nameof(LoadUnboundAnchors)} on {anchorIds.Length} anchors");

        OVRSpatialAnchor.LoadUnboundAnchorsAsync(new OVRSpatialAnchor.LoadOptions()
        {
            StorageLocation = OVRSpace.StorageLocation.Cloud,
            Timeout = 0,
            Uuids = anchorIds
        }).ContinueWith(OnLoadUnboundAnchorsComplete);
        loadUnboundAnchorCount++;
        anchorsLoading = anchorIds;
    }

    private void OnLoadUnboundAnchorsComplete(UnboundAnchor[] unboundAnchors)
    {
        if (unboundAnchors == null)
        {
            SampleController.Instance.LogError($"{nameof(OnLoadUnboundAnchorsComplete)}: Failed to query anchors - {nameof(OVRSpatialAnchor.LoadUnboundAnchors)} returned null.");

            if(loadUnboundAnchorCount < MAX_ANCHOR_LOAD_RETRY)
            {
                RetrieveAnchorsFromCloud(anchorsLoading, queryMode);
            }

            return;
        }

        SampleController.Instance.Log($"{nameof(OnLoadUnboundAnchorsComplete)}: {unboundAnchors.Length} anchors found");

        StartCoroutine(BindAnchorsCoroutine(unboundAnchors));
    }

    private IEnumerator BindAnchorsCoroutine(UnboundAnchor[] unboundAnchors)
    {
        foreach (var unboundAnchor in unboundAnchors)
        {
            OVRSpatialAnchor anchorToBind = InstantiateUnboundAnchor();

            if (anchorToBind != null)
            {
                try
                {
                    SampleController.Instance.Log("Binding Anchor...");
                    unboundAnchor.BindTo(anchorToBind);
                }
                catch
                {
                    SampleController.Instance.Log($"UnboundAnchor.BindTo has failed");
                    GameObject.Destroy(anchorToBind.gameObject);
                    throw;
                }

                while (anchorToBind.PendingCreation)
                {
                    yield return new WaitForEndOfFrame();
                    SampleController.Instance.Log("Waiting on pending anchor creation...");
                }

                SampleController.Instance.Log($"Anchor created and bound to cloud anchor {anchorToBind.Uuid}");

                _loadedAnchorUuids.Add(anchorToBind.Uuid);
                if (queryMode == AnchorQueryMode.CLOUD)
                    RecievedSharedAnchored(anchorToBind);

                if (SampleController.Instance.automaticCoLocation && anchorToBind)
                {
                    colocationAnchor = anchorToBind.GetComponent<SharedAnchor>();
                    StartCoroutine(WaitingForAnchorLocalization());
                }
            }
            else
            {
                SampleController.Instance.LogError($"{nameof(BindAnchorsCoroutine)}: {nameof(anchorToBind)} is null");
            }
        }
    }

    private OVRSpatialAnchor InstantiateUnboundAnchor()
    {
        var spatialAnchor = Instantiate(SampleController.Instance.anchorPrefab);

        var sharedAnchor = spatialAnchor.GetComponent<SharedAnchor>();
        if (sharedAnchor != null && queryMode == AnchorQueryMode.CLOUD)
        {
            //Disable the share icon since this anchor has been shared with me
            sharedAnchor.DisableShareIcon();
        }
        else if (sharedAnchor != null && queryMode == AnchorQueryMode.LOCAL)
        {
            sharedAnchor.IsSavedLocally = true;
        }

        var cachedAnchor = spatialAnchor.GetComponent<CachedSharedAnchor>();
        if (cachedAnchor != null)
        {
            cachedAnchor.IsSavedLocally = queryMode != AnchorQueryMode.CLOUD;

            if (queryMode == AnchorQueryMode.LOCAL_THEN_SHARE)
            {
                // we have loaded a previously shared anchor from local device, next:

                // (1) the anchor is sent immediately to pariticipants
                // who can query it locally if they have it cached.
                // this enables ultra fast colocation as no anchor data need to be shared through cloud.
                cachedAnchor.IsSavedLocally = true;
                cachedAnchor.SendLocalAnchor();

                // (2) the anchor is shared to cloud, which enables participants
                // who have not yet cached this anchor to query it from cloud.
                cachedAnchor.IsAnchorShared = false;
            }
            else
            {
                // we have loaded an anchor shared by someone else,
                // mark it as shared as we do not need to share it.
                cachedAnchor.IsAnchorShared = true;
            }
        }
        return spatialAnchor;
    }

    public void AddLocallySavedAnchor(OVRSpatialAnchor anchor)
    {
        _locallySavedAnchorUuids.Add(anchor.Uuid.ToString());
        PlayerPrefs.SetString("local_anchors", string.Join("|", _locallySavedAnchorUuids));
    }

    public void RemoveLocallySavedAnchor(OVRSpatialAnchor anchor)
    {
        _locallySavedAnchorUuids.Remove(anchor.Uuid.ToString());
        PlayerPrefs.SetString("local_anchors", string.Join("|", _locallySavedAnchorUuids));
    }

    public void RecievedSharedAnchored(OVRSpatialAnchor anchor)
    {
        if (!_recievedSharedAnchorUuids.Contains(anchor.Uuid.ToString()))
        {
            _recievedSharedAnchorUuids.Add(anchor.Uuid.ToString());
            PlayerPrefs.SetString("shared_anchors", string.Join("|", _recievedSharedAnchorUuids));
        }
    }
}
