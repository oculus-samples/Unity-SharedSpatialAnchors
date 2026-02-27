// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;

using System;
using System.Collections.Generic;
using System.Linq;

using Photon.Pun;

using TMPro;

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

using Sampleton = SampleController;

/// <summary>
///   Controls anchor data and anchor control panel behavior.
/// </summary>
/// <remarks>
///   You can locate key API calls throughout the sample code by searching for "KEY API CALL" (use CTRL+F in most IDEs).
/// </remarks>
[RequireComponent(typeof(OVRSpatialAnchor))]
[MetaCodeSample("SharedSpatialAnchors")]
public class SharedAnchor : MonoBehaviour
{
    //
    // Static interface

    public static IReadOnlyCollection<SharedAnchor> All
        => s_Instances.Values.Where(a => a).ToArray();

    public static IReadOnlyCollection<SharedAnchor> AllMine
        => s_Instances.Values.Where(a => a && a.Source.IsMine).ToArray();

    public static bool Find(Guid uuid, out SharedAnchor anchor)
        => s_Instances.TryGetValue(uuid, out anchor) && anchor;

    static readonly Dictionary<Guid, SharedAnchor> s_Instances = new();


    public static async void ShareAllMineTo(ulong oneUser, bool reshareOnly = false)
    {
        Sampleton.Log($"{nameof(ShareAllMineTo)}({oneUser}{(reshareOnly ? $", {nameof(reshareOnly)}" : "")})");

        // need to ask the OVR backend if this user ID is valid for our app to use as a share target:
        if (!OVRSpaceUser.TryCreate(oneUser, out var spaceUser))
        {
            Sampleton.LogError("- ERR: uid is not a valid space user here.");
            return;
        }

        var anchors = (
            from a in AllMine
            where !reshareOnly || a.ShareSucceeded
            select a
        ).ToList();

        if (anchors.Count == 0)
        {
            Sampleton.Log($"- SKIP: no loaded anchors to share.");
            return;
        }

        // KEY API CALL: static OVRSpatialAnchor.ShareAsync(anchors, users)
        var shareResult = await OVRSpatialAnchor.ShareAsync(anchors.Select(a => a.SpatialAnchor), new[] { spaceUser });

        string loggedResult = shareResult.ForLogging();

        bool success = shareResult.IsSuccess();

        var uuids = new HashSet<Guid>(anchors.Count);
        foreach (var anchor in anchors)
        {
            anchor.ShareSucceeded = success;
            uuids.Add(anchor.Uuid);
        }

        if (!success)
        {
            Sampleton.LogError($"- Share FAILED: {loggedResult}");
            return;
        }

        Sampleton.Log($"+ Shared {uuids.Count} spatial anchors: {loggedResult}");

        PhotonAnchorManager.PublishAnchorsToUsers(uuids, new[] { oneUser });
    }

    public static async void ShareAllMineTo(IReadOnlyCollection<ulong> users, bool reshareOnly = false)
    {
        Sampleton.Log($"{nameof(ShareAllMineTo)}:");

        var anchors = (
            from a in AllMine
            where !reshareOnly || a.ShareSucceeded
            select a
        ).ToList();

        if (anchors.Count == 0)
        {
            Sampleton.Log($"- SKIP: no loaded anchors to share.");
            return;
        }

        var spaceUsers = new List<OVRSpaceUser>(users.Count);
        foreach (var uid in users)
        {
            if (OVRSpaceUser.TryCreate(uid, out var spaceUser))
            {
                spaceUsers.Add(spaceUser);
            }
            else
            {
                Sampleton.LogError($"- ERR: {uid} is not a valid space user here.");
                return;
            }
        }

        Sampleton.Log($"+ sharing {anchors.Count} anchors to {spaceUsers.Count} uids..");

        // KEY API CALL: static OVRSpatialAnchor.ShareAsync(anchors, users)
        var shareResult = await OVRSpatialAnchor.ShareAsync(anchors.Select(a => a.SpatialAnchor), spaceUsers);

        string loggedResult = shareResult.ForLogging();

        bool success = shareResult.IsSuccess();

        var uuids = new HashSet<Guid>(anchors.Count);
        foreach (var anchor in anchors)
        {
            anchor.ShareSucceeded = success;
            uuids.Add(anchor.Uuid);
        }

        if (!success)
        {
            Sampleton.LogError($"- Share FAILED: {loggedResult}");
            return;
        }

        Sampleton.Log($"+ Shared {uuids.Count} spatial anchors: {loggedResult}");

        PhotonAnchorManager.PublishAnchorsToUsers(AllMine.Select(a => a.Uuid), users);
    }


    //
    // Public Properties & UnityEvent Callbacks (for Buttons)

    public OVRSpatialAnchor SpatialAnchor => m_SpatialAnchor;

    public Guid Uuid => SpatialAnchor && SpatialAnchor.Created ? SpatialAnchor.Uuid
                                                               : Source.Uuid;

    public bool IsSaved
    {
        get => LocallySaved.AnchorIsRemembered(Uuid);
        set
        {
            if (value)
                LocallySaved.RememberAnchor(Uuid, Source.IsMine);
            else
                LocallySaved.ForgetAnchor(Uuid);

            UpdateUI();
        }
    }

    public bool ShareSucceeded
    {
        get => m_ShareSucceeded;
        set
        {
            m_ShareAttempted = true;
            m_ShareSucceeded = value;
            UpdateUI();
        }
    }
    bool m_ShareAttempted;
    bool m_ShareSucceeded;

    public AnchorSource Source
    {
        get => m_Source;
        set
        {
            if (m_Source.Equals(value))
                return;

            if (m_Source.IsSet)
            {
                Sampleton.LogError($"{name}.{nameof(Source)} is already set!");
                return;
            }

            m_Source = value;

            m_ShareSucceeded = value.Origin == AnchorSource.Type.FromSpaceUserShare;
        }
    }


    public async void OnSaveLocalButtonPressed()
    {
        if (Uuid == Guid.Empty)
        {
            Sampleton.LogError($"{nameof(OnSaveLocalButtonPressed)}: Not saving! null, missing, or invalid OVRSpatialAnchor reference");
            return;
        }

        if (IsSaved)
        {
            IsSaved = false;
            // only "un"saves the UUID serialized by the app logic~
            // (distinct from the "Erase" API call)
            LocallySaved.CommitToDisk();
            return;
        }

        Sampleton.Log($"{nameof(OnSaveLocalButtonPressed)}: {Uuid}");

        // API call: instance OVRSpatialAnchor.SaveAnchorAsync()
        var saveResult = await SpatialAnchor.SaveAnchorAsync();
        // note: saving is not "KEY" since it is no longer required for apps to save their anchors before sharing;
        // saving is now done automatically when you call any sharing API.

        string loggedResult = saveResult.Status.ForLogging();

        if (!saveResult.Success)
        {
            Sampleton.LogError($"- Saving Spatial Anchor FAILED: {loggedResult}");
            return;
        }

        Sampleton.Log($"+ Saved Spatial Anchor: {loggedResult}");

        IsSaved = true;
        LocallySaved.CommitToDisk();
    }

    public void OnHideButtonPressed()
    {
        var uuid = Uuid;
        Sampleton.Log($"{nameof(OnHideButtonPressed)}: {uuid}");

        Destroy(gameObject);

        // NOTE: "Destroy" == "Hide" for anchors only if they have been saved locally or shared.
        // Otherwise, this is a proper deletion as far as your app is concerned; even if you saved its Uuid prior to
        // destroying the anchor, there is no guarantee you'll be able to load it back (although it isn't guaranteed
        // that you *can't* load it back, either.. better safe than ???!)
    }

    public async void OnEraseButtonPressed()
    {
        if (!SpatialAnchor)
        {
            Sampleton.Log($"{nameof(OVRSpatialAnchor.EraseAnchorAsync)}: NO-OP (anchor already destroyed)");
            return;
        }

        var uuid = Uuid;

        Sampleton.Log($"{nameof(OVRSpatialAnchor.EraseAnchorAsync)}: {uuid.Brief()}");

        var anchor = SpatialAnchor;
        m_SpatialAnchor = null;

        // API call: instance OVRSpatialAnchor.EraseAnchorAsync()
        var eraseResult = await anchor.EraseAnchorAsync();

        string loggedResult = eraseResult.Status.ForLogging();

        if (!eraseResult.Success)
        {
            Sampleton.LogError($"- Erasing Spatial Anchor FAILED: {loggedResult}");
            m_SpatialAnchor = anchor;
            return;
        }

        Sampleton.Log($"+ Erased Spatial Anchor: {loggedResult}");

        LocallySaved.IgnoreAnchor(uuid);
        LocallySaved.CommitToDisk();

        Destroy(gameObject);
    }

    public void OnShareButtonPressed()
    {
        ShareWithRoom();
    }

    public void OnAlignButtonPressed()
    {
        if (!ShareSucceeded)
        {
            Sampleton.Log(
                "OnAlignButtonPressed: You must successfully share an anchor with the room before you can designate" +
                " it as the room's alignment anchor.",
                LogType.Error
            );
            return;
        }

        Sampleton.Log("OnAlignButtonPressed");

        var offset = new Pose(transform.position, transform.rotation);

        Alignment.SetMRUKOrigin(this, offset);

        PhotonAnchorManager.PublishAlignmentAnchor(Uuid, offset);
    }


    public void ShareWithRoom()
    {
        Sampleton.Log($"{nameof(ShareWithRoom)}:");

        // userIds is an array of scoped Oculus User IDs of peers in this Photon Room:
        if (!IsReadyToShare(out var userIds, printReason: true))
            return;

        // (note: Photon won't have any valid IDs if your app hasn't gone through the DUC & Horizon store entitlement steps yet!)

        ShareToUsers(userIds);
    }


    public void UpdateUI()
    {
        gameObject.name = $"{Source}:{Uuid:N}";

        if (m_AnchorName)
        {
            m_AnchorName.text = $"{Uuid}\n({nameof(Source)}: {Source})";
        }

        if (m_ShareIcon)
        {
            m_ShareIcon.color = ShareSucceeded ? SampleColors.Green
                                               : m_ShareAttempted ? SampleColors.Red
                                                                  : SampleColors.Gray;
        }

        if (m_SaveIcon)
        {
            m_SaveIcon.color = IsSaved ? SampleColors.Green
                                       : SampleColors.Gray;
        }

        if (m_AlignIcon)
        {
            m_AlignIcon.color = Alignment.MRUKWorldLockAnchor == this ? SampleColors.Green
                                                                      : SampleColors.Gray;
        }
    }


    //
    // Fields & Constants

    [SerializeField]
    TextMeshProUGUI m_AnchorName;

    [SerializeField]
    Image m_SaveIcon;

    [SerializeField]
    GameObject m_ShareButton;

    [SerializeField]
    Image m_ShareIcon;

    [SerializeField]
    Image m_AlignIcon;

    [SerializeField, HideInInspector]
    OVRSpatialAnchor m_SpatialAnchor;

    AnchorSource m_Source;


    //
    // MonoBehaviour Messages

    void OnValidate()
    {
        if (!TryGetComponent(out m_SpatialAnchor))
        {
            Debug.LogError($"<{nameof(SharedAnchor)}> on '{name}': please fix my refs in the inspector!", this);
        }
    }

    async void Awake()
    {
        var canvas = GetComponentInChildren<Canvas>();
        if (canvas)
            canvas.enabled = false; // don't render controls until creation & localization is complete

        // handy API: instance OVRSpatialAnchor.WhenCreatedAsync()
        if (await SpatialAnchor.WhenCreatedAsync())
        {
            var pose = new Pose(transform.position, transform.rotation);
            Sampleton.Log($"{nameof(SharedAnchor)}: Created! {pose.Brief()}");
        }
        else
        {
            Sampleton.LogError($"{nameof(SharedAnchor)}: FAILED TO CREATE!\n- destroying instance..");
            Destroy(gameObject);
            return;
        }

        var uuid = Uuid;

        Sampleton.Log($"+ {uuid}");

        if (!Source.IsSet)
            Source = AnchorSource.New(uuid);

        // handy API: instance OVRSpatialAnchor.WhenLocalizedAsync()
        if (await SpatialAnchor.WhenLocalizedAsync())
        {
            var pose = new Pose(transform.position, transform.rotation);
            // note: this pose can differ from the one reported earlier when this is not a brand-new anchor.
            Sampleton.Log($"+ {uuid.Brief()} Localized! {pose.Brief()}");
        }
        else
        {
            Sampleton.LogError($"- {uuid.Brief()} Localization FAILED!");
            Destroy(gameObject);
            return;
        }

        s_Instances[uuid] = this;

        if (PhotonAnchorManager.CheckIsAlignmentAnchor(this, out var offsetOnHost))
        {
            Sampleton.Log($"+ {uuid.Brief()} IsAlignmentAnchor");
            Alignment.SetMRUKOrigin(this, offsetOnHost);
        }

        if (canvas)
        {
            UpdateUI();
            canvas.enabled = true;
        }
    }

    void OnDestroy()
    {
        var uuid = Uuid;

        _ = s_Instances.Remove(uuid);

        if (!IsSaved && uuid != Guid.Empty)
        {
            Sampleton.Log(
                $"WARN: anchor {uuid} was destroyed but never saved!\n  (It *may* be gone forever.)",
                LogType.Warning
            );
        }

        if (Alignment.MRUKWorldLockAnchor == this) // && !Sampleton.IsChangingScenes
        {
            Sampleton.Log(
                $"WARN: destroyed anchor {uuid.Brief()} was previously designated as an alignment anchor.",
                LogType.Warning
            );
            Alignment.ResetMRUKOrigin();
        }
    }


    //
    // impl. methods

    bool IsReadyToShare(out IReadOnlyCollection<ulong> userIds, bool printReason = true)
    {
        userIds = null;

        if (!SpatialAnchor)
        {
            if (printReason)
                Sampleton.LogError("- Can't share - no associated spatial anchor");
            return false;
        }

        if (!PhotonNetwork.IsConnected)
        {
            if (printReason)
                Sampleton.LogError("- Can't share - not connected to the Photon network");
            return false;
        }

        userIds = PhotonAnchorManager.RoomUserIds;
        if (userIds is null || userIds.Count == 0)
        {
            if (printReason)
                Sampleton.LogError("- Can't share - no users to share with or can't get user list from Photon");
            return false;
        }

        return true;
    }

    async void ShareToUsers(IReadOnlyCollection<ulong> userIds)
    {
        Assert.IsNotNull(SpatialAnchor, "SpatialAnchor != null");

        // essentially need to ask the OVR backend if each of these IDs is valid for us to use for sharing:
        var spaceUsers = new List<OVRSpaceUser>(userIds.Count);
        foreach (ulong id in userIds)
        {
            // (maybe a) KEY API CALL: static OVRSpaceUser.TryCreate(platformUserId, out spaceUser)
            if (OVRSpaceUser.TryCreate(id, out var user))
            {
                Sampleton.Log($"  + anchor {Uuid.Brief()} --> user {id}");
                spaceUsers.Add(user);

                // p.s. - it's a maybe because we are moving toward promoting group sharing over the "vanilla" user-based sharing.
                // iff you are using the user-based sharing in your app, *then* this becomes a KEY API call.
            }
            else
            {
                Sampleton.LogError($"  - FAILED to create OVRSpaceUser for {id}");
            }
        }

        if (spaceUsers.Count == 0)
        {
            Sampleton.LogError($"{nameof(ShareToUsers)} FAILED: No valid users to share with!");
            return;
        }

        // KEY API CALL: instance OVRSpatialAnchor.ShareAsync(users)
        var shareResult = await SpatialAnchor.ShareAsync(spaceUsers);

        string loggedResult = shareResult.ForLogging();

        ShareSucceeded = shareResult.IsSuccess();
        if (ShareSucceeded)
        {
            IsSaved = true; // shared anchors are implicitly cloud-saved
            Sampleton.Log($"  + Share Result: {loggedResult}");
            // In this context of the sample, we need to transmit this now-shared anchor's id by some other means to our
            // peers. The "means" that we presently choose to demo with is Photon Realtime + PUN.
            PhotonAnchorManager.PublishAnchorToUsers(Uuid, userIds);
            return;
        }

        Sampleton.LogError($"  - Share FAILED: {loggedResult}");
    }

}
