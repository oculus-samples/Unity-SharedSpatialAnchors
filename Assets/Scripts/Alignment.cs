// Copyright (c) Meta Platforms, Inc. and affiliates.

using JetBrains.Annotations;

using Meta.XR.MRUtilityKit;
using Meta.XR.Samples;

using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Assertions;

using Object = UnityEngine.Object;
using Sampleton = SampleController;


/// <summary>
///     What is alignment and why do we need it in the context of colocated (multiplayer) mixed reality?
///     By default, two HMDs running the same Unity app in the same physical space will have completely different
///     virtual world-space origins (0,0,0) and orientations.  This will cause normal GameObjects (of the non-tracked,
///     non-anchored variety) to appear in different places on each client, even if they are properly networked.
///     <br/><br/>
///     "Alignment" is the process of synchronizing these virtual world-space origins such that they are anchored to
///     the same fixed physical point and orientation (relative to the real world) on each client.
///     We do this so that a GameObject at, say, position [1,2,3] and orientation (0°,0°,0°) will appear the same for
///     every peer in MR who has aligned their space to the same shared anchor.
/// </summary>
/// <remarks>
///     This class implements two methods of alignment, each used by a different scene: <br/>
///     (1) <see cref="SetOrigin(Component)"/> implements the "classic" method of alignment (used in Scene 2); <br/>
///     (2) <see cref="SetMRUKOrigin"/> implements the newer, MRUK-dependent method (used in Scene 1).
/// </remarks>
[MetaCodeSample("SharedSpatialAnchors")]
[MetaCodeSample("SharedSpatialAnchors-ColocationSessionGroups")]
public static class Alignment
{
    public static bool IsSet
        => s_Anchor;

    [CanBeNull]
    public static Component OriginAnchor
        => s_Anchor;

    [CanBeNull]
    public static Component MRUKWorldLockAnchor
        => s_UsingCustomMRUKWorldLock ? s_Anchor : null;

    [NotNull]
    public static Transform TrackingSpaceRoot
    {
        get
        {
            if (s_TrackingSpaceRoot)
                return s_TrackingSpaceRoot;

            var cameraRig = Object.FindFirstObjectByType<OVRCameraRig>();
            if (cameraRig && cameraRig.trackingSpace)
                return s_TrackingSpaceRoot = cameraRig.trackingSpace;

            throw new InvalidOperationException("No loaded OVRCameraRig found.");
        }
    }


    /// <summary>
    ///     (DEMO CODE) This is a stripped yet lean implementation for local origin alignment, intended to work on
    ///     spatial anchors ("local" = does not depend on any custom networking for colocated peers to get aligned).
    ///     A non-null <paramref name="newAnchor"/> that is—or is at least anchored by—an OVRSpatialAnchor, will become
    ///     centered at the world origin (0,0,0).  For all intents and purposes, <paramref name="newAnchor"/>
    ///     <em>becomes</em> the new origin.
    ///     <br/><br/>
    ///     This implementation leverages the insight that, given a world-space pose P, setting OVRCameraRig's
    ///     tracking space (<see cref="TrackingSpaceRoot"/>) transform to the <i>inverse</i> of P effectively
    ///     "redefines" the world-space origin to be P.  Using this, we can construct a scheme by which colocated peers
    ///     can all set the same shared spatial anchor to be their world-space origin; <b>this would achieve
    ///     alignment</b>.
    ///     <br/><br/>
    ///     Without proper precautions or adjustments, this will cause any non-anchored, non-tracked ("tracked" = e.g.
    ///     OVRCameraRig) GameObjects to appear to teleport, become tilted/rotated about the world origin, or
    ///     potentially disappear from view.  You can avoid this issue by either: <br/>
    ///     (1) not spawning any non-anchored objects until after all peers have completed alignment; <br/>
    ///     (2) adjusting previously-spawned objects as part of your alignment routine (this is implemented in
    ///     <see cref="SetOrigin(Component, IReadOnlyCollection{GameObject})"/>); or <br/>
    ///     (3) anchor ALL your GameObjects by childing them to OVRSpatialAnchors, MRUKAnchors (scene anchors), or
    ///     tracked GameObjects.
    /// </summary>
    /// <param name="newAnchor">
    ///     This anchor will become the world-space origin for the local client.
    ///     <br/><br/>
    ///     <b>To complete colocated alignment, all peers should call this function on the same anchor.</b> <br/>
    ///     Of course, this is intended to work with <em>shared</em> anchors.
    /// </param>
    /// <remark>
    ///     This overload is not invoked by this sample!
    /// </remark>
    public static void SetOrigin([CanBeNull] Component newAnchor)
    {
        // (These overloaded implementations are copied rather than composed for illustrative purposes.)

        if (!newAnchor)
        {
            s_Anchor = null;
            return;
        }

        var name = newAnchor.name.Length > 20 ? newAnchor.name.Remove(20) + "[..]"
                                              : newAnchor.name;

        Sampleton.Log($"{nameof(Alignment)}.{nameof(SetOrigin)}({name})");

        // 1.  Inputs = references to the scene's OVRCameraRig and the desired OVRSpatialAnchor transforms:
        var cameraRig = TrackingSpaceRoot;
        var rigToWorldSpace = cameraRig.localToWorldMatrix;
        var worldToAnchorSpace = newAnchor.transform.worldToLocalMatrix;

        // 2.  Put the camera rig transform into the local space of the new origin:
        rigToWorldSpace = worldToAnchorSpace * rigToWorldSpace;

        // 3.  Apply the result to the OVRCameraRig:
        cameraRig.SetPositionAndRotation(
            position: rigToWorldSpace.GetPosition(),
            rotation: rigToWorldSpace.rotation
        );

        // 4.  (Optional)  Update internal state and UI
        s_Anchor = newAnchor;

        // 5.  (Optional)  Profit
    }

    /// <summary>
    ///     A clone of the leaner implementation of <see cref="SetOrigin(Component)"/>, plus more intermediate extras
    ///     such as re-aligning the poses of non-anchored GameObjects passed in.  Also updates UI for the sample.
    /// </summary>
    public static void SetOrigin([CanBeNull] Component newAnchor,
        [CanBeNull] IReadOnlyCollection<GameObject> nonAnchorObjects)
    {
        // (These overloaded implementations are copied rather than composed for illustrative purposes.)

        var previous = s_Anchor;

        if (!newAnchor)
        {
            Sampleton.Log($"{nameof(Alignment)}.{nameof(SetOrigin)}(null)");
            s_Anchor = null;
            UpdateUIFor(previous);
            return;
        }

        var name = newAnchor.name.Length > 20 ? newAnchor.name.Remove(20) + "[..]"
                                              : newAnchor.name;
        int nObjs = nonAnchorObjects?.Count ?? 0;

        Sampleton.Log($"{nameof(Alignment)}.{nameof(SetOrigin)}({name}, {nObjs})");

        if (newAnchor == s_Anchor)
        {
            Sampleton.Log($"  - SKIPPED: {name} is already the alignment origin.");
            return;
        }

        // 1.  Inputs = references to the scene's OVRCameraRig and the desired OVRSpatialAnchor transforms:
        var cameraRig = TrackingSpaceRoot;
        var rigToWorldSpace = cameraRig.localToWorldMatrix;
        var worldToAnchorSpace = newAnchor.transform.worldToLocalMatrix;

        var origWorldSpaceToRigSpace = cameraRig.worldToLocalMatrix;
        // ^ We also cache this "original-world-to-rig" transformation *before* we modify the camera rig.
        //     (only necessary if we have nonAnchorObjects to fix in step 6.)
        // This is so we can transform each object from unmodified-world space into rig-local space,
        // after which we can transform them again, from rig-local --> modified-world space.

        // 2.  Put the camera rig transform into the local space of the new origin:
        rigToWorldSpace = worldToAnchorSpace * rigToWorldSpace;

        // 3.  Apply the result to the OVRCameraRig:
        cameraRig.SetPositionAndRotation(
            position: rigToWorldSpace.GetPosition(),
            rotation: rigToWorldSpace.rotation
        );

        // 4.  (Optional)  Update internal state and UI
        s_Anchor = newAnchor;

        Sampleton.Log($"  + alignment anchor set!");

        UpdateUIFor(previous);
        UpdateUIFor(newAnchor);

        if (nObjs == 0)
            return;

        // 5.  (Optional)  Profit

        // 6.  (BONUS)  Convert non-anchored objects from the old origin space to the new origin's space
        //              (such that they appear to remain where they were before we redefined the origin)

        // Now we cache the *modified* rig's to-world transformation:
        var newRigSpaceToWorldSpace = cameraRig.localToWorldMatrix;

        foreach (var obj in nonAnchorObjects!)
        {
            if (!obj)
                continue;

            // note: Here we use "pose" in place of "transformation matrix", partly for brevity,
            //       but also because it may be helpful to recognize that all transformation
            //       matrices involved here (yes, even that of the objs) can be appropriately
            //       conceptualized as poses, transformations without meaningful scaling.

            //  and now, the matrix magic!    (reminder: read matrix multiplication from right-to-left)

            // 1.  Take obj's original pose relative to the world (aka "OLD world-space pose"):
            var origPoseInWorldSpace = obj.transform.localToWorldMatrix;

            // 2.  Transform the "OLD world-space pose" into a pose relative to the camera rig ("rig-space pose"):
            var poseInRigSpace = origWorldSpaceToRigSpace * origPoseInWorldSpace;

            // 3.  Transform the "rig-space pose" into a pose relative to the new world space ("NEW world-space pose"):
            var newPoseInWorldSpace = newRigSpaceToWorldSpace * poseInRigSpace;

            // or in one line:
            // newPoseInWorldSpace = newRigSpaceToWorldSpace * (origWorldSpaceToRigSpace * obj.transform.localToWorldMatrix);

            // Finally, we extract a position and rotation from the result matrix and apply it.
            var pos = newPoseInWorldSpace.GetPosition();
            var rot = newPoseInWorldSpace.rotation;

            if (obj.TryGetComponent(out PoseOrigin tweenable))
            {
                // note: These tweenable objects are entirely for demonstration purposes, to visually highlight how the
                //       world space origin shifts whenever you change the active alignment anchor.
                tweenable.TweenTo(
                    pos: pos,
                    rot: rot,
                    overSec: Mathf.Min((pos - tweenable.transform.position).magnitude, 7.5f)
                );
            }
            else
            {
                // IRL, you'll typically want to set the new pose immediately:
                obj.transform.SetPositionAndRotation(
                    position: pos,
                    rotation: rot
                );
            }
        }
    }


    /// <summary>
    ///     Implementation that relies on MRUK::SetCustomWorldLockAnchor(anchor, poseOffset) instead of the maths
    ///     detailed in <see cref="SetOrigin(Component)"/>. <br/>
    ///     <b>This is considered the modern approach</b>, as it removes the need for you to do the math yourself.
    /// </summary>
    /// <remarks>
    ///     However, using MRUK's implementation comes with some prerequisites: <br/>
    ///     (1) The project MUST import MRUK (v83+); <br/>
    ///     (2) The scene MUST have an MRUK script enabled in the scene with World Locking turned ON. <br/>
    ///     (3) Peers MUST receive an anchor pose via some custom networking solution in order to align with others.
    ///     <br/><br/>
    ///     Because of (3), this is NOT a "local" or host-agnostic alignment solution (unlike in
    ///     <see cref="SetOrigin(Component)"/>).
    /// </remarks>
    public static void SetMRUKOrigin([NotNull] Component newAnchor, Pose anchorPoseOnHost)
    {
        Assert.IsNotNull(newAnchor, "newAnchor");
        Assert.AreNotEqual(anchorPoseOnHost, default, "anchorPoseOnHost");

        var name = newAnchor.name.Length > 20 ? newAnchor.name.Remove(20) + "[..]"
                                              : newAnchor.name;

        Sampleton.Log($"{nameof(Alignment)}.{nameof(SetMRUKOrigin)}({name}, {anchorPoseOnHost.Brief()})");

        if (newAnchor is not OVRSpatialAnchor ovrAnchor)
        {
            if (newAnchor is SharedAnchor scene1Anchor)
                ovrAnchor = scene1Anchor.SpatialAnchor;
            else
                ovrAnchor = newAnchor.GetComponent<OVRSpatialAnchor>();
        }

        Assert.IsNotNull(ovrAnchor, $"{name} doesn't seem to represent any OVRSpatialAnchor");

        Assert.IsNotNull(MRUK.Instance, "MRUK.Instance");
        Assert.IsTrue(MRUK.Instance.EnableWorldLock, "MRUK.Instance.EnableWorldLock");

        Sampleton.Log($"  + MRUK::SetCustomWorldLockAnchor(...)");

        // KEY API call: MRUK.Instance.SetCustomWorldLockAnchor(anchor, poseOffset)
        MRUK.Instance.SetCustomWorldLockAnchor(ovrAnchor, anchorPoseOnHost);

        var previous = s_Anchor;

        s_Anchor = newAnchor;
        s_UsingCustomMRUKWorldLock = true;

        Sampleton.Log($"  + alignment anchor set!");

        UpdateUIFor(previous);
        UpdateUIFor(newAnchor);
    }

    /// <summary>
    ///     Resets MRUK's world lock anchor to the default.  This should be done whenever a custom one is destroyed.
    /// </summary>
    public static void ResetMRUKOrigin()
    {
        if (!MRUK.Instance || !MRUK.Instance.EnableWorldLock)
            return;

        Sampleton.Log($"  - {nameof(Alignment)}.{nameof(ResetMRUKOrigin)}()");

        var previous = s_Anchor;

        // KEY API call: MRUK.Instance.SetCustomWorldLockAnchor(anchor, poseOffset)
        MRUK.Instance.SetCustomWorldLockAnchor(null, Pose.identity);

        s_Anchor = null;
        s_UsingCustomMRUKWorldLock = false;

        UpdateUIFor(previous);
    }


    //
    // private impl.

    static Transform s_TrackingSpaceRoot;
    static Component s_Anchor;
    static bool s_UsingCustomMRUKWorldLock;

    static void UpdateUIFor([CanBeNull] Component anchor)
    {
        if (anchor is SharedAnchor scene1Anchor && scene1Anchor)
            scene1Anchor.UpdateUI();
        else if (anchor is ColoDiscoAnchor scene2Anchor && scene2Anchor)
            scene2Anchor.UpdateUI();
        // these types really should be coalesced..
    }

} // end static class Alignment
