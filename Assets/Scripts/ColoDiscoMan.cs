// Copyright (c) Meta Platforms, Inc. and affiliates.
// This code is licensed under the MIT license (see LICENSE for details).

using Oculus.Platform;
using User = Oculus.Platform.Models.User;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using TMPro;

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

using Sampleton = SampleController; // only transitional

public class ColoDiscoMan : MonoBehaviour // AKA ColocationSessionDiscoveryAndGroupSharingManager
{
    //
    // public interface

    #region Static section

    public static ulong CurrentUserID => s_Instance && s_Instance.m_My != null ? s_Instance.m_My.ID : 0;

    public static Transform TrackingSpaceRoot
    {
        get
        {
            if (!s_Instance)
                return null;
            if (s_Instance.m_TrackingSpaceRoot)
                return s_Instance.m_TrackingSpaceRoot;
            if (!OVRManager.instance)
                return null;
            s_Instance.m_TrackingSpaceRoot = OVRManager.instance.GetComponentInChildren<OVRCameraRig>().trackingSpace;
            return s_Instance.m_TrackingSpaceRoot;
        }
    }

    public static void NotifyAnchorLocalized(ColoDiscoAnchor anchor)
    {
        Assert.IsNotNull(s_Instance, "s_Instance should be initialized!");

        var uuid = anchor.Uuid;
        if (s_Instance.m_KnownAnchors.TryGetValue(uuid, out var existing) && existing)
        {
            Sampleton.Log($"{nameof(NotifyAnchorLocalized)}: SKIPPING known anchor {uuid.Brief()}");
            return;
        }

        s_Instance.m_KnownAnchors[uuid] = anchor;

        Sampleton.Log($"{nameof(NotifyAnchorLocalized)}: {uuid.Brief()}");

        s_Instance.NotifyAnchorLocalizedAsync(anchor);
    }

    public static void NotifyAnchorErased(Guid uuid)
    {
        Assert.IsNotNull(s_Instance, "s_Instance should be initialized!");

        s_Instance.m_KnownAnchors.Remove(uuid);

        foreach (var group in s_Instance.m_KnownGroups.Values)
        {
            _ = group.SharedAnchors.Remove(uuid);
        }
    }

    public static void NotifyAnchorAlignment(ColoDiscoAnchor anchor, ColoDiscoAnchor previous)
    {
        Assert.IsNotNull(s_Instance, "s_Instance should be initialized!");

        _ = previous;

        s_Instance.AlignPlayer(anchor.transform);
    }

    public static void ShareToActiveGroup(ColoDiscoAnchor anchor)
    {
        Assert.IsNotNull(s_Instance, "s_Instance should be initialized!");

        Sampleton.Log($"{nameof(ShareToActiveGroup)}{(s_Instance.ForceShareAnchors ? "(force=true)" : "")}:\n+ anchor: {anchor.Uuid}");

        s_Instance.ShareAnchorsToActiveGroupsAsync(new[] { anchor });
    }

    public static IEnumerable<Guid> GetGroupsFor(Guid anchorId)
    {
        Assert.IsNotNull(s_Instance, "s_Instance should be initialized!");

        return from g in s_Instance.m_KnownGroups.Values
               where g.SharedAnchors.Contains(anchorId)
               select g.Uuid;
    }

    public static IEnumerable<string> GetDisplayNamesFor(IEnumerable<Guid> groupIds)
    {
        Assert.IsNotNull(s_Instance, "s_Instance should be initialized!");

        foreach (var groupId in groupIds)
        {
            if (s_Instance.m_KnownGroups.TryGetValue(groupId, out var group))
            {
                yield return FixupDisplayName(group.Metadata.DisplayName, groupId);
            }
            else
            {
                yield return "(invalid group)";
            }
        }
    }

    #endregion Static section


    #region UnityEvent (UI) listeners

    // The following are attached to hidden toggles, which you can enable in the scene hierarchy.
    public bool ShareAutomatically { get; set; } = false;
    public bool ApplyToAllGroups { get; set; } = false;
    public bool LoadOnGroupSwitch { get; set; } = true;
    public bool AdvertiseNullData { get; set; } = false;
    public bool ForceShareAnchors { get; set; } = false;
    public bool OnlyRememberedAnchors { get; set; } = false;

    /// <summary>
    ///   Explicitly localizes loaded anchors before they are bound to GameObjects, so that initial poses can be
    ///   calculated before they are bound, thus avoiding the quirk that auto-localized anchors typically spend one or
    ///   more frames at the world origin before snapping to their proper (localized) pose
    ///   (AKA, the "teleporting anchors" glitch).
    /// </summary>
    /// <remarks>
    ///   https://developers.meta.com/horizon/documentation/unity/unity-spatial-anchors-persist-content#localize-each-anchor
    /// </remarks>
    public bool LocalizeBeforeBinding { get; set; } = true;


    public void ToggleAdvertising()
    {
        if (m_IsAdvertising)
            StopAdvertising();
        else
            StartAdvertising();
    }

    public void StartAdvertising(bool spammable = false)
    {
        Sampleton.Log($"{nameof(StartAdvertising)}:");

        var newGroup = new Group
        {
            IsMine = true,
        };

        if (AdvertiseNullData || m_My is null)
            newGroup.Metadata = new CustomAdvertData(null, 0, anchors: null);
        else
            newGroup.Metadata = new CustomAdvertData(m_My.OculusID, m_My.ID, anchors: null);

        var alignmentAnchor = ColoDiscoAnchor.Alignment;
        if (alignmentAnchor)
        {
            newGroup.Metadata.Anchors.Add(alignmentAnchor.Uuid);

            var worldToLocal = alignmentAnchor.transform.ToOVRPose().Inverse();

            foreach (var spawn in m_Spawned)
            {
                var ovrPose = worldToLocal * spawn.transform.ToOVRPose();
                newGroup.Metadata.Poses.Add(new Pose(ovrPose.position, ovrPose.orientation));
            }
        }

        byte[] bytes = null;
        if (AdvertiseNullData)
            Sampleton.Log("+ Advertising NULL for custom data");
        else if (newGroup.Metadata.TryWrite(out bytes))
            Sampleton.Log($"+ Advertising {bytes.Length} bytes of custom data~");
        else
            Sampleton.Log("- Advertising 0 bytes (I hope this was expected?)", LogType.Warning);

        if (!spammable)
            m_AdvertBtn.interactable = false;

        StartAdvertisingAsync(bytes, newGroup);
    }

    public void StopAdvertising(bool spammable = false)
    {
        Sampleton.Log($"{nameof(StopAdvertising)}:");

        if (!spammable)
            m_AdvertBtn.interactable = false;

        StopAdvertisingAsync();
    }


    public void ToggleDiscovering()
    {
        if (m_IsDiscovering)
            StopDiscovering();
        else
            StartDiscovering();
    }

    public void StartDiscovering(bool spammable = false)
    {
        Sampleton.Log($"{nameof(StartDiscovering)}:");

        if (!spammable)
            m_DiscoBtn.interactable = false;

        StartDiscoveringAsync();
    }

    public void StopDiscovering(bool spammable = false)
    {
        Sampleton.Log($"{nameof(StopDiscovering)}:");

        if (!spammable)
            m_DiscoBtn.interactable = false;

        StopDiscoveringAsync();
    }


    public void ShareKnownAnchors()
    {
        if (m_KnownAnchors.Count == 0 && !ForceShareAnchors)
        {
            Sampleton.Log($"{nameof(ShareKnownAnchors)}: SKIPPING: No known anchors localized.");
            return;
        }

        Sampleton.Log($"{nameof(ShareKnownAnchors)}{(ForceShareAnchors ? "(force=true)" : "")}: {m_KnownAnchors.Count} known anchors:");

        ShareAnchorsToActiveGroupsAsync(m_KnownAnchors.Values);
    }

    public void LoadGroupAnchors()
    {
        if (ApplyToAllGroups)
        {
            var groupIds = m_KnownGroups.Keys;
            if (groupIds.Count == 0)
            {
                Sampleton.Log($"{nameof(LoadGroupAnchors)}: SKIPPING: No known groups exist yet.\n- Create one by advertising, or find one by discovering.");
                return;
            }

            Sampleton.Log($"{nameof(LoadGroupAnchors)}: Requesting anchors from {groupIds.Count} group(s)..");

            foreach (var groupId in groupIds)
            {
                LoadGroupAnchorsAsync(groupId);
            }

            return;
        }

        if (m_ActiveGroup == Guid.Empty)
        {
            Sampleton.Log($"{nameof(LoadGroupAnchors)}: SKIPPING: No active group.\n- Create one by Advertising, or find one by Discovering a session.");
            return;
        }

        Sampleton.Log($"{nameof(LoadGroupAnchors)}: Requesting anchors from {m_ActiveGroup.Brief()}");

        LoadGroupAnchorsAsync(m_ActiveGroup);
    }


    public void UseHardcodedGroup()
    {
        var guid = new Guid(m_HardcodedGroupUUID);

        if (!m_KnownGroups.ContainsKey(guid))
        {
            m_KnownGroups[guid] = new Group
            {
                Uuid = guid,
                Metadata = new CustomAdvertData(m_HardcodedGroupName, m_My?.ID ?? 0, m_KnownAnchors.Keys)
            };

            UIAddDiscoveryItem(m_HardcodedGroupName, guid, isSelected: true);
        }

        SetActiveGroup(guid);

        if (!LoadOnGroupSwitch)
            return;

        Sampleton.Log("[x] Load Anchors On Select:");
        LoadGroupAnchorsAsync(guid);
    }

    public void LoadRememberedAnchors()
    {
        if (!ColoDiscoAnchor.HasRememberedAnchor)
        {
            Sampleton.LogError($"{nameof(LoadRememberedAnchors)}: No remembered anchors.");
            return;
        }

        Sampleton.Log($"{nameof(LoadRememberedAnchors)}: Requesting the last remembered anchor..");

        LoadAnchorsById(new[] { ColoDiscoAnchor.RememberedAnchorId });
    }


    // more boilerplate-y button listeners:

    public void LoadScene(int idx)
    {
        Debug.Log($"{nameof(LoadScene)}({idx}) ...", this);
        SceneManager.LoadScene(idx);
    }

    public void AdvanceLogPage(int n)
    {
        if (n == 0)
            return;

        var log = SampleController.Instance.logText;
        if (!log)
            return;

        int nPages = log.textInfo.pageCount;

        int page = Mathf.Clamp(log.pageToDisplay + n, 1, nPages);
        if (page == log.pageToDisplay)
            return;

        log.pageToDisplay = page;

        var pageText = SampleController.Instance.pageText;
        if (pageText)
            pageText.text = $"{log.pageToDisplay}/{nPages}";
    }

    public void ToggleGreenTint(Image icon)
    {
        icon.color = (icon.color == Color.green) ? Color.white : Color.green;
    }

    #endregion UnityEvent (UI) listeners


    //
    // private impl.

    sealed class Group // aka Session
    {
        public Guid Uuid;
        public bool IsMine;
        public CustomAdvertData Metadata;
        public Button ListButton;
        public readonly HashSet<Guid> SharedAnchors = new();
    }

    #region Fields

    [Header("[Variation Control]")]

    [SerializeField]
    [Tooltip("This scene doesn't technically require Oculus usernames / IDs, but it can make use of them to help identify peers when testing.")]
    bool m_RequireLoggedInUser = false;
    [SerializeField, Delayed]
    [Tooltip("Only used if you find \"Button: Use Hardcoded Group\" in the hierarchy and activate it.\n\nThe special value \"build\" automatically uses the current Application.buildGuid.")]
    string m_HardcodedGroupUUID = "build";
    [SerializeField, Delayed]
    [Tooltip("Only used if you find \"Button: Use Hardcoded Group\" in the hierarchy and activate it.")]
    string m_HardcodedGroupName = "HARDCODED";

    [Header("[Optional] - certain behaviors off if missing:")]

    [SerializeField]
    GameObject m_SpawnWithA;

    [Header("[Manual] - you MAY need to fixup these refs:")]

    [SerializeField, FormerlySerializedAs("m_PlayerFace")]
    Transform m_TrackingSpaceRoot;
    [SerializeField]
    Transform m_RigAnchor;

    [Header("[ReadOnly] - no need to touch these manually:")]

    [SerializeField]
    Button m_AdvertBtn;
    [SerializeField]
    Button m_DiscoBtn;
    [SerializeField]
    Button m_ShareAllBtn;

    [SerializeField]
    TMP_Text m_AdvertBtnLabel, m_DiscoBtnLabel;
    [SerializeField]
    TMP_Text m_StatusLabel;
    [SerializeField]
    TMP_Text m_GroupListLabel;

    [SerializeField]
    Image m_AdvertBtnIcon, m_DiscoBtnIcon;

    [SerializeField]
    GameObject m_GroupListItem;

    [SerializeField]
    [Tooltip("Depends on the value of 'Require Logged In User'.")]
    Selectable[] m_DisabledWithoutValidUser = Array.Empty<Selectable>();


    static ColoDiscoMan s_Instance;

    bool m_AutoShare;
    bool m_IsAdvertising, m_IsDiscovering;

    User m_My;

    Guid m_ActiveGroup;

    readonly Dictionary<Guid, Group> m_KnownGroups = new();
    readonly Dictionary<Guid, ColoDiscoAnchor> m_KnownAnchors = new();
    readonly List<GameObject> m_Spawned = new();
    readonly List<(int, string)> m_StatusLines = new(); // TODO status text should be encapsulated outta here

    #endregion Fields


    #region Unity messages

    void OnValidate()
    {
        if (string.IsNullOrEmpty(m_HardcodedGroupUUID))
        {
            m_HardcodedGroupUUID = Guid.NewGuid().ToString("N");
        }
        else if (m_HardcodedGroupUUID.Equals("build", StringComparison.InvariantCultureIgnoreCase))
        {
            // uses build guid at runtime so that clients sharing the same APK can use a hardcoded group
        }
        else if (!Guid.TryParse(m_HardcodedGroupUUID, out _))
        {
            Debug.LogError($"\"{m_HardcodedGroupUUID}\" is not parseable as a Guid!", this);
        }

        if (!m_TrackingSpaceRoot)
        {
            var find = FindObjectOfType<OVRCameraRig>();
            if (find)
                m_TrackingSpaceRoot = find.trackingSpace;
        }

        if (!m_RigAnchor)
        {
            var find = GameObject.Find("Ref Point");
            if (find)
                m_RigAnchor = find.transform;
        }

        if (gameObject.scene.IsValid() && !m_RigAnchor) // avoids erroring in prefab view
        {
            Debug.LogError($"\"{name}\" seems to be improperly set-up.", this);
        }

        var child = transform.FindChildRecursive("Text: Status Text");
        if (!child || !child.TryGetComponent(out m_StatusLabel))
        {
            Debug.LogError($"\"{name}\" seems to be improperly set-up.", this);
        }

        child = transform.FindChildRecursive("Text: Group List");
        if (!child || !child.TryGetComponent(out m_GroupListLabel))
        {
            Debug.LogError($"\"{name}\" seems to be improperly set-up.", this);
        }

        var commandsRoot = transform.FindChildRecursive("Commands");
        m_DisabledWithoutValidUser = commandsRoot.GetComponentsInChildren<Selectable>(includeInactive: true);

        foreach (var uiThing in m_DisabledWithoutValidUser)
        {
            switch (uiThing.name)
            {
                case "Button: Advertise":
                    m_AdvertBtn = uiThing as Button;
                    m_AdvertBtnLabel = uiThing.GetComponentInChildren<TMP_Text>(includeInactive: true);
                    break;
                case "Button: Discover":
                    m_DiscoBtn = uiThing as Button;
                    m_DiscoBtnLabel = uiThing.GetComponentInChildren<TMP_Text>(includeInactive: true);
                    break;
                case "Button: Group List Item":
                    m_GroupListItem = uiThing.gameObject;
                    break;
                case "Button: Share All":
                    m_ShareAllBtn = uiThing as Button;
                    break;
            }
        }

        if (!m_AdvertBtn || !m_AdvertBtnLabel || !m_DiscoBtn || !m_DiscoBtnLabel || !m_GroupListItem)
        {
            Debug.LogError($"\"{name}\" seems to be improperly set-up.", this);
            return;
        }

        m_AdvertBtnIcon = m_AdvertBtnLabel.transform.parent.Find("Icon").GetComponent<Image>();
        m_DiscoBtnIcon = m_DiscoBtnLabel.transform.parent.Find("Icon").GetComponent<Image>();
    }

    void Awake()
    {
        Assert.IsNull(s_Instance, "s_Instance already exists");
        s_Instance = this;

        if (m_HardcodedGroupUUID?.Equals("build", StringComparison.InvariantCultureIgnoreCase) == true)
        {
            m_HardcodedGroupUUID = UnityEngine.Application.buildGUID;
        }

        UISetStatusText("Active Group UUID:\n(none)", order: 0);

        foreach (var uiThing in m_DisabledWithoutValidUser)
        {
            uiThing.interactable = !m_RequireLoggedInUser || m_My != null;
        }

        if (m_GroupListItem && m_GroupListItem.activeSelf)
        {
            m_GroupListItem.SetActive(false);
            m_GroupListItem.GetComponentInParent<RectTransform>().sizeDelta = new(-17f, 32f); // screwy
        }

        if (!m_RigAnchor)
            return;

        transform.SetParent(m_RigAnchor, worldPositionStays: false);
    }

    void OnDestroy()
    {
        s_Instance = null;

        if (m_IsAdvertising)
        {
            // KEY API CALL: static OVRColocationSession.StopAdvertisementAsync()
            _ = OVRColocationSession.StopAdvertisementAsync();
        }

        if (m_IsDiscovering)
        {
            // KEY API CALL: static OVRColocationSession.StopDiscoveryAsync()
            _ = OVRColocationSession.StopDiscoveryAsync();
        }

        // KEY API CALL: static event OVRColocationSession.ColocationSessionDiscovered
        OVRColocationSession.ColocationSessionDiscovered -= ReceivedSessionData;
    }

    IEnumerator Start()
    {
        Sampleton.Log($"Scene loaded: {gameObject.scene.name}\n({gameObject.scene.path})");
        Sampleton.Log($"OVRPlugin.version: {OVRPlugin.version}");

        // API call: static Oculus.Platform.Core.Initialize()
        Core.Initialize();

        string log = "Attempting to get logged in user info, " +
                     (m_RequireLoggedInUser ? "which is REQUIRED"
                                            : "though it is NOT REQUIRED") +
                     " by this version of the sample...";
        Sampleton.Log(log);
#if UNITY_EDITOR || UNITY_STANDALONE
        Sampleton.Log("    <color=\"grey\">* this may take upwards of several minutes when using PC Link...</color>");
#endif

        // API call: static Oculus.Platform.Users.GetLoggedInUser()
        Users.GetLoggedInUser().OnComplete(ReceivedLoggedInUser);

        // KEY API CALL: static event OVRColocationSession.ColocationSessionDiscovered
        OVRColocationSession.ColocationSessionDiscovered += ReceivedSessionData;

        yield return null;

        var buildInfoRequest = Resources.LoadAsync<TextAsset>("buildInfo");

        yield return buildInfoRequest;

        const int kOrder = 10;
        if (buildInfoRequest.asset is not TextAsset textAsset || textAsset.dataSize < 10)
        {
            UISetStatusText("<build unknown>", kOrder);
        }
        else
        {
            string text = textAsset.text;
            int hash = text.IndexOf('#');
            if (hash > 0)
                text = $"rev {text.Substring(hash)}\nbuilt {text.Remove(hash - 1)}";
            UISetStatusText(text, kOrder);
        }
    }

    void Update()
    {
        PoseOrigin.UseLocalCoords = OVRInput.Get(OVRInput.RawButton.LShoulder);

        if (OVRInput.IsControllerConnected(OVRInput.Controller.RTouch))
        {
            // the pinch gesture gets quietly mapped to (A), which makes hands annoying to use with this hidden functionality.
            // (hence the IsControllerConnected check)
            SpawnWithButton(OVRInput.RawButton.A, m_SpawnWithA);
        }
    }

    void SpawnWithButton(OVRInput.RawButton btn, GameObject prefab)
    {
        if (!prefab || !ColoDiscoAnchor.Alignment || !OVRInput.GetUp(btn))
            return;

        if (!OVRManager.instance.TryGetComponent(out OVRCameraRig rig))
        {
            Sampleton.LogError($"{nameof(SpawnWithButton)}: No OVRCameraRig attached to OVRManager!");
            return;
        }

        var rightHand = rig.rightControllerAnchor;

        var spawn = Instantiate(
            original: prefab,
            position: rightHand.position,
            rotation: rightHand.rotation
        );

        if (spawn.TryGetComponent(out PoseOrigin poo))
        {
            poo.UpdateCoordsNow();
        }

        Sampleton.Log($"{nameof(SpawnWithButton)}: created \"{spawn.name}\"");

        m_Spawned.Add(spawn);
    }

    #endregion Unity messages


    #region Alignment
    // TODO refactor alignment-related code (from all scenes) into its own sample scene
    //     (these other scenes will do automatic alignment if necessary, to remain focused on the subject matter)

    void AlignPlayer(Transform anchor)
    {
        var player = TrackingSpaceRoot;

        if (!player)
        {
            Sampleton.LogError($"{nameof(AlignPlayer)}: No OVRCameraRig.trackingSpace?");
            return;
        }

        var toLocal = player.worldToLocalMatrix;
        var rot = Quaternion.Inverse(anchor.rotation);

        player.SetPositionAndRotation(
            position: rot * -anchor.position,
            rotation: Quaternion.Euler(0, rot.eulerAngles.y, 0)
        );

        if (m_Spawned.Count == 0)
            return;

        var toWorld = player.localToWorldMatrix;

        foreach (var spawn in m_Spawned)
        {
            var matr = toWorld * (toLocal * spawn.transform.localToWorldMatrix);
            var pos = matr.GetPosition();
            rot = matr.rotation;

            if (spawn.TryGetComponent(out PoseOrigin poe))
            {
                poe.TweenTo(
                    pos: pos,
                    rot: rot,
                    overSec: Mathf.Min((pos - poe.transform.position).magnitude, 7.5f)
                );
            }
            else
            {
                spawn.transform.SetPositionAndRotation(
                    position: pos,
                    rotation: rot
                );
            }
        }
    }

    void ProcessSpawnQueueAligned(Transform anchor, ICollection<Pose> queue)
    {
        if (!m_SpawnWithA || queue.Count == 0)
            return;

        Sampleton.Log($"{nameof(ProcessSpawnQueueAligned)}:");

        foreach (var pose in queue)
        {
            var spawn = Instantiate(
                original: m_SpawnWithA,
                position: anchor.position + anchor.rotation * pose.position,
                rotation: anchor.rotation * pose.rotation
            );

            spawn.name = spawn.name.Replace("(Clone)", $"{m_Spawned.Count}");

            if (spawn.TryGetComponent(out PoseOrigin poo))
            {
                poo.UpdateCoordsNow();
            }

            Sampleton.Log($"+ created \"{spawn.name}\"");

            m_Spawned.Add(spawn);
        }
    }

    #endregion Alignment


    #region UI helpers

    void SetActiveGroup(Guid groupId)
    {
        if (groupId == m_ActiveGroup)
            return;

        bool wasEmpty = Guid.Empty == m_ActiveGroup;
        bool isEmpty = Guid.Empty == groupId;

        Sampleton.Log($"{nameof(SetActiveGroup)}:");
        if (!wasEmpty)
            Sampleton.Log($"- {m_ActiveGroup}");
        if (!isEmpty)
            Sampleton.Log($"+ {groupId}");

        // UI updates:

        if (m_KnownGroups.TryGetValue(m_ActiveGroup, out var currGroup) && currGroup.ListButton)
            currGroup.ListButton.interactable = true;

        if (m_KnownGroups.TryGetValue(groupId, out var nextGroup) && nextGroup.ListButton)
            nextGroup.ListButton.interactable = false;

        UISetStatusText(
            $"Active Group UUID:\n{(isEmpty ? "(none)" : groupId.ToString())}",
            order: 0
        );

        if (m_IsAdvertising)
        {
            Sampleton.Log($"* NOTICE: Active group changed while you are still advertising!", LogType.Warning);
        }

        m_ActiveGroup = groupId;

        if (nextGroup is not null && ColoDiscoAnchor.Alignment)
        {
            ProcessSpawnQueueAligned(ColoDiscoAnchor.Alignment.transform, nextGroup.Metadata.Poses);
        }
    }

    /// <summary>
    ///   In most cases outputs 1 group (the active), except when you enable <see cref="ApplyToAllGroups"/>,
    ///   which must be done through code or by activating the hidden toggle in the UI.
    /// </summary>
    /// <returns>
    ///   False if there are no group UUIDs active and <see cref="ForceShareAnchors"/> is off (default).
    /// </returns>
    bool AnyActiveGroups(out Guid[] groupIds)
    {
        groupIds = Array.Empty<Guid>();

        var allGroups = m_KnownGroups.Keys;

        if ((allGroups.Count == 0 || m_ActiveGroup == Guid.Empty) && !ForceShareAnchors)
        {
            Sampleton.LogError("- Cannot share anchor; no active group!");
            Sampleton.Log("* You must <b>Advertise or Discover</b> sessions in order to work with groups.", LogType.Warning);
            return false;
        }

        if (ApplyToAllGroups)
        {
            groupIds = new Guid[allGroups.Count];
            allGroups.CopyTo(groupIds, 0);
        }
        else
        {
            groupIds = new[] { m_ActiveGroup };
        }

        return true;
    }

    static string FixupDisplayName(string orig, Guid groupId)
    {
        const int kMaxOrigLen = 15;
        if (string.IsNullOrEmpty(orig))
            orig = "<err>";
        else if (orig.Length > kMaxOrigLen)
            orig = orig.Remove(kMaxOrigLen);
        return $"{orig} ({groupId.Brief()})";
    }

    void UISetStatusText(string text, int order)
    {
        Action<int> replace;
        Func<int, bool> insert;
        if (string.IsNullOrEmpty(text))
        {
            replace = i => m_StatusLines.RemoveAt(i);
            insert = _ => false;
        }
        else
        {
            replace = i => m_StatusLines[i] = (order, text);
            insert = i => { m_StatusLines.Insert(i, (order, text)); return true; };
        }

        int i = m_StatusLines.Count;
        while (i-- > 0)
        {
            var current = m_StatusLines[i].Item1;
            if (order == current)
            {
                replace(i);
                goto Apply;
            }
            if (order > current && insert(i + 1))
            {
                goto Apply;
            }
        }

        insert(0);
        Apply:
        m_StatusLabel.text = string.Join("\n\n", m_StatusLines.Select(p => p.Item2));
    }

    void UIAddDiscoveryItem(string labelTxt, Guid groupId, bool isSelected)
    {
        if (!m_GroupListItem || !m_KnownGroups.TryGetValue(groupId, out var group) || group.ListButton)
            return;

        labelTxt = FixupDisplayName(labelTxt, groupId);

        var item = Instantiate(m_GroupListItem, m_GroupListItem.transform.parent);
        item.SetActive(false);

        item.name = labelTxt;

        var label = item.GetComponentInChildren<TMP_Text>(includeInactive: true);
        label.text = labelTxt;

        var btn = item.GetComponent<Button>();
        btn.interactable = !isSelected;

        btn.onClick.AddListener(() =>
        {
            SetActiveGroup(groupId);

            if (!LoadOnGroupSwitch)
                return;

            Sampleton.Log("(attempting to auto-load current group anchors)");
            LoadGroupAnchorsAsync(groupId);
        });

        group.ListButton = btn;

        item.SetActive(true);

        if (m_GroupListLabel)
            m_GroupListLabel.gameObject.SetActive(false);
    }

    void UIStateAdvertStart(bool warning)
    {
        bool hasAdvertised = m_KnownGroups.Values.Any(g => g.IsMine);

        m_AdvertBtnLabel.text = hasAdvertised ? "Advertise NEW Uuid" : "Start Advertising";

        if (m_AdvertBtnIcon)
            m_AdvertBtnIcon.color = warning ? Color.yellow : Color.white;

        m_AdvertBtn.interactable = !m_IsDiscovering;
        m_DiscoBtn.interactable = true;
    }

    void UIStateAdvertStop(bool warning)
    {
        m_AdvertBtnLabel.text = "Stop Advertising";

        if (m_AdvertBtnIcon)
            m_AdvertBtnIcon.color = warning ? Color.yellow : Color.green;

        m_AdvertBtn.interactable = true;
        m_DiscoBtn.interactable = m_IsDiscovering || warning;
    }

    void UIStateDiscoStart(bool warning)
    {
        m_DiscoBtnLabel.text = "Start Discovering";

        if (m_DiscoBtnIcon)
            m_DiscoBtnIcon.color = warning ? Color.yellow : Color.white;

        m_DiscoBtn.interactable = !m_IsAdvertising;
        m_AdvertBtn.interactable = true;
    }

    void UIStateDiscoStop(bool warning)
    {
        m_DiscoBtnLabel.text = "Stop Discovering";

        if (m_DiscoBtnIcon)
            m_DiscoBtnIcon.color = warning ? Color.yellow : Color.green;

        m_DiscoBtn.interactable = true;
        m_AdvertBtn.interactable = m_IsAdvertising || warning;
    }

    #endregion UI helpers


    #region HOT: API callbacks

    void ReceivedLoggedInUser(Message msg)
    {
        if (msg.IsError)
        {
            var err = msg.GetError();
            string codeStr = err.Code switch // lol ever heard of an enum?
            {
                2 => $"AUTHENTICATION_ERROR({err.Code})",
                3 => $"NETWORK_ERROR({err.Code})",
                4 => $"STORE_INSTALLATION_ERROR({err.Code})",
                5 => $"CALLER_NOT_SIGNED({err.Code})",
                6 => $"UNKNOWN_SERVER_ERROR({err.Code})",
                7 => $"PERMISSIONS_FAILURE({err.Code})",
                _ => $"UNKNOWN_ERROR({err.Code})"
            };
            Sampleton.LogError($"{nameof(ReceivedLoggedInUser)} FAILED: code={codeStr} message=\"{err.Message}\"");
#if UNITY_EDITOR || UNITY_STANDALONE
            if (err.Code == 1)
            {
                Sampleton.Log("- Did you remember to enter your test user credentials into the OculusPlatformSettings asset?");
            }
#endif
            if (!m_RequireLoggedInUser)
            {
                Sampleton.Log("* No worries! This build does not have a hard requirement for user login.\n<color=\"yellow\">* However, expect some elements of the sample to be less consistent without this info!</color>");
            }
            return;
        }

        Sampleton.Log($"{nameof(ReceivedLoggedInUser)} success:");

        Assert.AreEqual(Message.MessageType.User_GetLoggedInUser, msg.Type, "msg.Type");

        m_My = msg.GetUser();

        Assert.IsNotNull(m_My, "msg.GetUser()");

        Sampleton.Log($"+ Oculus Username: \'{m_My.OculusID}\'\n+ Oculus User ID: {m_My.ID}");

        if (m_My.ID == 0)
        {
            Sampleton.LogError("- You are not authenticated to use this app.\n- Shared Spatial Anchors will not work.");
            return;
        }

        foreach (var uiThing in m_DisabledWithoutValidUser)
        {
            uiThing.interactable = true;
        }
    }

    void ReceivedSessionData(OVRColocationSession.Data data)
    {
        Sampleton.Log($"{nameof(ReceivedSessionData)}:\n+ Uuid: {data.AdvertisementUuid}");

        if (m_KnownGroups.TryGetValue(data.AdvertisementUuid, out var group))
        {
            Sampleton.Log("- Group Uuid is already known!", LogType.Warning);
        }
        else
        {
            m_KnownGroups[data.AdvertisementUuid] = group = new Group
            {
                Uuid = data.AdvertisementUuid
            };
        }

        if (data.Metadata is null)
        {
            Sampleton.Log("- Received NULL metadata.\n- Continuing anyway...", LogType.Warning);
        }
        else if (!CustomAdvertData.TryRead(data.Metadata, out group.Metadata))
        {
            Sampleton.Log("- Had trouble reading metadata section.\n- Continuing anyway...", LogType.Warning);
        }

        group.Metadata ??= new CustomAdvertData(null, 0, null);

        Sampleton.Log($"+ DisplayName: \"{group.Metadata.DisplayName}\"");

        if (group.Metadata.Users.Count > 0)
            Sampleton.Log($"+ {group.Metadata.Users.Count} immutable OVRSpaceUsers");
        if (group.Metadata.Anchors.Count > 0)
            Sampleton.Log($"+ {group.Metadata.Anchors.Count} immutable anchor Guids");
        if (group.Metadata.Poses.Count > 0)
            Sampleton.Log($"+ {group.Metadata.Poses.Count} immutable non-anchor poses");

        UIAddDiscoveryItem(group.Metadata.DisplayName, group.Uuid, isSelected: false);
    }

    #endregion HOT: API callbacks


    #region HOT: API callsites

    async void StartAdvertisingAsync(byte[] bytes, Group newGroup)
    {
        // KEY API CALL: static OVRColocationSession.StartAdvertisementAsync(colocationSessionData)
        var startAdvert = await OVRColocationSession.StartAdvertisementAsync(bytes);

        Sampleton.Log($"* {startAdvert.Status.ForLogging()}", !startAdvert.Success);

        if (!startAdvert.TryGetValue(out var guid))
        {
            UIStateAdvertStart(warning: true);
            return;
        }

        Sampleton.Log($"+ DisplayName: \"{(AdvertiseNullData ? "null" : newGroup.Metadata.DisplayName)}\"");
        Sampleton.Log($"+ Uuid: {guid}");

        newGroup.Uuid = guid;
        m_KnownGroups[guid] = newGroup;

        UIStateAdvertStop(warning: false);
        UIAddDiscoveryItem(newGroup.Metadata.DisplayName, guid, isSelected: true);
        SetActiveGroup(guid);

        m_IsAdvertising = true;
    }

    async void StopAdvertisingAsync()
    {
        // KEY API CALL: static OVRColocationSession.StopAdvertisementAsync()
        var stopAdvert = await OVRColocationSession.StopAdvertisementAsync();

        Sampleton.Log($"* {stopAdvert.Status.ForLogging()}", !stopAdvert.Success);

        if (!stopAdvert.Success)
        {
            UIStateAdvertStop(warning: true);
            return;
        }

        m_IsAdvertising = false;

        UIStateAdvertStart(warning: false);
    }


    async void StartDiscoveringAsync()
    {
        // KEY API CALL: static OVRColocationSession.StartDiscoveryAsync()
        var startDisco = await OVRColocationSession.StartDiscoveryAsync();
        // note: this call is pretty useless unless your code has subscribed something to the static event
        //       OVRColocationSession.ColocationSessionDiscovered!

        Sampleton.Log($"* {startDisco.Status.ForLogging()}", !startDisco.Success);

        if (!startDisco.Success)
        {
            UIStateDiscoStart(warning: true);
            return;
        }

        m_IsDiscovering = true;

        UIStateDiscoStop(warning: false);
    }

    async void StopDiscoveringAsync()
    {
        // KEY API CALL: static OVRColocationSession.StopDiscoveryAsync()
        var stopDisco = await OVRColocationSession.StopDiscoveryAsync();

        Sampleton.Log($"* {stopDisco.Status.ForLogging()}", !stopDisco.Success);

        if (!stopDisco.Success)
        {
            UIStateDiscoStop(warning: true);
            return;
        }

        m_IsDiscovering = false;

        UIStateDiscoStart(warning: false);
    }


    void NotifyAnchorLocalizedAsync(ColoDiscoAnchor anchor, bool autoShare = false)
    {
        m_ShareAllBtn.interactable = true;

        if (!ShareAutomatically && !autoShare)
        {
            // we'll leave sharing up to some future button callback.
            return;
        }

        ShareAnchorsToActiveGroupsAsync(new[] { anchor });
    }

    async void ShareAnchorsToActiveGroupsAsync(IEnumerable<ColoDiscoAnchor> anchors)
    {
        if (!AnyActiveGroups(out var groupIds))
            return;

        // start with all known anchors
        var toShare = new List<ColoDiscoAnchor>(anchors);

        int i = toShare.Count;
        while (i-- > 0)
        {
            var anchor = toShare[i];
            // remove anything deleted ("hidden"), disabled, ~~or not owned by us~~ (scratch that last for now)
            // TODO anchor.Source.IsMine is not fully reliable in this scene (it is more reliable in "Sharing to Users")
            if (!anchor || !anchor.isActiveAndEnabled) // || !anchor.Source.IsMine)
            {
                toShare.RemoveAt(i);
                // note: We could technically still try sharing these destroyed anchors (we have the original UUID in
                //       m_KnownAnchors.Keys), but we are choosing to assume that if the anchor was Destroy()'ed, the
                //       app user probably does not intend to propagate it to others in the room (at least, not without
                //       explicitly reloading it first).
            }

            if (anchor.Source.IsMine)
                continue;

            Sampleton.Log(
                $"- WARNING: You are not the original creator of {anchor.Uuid.Brief()}." +
                "  (The following share operation is likely to fail.)",
                LogType.Warning
            );
            // pst: your code could try loading these anchors individually as a fallback,
            // so any that DO belong to the local user actually get shared.
        }

        if (toShare.Count == 0 && !ForceShareAnchors)
        {
            Sampleton.Log("+ Share Anchor SKIPPED: No shareable anchors.");
            return;
        }

        Sampleton.Log($"+ {toShare.Count} shareable anchors.");

        OVRResult<OVRAnchor.ShareResult> shareResult;
        if (groupIds.Length == 1)
            // KEY API CALL: static OVRSpatialAnchor.ShareAsync(anchors, [one] groupUuid)
            shareResult = await OVRSpatialAnchor.ShareAsync(toShare, groupIds[0]);
        else
            // KEY API CALL: static OVRSpatialAnchor.ShareAsync(anchors, [many] groupUuids)
            shareResult = await OVRSpatialAnchor.ShareAsync(toShare, groupIds);

        // notice:  The overloads of ShareAsync used for group sharing do not have instance method equivalents.

        var loggedResult = shareResult.Status.ForLogging();

        if (!shareResult.Success)
        {
            Sampleton.LogError($"- Share {loggedResult}");
            return;
        }

        Sampleton.Log($"+ Share {loggedResult}:");

        // now all that's left is to update our internal states and external UI ~

        foreach (var id in groupIds)
        {
            Sampleton.Log($"  group: {id}");

            var shareSet = m_KnownGroups[id].SharedAnchors;
            foreach (var anchor in toShare)
            {
                Sampleton.Log($"  + anchor: {anchor.Uuid.Brief()}");
                shareSet.Add(anchor.Uuid);
            }
        }

        foreach (var anchor in toShare)
        {
            anchor.IsSaved = true; // (anchors are implicitly saved upon successful share)
            anchor.UpdateUI();
        }
    }

    async void LoadGroupAnchorsAsync(Guid groupId)
    {
        var prefab = SampleController.Instance.anchorPrefab;
        if (!prefab || !(prefab is ColoDiscoAnchor discoPrefab))
        {
            Sampleton.LogError($"{nameof(LoadGroupAnchorsAsync)}: Invalid {nameof(ColoDiscoAnchor)} prefab reference!");
            Debug.LogError($"- See Also: {nameof(SampleController)}.cs", SampleController.Instance);
            return;
        }

        Sampleton.Log($"* group: {groupId.Brief()}");

        var unboundAnchors = new List<OVRSpatialAnchor.UnboundAnchor>();

        // KEY API CALL: static OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync(groupUuid, unboundAnchors)
        var loadResult = await OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync(groupId, unboundAnchors);

        var loggedResult = loadResult.Status.ForLogging();

        // smol note:  the `out` parameter below does reassign the value of the `unboundAnchors` variable, however it
        // will be reassigned to *itself* since it's the same list we passed into LoadUnboundSharedAnchorsAsync.

        if (!loadResult.TryGetValue(out unboundAnchors))
        {
            Sampleton.LogError($"- FAILED: {loggedResult}");
            return;
        }

        if (OnlyRememberedAnchors)
        {
            if (!ColoDiscoAnchor.HasRememberedAnchor)
            {
                Sampleton.Log($"- No anchors chosen to be remembered! Nothing to load.");
                return;
            }

            // TODO v72+ offers a better way to filter for specific anchors when loading from a group UUID. This is a stopgap until then.
            unboundAnchors.RemoveAll(unb => !ColoDiscoAnchor.IsRemembered(unb.Uuid));
        }

        if (unboundAnchors.Count == 0)
        {
            Sampleton.Log($"- Received 0 anchors. API result: {loggedResult}");
            return;
        }

        Sampleton.Log($"+ received {unboundAnchors.Count} unbound anchors ({loggedResult})");

        BindAnchorResults(unboundAnchors, discoPrefab, groupId);
    }

    /// <seealso cref="SharedAnchorLoader.BindAnchorsAsync"/>
    async void BindAnchorResults(List<OVRSpatialAnchor.UnboundAnchor> unboundAnchors, ColoDiscoAnchor discoPrefab, Guid? groupId)
    {
        int i = unboundAnchors.Count;

        // use lists so we can shrink them in the case of errored individuals:
        var discoAnchors = new List<ColoDiscoAnchor>(i);
        var areDone = new List<OVRTask<bool>>(i);
        var batchResultBuffer = new List<bool>(i);

        if (LocalizeBeforeBinding)
        {
            while (i-- > 0)
            {
                areDone.Add(unboundAnchors[i].LocalizeAsync());
                // intentionally no await in loop
            }

            // NOW we await:
            batchResultBuffer = await OVRTask.WhenAll(areDone, batchResultBuffer);

            i = unboundAnchors.Count;
            while (i-- > 0)
            {
                if (batchResultBuffer[i])
                    continue;

                Sampleton.LogError(
                    $"{nameof(LocalizeBeforeBinding)}: pre-localization FAILED:\n- {unboundAnchors[i].Uuid}\n- anchor will NOT be bound."
                );
                unboundAnchors.RemoveAt(i);
            }

            i = unboundAnchors.Count;
            areDone.Clear(); // important for our GC reuse schemes to not bite us in the face
        }

        while (i-- > 0)
        {
            var anchor = makeAndBindSpatialAnchor(unboundAnchors[i]);

            if (!anchor)
                continue;

            discoAnchors.Add(anchor);
            if (LocalizeBeforeBinding)
                areDone.Add(anchor.WhenCreatedAsync());
            else
                areDone.Add(anchor.WhenLocalizedAsync()); // which also awaits WhenCreatedAsync (when awaited)
            // intentionally no await in loop
        }

        // NOW we await:
        batchResultBuffer = await OVRTask.WhenAll(areDone, batchResultBuffer);

        var groupAnchors = groupId.HasValue ? m_KnownGroups[groupId.Value].SharedAnchors : null;

        for (i = 0; i < batchResultBuffer.Count; --i)
        {
            if (!batchResultBuffer[i]) // already should be error logged, and discoAnchor would have self-destructed.
                continue;

            var discoAnchor = discoAnchors[i];

            Assert.IsTrue(discoAnchor, $"{nameof(discoAnchor)} #{i} existence");

            var uuid = discoAnchor.Uuid;

            // now all that's left is to update our internal states and activate the anchor ~
            _ = groupAnchors?.Add(uuid);
            // note: no need to update m_KnownAnchors; see NotifyAnchorLocalized

            discoAnchor.gameObject.SetActive(true); // (if LocalizeBeforeBinding, this is a no-op)
            discoAnchor.UpdateUI();
        }

        return;

        // local function section:

        ColoDiscoAnchor makeAndBindSpatialAnchor(OVRSpatialAnchor.UnboundAnchor unbound)
        {
            var uuid = unbound.Uuid;
            var spatialAnchor = Instantiate(discoPrefab);

            if (!LocalizeBeforeBinding)
            {
                // we deactivate the GameObject until deferred localization is complete, to avoid the minor glitch of
                // "teleporting anchors".
                spatialAnchor.gameObject.SetActive(false);
            }

            spatialAnchor.Source =
                groupId.HasValue ? AnchorSource.FromGroupShare(groupId.Value)
                                 : AnchorSource.FromSave(uuid, ColoDiscoAnchor.IsRemembered(uuid) ? CurrentUserID : 0);

            try
            {
                // KEY API CALL: instance OVRSpatialAnchor.UnboundAnchor.BindTo(spatialAnchor)
                unbound.BindTo(spatialAnchor);
                // (OVRSpatialAnchors cannot be successfully loaded without following this step!)
                return spatialAnchor;
            }
            catch (Exception e)
            {
                Destroy(spatialAnchor.gameObject);
                Sampleton.Log(
                    $"  - Binding {uuid.Brief()} FAILED: {e.GetType().Name} (see logcat)",
                    LogType.Exception,
                    LogOption.None
                );
                Debug.LogException(e);
                return null;
            }
        } // end local function makeAndBindSpatialAnchor
    }

    /// <remarks>
    ///   Currently unused, but included for comparing group sharing with the original OVRSpaceUser-based sharing API.
    ///   See instead: <see cref="SharedAnchor.ShareAllMineTo(ulong, bool)"/> and similar implementations
    ///   in <see cref="SharedAnchor"/>.
    /// </remarks>
    async void ShareToUsers(ColoDiscoAnchor anchor)
    {
        var users = new List<OVRSpaceUser>();
        foreach (var id in m_KnownGroups.Values.SelectMany(g => g.Metadata.Users))
        {
            // (maybe a) KEY API CALL: static OVRSpaceUser.TryCreate(platformUserId, out spaceUser)
            if (OVRSpaceUser.TryCreate(id, out var user))
                users.Add(user);
            // p.s. - it's a maybe because we are moving toward promoting group sharing over the original user-based sharing.
            // iff you are using the user-based sharing in your app, *then* this becomes a KEY API call.
            else
                Sampleton.LogError($"    - bad space user: {id}");
        }

        if (users.Count == 0)
        {
            Sampleton.Log($"- Share {anchor.Uuid.Brief()} SKIPPED: No known users.");
            return;
        }

        string loggedResult;

        if (!anchor.IsSaved)
        {
            // API call: instance OVRSpatialAnchor.SaveAnchorAsync()
            var saveResult = await anchor.SaveAnchorAsync();

            loggedResult = saveResult.Status.ForLogging();

            if (!saveResult.Success)
            {
                Sampleton.LogError($"- {nameof(OVRSpatialAnchor.SaveAnchorAsync)} FAILED: {loggedResult}");
                return;
            }

            Sampleton.Log($"+ {nameof(OVRSpatialAnchor.SaveAnchorAsync)} = {loggedResult}");
        }

        // KEY API CALL: instance OVRSpatialAnchor.ShareAsync(users)
        var shareResult = await anchor.ShareAsync(users);

        loggedResult = shareResult.ForLogging();

        if (!shareResult.IsSuccess())
        {
            Sampleton.LogError($"- Share {anchor.Uuid.Brief()} FAILED: {loggedResult}");
            return;
        }

        Sampleton.Log($"+ Share {anchor.Uuid.Brief()} = {loggedResult}");

        foreach (var user in users)
        {
            Sampleton.Log($"  + user: {user.Id}");
        }

        anchor.UpdateUI();
    }

    /// <remarks>
    ///   Included for comparing group sharing/loading with the original OVRSpaceUser-based sharing and loading API.
    /// </remarks>
    /// <seealso cref="SharedAnchorLoader.LoadAnchorsFromRemote"/>
    async void LoadAnchorsById(ICollection<Guid> anchorIds)
    {
        var prefab = SampleController.Instance.anchorPrefab;
        if (!prefab || !(prefab is ColoDiscoAnchor discoPrefab))
        {
            Sampleton.LogError($"{nameof(LoadGroupAnchorsAsync)}: Invalid {nameof(ColoDiscoAnchor)} prefab reference!");
            Debug.LogError($"- See Also: {nameof(SampleController)}.cs", SampleController.Instance);
            return;
        }

        if (anchorIds.Count == 1)
            Sampleton.Log($"* querying for anchor:{anchorIds.First().Brief()}");
        else
            Sampleton.Log($"* querying for {anchorIds.Count} anchors...");

        var unboundAnchors = new List<OVRSpatialAnchor.UnboundAnchor>();

        // KEY API CALL: static OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync(uuids, unboundAnchors)
        var loadResult = await OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync(anchorIds, unboundAnchors);

        var loggedResult = loadResult.Status.ForLogging();

        if (!loadResult.TryGetValue(out unboundAnchors))
        {
            Sampleton.LogError($"- Load FAILED: {loggedResult}");
            return;
        }

        if (unboundAnchors.Count == 0)
        {
            Sampleton.Log($"- Received 0 anchors. API result: {loggedResult}");
            return;
        }

        Sampleton.Log($"+ received {unboundAnchors.Count} unbound anchors ({loggedResult})");

        BindAnchorResults(unboundAnchors, discoPrefab, null);
    }

    #endregion HOT: API callsites

} // end MonoBehaviour ColoDiscoMan
