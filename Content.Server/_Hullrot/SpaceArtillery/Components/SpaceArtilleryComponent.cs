using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server._Hullrot.SpaceArtillery.Components;

[RegisterComponent]
public sealed partial class SpaceArtilleryComponent : Component
{

    /// <summary>
    /// Amount of power being used when operating
    /// </summary>
    [DataField("powerUsePassive"), ViewVariables(VVAccess.ReadWrite)]
    public int PowerUsePassive = 600;

    /// <summary>
    /// Rate of charging the battery
    /// </summary>
    [DataField("powerChargeRate"), ViewVariables(VVAccess.ReadWrite)]
    public int PowerChargeRate = 3000;

    /// <summary>
    /// Amount of power used when firing
    /// </summary>
    [DataField("powerUseActive"), ViewVariables(VVAccess.ReadWrite)]
    public int PowerUseActive = 6000;


    ///Sink Ports
    /// <summary>
    /// Signal port that makes space artillery fire.
    /// </summary>
    [DataField("spaceArtilleryFirePort", customTypeSerializer: typeof(PrototypeIdSerializer<SinkPortPrototype>))]
    public string SpaceArtilleryFirePort = "SpaceArtilleryFire";

    ///Source Ports
    /// <summary>
    /// The port that gets set to high while the alarm is in the danger state, and low when not.
    /// </summary>
    [DataField("spaceArtilleryDetectedFiringPort", customTypeSerializer: typeof(PrototypeIdSerializer<SourcePortPrototype>))]
    public string SpaceArtilleryDetectedFiringPort = "SpaceArtilleryDetectedFiring";

    /// <summary>
    /// The port that gets set to high while the alarm is in the danger state, and low when not.
    /// </summary>
    [DataField("spaceArtilleryDetectedMalfunctionPort", customTypeSerializer: typeof(PrototypeIdSerializer<SourcePortPrototype>))]
    public string SpaceArtilleryDetectedMalfunctionPort = "SpaceArtilleryDetectedMalfunction";
}
