using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Hullrot.Shipyard;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedShipyardSystem))]
public sealed partial class ShipyardComponent : Component
{
    public static string TargetIdCardSlotId = "Shipyard-targetId";
    public static string TargetLPCSlotId = "Shipyard-targetLPC";

    [DataField("targetIdSlot")]
    public ItemSlot TargetIdSlot = new();

    [DataField("targetLPCSlot")]
    public ItemSlot TargetLPCSlot = new();

    [DataField("soundError")]
    public SoundSpecifier ErrorSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    [DataField("soundConfirm")]
    public SoundSpecifier ConfirmSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

}
