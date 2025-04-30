// Copyright (c) Meta Platforms, Inc. and affiliates.

using Photon.Pun;

using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.Assertions;

// using static OVRSpatialAnchor; // This would be nice, but is commented out to help highlight where OVR calls are made in the sample.

using Object = UnityEngine.Object;
using Sampleton = SampleController; // only transitional

/// <remarks>
///   You can locate key API calls throughout the sample code by searching for "KEY API CALL" (use CTRL+F in most IDEs).
/// </remarks>
public static class SharedAnchorLoader
{
    //
    // Public Interface

    public static void LoadSavedAnchors()
    {
        var persistedAnchors = LocallySaved.Anchors.ToHashSet();
        if (persistedAnchors.Count == 0)
        {
            Sampleton.Log($"{nameof(LoadSavedAnchors)}: NO-OP: there are no anchors saved to this build/device.");
            return;
        }

        Sampleton.Log($"{nameof(LoadSavedAnchors)}: {persistedAnchors.Count} saved anchors:");

        foreach (var uuid in persistedAnchors)
        {
            if (LocallySaved.AnchorIsMine(uuid))
                Sampleton.Log($"  + {uuid.Brief()} (yours)");
            else
                Sampleton.Log($"  + {uuid.Brief()}");
        }

        RetrieveAnchorsFromLocalThenCloud(persistedAnchors);
    }

    public static void ReloadSharedAnchors()
    {
        var sharedAnchors = PhotonAnchorManager.AllPublishedAnchors.ToHashSet();

        int nIgnored = sharedAnchors.Count;
        sharedAnchors.ExceptWith(LocallySaved.AnchorsIgnored);
        nIgnored = sharedAnchors.Count - nIgnored;

        if (sharedAnchors.Count == 0)
        {
            Sampleton.Log($"{nameof(ReloadSharedAnchors)}: NO-OP: no anchors are published to this room.", LogType.Warning);
            return;
        }

        Sampleton.Log($"{nameof(ReloadSharedAnchors)}: {sharedAnchors.Count} anchors");

        foreach (var uuid in sharedAnchors)
        {
            if (LocallySaved.AnchorIsMine(uuid))
                Sampleton.Log($"  + {uuid.Brief()} (yours)");
            else
                Sampleton.Log($"  + {uuid.Brief()}");
        }

        if (nIgnored > 0)
            Sampleton.Log($"  - (ignored {nIgnored} anchors)");

        RetrieveAnchorsFromCloud(sharedAnchors);
    }


    //
    // impl.

    static async void RetrieveAnchorsFromLocalThenCloud(ICollection<Guid> anchorIds)
    {
        Sampleton.Log($"--> {nameof(OVRSpatialAnchor.LoadUnboundAnchorsAsync)}:");

        // KEY API CALL: static OVRSpatialAnchor.LoadUnboundAnchorsAsync(uuids, unboundAnchors)
        var loadResult = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(anchorIds, new List<OVRSpatialAnchor.UnboundAnchor>());
        // notice: this API does not contain the word "Shared" = loads only locally-stored anchors

        string loggedResult = loadResult.Status.ForLogging();

        if (!loadResult.TryGetValue(out var unboundAnchors))
        {
            Sampleton.Log($"  - {loggedResult} (attempt to load from local)\n  + Checking shared...");
            RetrieveAnchorsFromCloud(anchorIds);
            return;
        }

        Sampleton.Log($"  + Load Success! {loggedResult}\n  + {unboundAnchors.Count} unbound spatial anchors");

        if (unboundAnchors.Count > 0)
            BindAnchorsAsync(unboundAnchors);

        if (unboundAnchors.Count >= anchorIds.Count)
            return;

        Sampleton.Log(
            $"  *** Not all requested anchors could load!\n" +
            $"  + (retrying {anchorIds.Count - unboundAnchors.Count}/{anchorIds.Count} uuids)"
        );

        foreach (var uuid in unboundAnchors.Select(unb => unb.Uuid))
            anchorIds.Remove(uuid);

        RetrieveAnchorsFromCloud(anchorIds);
    }

    static async void RetrieveAnchorsFromCloud(ICollection<Guid> anchorIds)
    {
        Sampleton.Log($"--> {nameof(OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync)}:");

        // KEY API CALL: static OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync(uuids, unboundAnchors)
        var loadResult = await OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync(anchorIds, new List<OVRSpatialAnchor.UnboundAnchor>());
        // notice: this API contains the word "Shared" = will always query the cloud for the given anchor IDs
        // (unless all anchors are already found on the local device, in which case the cloud will be skipped)

        string loggedResult = loadResult.Status.ForLogging();

        if (!loadResult.TryGetValue(out var unboundAnchors))
        {
            Sampleton.LogError($"  - Load FAILED: {loggedResult}");
            if (loadResult.Status.RetryingMightSucceed())
            {
                Sampleton.Log(
                    $"  - Hint: This result code indicates retrying after some delay, or after moving around your physical space some more, <i>might</i> succeed.",
                    LogType.Warning
                );
            }
            return;
        }

        Sampleton.Log($"  + Load Success! {loggedResult}\n+ {unboundAnchors.Count} unbound spatial anchors");

        if (unboundAnchors.Count > 0)
            BindAnchorsAsync(unboundAnchors);
    }


    static async void BindAnchorsAsync(List<OVRSpatialAnchor.UnboundAnchor> unboundAnchors)
    {
        var areCreated = new OVRTask<bool>[unboundAnchors.Count];
        int i = 0;
        while (i < unboundAnchors.Count)
        {
            var uuid = unboundAnchors[i].Uuid;
            bool wasSaved = LocallySaved.AnchorIsRemembered(uuid, out bool isMine);

            var spatialAnchor = InstantiateAnchorForBinding(wasSaved, out var sharedAnchor);
            if (!spatialAnchor)
            {
                Sampleton.LogError($"  - {nameof(InstantiateAnchorForBinding)} FAILED!");
                return; // because we most likely can't be instantiating anything anymore
            }

            if (sharedAnchor)
            {
                sharedAnchor.Source =
                    wasSaved ? AnchorSource.FromSave(uuid, isMine: isMine)
                             : AnchorSource.FromSpaceUserShare(uuid, isMine: isMine);
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
