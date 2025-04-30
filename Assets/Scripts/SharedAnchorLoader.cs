// Copyright (c) Meta Platforms, Inc. and affiliates.
// This code is licensed under the MIT license (see LICENSE for details).

using Photon.Pun;

using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

// using static OVRSpatialAnchor; // This would be nice, but is commented out to help highlight where OVR calls are made in the sample.

using Sampleton = SampleController; // only transitional

/// <remarks>
///   You can locate key API calls throughout the sample code by searching for "KEY API CALL" (use CTRL+F in most IDEs).
/// </remarks>
public static class SharedAnchorLoader
{
    //
    // Public Interface

    public static readonly HashSet<Guid> LoadedAnchorIds = new();


    public static void AddPersistedAnchor(Guid uuid, bool isMine)
    {
        Assert.AreNotEqual(Guid.Empty, uuid, "anchor.Uuid != null");

        if (!s_LocalAnchorIdsParsed.TryAdd(uuid, isMine))
            return;

        s_LocalAnchorIdsInOrder.Add(uuid.Serialize(isMine ? k_OwnedMarker : ""));
        s_Dirty = true;
    }

    public static void RemovePersistedAnchor(Guid uuid)
    {
        Assert.AreNotEqual(Guid.Empty, uuid, "anchor.Uuid != null");

        if (!s_LocalAnchorIdsParsed.Remove(uuid))
            return;

        s_LocalAnchorIdsInOrder.RemoveAll(str => str.EndsWith(uuid.Serialize()));
        s_Dirty = true;
    }

    public static bool IsPersisted(Guid uuid)
    {
        DeserializePersistentAnchors(force: false);
        return s_LocalAnchorIdsParsed.ContainsKey(uuid);
    }

    public static bool IsMine(Guid uuid)
    {
        DeserializePersistentAnchors(force: false);
        if (s_LocalAnchorIdsParsed.TryGetValue(uuid, out bool isMine))
            return isMine;
        return SharedAnchor.Find(uuid, out var anchor) && anchor.Source.IsMine;
    }


    public static void LoadSavedAnchors()
    {
        Sampleton.Log($"{nameof(LoadSavedAnchors)}:");

        var persistedAnchors = DeserializePersistentAnchors();
        if (persistedAnchors.Count == 0)
        {
            Sampleton.Log($"- NO-OP: there are no anchors saved to this build/device.");
            return;
        }

        Sampleton.Log($"+ Found {persistedAnchors.Count} anchor UUIDs saved to this build/device...");

        RetrieveAnchorsFromLocalThenCloud(persistedAnchors);
    }

    public static void ReloadSharedAnchors()
    {
        var sharedAnchors = PhotonAnchorManager.PublishedAnchors;

        if (sharedAnchors.Count == 0)
        {
            Sampleton.Log($"{nameof(ReloadSharedAnchors)}: NO-OP: no anchors are published to this room.", LogType.Warning);
            return;
        }

        Sampleton.Log($"{nameof(ReloadSharedAnchors)}: {sharedAnchors.Count} anchors");

        RetrieveAnchorsFromCloud(sharedAnchors);
    }

    public static void LoadAnchorsFromRemote(HashSet<Guid> uuids)
    {
        // Load anchors received from remote participant
        Sampleton.Log($"{nameof(LoadAnchorsFromRemote)} uuids count: {uuids.Count}");

        if (uuids.Count == 0)
        {
            Sampleton.Log($"{nameof(LoadAnchorsFromRemote)}: no new anchors to load");
            return;
        }

        RetrieveAnchorsFromCloud(uuids);
    }

    //
    // Fields

    static readonly Dictionary<Guid, bool> s_LocalAnchorIdsParsed = new();
    static readonly List<string> s_LocalAnchorIdsInOrder = new();

    static bool s_Dirty;        // persistent data needs save.
    static bool s_Stale = true; // persistent data needs load.

    const int k_MaxLoadRetries = 3;
    const string k_UuidSeparator = "\n";
    const string k_OwnedMarker = "@";

    static string PersistedUuidsKey
    {
        get
        {
            if (s_PersistedUuidsKey == "saved_uuids")
                s_PersistedUuidsKey += $"[{Application.buildGUID}]";
            return s_PersistedUuidsKey;
        }
    }
    static string s_PersistedUuidsKey = "saved_uuids";

    //
    // impl.

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RuntimeInit()
    {
        DeserializePersistentAnchors(force: true);

        Application.focusChanged += hasFocus =>
        {
            if (hasFocus)
                _ = DeserializePersistentAnchors(force: true);
            else
                _ = SerializePersistentAnchors(force: true);
        };
    }


    static ICollection<Guid> DeserializePersistentAnchors(bool force = false)
    {
        if (!s_Stale && !force)
            return s_LocalAnchorIdsParsed.Keys;

        Sampleton.Log(nameof(DeserializePersistentAnchors));

        s_Stale = false;

        s_LocalAnchorIdsParsed.Clear();
        s_LocalAnchorIdsInOrder.Clear();

        var rawAnchorIdList = PlayerPrefs.GetString(PersistedUuidsKey);
        if (string.IsNullOrEmpty(rawAnchorIdList))
            return s_LocalAnchorIdsParsed.Keys;

        foreach (var rawGuid in rawAnchorIdList.Split(k_UuidSeparator))
        {
            bool isMine = rawGuid.StartsWith(k_OwnedMarker);
            bool ok = Guid.TryParse(isMine ? rawGuid.Substring(k_OwnedMarker.Length) : rawGuid, out var parsedUuid);

            Assert.IsTrue(ok, $"Guid.Parse(\"{rawGuid}\") failed.");

            if (s_LocalAnchorIdsParsed.TryAdd(parsedUuid, isMine))
                s_LocalAnchorIdsInOrder.Add(rawGuid);
        }

        return s_LocalAnchorIdsParsed.Keys;
    }

    static bool SerializePersistentAnchors(bool force = false)
    {
        if (!s_Dirty && !force)
            return false;

        Sampleton.Log(nameof(SerializePersistentAnchors));

        if (s_LocalAnchorIdsInOrder.Count == 0)
            PlayerPrefs.DeleteKey(PersistedUuidsKey);
        else
            PlayerPrefs.SetString(PersistedUuidsKey, string.Join(k_UuidSeparator, s_LocalAnchorIdsInOrder));

        s_Dirty = false;
        s_Stale = false;
        return true;
    }


    static async void RetrieveAnchorsFromLocalThenCloud(ICollection<Guid> anchorIds)
    {
        Sampleton.Log($"{nameof(OVRSpatialAnchor.LoadUnboundAnchorsAsync)} w/ {anchorIds.Count} anchor uuids:");

        // KEY API CALL: static OVRSpatialAnchor.LoadUnboundAnchorsAsync(uuids, unboundAnchors)
        var loadResult = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(anchorIds, new List<OVRSpatialAnchor.UnboundAnchor>());
        // notice: this API does not contain the word "Shared" = loads only locally-stored anchors

        string loggedResult = loadResult.Status.ForLogging();

        if (!loadResult.TryGetValue(out var unboundAnchors))
        {
            Sampleton.Log($"- Load Attempt #1: {loggedResult}\n  - (local; next attempt = cloud)");
            RetrieveAnchorsFromCloud(anchorIds);
            return;
        }

        Sampleton.Log($"+ Load Success! {loggedResult}\n+ {unboundAnchors.Count} unbound spatial anchors");

        if (unboundAnchors.Count > 0)
            BindAnchorsAsync(unboundAnchors);

        if (unboundAnchors.Count >= anchorIds.Count)
            return;

        Sampleton.Log(
            $"*** Not all requested anchors could load!\n" +
            $"  + (retrying {anchorIds.Count - unboundAnchors.Count}/{anchorIds.Count} uuids)"
        );

        foreach (var uuid in unboundAnchors.Select(unb => unb.Uuid))
            anchorIds.Remove(uuid);

        RetrieveAnchorsFromCloud(anchorIds);
    }

    static async void RetrieveAnchorsFromCloud(ICollection<Guid> anchorIds, int retry = 0)
    {
        if (retry == 0)
        {
            Sampleton.Log($"{nameof(OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync)} w/ {anchorIds.Count} anchor uuids:");
            foreach (var uuid in anchorIds)
                Sampleton.Log($"  -> {uuid.Brief()}");
        }

        Retry:

        // KEY API CALL: static OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync(uuids, unboundAnchors)
        var loadResult = await OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync(anchorIds, new List<OVRSpatialAnchor.UnboundAnchor>());
        // notice: this API contains the word "Shared" = will always query the cloud for the given anchor IDs
        // (unless all anchors are already found on the local device, in which case the cloud will be skipped)

        string loggedResult = loadResult.Status.ForLogging();

        if (!loadResult.TryGetValue(out var unboundAnchors))
        {
            if (++retry < k_MaxLoadRetries)
            {
                Sampleton.Log($"- Load Attempt #{retry}: {loggedResult}");
                goto Retry;
            }
            Sampleton.LogError($"- Load FAILED: {loggedResult}");
            return;
        }

        Sampleton.Log($"+ Load Success! {loggedResult}\n+ {unboundAnchors.Count} unbound spatial anchors");

        if (unboundAnchors.Count > 0)
            BindAnchorsAsync(unboundAnchors);
    }


    static async void BindAnchorsAsync(List<OVRSpatialAnchor.UnboundAnchor> unboundAnchors)
    {
        bool hasPlatformId = PhotonNetwork.LocalPlayer.TryGetPlatformID(out ulong platId);

        var areCreated = new OVRTask<bool>[unboundAnchors.Count];
        int i = 0;
        while (i < unboundAnchors.Count)
        {
            var uuid = unboundAnchors[i].Uuid;
            bool wasSaved = s_LocalAnchorIdsParsed.TryGetValue(uuid, out bool isMine);

            var spatialAnchor = InstantiateAnchorForBinding(wasSaved, out var sharedAnchor);
            if (!spatialAnchor)
            {
                Sampleton.LogError($"  - {nameof(InstantiateAnchorForBinding)} FAILED!");
                return; // because we most likely can't be instantiating anything anymore
            }

            if (sharedAnchor)
            {
                if (hasPlatformId && isMine)
                    sharedAnchor.Source = AnchorSource.FromSave(uuid, platId);
                else
                    sharedAnchor.Source = AnchorSource.FromSpaceUserShare(uuid);
            }

            try
            {
                // KEY API CALL: instance OVRSpatialAnchor.UnboundAnchor.BindTo(spatialAnchor)
                unboundAnchors[i].BindTo(spatialAnchor);
                // (OVRSpatialAnchors cannot be successfully loaded without following this step!)
            }
            catch (Exception e)
            {
                Object.Destroy(spatialAnchor.gameObject);
                Sampleton.Log(
                    $"  - Binding {uuid.Brief()} FAILED: {e.GetType().Name} (see logcat)",
                    LogType.Exception,
                    LogOption.None
                );
                Debug.LogException(e);
            }

            Sampleton.Log($"  + {uuid.Brief()} bound! Localizing...");

            areCreated[i++] = spatialAnchor.WhenCreatedAsync();
        }

        // wait for all creations to finish simultaneously:
        var creationResults = await OVRTask.WhenAll(areCreated);

        while (i-- > 0)
        {
            var uuid = unboundAnchors[i].Uuid;
            if (!creationResults[i])
            {
                Sampleton.LogError($"  - creation FAILED for {uuid.Brief()}");
                continue;
            }

            LoadedAnchorIds.Add(uuid);

            Sampleton.Log($"  + spatial anchor created and bound to {uuid.Brief()}");
        }
    }

    static OVRSpatialAnchor InstantiateAnchorForBinding(bool wasSaved, out SharedAnchor sharedAnchor)
    {
        Assert.IsNotNull(SampleController.Instance, "SampleController.Instance");
        Assert.IsNotNull(SampleController.Instance.anchorPrefab, "SampleController.anchorPrefab");

        var spatialAnchor = Object.Instantiate(SampleController.Instance.anchorPrefab);

        // Note: There are some implicit API calls when instantiating an OVRSpatialAnchor,
        // however they are not unique to *shared* spatial anchors.
        // The only relevant detail you might like to know is that if your code fails to bind an UnboundAnchor to an
        // OVRSpatialAnchor within the same frame it was instantiated, the newborn anchor will initialize itself as an
        // entirely new anchor, with a new Uuid, and it will likely be positioned at the world origin
        // (unless your code moved it).

        if (spatialAnchor.TryGetComponent(out sharedAnchor))
        {
            sharedAnchor.IsSaved = wasSaved;
        }

        return spatialAnchor;
    }

} // end static class SharedAnchorLoader
