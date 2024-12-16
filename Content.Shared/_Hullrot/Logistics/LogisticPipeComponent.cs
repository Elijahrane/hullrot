using Robust.Shared.Serialization;

namespace Content.Shared._Hullrot.Logistics;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class LogisticPipeComponent : Component
{
    public DirectionFlag connectionDirs =
        DirectionFlag.East | DirectionFlag.West | DirectionFlag.North | DirectionFlag.South;
}


