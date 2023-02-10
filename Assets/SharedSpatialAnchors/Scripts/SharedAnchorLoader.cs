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
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
    private bool isSpaceQueryCallbackAdded;
    private readonly HashSet<Guid> _anchorsToRetryLoading = new HashSet<Guid>();
    private readonly HashSet<Guid> _loadedAnchorUuids = new HashSet<Guid>();

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

    private void OnEnable()
    {
    }

    private void OnDisable()
    {
        OVRManager.SpaceQueryComplete -= OnSpaceQueryComplete;
        isSpaceQueryCallbackAdded = false;
    }

    private void OnSpaceQueryComplete(ulong requestId, bool isSuccessful)
    {
        SampleController.Instance.Log("OnSpaceQueryComplete: requestId: " + requestId + " isSuccessful: " + isSuccessful);

        if (!isSuccessful)
        {
            RetryLoadingAnchors();
            return;
        }

        var didSuccessfullyRetrieveResults = OVRPlugin.RetrieveSpaceQueryResults(requestId, out var queryResults);
        if (!didSuccessfullyRetrieveResults)
        {
            SampleController.Instance.Log("RetrieveSpaceQueryResults: failed to query requestId: " + requestId + " version: " + OVRPlugin.version);
            RetryLoadingAnchors();
            return;
        }

        if (queryResults.Length == 0) {
            Debug.Log("RetrieveSpaceQueryResults: no anchors found");
            RetryLoadingAnchors();
            return;
        }

        foreach (var queryResult in queryResults)
        {
            SampleController.Instance.Log("OnSpaceQueryComplete: query [" + queryMode + "] found uuid: " + queryResult.uuid);

            var spatialAnchor = Instantiate(SampleController.Instance.anchorPrefab);
            spatialAnchor.InitializeFromExisting(queryResult.space, queryResult.uuid);

            var anchor = spatialAnchor.GetComponent<SharedAnchor>();
            if (anchor != null && queryMode == AnchorQueryMode.CLOUD)
            {
                //Disable the share icon since this anchor has been shared with me
                anchor.DisableShareIcon();
            }

            var cachedAnchor = spatialAnchor.GetComponent<CachedSharedAnchor>();
            if (cachedAnchor != null)
            {
                cachedAnchor.IsSavedLocally = queryMode != AnchorQueryMode.CLOUD;

                if (queryMode == AnchorQueryMode.LOCAL_THEN_SHARE) {
                    // we have loaded a previously shared anchor from local device, next:

                    // (1) the anchor is sent immediately to pariticipants
                    // who can query it locally if they have it cached.
                    // this enables ultra fast colocation as no anchor data need to be shared through cloud.
                    cachedAnchor.IsSavedLocally = true;
                    cachedAnchor.SendLocalAnchor();

                    // (2) the anchor is shared to cloud, which enables participants
                    // who have not yet cached this anchor to query it from cloud.
                    cachedAnchor.IsAnchorShared = false;
                } else {
                    // we have loaded an anchor shared by someone else,
                    // mark it as shared as we do not need to share it.
                    cachedAnchor.IsAnchorShared = true;
                }
            }
            _loadedAnchorUuids.Add(queryResult.uuid);

            if (SampleController.Instance.automaticCoLocation)
            {
                PhotonAnchorManager.Instance.SessionStart();
                anchor.OnAlignButtonPressed();
            }
        }
    }

    private void RetryLoadingAnchors() {
        if (queryMode == AnchorQueryMode.LOCAL_THEN_CLOUD && _anchorsToRetryLoading.Count > 0)
        {
            // We have tried to look for a shared anchor on local device but it was not found
            // retry querying the shared anchors from cloud.
            // Querying on local device takes <100 ms, querying from cloud takes ~5sec
            // It is best practice to always save shared anchor to local device and always
            // first attempt to load them from local device to optimize latency.
            Debug.Log(nameof(RetryLoadingAnchors));
            var uuids = new HashSet<Guid>(_anchorsToRetryLoading);
            _anchorsToRetryLoading.Clear();
            LoadCloudAnchorsFromRemote(new HashSet<Guid>(uuids));
        }
    }

    private void AddSpaceQueryCompleteCallback() {
        if (!isSpaceQueryCallbackAdded) {
            OVRManager.SpaceQueryComplete += OnSpaceQueryComplete;
            isSpaceQueryCallbackAdded = true;
        }
    }

    public void LoadLocalAnchors()
    {
        // Loads all local anchors
        AddSpaceQueryCompleteCallback();

        SampleController.Instance.Log(nameof(LoadLocalAnchors));

        var queryInfo = new OVRPlugin.SpaceQueryInfo()
        {
            QueryType = OVRPlugin.SpaceQueryType.Action,
            MaxQuerySpaces = 100,
            Timeout = 0,
            Location = OVRPlugin.SpaceStorageLocation.Local,
            ActionType = OVRPlugin.SpaceQueryActionType.Load,
            FilterType = OVRPlugin.SpaceQueryFilterType.None,
        };

        var didSuccessfullyQuerySpaces = OVRPlugin.QuerySpaces(queryInfo, out var requestId);

        if (didSuccessfullyQuerySpaces)
        {
            SampleController.Instance.Log("LoadLocalAnchors: successfully queried local anchors requestId: " + requestId);
            queryMode = AnchorQueryMode.LOCAL;
        }
        else
        {
            SampleController.Instance.Log("LoadLocalAnchors: failed to query local anchors");
        }
    }

    public void LoadLastUsedCachedAnchor()
    {
        // Loads the last used shared anchor form local device.
        string uuid = PlayerPrefs.GetString("cached_anchor_uuid");
        if (uuid == null) {
            SampleController.Instance.Log("LoadLastUsedCachedAnchor: no cached anchor found");
            return;
        }

        AddSpaceQueryCompleteCallback();

        SampleController.Instance.Log("LoadLastUsedCachedAnchor: uuid: " + uuid);

        HashSet<Guid> uuids = new HashSet<Guid>();
        uuids.Add(new Guid(uuid));

        var uuidInfo = new OVRPlugin.SpaceFilterInfoIds { NumIds = uuids.Count, Ids = uuids.ToArray() };
        var queryInfo = new OVRPlugin.SpaceQueryInfo()
        {
            QueryType = OVRPlugin.SpaceQueryType.Action,
            MaxQuerySpaces = 100,
            Timeout = 0,
            Location = OVRPlugin.SpaceStorageLocation.Local,
            ActionType = OVRPlugin.SpaceQueryActionType.Load,
            FilterType = OVRPlugin.SpaceQueryFilterType.Ids,
            IdInfo = uuidInfo
        };

        var didSuccessfullyQuerySpaces = OVRPlugin.QuerySpaces(queryInfo, out var requestId);

        if (didSuccessfullyQuerySpaces)
        {
            SampleController.Instance.Log("LoadLastUsedCachedAnchor: successfully queried local anchors requestId: " + requestId);
            queryMode = AnchorQueryMode.LOCAL_THEN_SHARE;
        }
        else
        {
            SampleController.Instance.Log("LoadLastUsedCachedAnchor: failed to query local anchors");
        }
    }

    public void LoadAnchorsFromRemote(HashSet<Guid> uuids)
    {
        // Load anchors received from remote participant
        SampleController.Instance.Log("LoadAnchorsFromRemote: uuids count: " + uuids.Count);

        AddSpaceQueryCompleteCallback();

        // Filter out uuids that are already localized
        uuids.ExceptWith(_loadedAnchorUuids);

        if (uuids.Count == 0) {
            SampleController.Instance.Log("LoadAnchorsFromRemote: no new anchors to load, return");
            return;
        }

        foreach (Guid uuid in uuids) {
            SampleController.Instance.Log("LoadAnchorsFromRemote: uuid: " + uuid);
        }

        if (SampleController.Instance.cachedAnchorSample) {
            // In the cached anchor sample, we first try to load anchors cached on local device
            // and only query the cloud if the shared anchors have not been found locally.
            // Querying on local device takes <100 ms while querying the cloud takes ~5 seconds.
            LoadLocalAnchorsFromRemote(uuids);
        } else {
            LoadCloudAnchorsFromRemote(uuids);
        }
    }

    private void LoadLocalAnchorsFromRemote(HashSet<Guid> uuids)
    {
        // Start by querying the anchors on local device, if not found, then query on cloud.
        SampleController.Instance.Log(nameof(LoadLocalAnchorsFromRemote));

        var uuidInfo = new OVRPlugin.SpaceFilterInfoIds { NumIds = uuids.Count, Ids = uuids.ToArray() };
        var queryInfo = new OVRPlugin.SpaceQueryInfo()
        {
            QueryType = OVRPlugin.SpaceQueryType.Action,
            MaxQuerySpaces = 100,
            Timeout = 0,
            Location = OVRPlugin.SpaceStorageLocation.Local,
            ActionType = OVRPlugin.SpaceQueryActionType.Load,
            FilterType = OVRPlugin.SpaceQueryFilterType.Ids,
            IdInfo = uuidInfo
        };

        var didSuccessfullyQuerySpaces = OVRPlugin.QuerySpaces(queryInfo, out var requestId);

        if (didSuccessfullyQuerySpaces)
        {
            SampleController.Instance.Log("LoadLocalAnchorsFromRemote: successfully queried local anchors requestId: " + requestId);
            _anchorsToRetryLoading.UnionWith(new HashSet<Guid>(uuids));
            queryMode = AnchorQueryMode.LOCAL_THEN_CLOUD;
        }
        else
        {
            SampleController.Instance.Log("LoadLocalAnchorsFromRemote: failed to query local anchors");
        }
    }

    private void LoadCloudAnchorsFromRemote(HashSet<Guid> uuids)
    {
        // Query cloud anchors received from remote participant
        SampleController.Instance.Log(nameof(LoadCloudAnchorsFromRemote));

        var uuidInfo = new OVRPlugin.SpaceFilterInfoIds { NumIds = uuids.Count, Ids = uuids.ToArray() };

        var queryInfo = new OVRPlugin.SpaceQueryInfo()
        {
            QueryType = OVRPlugin.SpaceQueryType.Action,
            MaxQuerySpaces = 100,
            Timeout = 0,
            Location = OVRPlugin.SpaceStorageLocation.Cloud,
            ActionType = OVRPlugin.SpaceQueryActionType.Load,
            FilterType = OVRPlugin.SpaceQueryFilterType.Ids,
            IdInfo = uuidInfo
        };

        var didSuccessfullyQuerySpaces = OVRPlugin.QuerySpaces(queryInfo, out var requestId);

        if (didSuccessfullyQuerySpaces)
        {
            SampleController.Instance.Log("LoadCloudAnchorsFromRemote: successfully queried cloud anchors: numSpaces: " + uuids.Count + " requestId: " + requestId);
            queryMode = AnchorQueryMode.CLOUD;
        }
        else
        {
            SampleController.Instance.Log("LoadCloudAnchorsFromRemote: failed to query cloud anchors: numSpaces: " + uuids.Count);
        }
    }
}
