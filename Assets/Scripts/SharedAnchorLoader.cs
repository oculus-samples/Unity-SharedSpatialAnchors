// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;

// using static OVRSpatialAnchor; // This would be nice, but is commented out to help highlight where OVR calls are made in the sample.

using Object = UnityEngine.Object;
using Sampleton = SampleController; // only transitional

/// <summary>
///   A static interface for loading locally-saved or shared spatial anchors for the local client,
///   in the "SharedSpatialAnchors" sample scene only.
/// </summary>
/// <remarks>
///   You can locate key API calls throughout the sample code by searching for "KEY API CALL" (use CTRL+F in most IDEs).
/// </remarks>
[MetaCodeSample("SharedSpatialAnchors")]
public static class SharedAnchorLoader
{
    //
    // Public Interface

    // note: the overloads with Task<int> out params are provided for cases where your code needs to wait
    //       for anchors to finish loading before continuing.  The int results indicate how many anchors
    //       were loaded; in most cases, a value of 0 means something went wrong.

    public static void LoadSavedAnchors()
    {
        LoadSavedAnchors(out _);
    }

    public static void LoadSavedAnchors(out Task<int> completion)
    {
        var persistedAnchors = LocallySaved.Anchors.ToHashSet();
        if (persistedAnchors.Count == 0)
        {
            Sampleton.Log($"{nameof(LoadSavedAnchors)}: NO-OP: there are no anchors saved to this build/device.");
            completion = Task.FromResult(0);
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

        completion = RetrieveAnchorsFromLocalThenCloud(persistedAnchors);
    }

    public static void ReloadSharedAnchors()
    {
        ReloadSharedAnchors(out _);
    }

    public static void ReloadSharedAnchors(out Task<int> completion)
    {
        var sharedAnchors = PhotonAnchorManager.AnchorsSharedWithMe.ToHashSet();
        LoadSharedAnchors(sharedAnchors, out completion);
    }

    public static void LoadSharedAnchors(IReadOnlyCollection<Guid> anchors)
    {
        LoadSharedAnchors(anchors, out _);
    }

    public static void LoadSharedAnchors(IReadOnlyCollection<Guid> anchors, out Task<int> completion)
    {
        if (anchors is not HashSet<Guid> sharedAnchors)
        {
            sharedAnchors = new HashSet<Guid>(anchors);
        }

        int nIgnored = sharedAnchors.Count;
        sharedAnchors.ExceptWith(LocallySaved.AnchorsIgnored);
        nIgnored = sharedAnchors.Count - nIgnored;

        if (sharedAnchors.Count == 0)
        {
            Sampleton.Log(
                $"{nameof(ReloadSharedAnchors)}: NO-OP:" +
                $" either no anchors are published to this room, or their owner(s) could not share them to you yet.",
                LogType.Warning
            );
            Sampleton.Log(
                $"- Note that if anchor owners are inactive, tasked-out, or doffed," +
                $" then they cannot re-share their anchors until they return to the app."
            );
            completion = Task.FromResult(0);
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

        completion = RetrieveAnchorsFromCloud(sharedAnchors);
    }


    //
    // impl.

    static async Task<int> RetrieveAnchorsFromLocalThenCloud(ICollection<Guid> anchorIds)
    {
        Sampleton.Log($"--> {nameof(OVRSpatialAnchor.LoadUnboundAnchorsAsync)}:");

        // KEY API CALL: static OVRSpatialAnchor.LoadUnboundAnchorsAsync(uuids, unboundAnchors)
        var loadResult = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(anchorIds, new List<OVRSpatialAnchor.UnboundAnchor>());
        // notice: this API does not contain the word "Shared" = loads only locally-stored anchors

        string loggedResult = loadResult.Status.ForLogging();

        if (!loadResult.TryGetValue(out var unboundAnchors))
        {
            Sampleton.Log($"  - {loggedResult} (attempt to load from local)\n  + Checking shared...");
            return await RetrieveAnchorsFromCloud(anchorIds);
        }

        Sampleton.Log($"  + Load Success! {loggedResult}\n  + {unboundAnchors.Count} unbound spatial anchors");

        int nBound = 0;
        if (unboundAnchors.Count > 0)
            nBound += await BindAnchorsAsync(unboundAnchors, fromCloud: false);

        if (unboundAnchors.Count >= anchorIds.Count)
            return nBound;

        Sampleton.Log(
            $"  *** Not all requested anchors could load!\n" +
            $"  + (retrying {anchorIds.Count - unboundAnchors.Count}/{anchorIds.Count} uuids)"
        );

        foreach (var uuid in unboundAnchors.Select(unb => unb.Uuid))
            anchorIds.Remove(uuid);

        return nBound + await RetrieveAnchorsFromCloud(anchorIds);
    }

    static async Task<int> RetrieveAnchorsFromCloud(ICollection<Guid> anchorIds)
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
            return 0;
        }

        Sampleton.Log($"  + Load Success! {loggedResult}\n+ {unboundAnchors.Count} unbound spatial anchors");

        if (unboundAnchors.Count == 0)
            return 0;

        int nBound = await BindAnchorsAsync(unboundAnchors, fromCloud: true);

        return nBound;
    }


    static async Task<int> BindAnchorsAsync(List<OVRSpatialAnchor.UnboundAnchor> unboundAnchors, bool fromCloud)
    {
        var areCreated = new OVRTask<bool>[unboundAnchors.Count];
        int i = 0;
        while (i < unboundAnchors.Count)
        {
            var uuid = unboundAnchors[i].Uuid;

            var spatialAnchor = InstantiateAnchorForBinding(uuid, fromCloud);
            if (!spatialAnchor)
            {
                Sampleton.LogError($"  - {nameof(InstantiateAnchorForBinding)} FAILED!");
                return 0; // because we most likely can't be instantiating anything anymore
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

        int nBound = 0;
        while (i-- > 0)
        {
            var uuid = unboundAnchors[i].Uuid;
            if (creationResults[i])
            {
                ++nBound;
                Sampleton.Log($"  + spatial anchor created and bound to {uuid.Brief()}");
            }
            else
            {
                Sampleton.LogError($"  - creation FAILED for {uuid.Brief()}");
                unboundAnchors.RemoveAt(i);
            }
        }

        return nBound;
    }

    static OVRSpatialAnchor InstantiateAnchorForBinding(Guid uuid, bool fromCloud)
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

        if (!spatialAnchor.TryGetComponent(out SharedAnchor sharedAnchor))
            return spatialAnchor;

        bool isMine = LocallySaved.AnchorIsMine(uuid);

        sharedAnchor.Source = fromCloud ? AnchorSource.FromSpaceUserShare(uuid, isMine)
                                        : AnchorSource.FromSave(uuid, isMine);

        return spatialAnchor;
    }

} // end static class SharedAnchorLoader
