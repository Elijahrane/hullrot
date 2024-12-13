using Robust.Shared.Serialization;

namespace Content.Shared._Hullrot.Logistics;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class LogisticPipeComponent : Component
{
    
}

[Flags]
[Serializable, NetSerializable]
public enum PipeDirection
{
    None = 0,

    //Half of a pipe in a direction
    North = 1 << 0,
    South = 1 << 1,
    West = 1 << 2,
    East = 1 << 3,
    
    All = -1,
}

