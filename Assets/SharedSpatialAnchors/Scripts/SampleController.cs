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

using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

/// <summary>
/// Main manager for sample interaction
/// </summary>
public class SampleController : MonoBehaviour
{
    public bool automaticCoLocation = false;
    public bool cachedAnchorSample = false;

    [HideInInspector]
    public SharedAnchor colocationAnchor;

    [HideInInspector]
    public CachedSharedAnchor colocationCachedAnchor;

    [SerializeField]
    private Transform rightHandAnchor;

    [SerializeField]
    private GameObject placementPreview;

    [SerializeField]
    private Transform placementRoot;

    [SerializeField]
    public TextMeshProUGUI logText;

    [SerializeField]
    public TextMeshProUGUI pageText;

    [SerializeField]
    public OVRSpatialAnchor anchorPrefab;

    public static SampleController Instance;
    private bool _isPlacementMode;

    private List<SharedAnchor> sharedanchorList = new List<SharedAnchor>();

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

        gameObject.AddComponent<SharedAnchorLoader>();

        placementPreview.transform.parent = rightHandAnchor;
        placementPreview.transform.localPosition = Vector3.zero;
        placementPreview.transform.localRotation = Quaternion.identity;
        placementPreview.transform.localScale = Vector3.one;
        placementPreview.SetActive(false);
    }

    private void Start()
    {
        if(automaticCoLocation)
        {
            PlaceAnchorAtRoot(placementRoot);
        }
    }

    private void Update()
    {
        var shouldPlaceNewAnchor = _isPlacementMode && OVRInput.GetDown(OVRInput.RawButton.RIndexTrigger);

        if (shouldPlaceNewAnchor)
        {
            PlaceAnchorAtRoot(placementRoot);
        }
    }

    public void StartPlacementMode()
    {
        _isPlacementMode = true;
        placementPreview.SetActive(true);
    }

    public void EndPlacementMode()
    {

        _isPlacementMode = false;
        placementPreview.SetActive(false);
    }

    private void PlaceAnchorAtRoot(Transform root)
    {
        Log("PlaceAnchorAtRoot: root: " + root.ToOVRPose().ToPosef());

        colocationAnchor = Instantiate(anchorPrefab, root.position, root.rotation).GetComponent<SharedAnchor>();
        if(automaticCoLocation)
        {
            colocationAnchor.OnAlignButtonPressed();
        }
    }

    public void Log(string message)
    {
        // In VR Logging

        logText.text = SampleController.Instance.logText.text + "\n" + message;
        logText.pageToDisplay = SampleController.Instance.logText.textInfo.pageCount;

        // Console logging (goes to logcat on device)

        const string anchorTag = "SpatialAnchorsUnity: ";
        Debug.Log(anchorTag + message);

        pageText.text = SampleController.Instance.logText.pageToDisplay + "/" + logText.textInfo.pageCount;
    }

    public void AddSharedAnchorToLocalPlayer(SharedAnchor anchor)
    {
        sharedanchorList.Add(anchor);
    }

    public List<SharedAnchor> GetLocalPlayerSharedAnchors()
    {
        return sharedanchorList;
    }
}
