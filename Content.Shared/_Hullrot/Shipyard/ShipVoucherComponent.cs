using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._Hullrot.Shipyard;

[RegisterComponent,NetworkedComponent]
public sealed partial class ShipVoucherComponent : Component
{
    /// <summary>
    ///     Vessel name.
    /// </summary>
    [DataField("name")]
    public string Name;

    [DataField("suffix")]
    public string NameSuffix;

    /// <summary>
    ///     Short description of the vessel.
    /// </summary>
    [DataField("description")]
    public string Description;

    /// <summary>
    ///     Relative directory path to the given shuttle, i.e. `/Maps/Shuttles/yourshittle.yml`
    /// </summary>
    [DataField("shuttlePath")]
    public ResPath ShuttlePath;

}
