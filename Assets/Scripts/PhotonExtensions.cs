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
            box is not long rawUid)
        {
            return false;
        }

        uid = new Reinterpret64 { Signed = rawUid }.Unsigned;

        return uid > 0;
    }

    public static void SetPlatformID(this Player photonPlayer, ulong uid)
    {
        var props = new Hashtable
        {
            // PUN weirdly can't transmit unsigned integers, even though they ZigZag signed ints into unsigned internally...
            [k_PlatIDKey] = new Reinterpret64 { Unsigned = uid }.Signed,
        };

        if (photonPlayer.SetCustomProperties(props))
            return;

        Sampleton.LogError($"Photon ERR: failed to set {photonPlayer.NickName}.CustomProperties[{k_PlatIDKey}] = {uid}");
    }


    //
    // impl. details

    const string k_PlatIDKey = "ocid";


    static ulong EnZigZag64(long signed)
        => (ulong)((signed << 1) ^ (signed >> 63));

    static long DeZigZag64(ulong unsigned)
        => (long)((unsigned >> 1) ^ (0L - (unsigned & 1)));

    [StructLayout(LayoutKind.Explicit)]
    struct Reinterpret64
    {
        [FieldOffset(0)]
        public long Signed;
        [FieldOffset(0)]
        public ulong Unsigned;
    }


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RegisterAdditionalTypeSerialization()
    {
        Sampleton.Log($"AfterSceneLoad: {nameof(PhotonExtensions)}::{nameof(RegisterAdditionalTypeSerialization)}");

        if (!Protocol.TryRegisterType(typeof(Guid), (byte)'G', guidWrite, guidRead))
            Sampleton.LogError($"Photon ERR: failed to register {nameof(Guid)} serde");

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
    }

} // end static class PhotonExtensions
