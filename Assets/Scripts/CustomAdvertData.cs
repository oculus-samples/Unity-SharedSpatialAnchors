// Copyright (c) Meta Platforms, Inc. and affiliates.
// This code is licensed under the MIT license (see LICENSE for details).

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.Assertions;

using ByteSpan = System.Span<byte>;
using ROByteSpan = System.ReadOnlySpan<byte>;

using Sampleton = SampleController; // only transitional


/// <summary>
///  Stores & serializes data intended for calls to <see cref="OVRColocationSession.StartAdvertisementAsync"/>.
/// </summary>
public sealed class CustomAdvertData
{
    public string DisplayName => m_DisplayName ?? "<err>";

    // note: the fact that this struct stores users and anchors is an artifact of my earlier misunderstanding
    //       of the whole point of this custom data. I'm keeping it, however, as a dummy data to test transmission.
    // UPDATE: This actually turned out to be quite fortuitous; now we have a way to transmit (without photon) these IDs
    //         for the express purpose of comparing the new APIs and the baseline SSA APIs.
    public readonly HashSet<ulong> Users = new();
    public readonly HashSet<Guid> Anchors = new();
    public readonly List<Pose> Poses = new();



    public CustomAdvertData(string displayName, ulong creator, IEnumerable<Guid> anchors)
    {
        SetDisplayName(displayName);

        if (creator != 0)
            Users.Add(creator);
        if (anchors != null)
            Anchors.UnionWith(anchors);
    }


    public bool TryWrite(out byte[] bytes)
    {
        bytes = Array.Empty<byte>();

        try
        {
            var pod = new POD
            {
                DisplayName = DisplayName,
                Users = Users.ToArray(),
                Anchors = Anchors.Select(guid => guid.ToString("N")).ToArray(),
                Poses = Poses.ToArray(),
            };

            bytes = SampleExtensions.SerializeToByteArray(pod);

            return bytes.Length <= k_MaxDataLength;
        }
        catch (Exception e)
        {
            Sampleton.LogError($"{e.GetType().Name}: {e.Message}");
            Debug.LogException(e);
            return false;
        }
    }

    public static bool TryRead(in byte[] bytes, out CustomAdvertData data)
    {
        data = new CustomAdvertData();

        if (bytes is null || bytes.Length < 2) // "{}".Length
            return false;

        try
        {
            // return data.TryReadThrows(in bytes);

            var pod = SampleExtensions.DeserializeFromByteArray<POD>(bytes);

            data.m_DisplayName = pod.DisplayName;
            if (pod.Users is not null)
                data.Users.UnionWith(pod.Users);
            if (pod.Anchors is not null)
                data.Anchors.UnionWith(pod.Anchors.Select(Guid.Parse));
            if (pod.Poses is not null)
                data.Poses.AddRange(pod.Poses);
            return true;
        }
        catch (Exception e)
        {
            Sampleton.LogError($"{e.GetType().Name}: {e.Message}");
            Debug.LogException(e);
            return false;
        }
    }


    //
    // private section

    [Serializable]
    struct POD
    {
        public string DisplayName;
        public ulong[] Users;
        public string[] Anchors;
        public Pose[] Poses;
    }

    const int k_MaxDisplayName = 60;              // somewhat arbitrary
    const int k_MaxDataLength = 1024;             // from OVRColocationSession.cs, but it's private soo copied
    const int k_BlockHeaderSize = sizeof(ushort); // won't need more than ~65k items of any kind (inc. chars)

    static readonly char[] s_StrBuffer = new char[k_MaxDisplayName];


    // instance
    string m_DisplayName;


    internal void SetDisplayName(string displayName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            m_DisplayName = "<anonymous>";
        }
        else if (displayName.Length > k_MaxDisplayName)
        {
            m_DisplayName = displayName.Remove(k_MaxDisplayName);
            Sampleton.Log($"* Group display name \"{displayName}\" is too long; truncating to \"{m_DisplayName}\"", LogType.Warning);
        }
        else
        {
            m_DisplayName = displayName;
        }
    }


    CustomAdvertData()
    {
    }

} // end struct CustomAdvertData
