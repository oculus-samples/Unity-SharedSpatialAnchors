// Copyright (c) Meta Platforms, Inc. and affiliates.

using ExitGames.Client.Photon;

using Photon.Pun;
using Photon.Realtime;

using System;
using System.Runtime.InteropServices;

using UnityEngine;

using Sampleton = SampleController;


public static class PhotonExtensions
{

    public static bool TryGetPlatformID(this Player photonPlayer, out ulong uid)
    {
        uid = 0;
        if (photonPlayer is null ||
            !photonPlayer.CustomProperties.TryGetValue(k_PlatIDKey, out var box) ||
            box is not ulong rawUid)
        {
            return false;
        }

        uid = rawUid;

        return uid > 0;
    }

    public static void SetPlatformID(this Player photonPlayer, ulong uid)
    {
        var props = new Hashtable
        {
            [k_PlatIDKey] = uid,
        };

        if (photonPlayer.SetCustomProperties(props))
            return;

        Sampleton.LogError($"Photon ERR: failed to set {photonPlayer.NickName}.CustomProperties[{k_PlatIDKey}] = {uid}");
    }


    //
    // impl. details

    const string k_PlatIDKey = "ocid";


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RegisterAdditionalTypeSerialization()
    {
        Sampleton.Log($"AfterSceneLoad: {nameof(PhotonExtensions)}::{nameof(RegisterAdditionalTypeSerialization)}");

        if (!Protocol.TryRegisterType(typeof(Guid), (byte)'G', guidWrite, guidRead))
            Sampleton.LogError($"Photon ERR: failed to register {nameof(Guid)} serde");

        // PUN weirdly can't transmit unsigned integers, even though they ZigZag signed ints into unsigned internally...
        if (!Protocol.TryRegisterType(typeof(ulong), (byte)'U', ulongWrite, ulongRead))
            Sampleton.LogError($"Photon ERR: failed to register {nameof(UInt64)} serde");

        // default fallback username
        PhotonNetwork.NickName = $"Anon{UnityEngine.Random.Range(0, 10000):0000}";

        return;

        static byte[] guidWrite(object box)
        {
            if (box is Guid guid)
                return guid.ToByteArray();
            return new byte[16];
        }

        static object guidRead(byte[] bytes)
        {
            if (bytes?.Length == 16)
                return new Guid(bytes);
            return Guid.Empty;
        }

        static byte[] ulongWrite(object box)
        {
            if (box is ulong ul)
                return BitConverter.GetBytes(ul);
            if (box is long l)
                return BitConverter.GetBytes(l);
            return new byte[8];
        }

        static object ulongRead(byte[] bytes)
        {
            if (bytes?.Length == 8)
                return BitConverter.ToUInt64(bytes);
            return 0UL;
        }
    }

} // end static class PhotonExtensions
