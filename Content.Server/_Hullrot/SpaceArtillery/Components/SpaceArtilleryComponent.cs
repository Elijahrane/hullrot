using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server._Hullrot.SpaceArtillery.Components;

[RegisterComponent]
public sealed partial class SpaceArtilleryComponent : Component
{
    /// <summary>
    /// Whether the space artillery's safety is enabled or not
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool IsArmed = false;

    /// <summary>
    /// Whether the space artillery has enough power
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool IsPowered = false;

    /// <summary>
    /// Whether the space artillery's battery is being charged
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool IsCharging = false;

    /// <summary>
    /// Rate of charging the battery
    /// </summary>
    [DataField("powerChargeRate"), ViewVariables(VVAccess.ReadWrite)]
    public int PowerChargeRate = 3000;

    /// <summary>
    /// Whether the space artillery requires whole vessel to activate its armaments,
    /// Use these for any armaments with high destructive capability
    /// </summary>
    [DataField("isDestructive"), ViewVariables(VVAccess.ReadWrite)]
    public bool IsDestructive = true;

    /// <summary>
    /// Whether the space artillery can send signals at all
    /// </summary>
    [DataField("isCapableOfSendingSignal"), ViewVariables(VVAccess.ReadWrite)]
    public bool IsCapableOfSendingSignal = true;

    /// <summary>
    /// Whether the space artillery need power to operate remotely from signal
    /// </summary>
    [DataField("isPowerRequiredForSignal"), ViewVariables(VVAccess.ReadWrite)]
    public bool IsPowerRequiredForSignal = true;

    /// <summary>
    /// Amount of power being used when operating
    /// </summary>
    [DataField("powerUsePassive"), ViewVariables(VVAccess.ReadWrite)]
    public int PowerUsePassive = 600;

    /// <summary>
    /// Whether the space artillery needs power to fire a shot
    /// </summary>
    [DataField("isPowerRequiredToFire"), ViewVariables(VVAccess.ReadWrite)]
    public bool IsPowerRequiredToFire = false;

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

    /// <summary>
    /// Signal port that toggles artillery's safety, which is the combat mode
    /// </summary>
    [DataField("spaceArtilleryToggleSafetyPort", customTypeSerializer: typeof(PrototypeIdSerializer<SinkPortPrototype>))]
    public string SpaceArtilleryToggleSafetyPort = "SpaceArtilleryToggleSafety";

    /// <summary>
    /// Signal port that sets artillery's safety to "SAFE"
    /// </summary>
    [DataField("spaceArtilleryOnSafetyPort", customTypeSerializer: typeof(PrototypeIdSerializer<SinkPortPrototype>))]
    public string SpaceArtilleryOnSafetyPort = "SpaceArtilleryOnSafety";

    /// <summary>
    /// Signal port that sets artillery's safety to "ARMED"
    /// </summary>
    [DataField("spaceArtilleryOffSafetyPort", customTypeSerializer: typeof(PrototypeIdSerializer<SinkPortPrototype>))]
    public string SpaceArtilleryOffSafetyPort = "SpaceArtilleryOffSafety";

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

    /// <summary>
    /// The port that gets set to high while the alarm is in the danger state, and low when not.
    /// </summary>
    [DataField("spaceArtilleryDetectedSafetyChangePort", customTypeSerializer: typeof(PrototypeIdSerializer<SourcePortPrototype>))]
    public string SpaceArtilleryDetectedSafetyChangePort = "SpaceArtilleryDetectedSafetyChange";


    /// <summary>
    /// The action for the weapon (if any)
    /// </summary>
    [DataField("fireActionEntity")]
    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? FireActionEntity;

}
