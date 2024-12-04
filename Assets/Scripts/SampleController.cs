// Copyright (c) Meta Platforms, Inc. and affiliates.
// This code is licensed under the MIT license (see LICENSE for details).

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
    public static SampleController Instance { get; private set; }

    // TODO SampleKit's Sampleton.Log is mo' betta!

    public static void Log(string message, LogType type = LogType.Log, LogOption opt = LogOption.NoStacktrace)
    {
        // Console logging (goes to logcat on device)

        const string kLogTag = "Unity-SharedSpatialAnchors: ";

        Debug.LogFormat(
            logType: type,
            logOptions: opt,
            context: null,
            format: kLogTag + message
        );

        if (Instance)
            Instance.LogInScene(message, type);
    }

    // for transitional compatibility:
    public static void LogError(string message)
        => Log(message, type: LogType.Error, opt: LogOption.None);

    // for transitional compatibility:
    public static void Log(string message, bool error, LogOption opt = LogOption.None)
        => Log(message, error ? LogType.Error : LogType.Log, opt);


    [SerializeField]
    Transform rightHandAnchor;

    [SerializeField]
    GameObject placementPreview;

    [System.NonSerialized] // fewer dangling references if we assign at runtime
    Transform placementRoot;

    [SerializeField]
    public TextMeshProUGUI logText;

    [SerializeField]
    public TextMeshProUGUI pageText;

    [SerializeField]
    public OVRSpatialAnchor anchorPrefab;

    [SerializeField]
    RayInteractor _rayInteractor;


    bool m_IsPlacementMode;


    void OnValidate()
    {
        if (!gameObject.scene.IsValid()) // I'm in a prefab, shouldn't expect scene references to exist yet
            return;

        if (!rightHandAnchor || !placementPreview || !logText || !pageText || !anchorPrefab)
        {
            Debug.LogError($"\"{name}\" seems to be improperly set-up.", this);
        }

        if (_rayInteractor)
            return;

        _rayInteractor = FindObjectOfType<RayInteractor>();
    }


    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
            return;
        }

        if (!placementPreview.scene.IsValid()) // is prefab
        {
            placementPreview = Instantiate(placementPreview, rightHandAnchor, worldPositionStays: false);
        }
        else // for compat with old sample scenes
        {
            placementPreview.transform.parent = rightHandAnchor;
            placementPreview.transform.localPosition = Vector3.zero;
            placementPreview.transform.localRotation = Quaternion.identity;
            placementPreview.transform.localScale = Vector3.one;
        }

        placementRoot = placementPreview.transform.Find("Anchor Placement Transform");

        placementPreview.SetActive(false);

        if (!_rayInteractor)
            _rayInteractor = FindObjectOfType<RayInteractor>();

        if (!logText)
            return;

        UpdateLogText();
    }

    void Update()
    {
        var rayInteractorHoveringUI = !_rayInteractor || (_rayInteractor && !_rayInteractor.Candidate);
        var shouldPlaceNewAnchor = m_IsPlacementMode && OVRInput.GetDown(OVRInput.RawButton.RIndexTrigger) && rayInteractorHoveringUI;

        if (shouldPlaceNewAnchor)
        {
            PlaceAnchorAtRoot();
        }
    }

    public void StartPlacementMode()
    {
        m_IsPlacementMode = true;
        placementPreview.SetActive(true);
    }

    public void EndPlacementMode()
    {

        m_IsPlacementMode = false;
        placementPreview.SetActive(false);
    }

    public void TogglePlacementMode()
    {
        placementPreview.SetActive(m_IsPlacementMode = !m_IsPlacementMode);
    }

    public void PlaceAnchorAtRoot()
    {
        Log($"{nameof(PlaceAnchorAtRoot)}: {placementRoot.ToOVRPose().ToPosef()}");

        _ = Instantiate(anchorPrefab, placementRoot.position, placementRoot.rotation);
    }


    // TODO SampleKit's SceneConsole is mo' betta!

    static readonly System.Text.StringBuilder s_LogBuilder = new();
    static readonly Dictionary<LogType, string> s_LogColors = new()
    {
        [LogType.Warning] = "<color=#FEFF00>",
        [LogType.Error] = "<color=#CA2622>",
        [LogType.Exception] = "<color=#CA2622>",
        [LogType.Assert] = "<color=#CA2622>",
    };

    void LogInScene(string message, LogType type)
    {
        // In VR Logging

        if (s_LogBuilder.Length > 0)
            s_LogBuilder.Append('\n');

        bool doColor = s_LogColors.TryGetValue(type, out string colorTag);
        if (doColor)
            s_LogBuilder.Append(colorTag);

        s_LogBuilder.Append(message);

        if (doColor)
            s_LogBuilder.Append("</color>");

        UpdateLogText();
    }

    void UpdateLogText()
    {
        if (!logText)
            return;

        bool trackLastPage = logText.pageToDisplay == logText.textInfo?.pageCount;

        logText.SetText(s_LogBuilder);

        if (!trackLastPage)
            return;

        logText.ForceMeshUpdate(); // so that pageCount is correctly updated

        int p = logText.textInfo.pageCount;
        logText.pageToDisplay = p;
        if (pageText)
            pageText.text = $"{p}/{p}";
    }

} // end MonoBehaviour SampleController
