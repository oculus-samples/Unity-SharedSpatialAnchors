// Copyright (c) Meta Platforms, Inc. and affiliates.

using JetBrains.Annotations;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

using Sampleton = SampleController; // only transitional

static class LocallySaved
{
    [NotNull]
    public static IReadOnlyCollection<Guid> Anchors => GetActiveDataStore().RememberedAnchors.Keys;
    [NotNull]
    public static IReadOnlyCollection<Guid> AnchorsMadeByMe => GetActiveDataStore().RememberedAnchors.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToArray();
    [NotNull]
    public static IReadOnlyCollection<Guid> AnchorsIgnored => GetActiveDataStore().IgnoredAnchors;


    public static bool AnchorsCanGrow => GetActiveDataStore().RememberedAnchors.Count < k_MaxAnchorMemory;

    public static void LogCannotGrow(string caller)
    {
        Sampleton.LogError($"{caller}: Not saving! Maximum saved anchor count ({k_MaxAnchorMemory}) reached.");
        Sampleton.Log($"- NOTE: This is an artificial limit set by this sample.", LogType.Warning);
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
        return GetActiveDataStore().IgnoredAnchors.Contains(anchorId);
    }


    public static bool RememberAnchor(Guid anchorId, bool isMine)
    {
        if (anchorId == Guid.Empty)
            return false;

        var dataStore = GetActiveDataStore();
        if (dataStore.RememberedAnchors.Count >= k_MaxAnchorMemory)
            return false;

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


    public static void DeleteAll()
    {
        PerSceneData.GlobalData.Clear();
        foreach (var data in s_PerSceneData.Values)
        {
            data.Clear();
        }
    }


    //
    // private impl.

    const string k_OwnedMarker = "@";
    const int k_MaxAnchorMemory = 60;

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

        Application.quitting += commitAllToDisk;

        // Using the Home button to quit the app WON'T trigger Application.quitting,
        // however it WILL have triggered Application.focusChanged (before user can click the "Quit" button).
        Application.focusChanged += isFocused =>
        {
            if (!isFocused)
                commitAllToDisk();
        };

        return;

        static void commitAllToDisk()
        {
            if (PerSceneData.GlobalData.IsDirty)
                PerSceneData.GlobalData.Save();

            foreach (var sceneData in s_PerSceneData.Values)
            {
                if (sceneData.IsDirty)
                    sceneData.Save();
            }
        }
    }


    sealed class PerSceneData
    {
        public static readonly PerSceneData GlobalData = new(nameof(GlobalData));

        public readonly string SceneName;
        public readonly Dictionary<Guid, bool> RememberedAnchors = new();
        public readonly HashSet<Guid> IgnoredAnchors = new();

        public readonly FileInfo FileInfo;
        public bool IsDirty;

        public PerSceneData(string sceneName)
        {
            SceneName = string.IsNullOrEmpty(sceneName) ? nameof(GlobalData)
                                                        : sceneName;
            IgnoredAnchors.Add(Guid.Empty);
            FileInfo = Application.isEditor ? new FileInfo($"{Application.persistentDataPath}/{nameof(PerSceneData)}/{SceneName}.save")
                                            : new FileInfo($"{Application.persistentDataPath}/{Application.buildGUID}/{SceneName}.save");
        }

        public void Save(bool pretty = true)
        {
            Assert.IsNotNull(FileInfo, "FileInfo != null");
            Assert.IsNotNull(FileInfo.Directory, "FileInfo.Directory != null");

            FileInfo.Refresh();
            if (!FileInfo.Directory.Exists)
                FileInfo.Directory.Create();

            FileInfo.Delete();

            using var file = new StreamWriter(
                stream: FileInfo.Open(FileMode.Create, FileAccess.Write, FileShare.Read),
                encoding: SampleExtensions.EncodingForSerialization,
                bufferSize: 4096, // std Android block size // also, this is a sample, we shouldn't care x~x
                leaveOpen: false
            );

            // lol:
            // var pod = new POD
            // {
            //     SceneName = SceneName,
            //     RememberedAnchors = RememberedAnchors.Select(kvp => kvp.Key.Serialize(kvp.Value ? k_OwnedMarker : "")).ToArray(),
            //     IgnoredAnchors = IgnoredAnchors.Select(guid => guid.Serialize()).ToArray(),
            // };
            // file.Write(JsonUtility.ToJson(pod, pretty));
            // return;

            // nahh.

            // more fun:
            string openObj, closeObj, openArr, closeArr, comma, colon, nl, idt;
            if (pretty)
            {
                openObj = "{\n";
                closeObj = "}";
                openArr = "[\n";
                closeArr = "]";
                comma = ",\n";
                colon = ": ";
                nl = "\n";
                idt = "  ";
            }
            else
            {
                openObj = "{";
                closeObj = "}";
                openArr = "[";
                closeArr = "]";
                comma = ",";
                colon = ":";
                nl = "";
                idt = "";
            }

            file.Write(openObj);
            {
                file.Write($"{idt}\"{nameof(SceneName)}\"{colon}\"{SceneName}\"{comma}");

                file.Write($"{idt}\"{nameof(RememberedAnchors)}\"{colon}{openArr}");
                bool doComma = false;
                foreach (var (uuid, isMine) in RememberedAnchors)
                {
                    if (doComma)
                        file.Write(comma);
                    doComma = true;
                    file.Write($"{idt}{idt}\"{uuid.Serialize(isMine ? k_OwnedMarker : "")}\"");
                }
                file.Write($"{nl}{idt}{closeArr}{comma}");

                file.Write($"{idt}\"{nameof(IgnoredAnchors)}\"{colon}{openArr}");
                doComma = false;
                foreach (var uuid in IgnoredAnchors)
                {
                    if (doComma)
                        file.Write(comma);
                    doComma = true;
                    file.Write($"{idt}{idt}\"{uuid.Serialize()}\"");
                }
                file.Write($"{nl}{idt}{closeArr}");
            }
            file.Write(closeObj);

            IsDirty = false;
        }

        public void Load()
        {
            FileInfo.Refresh();
            if (!FileInfo.Exists)
            {
                IsDirty = RememberedAnchors.Count + IgnoredAnchors.Count > 0;
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

            IsDirty = false;
        }

        public void Clear(bool wipeDisk = true)
        {
            RememberedAnchors.Clear();
            IgnoredAnchors.Clear();
            IsDirty = false;

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
