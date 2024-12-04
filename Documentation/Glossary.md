# Glossary of Terms – Unity-SharedSpatialAnchors

In no strict order, but loosely grouped firstly by relevance / relatedness, secondarily by alphabetic-ish order.

### anchor
> a transform in 3D space that is kept "anchored" to the real world by the HMD.
- While an anchor's pose in [*virtual* space](#virtual-space) may be redefined (i.e. for [alignment](#alignment)), by
  definition an anchor's pose in [*non*-virtual space](#non-virtual-space) is made to remain as close to constant as
  possible.
  - This is done by overriding anchor poses in Unity's [virtual space](#virtual-space) with poses calculated from a
    ["tracking space"](#tracking-space) every frame, which is the origin of all localized anchors.
- For our purposes, we aren't meaningfully referring to a [virtual object](#virtual-object) when we specify "anchor".

### spatial anchor
> an anchor defined by your app, typically representing a pose in non-virtual space for arbitrary virtual objects to
> affix themselves to.
- Classic Example: a floating virtual globe or astronomy simulation in the center of the room.
- another example: a waypoint marker indicating to MR players where they should throw a virtual ball for it to bounce
  optimally.
- See also:
  - ["What are spatial anchors?"](https://developers.meta.com/horizon/documentation/unity/unity-spatial-anchors-overview#what-are-spatial-anchors)

### scene anchor
> an anchor defined by the [HMD](#hmd)'s spatial scanning capabilities, representing impassable objects in the real
> world such as floors and walls.
- Unlike spatial anchors, scene anchors are represented by a pose, a plane, and (possibly) a bounding box, and share a hierarchical
  relationship with the other scene anchors in the same room.
  - The bounds encode the dimensions of the wall, desk, floor, etc. represented by the scene anchor.
  - Thanks to the hierarchical representation, scene anchors do not exhibit as much "drift" as spatial anchors with
    respect to each other.
- Scene anchors require additional app permissions to use, and require that users have completed a room scan of their
  current play space (which is distinct from boundary setup).
- See also:
  - ["Unity Scene Overview"](https://developers.meta.com/horizon/documentation/unity/unity-scene-overview)
    – discusses deprecated APIs but is a good overview of the same core concepts in non-deprecated MRUK.
  - [MRUK](https://developers.meta.com/horizon/documentation/unity/unity-mr-utility-kit-overview/)
    – Mixed Reality Utility Kit; contains handy abstractions for _scene_ anchors (but not _spatial_ anchors).

**NOTE: Scene anchors are not covered in this sample!**
- If you are interested in scene anchors, please check out MRUK, which comes with its own suite of samples!

### saved anchor
> an anchor that has been saved via calling `anchor.SaveAnchorAsync()` or equivalent.
- A "Success" result guarantees the anchor(s) are saved to the local device storage.
- Sharing an anchor automatically does the equivalent of `anchor.SaveAnchorAsync()`.
- Saving an anchor in this way is only step 1 of 2 to be able to load it again across app sessions.
  - Your app must also [serialize saved anchors' UUIDs](#serialized-anchor-locally-saved-anchor) so they can be
    deserialized later for loading.

### serialized anchor, locally-saved anchor
> an anchor whose [UUID](#uuid-systemguid) has been saved by your app's logic to disk, so the anchor can be loaded
> again later (e.g. with `OVRSpatialAnchor.LoadUnboundAnchorsAsync(...)`).
- For the sake of simplicity, this sample uses `UnityEngine.PlayerPrefs.{Set,Get}String(key)` to serialize anchor IDs
  to disk.
  - Your app may elect to use something more robust and controllable; we acknowledge that `PlayerPrefs` is not
    intended nor well-suited for persisting anything that could be considered experience-critical ("gameplay") state.

### shared anchor
> an anchor created by one individual and made loadable to others. <br/><br/>
> This is done with APIs like `OVRSpatialAnchor.ShareAsync(...)` or equivalent, after which others can load using
> `OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync(...)`.
- Sharing an anchor automatically does the equivalent of `anchor.SaveAnchorAsync()`.
- "SSA" = Shared Spatial Anchor

### to hide (an anchor)
> this term means literally to call `UnityEngine.Object.Destroy(anchor.gameObject)` on the anchor,
> thus deleting the anchor's representation in Unity from the Scene. <br/><br/>
> However, this alone does not mean the anchor is gone forever; we didn't call `anchor.EraseAnchorAsync()`
> (or equivalent), so we could easily reload our destroyed ("hidden") anchor back into the scene at any time.
- Assumes your app [saved the UUID in some way](#serialized-anchor-locally-saved-anchor) before calling `Destroy(*)`.
  Otherwise, the anchor is basically erased.

### to erase (an anchor)
> this means to call `anchor.EraseAnchorAsync()` or equivalent.  On its own, this call does not "hide" the anchor by
> definition, but for sake of simplicity this sample's "Erase" buttons will also "hide" the anchor as described above.

### to align (to an anchor)
> to redefine the poses of either the local tracking space *or* of every shared anchor, such that peers'
> [virtual spaces](#virtual-space) become aligned. <br/><br/>
> Aligned virtual spaces share an origin (0,0,0) and are oriented facing the same direction.
> This ensures that *non*-anchored [virtual objects](#virtual-object) appear at synchronized poses between clients.
- Has implementations in [AlignPlayer.cs](../Assets/Scripts/AlignPlayer.cs) and
  [ColoDiscoMan.cs](../Assets/Scripts/ColoDiscoMan.cs).

### colocated
> (typically of [peers](#peers)) located in the same non-virtual space (namely, the same room or set of connected rooms).
- "colocation" as a noun = one such location / space / room where multiple peers are present.
- "colocation" as an adjective = utilizes, targets, or enables colocated peers.
- "to colocate" = to connect peers who are colocated and running the same app such that their clients can share state.

### DUC
> data use checkup; a developer form completed on a per-app basis that declares what kinds of user data will be
> requested and used by the app.
- Can be found in each app's [developer dashboard](https://dashboard.oculus.com) under (sidebar) > "Requirements" >
  "Data Use Checkup".
- This sample makes use of two user data points that should be declared in the DUC:
  - **User ID** – *required* for creating `OVRSpaceUser`s, which are then passed to the sharing methods in
    `OVRSpatialAnchor` in the sample scene "Sharing to Users".
  - **User Profile** – *not required* for any specific API calls, however it is nice to have player nicknames,
    making it easy to identify participants in shared rooms / colocation sessions.

### group
> (as in "group sharing") an indirect "container" for shared anchors that is bidirectionally anonymous; groups are
> lightweight by design, its only datum being a [UUID](#uuid-systemguid) that uniquely identifies it.
- "bidirectionally anonymous" = sharers don't need to know the platform user IDs of sharees and vice versa.
- Your apps may feel free to make its own extensions of what constitutes a "group",
  [as we do in this sample](../Assets/Scripts/ColoDiscoMan.cs#L353-L360).

### guardian, boundary
> the virtual barrier anchored in the real world serving as a core safety feature, protecting you and your surroundings
> while you have your HMD donned. <br/><br/>
> This barrier warns you from leaving the designated play space with visual cues, and by pausing the app if you stray
> too far.
- Refers to two kinds (which are not meaningfully distinguished in this sample):
  - Stationary
  - Room-scale (either drawn, scanned, or both)
- You are required to set up a guardian for your current physical space before you can launch this kind of app.
- Spatial anchors meant for different guardians may still load successfully; **test these cases carefully** to avoid
  unexpected runtime behaviors.

### HMD
> Head-Mounted Device (XR-enabled, and in this sample assumed MR-enabled).
- AKA "headset", "Quest", "Oculus"
- "to don" = to put on, equip, mount the HMD to your head and face.
- "to doff" = to take off, remove the HMD from its mounted position.

### peers
> users/players/clients who are, have, or wish to be connected with each other in an ongoing session.

### pose
> a position and a rotation/orientation in 3D space. <br/><br/>
> See: [UnityEngine.Pose](https://docs.unity3d.com/ScriptReference/Pose.html),
> [OVRPose](https://developers.meta.com/horizon/reference/unity/latest/struct_o_v_r_pose/)

### PUN / PUN2
> Photon Unity Networking (2), the freemium 3rd-party networking provider leveraged by this sample. <br/><br/>
> Sometimes referred to as Photon Realtime, which is the engine-agnostic C# implementation underlying PUN2.

### session
> a continuous state where some or all sharable objects have been synchronized among some or all participants.
- AKA (loosely) "room", as in a Photon Realtime Room.

### to sideload, flash (an APK / app)
> to install a build of your app onto a device without going through the app store.
- Typically performed using [ADB](https://developers.meta.com/horizon/documentation/unity/ts-adb) or
  [MQDH](https://developers.meta.com/horizon/documentation/unity/unity-quickstart-mqdh) over a USB connection.

### virtual space
> usually, the 3D space reasoned with by the game engine, and distinct from the 3D space of the real world.
- AKA "world space", "Unity space"

### virtual object
> basically, a GameObject (or a tree of GameObjects forming a whole), but not exclusively.
- A single particle emitted from a ParticleSystem might be an example of a non-GameObject virtual object.
- FYI: The native representations of anchors (outside of Unity) are also non-GameObject virtual objects...
  - However, the scope of this sample does not explore this concept directly.
  - Try our online developer documentation for more details about
    [native-side spatial anchors](https://developers.meta.com/horizon/documentation/native/android/openxr-spatial-anchors-overview).
- For our purposes, we aren't meaningfully referring to an [anchor](#anchor) when we specify "virtual object".

### non-virtual space
> the real, physical world.
- Mixed reality renders this space alongside [virtual spaces](#virtual-space) using live camera data.
  This is often referred to as [Passthrough](https://developers.meta.com/horizon/documentation/unity/unity-passthrough).

### non-virtual object
> example: your face; often, a wall, or your cat.

### tracking space
> a subset of virtual space that is reasoned with by the [HMD](#hmd) hardware and native code, and can be manipulated
> via the Unity Transform
> [`OVRCameraRig.trackingSpace`](https://developers.meta.com/horizon/reference/unity/v69/class_o_v_r_camera_rig#trackingspace).
- AKA "anchor space", (rarely) "headset space"
- It should be thought of as another origin, separate from Unity's own worldspace origin, but sharing
  [a close relationship](#world-locking) with the Unity origin since altering the tracking space alters the rendered
  location and orientation of the Unity origin (and all virtual objects in it).
  - While all localized anchors are technically children of this space, this isn't apparent because anchors are
    internally updated each frame to maintain their perceived pose in non-virtual space.
- The tracking space is driven by a chosen ["Tracking Origin Type"](https://developers.meta.com/horizon/documentation/unity/unity-ovrcamerarig/#tracking)
  on the current scene's OVRManager (usually on an "OVRCameraRig" GameObject).
  - This sample chooses the "Floor Level" origin type, which is recommended for most mixed reality experiences.

### UUID, `System.Guid`
> acronym for [Universally Unique IDentifier](https://en.wikipedia.org/wiki/Universally_unique_identifier), a 128-bit
> stochastically unique number implemented via `System.Guid` (G for Globally) in standard C#. <br/><br/>
> In the context of this sample, this type of identifier is used to uniquely identify [anchors](#anchor) and
> [groups](#group).
- Throughout the sample (and indeed common practice), UUIDs regularly change their representation between:
    - struct form, most typically `System.Guid`;
        - used in this sample to pass to Core SDK APIs, to store in class fields, for comparison operators, etc.
    - `string` form, e.g. `"f81d4fae-7dec-11d0-a765-00a0c91e6bf6"` / `"f81d4fae7dec11d0a76500a0c91e6bf6"` (sans dashes);
        - used in this sample for [serialization to disk](#serialized-anchor-locally-saved-anchor).
    - byte array or span of size 16: `byte[16]`, `Span<byte>`, `ArraySegment<byte>` etc.;
        - used in this sample for transfer over network.
- The terms "anchor ID" and "group ID" both refer to UUIDs. "User ID" or "UID" *do not* refer to UUIDs, as such IDs are
  typically represented with a 64-bit `ulong` handle.

### world locking
> a small but significant adjustment to the local [tracking space](#tracking-space) pose that should be done every
> frame. <br/><br/>
> World locking ensures that when the [HMD](#hmd) moves frame-to-frame, non-anchor virtual objects maintain a smooth and
> accurate appearance of being anchored to the physical world.
- It also aims to mitigate "anchor drift".
- **Disambiguation:** World locking syncs the local client's virtual space with its own tracking (anchor) space, while
  [anchor alignment](#to-align-to-an-anchor) syncs the virtual spaces of multiple peers.
- Implemented in MRUK, and **not directly demonstrated in this sample**.

------------------------------------------------------------------------------------------------------------------------
