using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._Hullrot.Radar;

/// These are messages for exchanging information about radar signatures
/// between the client and server. See the Server's RadarBlipSystem and
/// the Client's ShuttleNavControl.

[Serializable, NetSerializable]
public sealed class GiveBlipsEvent : EntityEventArgs
{
    /// <summary>
    /// Blips are a position, a scale, and a color.
    /// </summary>
    public readonly List<(Vector2, float, Color)> Blips;
    public GiveBlipsEvent(List<(Vector2, float, Color)> blips)
    {
        Blips = blips;
    }
}

[Serializable, NetSerializable]
public sealed class RequestBlipsEvent : EntityEventArgs
{
    public NetEntity Radar;
    public RequestBlipsEvent(NetEntity radar)
    {
        Radar = radar;
    }
}
