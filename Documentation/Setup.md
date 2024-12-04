# Setup – Unity-SharedSpatialAnchors

| Table of Contents                                                                   |
|:------------------------------------------------------------------------------------|
| [Before You Begin](#before-you-begin)                                               |
| [Unity Editor](#unity-editor)                                                       |
| [Getting this sample project](#getting-this-sample-project)                         |
| [Opening this sample project](#opening-this-sample-project)                         |
| [Register app - Platform Developer Dashboard](#register-app-on-developer-dashboard) |
| [Register app - Photon](#photon)                                                    |
| [Pre-Build Checklist](#pre-build-checklist)                                         |
| [Building & Flashing](#build-and-flash)                                             |
| [Optional](#optional)                                                               |


------------------------------------------------------------------------------------------------------------------------

## Before You Begin

Whether you're brand new to working with the [Meta XR Core SDK] in Unity for Quest apps or a returning veteran, <br/>
please ensure that you have completed the requirements found in our ["Before You Begin" (Unity)](https://developers.meta.com/horizon/documentation/unity/unity-before-you-begin)
document.

## Unity Editor

Installation:
1. This project uses **2021.3.32f1**[^1]. Install this version for best results.
    - (although any 2021 LTS version after 2021.3.26f1 should be stable enough)
2. Make sure to install all Android modules.

## Getting this sample project

Use Git to clone this repo to your machine.

E.G.
```bash
git clone 'git@github.com:oculus-samples/Unity-SharedSpatialAnchors.git'
```

Alternatively, you can [download a zip here.](https://github.com/oculus-samples/Unity-SharedSpatialAnchors/archive/refs/heads/main.zip)

## Opening this sample project

In the Unity Hub > Projects tab, use the **"Add" > "Add project from disk"** dropdown and navigate to where you
downloaded this project when prompted.
- (You should choose the folder that contains "Assets", "ProjectSettings", etc.)

Before you _open_ the sample project for the first time, you should open it for the **Android build target** as follows:

![Select button in "Editor Version" column](Media/unityhub-open-as-1.png 'Select button in "Editor Version" column')

![Select "Android" before continuing](Media/unityhub-open-as-2.png 'Select "Android" before continuing')

**If you forgot to do this step**, you can remedy it at any time by switching build targets in the
Build Settings Window.
- Open the Build Settings Window via menu bar > "File" > "Build Settings...", or the default hotkey `CTRL+SHIFT+B`

![Switch to target Android](Media/unity-switch-target-android.png 'Switch to target Android')

This may take a few moments to several minutes, as all project assets must be re-imported for the new build target.


## Register app on Developer Dashboard and update Platform settings

Some sample scenes utilize or require the logged-in user's **"Platform ID"** (formerly referred to as "Oculus ID") to
function.

> For example, the "Sharing to Users" scene has participants share their Platform ID with their room so that others in
> the room can share their anchors directly to them through this ID.

Access to this Platform ID requires you to register your app on [dashboard.oculus.com](https://dashboard.oculus.com) and complete a
"Data Use Checkup" (DUC).[^2]

This assumes you've previously followed steps to create your free Meta developer account and organization.
1. Click **"Create New App"** under your developer organization.
2. Enter a name for your app.
3. Choose **"Meta Horizon Store"**.
4. From the left navigation, go to **"Requirements"** > **"Data Use Checkup"**,
5. Complete **"Age group self-certification"** before requesting access to **"User ID"** and **"User Profile"**
   platform features.
    - "User ID" is used for the Platform ID already mentioned.
    - "User Profile" is used to get a user's online username (aka "tag" or "nickname"), which is useful for identifying
      who's who in rooms.
6. From the left navigation, go to **"Development"** > **"API"**. Copy the value under **"App ID"**.

![Copy the platform App ID from the dashboard](Media/platform-dash-copy-appid.png 'Copy the platform App ID from the dashboard')

With the Unity project open, navigate via menu bar > "Meta" > "Platform" > "Edit Settings".
1. Enter the App ID from above in the **"Meta Quest/2/Pro"** field.
2. If you want to test with Link, check the boxes "Use Standalone Platform" and "Use Meta Quest App ID over Rift App ID
   in Editor", and enter your test user's credentials.
3. Under the **"Build Settings"** section, replace the **"Bundle Identifier"** with a valid and unique Android package
   name, like `com.yourcompany.ssa_sample`.
   - Your app's anchors will be associated with this package name.
   - You should not change the bundle ID once it has been connected with any app in the developer dashboard.

![Paste the platform App ID and set a new bundle ID](Media/platform-settings-paste-appid.png 'Paste the platform App ID and set a new bundle ID')

## Photon

This project uses [PUN2] to exchange anchor and user IDs with connected peers, and for full multiplayer networking
support.
- PUN is _not_ used in the "Colocation & Group Sharing" scene.
- For your own apps, PUN (or something like PUN[^3][^4][^5]) is likely needed for non-static networked objects / live
  client communications.

### Creating a new Photon application:

1. Create an account with [Photon](https://dashboard.photonengine.com).
2. [Create a New App](https://dashboard.photonengine.com/app/create).
    - **"Select Photon SDK"**: set to **"Realtime"**. (There is a known issue with selecting the "Pun" option—avoid it!)
    - "Name": your choice. Many find it useful to sync it with the bundle ID you created earlier.
    - "Description" & "Url": can be left blank.
3. ***In Unity:*** Copy the App ID from the dashboard to the "App Id PUN" field in `PhotonServerSettings.asset`.
   - You can locate the PhotonServerSettings asset via menu bar > "Window" > "Photon Unity Networking" > "Highlight
     Server Settings".

![Creating a new Photon app](Media/photon-dash-new-app.png 'Creating a new Photon app')

![Copy Photon App ID](Media/photon-dash-copy-appid.png 'Copy Photon App ID')

![Paste Photon App ID in Unity](Media/photon-settings-paste-appid.png 'Paste Photon App ID in Unity')


## Pre-Build Checklist

### Minimum Android API = 32

At the time of writing, you will need to ensure the project builds for Android API 32 or newer. <br/>
In "Edit" > "Project Settings..." > "Player" > "Other Settings", ensure this setting (which may have been altered on
import):

![Set Min Android API 32](Media/unity-min-api-level.png 'Set Min Android API 32')

### AndroidManifest.xml

This repo comes with a pre-baked [`AndroidManifest.xml`](../Assets/Plugins/Android/AndroidManifest.xml) that should work
out-of-the-box.

> **Unity 2023.2 or newer** *(including Unity 6)*:
> - You **MUST** update this file before building for Android.
> - Running menu bar > "Meta" > "Tools" > **"Update AndroidManifest.xml"** will apply the necessary adjustments.

If you're curious how we generate / keep it in sync:

- To generate, run menu bar > "Meta" > "Tools" > **"Create Store-compatible AndroidManifest.xml"**.
- After any edits made to [`OculusProjectConfig.asset`](../Assets/Oculus/OculusProjectConfig.asset) (which is
also editable via any `OVRManager` inspector e.g. on the in-scene OVRCameraRigs), you should propagate the changes by
running menu bar > "Meta" > "Tools" > **"Update AndroidManifest.xml"**.

### Recommended: Commit your changes

Even if you don't plan to push your sample environment anywhere, we recommend making a local commit at this point to
record the setup you've done so far.  This can be useful if, for example, you ever need to revert back to this clean
state after having made changes to your sandbox.

The following command would make a git commit staging only the files that should have changed so far:
```bash
git add \
  'ProjectSettings/ProjectSettings.asset' \
  'Assets/Resources/OculusPlatformSettings.asset' \
  'Assets/*/Resources/PhotonServerSettings.asset' \
  'Assets/Plugins/Android/AndroidManifest.xml' # <- MAY have changed.
git commit -m 'Connected test app with Platform and Photon services'
```

but you may wish to check `git status` / `git diff` for anything else you'd like saved in the current state
(or to check for potential accidental changes!).

If you see any unknown changes to assets such as `OculusProjectConfig.asset`, we recommend using `git reset` to undo
them.

## Build and flash an APK

> **If this is your first time building** for this API level with this installation of Unity: <br/><br/>
> Shortly after clicking "Build...", you MAY see a popup titled
> **"Android SDK is missing required platform API"**. <br/>
> If you do:
> **TREAD CAREFULLY!** <br/>
> Click **"Update Android SDK"** – Do NOT select "Use..." or otherwise dismiss the popup. <br/><br/>
> (The remediation process can be quite non-trivial if you accidentally missed this detail, and this documentation will
> not cover this edge case.)

Open the "Build Settings Window" via menu bar > "File" > "Build Settings...", or the default hotkey `CTRL+SHIFT+B`.

Ensure that the build target is set to Android.

We recommend enabling "Development Build", but it shouldn't be required for the samples to function.

You can choose
- **"Build"** to build an APK file which you can later flash to a device via [adb] or [MQDH], or
- **"Build And Run"** to automatically flash the built app onto any connected **adb-enabled** device, and launch it.
  - note: this is an "instant" install, which means when the app quits it will not persist as an "installed" app.

**For instructions on how to get `adb` working with your headset and Unity**, refer to these instructions:
https://developers.meta.com/horizon/documentation/unity/ts-adb

------------------------------------------------------------------------------------------------------------------------

## Optional

### Explore your new sandbox

> As long as you abide by the rules set forth by the project's [licensing](../README.md#license), you are clear to use
> this sample any way you like.

Some things to try:
- Run the build with two headsets or a partner and try both methods for anchor sharing.
- Turn on some of the optional (inactive by default) GameObjects in the UI hierarchy to enable intermediate /
  advanced test cases.
- Add your own non-anchor GameObjects and get them to synchronize between clients.
- Play around with how creating, sharing, and loading spatial anchors behave in different guardians.

### Meta Quest Developer Hub (MQDH)

- Quickstart: https://developers.meta.com/horizon/documentation/unity/unity-quickstart-mqdh

Sideloading builds onto you app is as easy as drag-and-drop!

![Sideloading with MQDH](Media/mqdh-sideloading.png 'Sideloading with MQDH')

------------------------------------------------------------------------------------------------------------------------

[^1]: Paste this into your browser's URL bar: `unityhub://2021.3.32f1/3b9dae9532f5`
[^2]: For privacy reasons, you must do these steps for your "fork" of our sample. Plus, it's a good way to familiarize yourself with our online developer dashboards!
[^3]: Photon Fusion is very popular in modern production games, and is as free to develop with as PUN: https://www.photonengine.com/fusion
[^4]: With nearly the same feature set as Photon, Mirror has no account obligations and is licensed under MIT: https://github.com/MirrorNetworking/Mirror
[^5]: From the makers of Mirror and also under MIT: Telepathy = simple, lightweight message passing transport layer with easy LAN serving: https://github.com/MirrorNetworking/Telepathy


[Meta XR Core SDK]: https://developers.meta.com/horizon/downloads/package/meta-xr-core-sdk
[Standard Unity Asset Store EULA]: https://unity.com/legal/as-terms
[Unity Hub]: https://unity.com/download
[PUN2]: https://assetstore.unity.com/packages/tools/network/pun-2-free-119922
[2021.3.32f1]: unityhub://2021.3.32f1/3b9dae9532f5
[dashboard.oculus.com]: https://dashboard.oculus.com
[dashboard.photonengine.com]: https://dashboard.photonengine.com
[adb]: https://developers.meta.com/horizon/documentation/unity/ts-adb
[MQDH]: https://developers.meta.com/horizon/documentation/unity/unity-quickstart-mqdh
