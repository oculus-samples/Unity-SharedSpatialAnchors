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
using Oculus.Interaction;
using System.Collections.Generic;
using Photon.Pun;

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

    private RayInteractor _rayInteractor;

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
        _rayInteractor = FindObjectOfType<RayInteractor>();
    }

    private void Update()
    {
        var rayInteractorHoveringUI = _rayInteractor == null || (_rayInteractor != null && _rayInteractor.Candidate == null);
        var shouldPlaceNewAnchor = _isPlacementMode && OVRInput.GetDown(OVRInput.RawButton.RIndexTrigger) && rayInteractorHoveringUI;

        if (shouldPlaceNewAnchor)
        {
            PlaceAnchorAtRoot();
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

    public void PlaceAnchorAtRoot()
    {
        Log("PlaceAnchorAtRoot: root: " + placementRoot.ToOVRPose().ToPosef());

        colocationAnchor = Instantiate(anchorPrefab, placementRoot.position, placementRoot.rotation).GetComponent<SharedAnchor>();

        if (automaticCoLocation)
            StartCoroutine(WaitingForAnchorLocalization());
    }

    private System.Collections.IEnumerator WaitingForAnchorLocalization()
    {
        while (!colocationAnchor.GetComponent<OVRSpatialAnchor>().Localized)
        {
            Log(nameof(WaitingForAnchorLocalization) + "...");
            yield return null;
        }

        Log($"{nameof(WaitingForAnchorLocalization)}: Anchor Localized");
        colocationAnchor.OnAlignButtonPressed();
    }

    public void Log(string message, bool error = false)
    {
        // In VR Logging

        logText.text = SampleController.Instance.logText.text + "\n" + message;
        logText.pageToDisplay = SampleController.Instance.logText.textInfo.pageCount;

        // Console logging (goes to logcat on device)

        const string anchorTag = "SpatialAnchorsUnity: ";
        if (error)
            Debug.LogError(anchorTag + message);
        else
            Debug.Log(anchorTag + message);

        pageText.text = SampleController.Instance.logText.pageToDisplay + "/" + logText.textInfo.pageCount;
    }

    public void LogError(string message)
    {
        Log(message, true);
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
