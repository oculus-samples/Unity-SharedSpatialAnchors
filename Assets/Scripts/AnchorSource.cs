// Copyright (c) Meta Platforms, Inc. and affiliates.
// This code is licensed under the MIT license (see LICENSE for details).

using Guid = System.Guid;

/// <summary>
///   Utility struct for storing write-once info regarding where an instantiated anchor came from.
/// </summary>
/// <remarks>
///   We track where our anchors came from *mostly* for illustration purposes.
///   We do use properties such as <see cref="IsMine"/> to make certain non-cosmetic decisions in this sample, but your
///   app is likely to be structured MUCH less generically, so your code can make more fenced guarantees with itself and
///   be able to safely assume more about state.
/// </remarks>
public readonly struct AnchorSource
{
    public static AnchorSource New(ulong creatorHandle, Guid anchorId)
    {
        return new AnchorSource(Type.New, anchorId, creatorHandle, true);
    }

    public static AnchorSource FromSave(Guid savedAnchorId, ulong creatorHandle = 0)
    {
        return new AnchorSource(Type.FromSave, savedAnchorId, creatorHandle, creatorHandle > 0);
    }

    public static AnchorSource FromSpaceUserShare(Guid sharedAnchorId)
    {
        return new AnchorSource(Type.FromSpaceUserShare, sharedAnchorId, 0, false);
    }

    public static AnchorSource FromGroupShare(Guid groupId)
    {
        return new AnchorSource(Type.FromGroupShare, groupId, 0, false);
    }


    public enum Type
    {
        New,
        FromSave,
        FromSpaceUserShare,
        FromGroupShare,
    }

    public readonly bool IsSet;
    public readonly Type Origin;
    public readonly Guid Uuid;
    public readonly ulong Handle;
    public readonly bool IsMine; // TODO AnchorSource.IsMine is not fully reliable for colocation group sharing (it is more reliable in scene "Sharing to Users")


    public override string ToString()
    {
        if (!IsSet)
            return "(unknown)";
        if (IsMine)
            return $"{Origin}(Mine)";
        if (Uuid != Guid.Empty)
            return $"{Origin}({Uuid.Brief()})";
        return $"{Origin}(null)";
    }


    // The idea behind the following helpers is that the out-params document which fields should be valid in each
    // true-returning case.

    public bool IsNew(out ulong creatorHandle)
    {
        creatorHandle = Handle;
        return IsSet && Origin == Type.New && Handle != 0;
    }

    public bool IsNew(out Guid anchorId)
    {
        anchorId = Uuid;
        return IsSet && Origin == Type.New && Uuid != Guid.Empty;
    }

    public bool IsFromSave(out Guid savedAnchorId)
    {
        savedAnchorId = Uuid;
        return IsSet && Origin == Type.FromSave && Uuid != Guid.Empty;
    }

    public bool IsShared(out Guid sharedAnchorId)
    {
        sharedAnchorId = Uuid;
        if (!IsSet || sharedAnchorId == Guid.Empty)
            return false;

        switch (Origin)
        {
            case Type.FromGroupShare:
            case Type.FromSpaceUserShare:
                return true;
            case Type.FromSave:
                return !IsMine;
            case Type.New:
                break; // TODO need to unify the sample implementations so that we have an authority on this kind of flag
        }
        return false;
    }

    public bool IsFromSpaceUserShare(out Guid sharedAnchorId)
    {
        sharedAnchorId = Uuid;
        return IsSet && Origin == Type.FromSpaceUserShare && Uuid != Guid.Empty;
    }

    public bool IsFromGroupShare(out Guid groupId)
    {
        groupId = Uuid;
        return IsSet && Origin == Type.FromGroupShare && Uuid != Guid.Empty;
    }


    //
    // private impl.

    AnchorSource(Type t, Guid g, ulong u, bool m)
    {
        IsSet = true;
        Origin = t;
        Uuid = g;
        Handle = u;
        IsMine = m;
    }

} // end struct AnchorSource
