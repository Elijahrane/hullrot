/// Copyright Rane (elijahrane@gmail.com) 2025
/// All rights reserved.

namespace Content.Server._Hullrot.FireControl;

[RegisterComponent]
public sealed partial class FireControlServerComponent : Component
{
    [ViewVariables]
    public EntityUid? ConnectedGrid = null;

    [ViewVariables]
    public HashSet<EntityUid> Controlled = new();

    [ViewVariables]
    public HashSet<EntityUid> Consoles = new();

    [ViewVariables]
    public Dictionary<EntityUid, EntityUid> Leases;
}
