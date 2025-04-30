// Copyright (c) Meta Platforms, Inc. and affiliates.
// This code is licensed under the MIT license (see LICENSE for details).

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

        var uuids = new List<Guid>(anchors.Count);
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

        PhotonAnchorManager.PublishAnchorsToRoom(uuids);
    }

    public static async void ShareAllMineTo(ICollection<ulong> users, bool reshareOnly = false)
    {
        Sampleton.Log($"{nameof(ShareAllMineTo)}:");

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

        Sampleton.Log($"+ sharing {anchors.Count} anchors to {spaceUsers.Count} uids..");

        // KEY API CALL: static OVRSpatialAnchor.ShareAsync(anchors, users)
        var shareResult = await OVRSpatialAnchor.ShareAsync(anchors.Select(a => a.m_SpatialAnchor), spaceUsers);

        string loggedResult = shareResult.ForLogging();

        bool success = shareResult.IsSuccess();

        var uuids = new List<Guid>(anchors.Count);
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

        PhotonAnchorManager.PublishAnchorsToRoom(AllMine.Select(a => a.Uuid));
    }


    //
    // Public Properties & UnityEvent Callbacks (for Buttons)

    public Guid Uuid
    {
        get
        {
            if (!m_CachedUuid.HasValue)
            {
                if (!m_SpatialAnchor) // yikes
                    return Guid.Empty;
                m_CachedUuid = m_SpatialAnchor.Uuid;
            }
            return m_CachedUuid.Value;
        }
    }
    Guid? m_CachedUuid; // in case we need it for cleanup after the OVRSpatialAnchor has already met its demise.

    public bool IsSaved
    {
        get => SharedAnchorLoader.IsPersisted(Uuid);
        set
        {
            if (m_SaveIcon)
                m_SaveIcon.color = value ? k_ColorGreen : k_ColorGray;
        }
    }

    public bool ShareSucceeded
    {
        get => m_ShareSucceeded;
        set
        {
            m_ShareSucceeded = value;
            if (m_ShareIcon)
                m_ShareIcon.color = value ? k_ColorGreen : k_ColorRed;
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
                m_AlignIcon.color = value ? k_ColorGreen : k_ColorGray;
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

        SharedAnchorLoader.AddPersistedAnchor(Uuid, Source.IsMine);
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

        Sampleton.Log($"{nameof(OVRSpatialAnchor.EraseAnchorAsync)}:");

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

        SharedAnchorLoader.RemovePersistedAnchor(Uuid);

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
        if (!IsReadyToShare(printReason: true))
            return;

        Sampleton.Log($"{nameof(ShareWithRoom)}: anchor {Uuid.Brief()}");

        // get the scoped Oculus User IDs of our peers from Photon:
        var userIds = PhotonAnchorManager.GetRoomUserIds();
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

    static readonly Color k_ColorGray = new Color32(0x8B, 0x8C, 0x8E, 0xFF);
    static readonly Color k_ColorGreen = new Color32(0x5A, 0xCA, 0x25, 0xFF);
    static readonly Color k_ColorRed = new Color32(0xDD, 0x25, 0x35, 0xFF);

    const int k_MaxAsyncAttempts = 3;


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
        if (!m_SpatialAnchor && !TryGetComponent(out m_SpatialAnchor))
        {
            Debug.LogError($"<{nameof(SharedAnchor)}> on '{name}' has done the impossible...", this);
            Destroy(this);
            return;
        }

        var canvas = GetComponentInChildren<Canvas>();
        if (canvas)
            canvas.gameObject.SetActive(false); // don't render controls until creation & localization is complete

        // handy API: instance OVRSpatialAnchor.WhenCreatedAsync()
        if (await m_SpatialAnchor.WhenCreatedAsync())
        {
            Sampleton.Log($"{nameof(SharedAnchor)}: Created!\n+ {Uuid}");
        }
        else
        {
            Sampleton.LogError($"{nameof(SharedAnchor)}: FAILED TO CREATE!\n- destroying instance..");
            Destroy(gameObject);
            return;
        }

        var uuid = Uuid;

        s_Instances[uuid] = this;

        gameObject.name = $"anchor:{uuid:N}";
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

        if (m_Source.IsSet)
            return;

        if (PhotonNetwork.LocalPlayer.TryGetPlatformID(out ulong userId))
            m_Source = AnchorSource.New(userId, uuid);
    }

    void OnDestroy()
    {
        _ = s_Instances.Remove(Uuid);
        SharedAnchorLoader.LoadedAnchorIds.Remove(Uuid);
    }


    //
    // impl. methods

    bool IsReadyToShare(bool printReason = true)
    {
        if (!m_SpatialAnchor)
        {
            if (printReason)
                Sampleton.Log("- Can't share - no associated spatial anchor");
            return false;
        }

        if (!Photon.Pun.PhotonNetwork.IsConnected)
        {
            if (printReason)
                Sampleton.Log("- Can't share - not connected to the Photon network");
            return false;
        }

        var userIds = PhotonAnchorManager.GetRoomUserIds();
        if (userIds.Count == 0)
        {
            if (printReason)
                Sampleton.Log("- Can't share - no users to share with or can't get user list from Photon");
            return false;
        }

        return true;
    }

    [Obsolete("You should no longer need to save an anchor before sharing it.")]
    async void SaveThenShare()
    {
        int attempt = 0;

        Retry:

        // API call: instance OVRSpatialAnchor.SaveAnchorAsync()
        var saveResult = await m_SpatialAnchor.SaveAnchorAsync();

        string loggedResult = saveResult.Status.ForLogging();

        if (saveResult.Success)
        {
            Sampleton.Log($"+ Saved Spatial Anchor: {loggedResult}");
            if (IsReadyToShare(printReason: true))
            {
                // get the scoped Oculus User IDs of our peers from Photon:
                var userIds = PhotonAnchorManager.GetRoomUserIds();
                // (note: Photon won't have any valid IDs if your app hasn't gone through the DUC & Horizon store entitlement steps yet!)
                ShareToUsers(userIds);
            }
            return;
        }

        Sampleton.LogError($"- Saving Spatial Anchor FAILED: {loggedResult}");

        if (++attempt < k_MaxAsyncAttempts)
        {
            Sampleton.Log($"- Retry {attempt}/{k_MaxAsyncAttempts} ...");
            goto Retry;
        }

        Sampleton.LogError($"{nameof(SaveThenShare)} FAILED: Max save attempts exceeded");
    }

    async void ShareToUsers(ICollection<ulong> userIds)
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
            // In this context of the sample, we need to transmit this now-shared anchor's id by some other means to our
            // peers. The "means" that we presently choose to demo with is Photon Realtime + PUN.
            PhotonAnchorManager.PublishAnchorToRoom(Uuid);
            Sampleton.Log($"+ Shared Spatial Anchor: {loggedResult}");
            return;
        }

        Sampleton.LogError($"- Sharing Spatial Anchor FAILED: {loggedResult}");
    }

}
