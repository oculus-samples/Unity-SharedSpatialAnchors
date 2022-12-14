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
    public static SharedAnchorLoader Instance;

    private bool localizingCloudAnchors = false;

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
    }

    private void OnSpaceQueryComplete(ulong requestId, bool isSuccessful)
    {
        Debug.Log("OnSpaceQueryComplete: requestId: " + requestId + " isSuccessful: " + isSuccessful);

        if (!isSuccessful)
        {
            return;
        }

        var didSuccessfullyRetrieveResults = OVRPlugin.RetrieveSpaceQueryResults(requestId, out var queryResults);

        if (!didSuccessfullyRetrieveResults)
        {
            SampleController.Instance.Log("RetrieveSpaceQueryResults: failed to query requestId: " + requestId + " version: " + OVRPlugin.version);
            return;
        }

        Debug.Log("RetrieveSpaceQueryResults: requestId: " + requestId + " result count: " + queryResults.Length);

        foreach (var queryResult in queryResults)
        {
            if (localizingCloudAnchors)
            {
                SampleController.Instance.Log("Cloud Anchor Localized and Instantiated");
            }
            else
            {
                SampleController.Instance.Log("Local Anchor Localized and Instantiated");
            }
            var spatialAnchor = Instantiate(SampleController.Instance.anchorPrefab);
            spatialAnchor.InitializeFromExisting(queryResult.space, queryResult.uuid);

            var anchor = spatialAnchor.GetComponent<SharedAnchor>();
            if (anchor != null && localizingCloudAnchors)
            {
                //Disable the share icon since this anchor has been shared with me
                anchor.DisableShareIcon();
            }

            if (SampleController.Instance.automaticCoLocation)
            {
                PhotonAnchorManager.Instance.SessionStart();
                anchor.OnAlignButtonPressed();
            }
        }
    }

    public void LoadLocalAnchors()
    {
        OVRManager.SpaceQueryComplete += OnSpaceQueryComplete;

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
            localizingCloudAnchors = false;
        }
        else
        {
            SampleController.Instance.Log("LoadLocalAnchors: failed to query local anchors");
        }
    }

    public void LoadCloudAnchors(HashSet<Guid> uuids)
    {
        OVRManager.SpaceQueryComplete += OnSpaceQueryComplete;

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
            SampleController.Instance.Log("LoadCloudAnchors: successfully queried cloud anchors: numSpaces: " + uuids.Count + " requestId: " + requestId);
            localizingCloudAnchors = true;
        }
        else
        {
            SampleController.Instance.Log("LoadCloudAnchors: failed to query cloud anchors: numSpaces: " + uuids.Count);
        }
    }
}
