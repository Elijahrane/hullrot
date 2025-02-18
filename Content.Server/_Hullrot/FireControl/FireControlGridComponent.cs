/// Copyright Rane (elijahrane@gmail.com) 2025
/// All rights reserved.

namespace Content.Server._Hullrot.FireControl;

[RegisterComponent]
public sealed partial class FireControlGridComponent : Component
{
    [ViewVariables]
    public EntityUid? ControllingServer = null;
}
