using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Hullrot.Shipyard;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedShipyardSystem))]
public sealed partial class ShipyardComponent : Component
{
    public static string TargetDeedCardSlotId = "Shipyard-TargetDeed";
    public static string TargetLPCSlotId = "Shipyard-TargetLPC";

    [DataField("TargetDeedSlot")]
    public ItemSlot TargetDeedSlot = new();

    [DataField("TargetLPCSlot")]
    public ItemSlot TargetLPCSlot = new();

    [DataField("soundError")]
    public SoundSpecifier ErrorSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    [DataField("soundConfirm")]
    public SoundSpecifier ConfirmSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

}


[Serializable, NetSerializable]
public enum ShipyardUiKey : byte
{
    Key
}
