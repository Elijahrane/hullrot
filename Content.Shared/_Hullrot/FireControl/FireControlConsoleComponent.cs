
using Robust.Shared.GameStates;


/// Copyright Rane (elijahrane@gmail.com) 2025
/// All rights reserved.
namespace Content.Shared._Hullrot.FireControl;

/// <summary>
/// These are for the consoles that provide the user interface for fire control servers.
/// </summary>
[RegisterComponent]
public sealed partial class FireControlConsoleComponent : Component
{
    [ViewVariables]
    public EntityUid? ConnectedServer = null;
}
