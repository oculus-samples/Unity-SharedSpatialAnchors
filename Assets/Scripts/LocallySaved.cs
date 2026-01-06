// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;

using JetBrains.Annotations;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

using Sampleton = SampleController; // only transitional

/// <summary>
///   Stores/Loads anchors on the local app-space disk.
/// </summary>
/// <remarks>
///   Save data is segregated by Application.buildGUID (unless in the Editor) and the active scene name,
///   and can be found under Application.persistentDataPath.  You can check runtime logs for the actual
///   full paths written to. <br/><br/>
///   The only automatic <see cref="CommitToDisk"/> this class does is on Application.quitting and
///   Application.focusChanged(false).  Else, it is up to callers to call <see cref="CommitToDisk"/>
///   as they see fit; currently, that means after every explicitly-intentional save request made by
///   the user.
/// </remarks>
[MetaCodeSample("SharedSpatialAnchors")]
[MetaCodeSample("SharedSpatialAnchors-ColocationSessionGroups")]
static class LocallySaved
{
    [NotNull]
    public static IReadOnlyCollection<Guid> Anchors => GetActiveDataStore().RememberedAnchors.Keys;
    [NotNull]
    public static IReadOnlyCollection<Guid> AnchorsMadeByMe => GetActiveDataStore().RememberedAnchors.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToArray();
    [NotNull]
    public static IReadOnlyCollection<Guid> AnchorsIgnored => GetActiveDataStore().IgnoredAnchors;


    public static void CommitToDisk()
    {
        if (PerSceneData.GlobalData.IsDirty)
            PerSceneData.GlobalData.Save();

        foreach (var sceneData in s_PerSceneData.Values)
        {
            if (sceneData.IsDirty)
                sceneData.Save();
        }
    }


    public static bool AnchorIsRemembered(Guid anchorId)
    {
        return GetActiveDataStore().RememberedAnchors.ContainsKey(anchorId);
    }

    public static bool AnchorIsRemembered(Guid anchorId, out bool isMine)
    {
        return GetActiveDataStore().RememberedAnchors.TryGetValue(anchorId, out isMine);
    }

    public static bool AnchorIsMine(Guid anchorId)
    {
        return GetActiveDataStore().RememberedAnchors.TryGetValue(anchorId, out bool mine) && mine;
    }

    public static bool AnchorIsIgnored(Guid anchorId)
    {
        return anchorId == Guid.Empty || GetActiveDataStore().IgnoredAnchors.Contains(anchorId);
    }


    public static bool RememberAnchor(Guid anchorId, bool isMine)
    {
        if (anchorId == Guid.Empty)
            return false;

        var dataStore = GetActiveDataStore();

        if (!dataStore.WarnedAnchorCap && dataStore.RememberedAnchors.Count >= k_MaybeTooManySavedAnchors)
        {
            Sampleton.Log(
                $"WARN: You have locally saved over {k_MaybeTooManySavedAnchors} anchors.\n" +
                $"- This is likely more than you need for one space.\n" +
                $"- You can clear your saved anchors from the app's Main Menu > \"Clear Local Save Data\" button.",
                LogType.Warning
            );
            dataStore.WarnedAnchorCap = true;
        }

        if (dataStore.RememberedAnchors.Remove(anchorId, out bool wasMine))
            dataStore.IsDirty |= isMine ^ wasMine;
        else
            dataStore.IsDirty = true;

        dataStore.RememberedAnchors[anchorId] = isMine;
        dataStore.IsDirty |= dataStore.IgnoredAnchors.Remove(anchorId);
        return true;
    }

    public static bool ForgetAnchor(Guid anchorId)
    {
        if (anchorId == Guid.Empty)
            return false;

        var dataStore = GetActiveDataStore();
        dataStore.IsDirty |= dataStore.RememberedAnchors.Remove(anchorId);
        return true;
    }


    public static bool IgnoreAnchor(Guid anchorId)
    {
        if (anchorId == Guid.Empty)
            return false;

        var dataStore = GetActiveDataStore();
        dataStore.IsDirty |= dataStore.RememberedAnchors.Remove(anchorId);
        dataStore.IsDirty |= dataStore.IgnoredAnchors.Add(anchorId);
        return true;
    }

    public static bool UnignoreAnchor(Guid anchorId)
    {
        if (anchorId == Guid.Empty)
            return false;

        var dataStore = GetActiveDataStore();
        dataStore.IsDirty |= dataStore.IgnoredAnchors.Remove(anchorId);
        return false;
    }


    public static void DeleteAll(bool wipeDisk)
    {
        PerSceneData.GlobalData.Clear(wipeDisk);
        foreach (var data in s_PerSceneData.Values)
        {
            data.Clear(wipeDisk);
        }

#if UNITY_EDITOR
        if (!wipeDisk)
            return;

        // if called at edit-time, s_PerSceneData is probably empty, even if there *is* save data in persistentDataPath.

        if (PerSceneData.GlobalData.FileInfo.Directory is not { } dataDir)
            return;

        dataDir.Refresh();

        if (!dataDir.Exists)
            return;

        dataDir.Delete(recursive: true);

#endif // UNITY_EDITOR
    }


    //
    // private impl.

    const string k_OwnedMarker = "@";
    const int k_MaybeTooManySavedAnchors = 60;

    static readonly Dictionary<string, PerSceneData> s_PerSceneData = new();

    [NotNull]
    static PerSceneData GetActiveDataStore()
    {
        var scene = SceneManager.GetActiveScene();
        return s_PerSceneData.TryGetValue(scene.name, out var sceneData) ? sceneData
                                                                         : PerSceneData.GlobalData;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void SetupSceneListener()
    {
        SceneManager.activeSceneChanged += (prev, curr) =>
        {
            if (!prev.IsValid() || !s_PerSceneData.TryGetValue(prev.name, out var sceneData))
                sceneData = PerSceneData.GlobalData;

            if (sceneData.IsDirty)
                sceneData.Save();

            if (!curr.IsValid())
            {
                sceneData = PerSceneData.GlobalData;
            }
            else if (!s_PerSceneData.TryGetValue(curr.name, out sceneData))
            {
                s_PerSceneData[curr.name] = sceneData = new PerSceneData(curr.name);
            }

            sceneData.Load();
        };

        Application.quitting += CommitToDisk;

        // Using the Home button to quit the app WON'T trigger Application.quitting,
        // however it WILL have triggered Application.focusChanged (before user can click the "Quit" button).
        Application.focusChanged += isFocused =>
        {
            if (!isFocused)
            {
                CommitToDisk();
                return;
            }

            var reloadSceneData = GetActiveDataStore();
            if (reloadSceneData.IsDirty) // don't clobber new data
                return;
            reloadSceneData.Load();
        };
    }


    [MetaCodeSample("SharedSpatialAnchors")]
    sealed class PerSceneData
    {
        public static readonly PerSceneData GlobalData = new(null);
        const string GlobalDataName = "DontDestroyOnLoad";

        public readonly string SceneName;
        public readonly Dictionary<Guid, bool> RememberedAnchors = new();
        public readonly HashSet<Guid> IgnoredAnchors = new();

        public readonly FileInfo FileInfo;
        public bool IsDirty;
        public bool WarnedAnchorCap;

        public bool IsEmpty => RememberedAnchors.Count + IgnoredAnchors.Count == 0;

        public PerSceneData(string sceneName)
        {
            SceneName = string.IsNullOrEmpty(sceneName) ? GlobalDataName
                                                        : sceneName;
            IgnoredAnchors.Add(Guid.Empty);

            string rootDir = Application.persistentDataPath.Replace('\\', '/'); // extraneous Replace()
            FileInfo = Application.isEditor ? new FileInfo($"{rootDir}/{nameof(PerSceneData)}/{SceneName}.save")
                                            : new FileInfo($"{rootDir}/{Application.buildGUID}/{SceneName}.save");
        }

        public void Save(bool pretty = true)
        {
            Assert.IsNotNull(FileInfo, $"PerSceneData({SceneName}).FileInfo");
            Assert.IsNotNull(FileInfo.Directory, $"PerSceneData({SceneName}).FileInfo.Directory");

            FileInfo.Refresh();

            if (IsEmpty)
            {
                FileInfo.Delete();
                IsDirty = false;
                return;
            }

            if (!FileInfo.Directory.Exists)
                FileInfo.Directory.Create();

            bool createdNew = !FileInfo.Exists;

            FileInfo.Delete();

            using var file = new StreamWriter(
                stream: FileInfo.Open(FileMode.Create, FileAccess.Write, FileShare.Read),
                encoding: SampleExtensions.EncodingForSerialization,
                bufferSize: 4096, // std Android block size
                leaveOpen: false
            );

            var pod = new POD
            {
                SceneName = SceneName,
                RememberedAnchors = RememberedAnchors.Select(kvp => kvp.Key.Serialize(kvp.Value ? k_OwnedMarker : "")).ToArray(),
                IgnoredAnchors = IgnoredAnchors.Select(guid => guid.Serialize()).ToArray(),
            };

            file.NewLine = "\n";
            file.Write(JsonUtility.ToJson(pod, pretty));

            IsDirty = false;

            if (createdNew)
                Sampleton.Log($"INFO: Created new local file '{FileInfo}' (for anchor UUIDs)");
        }

        public void Load()
        {
            FileInfo.Refresh();
            if (!FileInfo.Exists)
            {
                IsDirty = !IsEmpty;
                return;
            }

            string rawJson;
            using (var file = new StreamReader(
                       stream: FileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read),
                       encoding: SampleExtensions.EncodingForSerialization,
                       detectEncodingFromByteOrderMarks: false,
                       bufferSize: 4096,
                       leaveOpen: false
                   ))
            {
                rawJson = file.ReadToEnd();
            }

            Assert.IsFalse(string.IsNullOrEmpty(rawJson), "rawJson != null");
            Assert.IsTrue(rawJson.Length > 2, "rawJson.Length > \"{}\".Length");

            var pod = JsonUtility.FromJson<POD>(rawJson);

            Assert.AreEqual(SceneName, pod.SceneName, nameof(SceneName));

            RememberedAnchors.Clear();
            IgnoredAnchors.Clear();

            foreach (var rawUuid in pod.RememberedAnchors)
            {
                if (rawUuid.StartsWith(k_OwnedMarker))
                    RememberedAnchors[Guid.Parse(rawUuid[k_OwnedMarker.Length..])] = true;
                else
                    RememberedAnchors[Guid.Parse(rawUuid)] = false;
            }

            foreach (var rawUuid in pod.IgnoredAnchors)
            {
                IgnoredAnchors.Add(Guid.Parse(rawUuid));
            }

            WarnedAnchorCap = false; // reset "warn once" behaviour whenever deserializing saved anchors
            IsDirty = false;
        }

        public void Clear(bool wipeDisk = true)
        {
            IsDirty = !wipeDisk && !IsEmpty;
            RememberedAnchors.Clear();
            IgnoredAnchors.Clear();

            if (!wipeDisk)
                return;
            FileInfo.Refresh();
            FileInfo.Delete();
        }


        [Serializable]
        struct POD
        {
            public string SceneName;
            public string[] RememberedAnchors;
            public string[] IgnoredAnchors;
        }

    } // end nested class PerSceneData

} // end static class LocallySaved
