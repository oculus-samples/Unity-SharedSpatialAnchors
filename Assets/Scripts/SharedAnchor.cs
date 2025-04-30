// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
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
public class SharedAnchor : MonoBehaviour
{
    //
    // Static interface

    public static IReadOnlyCollection<SharedAnchor> All => s_Instances.Values;
    public static IEnumerable<SharedAnchor> AllMine => s_Instances.Values.Where(a => a.Source.IsMine);

    public static bool Find(Guid uuid, out SharedAnchor anchor)
        => s_Instances.TryGetValue(uuid, out anchor);

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

        Sampleton.Log($"+ sharing {anchors.Count} anchors...");

        // KEY API CALL: static OVRSpatialAnchor.ShareAsync(anchors, users)
        var shareResult = await OVRSpatialAnchor.ShareAsync(anchors.Select(a => a.m_SpatialAnchor), new[] { spaceUser });

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
        var shareResult = await OVRSpatialAnchor.ShareAsync(anchors.Select(a => a.m_SpatialAnchor), spaceUsers);

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

    public Guid Uuid => m_SpatialAnchor ? m_SpatialAnchor.Uuid : Guid.Empty;

    public bool IsSaved
    {
        get => LocallySaved.AnchorIsRemembered(Uuid);
        set
        {
            if (value)
                LocallySaved.RememberAnchor(Uuid, Source.IsMine);
            else
                LocallySaved.ForgetAnchor(Uuid);
            if (m_SaveIcon)
                m_SaveIcon.color = value ? SampleColors.Green : SampleColors.Gray;
        }
    }

    public bool ShareSucceeded
    {
        get => m_ShareSucceeded;
        set
        {
            m_ShareSucceeded = value;
            if (m_ShareIcon)
                m_ShareIcon.color = value ? SampleColors.Green : SampleColors.Red;
        }
    }
    bool m_ShareSucceeded;

    public bool IsSelectedForAlign
    {
        get => m_IsSelectedForAlign;
        set
        {
            m_IsSelectedForAlign = value;
            if (m_AlignIcon)
                m_AlignIcon.color = value ? SampleColors.Green : SampleColors.Gray;
        }
    }
    bool m_IsSelectedForAlign;

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
        }
    }


    public async void OnSaveLocalButtonPressed()
    {
        if (Uuid == Guid.Empty)
        {
            Sampleton.LogError($"{nameof(OnSaveLocalButtonPressed)}: Not saving! null, missing, or invalid OVRSpatialAnchor reference");
            return;
        }

        if (!LocallySaved.AnchorsCanGrow)
        {
            LocallySaved.LogCannotGrow(nameof(OnSaveLocalButtonPressed));
            return;
        }

        Sampleton.Log($"{nameof(OnSaveLocalButtonPressed)}: {Uuid}");

        // API call: instance OVRSpatialAnchor.SaveAnchorAsync()
        var saveResult = await m_SpatialAnchor.SaveAnchorAsync();
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

        if (IsSaved || uuid == Guid.Empty)
            return;

        Sampleton.Log(
            "- WARNING: anchor was hidden (destroyed) but never saved!\n  (It *may* be gone forever.)",
            LogType.Warning
        );
    }

    public async void OnEraseButtonPressed()
    {
        if (!m_SpatialAnchor)
        {
            Sampleton.Log($"{nameof(OVRSpatialAnchor.EraseAnchorAsync)}: NO-OP (anchor already destroyed)");
            return;
        }

        var uuid = Uuid;

        Sampleton.Log($"{nameof(OVRSpatialAnchor.EraseAnchorAsync)}: {uuid.Brief()}");

        var anchor = m_SpatialAnchor;
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

        Destroy(gameObject);
    }

    public void OnShareButtonPressed()
    {
        ShareWithRoom();
    }

    public void OnAlignButtonPressed()
    {
        Sampleton.Log("OnAlignButtonPressed: aligning to anchor");

        AlignPlayer.Instance.SetAlignmentAnchor(this);
    }


    public void ShareWithRoom()
    {
        Sampleton.Log($"{nameof(ShareWithRoom)}: anchor {Uuid.Brief()}");

        // userIds is an array of scoped Oculus User IDs of peers in this Photon Room:
        if (!IsReadyToShare(out var userIds, printReason: true))
            return;

        // (note: Photon won't have any valid IDs if your app hasn't gone through the DUC & Horizon store entitlement steps yet!)

        ShareToUsers(userIds);
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
            canvas.gameObject.SetActive(false); // don't render controls until creation & localization is complete

        // handy API: instance OVRSpatialAnchor.WhenCreatedAsync()
        if (await m_SpatialAnchor.WhenCreatedAsync())
        {
            Sampleton.Log($"{nameof(SharedAnchor)}: Created!");
        }
        else
        {
            Sampleton.LogError($"{nameof(SharedAnchor)}: FAILED TO CREATE!\n- destroying instance..");
            Destroy(gameObject);
            return;
        }

        var uuid = Uuid;

        Sampleton.Log($"+ Uuid: {uuid}");

        s_Instances[uuid] = this;

        if (!m_Source.IsSet)
            m_Source = AnchorSource.New(uuid);

        gameObject.name =
            m_Source.Origin == AnchorSource.Type.FromSpaceUserShare ? $"anchor:{uuid:N}-SHARED"
                                                                    : $"anchor:{uuid:N}";
        m_AnchorName.text = $"{uuid}\n({nameof(Source)}: {m_Source})";

        // handy API: instance OVRSpatialAnchor.WhenLocalizedAsync()
        if (await m_SpatialAnchor.WhenLocalizedAsync())
        {
            Sampleton.Log($"+ Localized! ({uuid.Brief()})");
        }
        else
        {
            Sampleton.LogError($"- Localization FAILED! ({uuid.Brief()})");
            Destroy(gameObject);
            return;
        }

        if (canvas)
            canvas.gameObject.SetActive(true);
    }

    void OnDestroy()
    {
        _ = s_Instances.Remove(Uuid);
    }


    //
    // impl. methods

    bool IsReadyToShare(out IReadOnlyCollection<ulong> userIds, bool printReason = true)
    {
        userIds = null;

        if (!m_SpatialAnchor)
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
        Assert.IsNotNull(m_SpatialAnchor, "m_SpatialAnchor != null");

        // essentially need to ask the OVR backend if each of these IDs is valid for us to use for sharing:
        var spaceUsers = new List<OVRSpaceUser>(userIds.Count);
        foreach (ulong id in userIds)
        {
            // (maybe a) KEY API CALL: static OVRSpaceUser.TryCreate(platformUserId, out spaceUser)
            if (OVRSpaceUser.TryCreate(id, out var user))
            {
                Sampleton.Log($"    + anchor {Uuid.Brief()} --> user {id}");
                spaceUsers.Add(user);

                // p.s. - it's a maybe because we are moving toward promoting group sharing over the "vanilla" user-based sharing.
                // iff you are using the user-based sharing in your app, *then* this becomes a KEY API call.
            }
            else
            {
                Sampleton.LogError($"    - FAILED to create OVRSpaceUser for {id}");
            }
        }

        if (spaceUsers.Count == 0)
        {
            Sampleton.LogError($"{nameof(ShareToUsers)} FAILED: No valid users to share with!");
            return;
        }

        // KEY API CALL: instance OVRSpatialAnchor.ShareAsync(users)
        var shareResult = await m_SpatialAnchor.ShareAsync(spaceUsers);

        string loggedResult = shareResult.ForLogging();

        ShareSucceeded = shareResult.IsSuccess();
        if (ShareSucceeded)
        {
            Sampleton.Log($"+ Shared Spatial Anchor: {loggedResult}");
            // In this context of the sample, we need to transmit this now-shared anchor's id by some other means to our
            // peers. The "means" that we presently choose to demo with is Photon Realtime + PUN.
            PhotonAnchorManager.PublishAnchorToUsers(Uuid, userIds);
            return;
        }

        Sampleton.LogError($"- Sharing Spatial Anchor FAILED: {loggedResult}");
    }

}
