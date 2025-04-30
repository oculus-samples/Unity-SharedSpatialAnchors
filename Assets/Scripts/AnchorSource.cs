// Copyright (c) Meta Platforms, Inc. and affiliates.

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
    public static AnchorSource New(Guid anchorId)
    {
        return new AnchorSource(Type.New, anchorId, m: true);
    }

    public static AnchorSource FromSave(Guid savedAnchorId, bool isMine = true)
    {
        return new AnchorSource(Type.FromSave, savedAnchorId, m: isMine);
    }

    public static AnchorSource FromSpaceUserShare(Guid sharedAnchorId, bool isMine = false)
    {
        return new AnchorSource(Type.FromSpaceUserShare, sharedAnchorId, m: isMine);
    }

    public static AnchorSource FromGroupShare(Guid groupId, bool isMine = false)
    {
        return new AnchorSource(Type.FromGroupShare, groupId, m: isMine);
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
    public readonly bool IsMine;


    public override string ToString()
    {
        if (!IsSet)
            return "(unknown)";
        string origin =
            Origin == Type.FromGroupShare ? $"{Origin}[{Uuid.Brief()}]"
                                          : $"{Origin}";
        return IsMine ? $"{origin}(Mine)" : origin;
    }


    //
    // private impl.

    AnchorSource(Type t, Guid g, bool m)
    {
        IsSet = true;
        Origin = t;
        Uuid = g;
        IsMine = m;
    }

} // end struct AnchorSource
