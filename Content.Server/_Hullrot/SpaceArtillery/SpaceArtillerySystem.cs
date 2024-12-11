using System.Numerics;
using Content.Server.DeviceLinking.Events;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._Hullrot.SpaceArtillery;
using Content.Shared.Actions;
using Content.Shared.Camera;
using Content.Shared.CombatMode;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Power;
using Content.Shared.Projectiles;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using SpaceArtilleryComponent = Content.Server._Hullrot.SpaceArtillery.Components.SpaceArtilleryComponent;

namespace Content.Server._Hullrot.SpaceArtillery;

public sealed partial class SpaceArtillerySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] private readonly DeviceLinkSystem _deviceLink = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedShuttleSystem _shuttleSystem = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _recoilSystem = default!;

    private const float DISTANCE = 100;
    private const float BIG_DAMAGE = 1000;
    private const float BIG_DAMGE_KICK = 35;
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("SpaceArtillery");
        SubscribeLocalEvent<SpaceArtilleryComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<SpaceArtilleryComponent, SignalReceivedEvent>(OnSignalReceived);
        SubscribeLocalEvent<SpaceArtilleryComponent, AmmoShotEvent>(OnShotEvent);
        SubscribeLocalEvent<SpaceArtilleryComponent, OnEmptyGunShotEvent>(OnEmptyShotEvent);
        SubscribeLocalEvent<SpaceArtilleryComponent, PowerChangedEvent>(OnApcChanged);
        SubscribeLocalEvent<SpaceArtilleryComponent, ChargeChangedEvent>(OnBatteryChargeChanged);
        SubscribeLocalEvent<SpaceArtilleryComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<ShipWeaponProjectileComponent, ProjectileHitEvent>(OnProjectileHit);

    }

    private void OnComponentInit(EntityUid uid, SpaceArtilleryComponent component, ComponentInit args)
    {
        if (!TryComp<BatteryComponent>(uid, out var battery))
            return;

        component.IsPowered = (battery.CurrentCharge > 0);
    }

    private void OnExamine(EntityUid uid, SpaceArtilleryComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (component.IsArmed == true)
        {
            args.PushMarkup(Loc.GetString("space-artillery-on-examine-safe"));
        }
        else
        {
            args.PushMarkup(Loc.GetString("space-artillery-on-examine-armed"));
        }
    }

    private void OnSignalReceived(EntityUid uid, SpaceArtilleryComponent component, ref SignalReceivedEvent args)
    {
        if (component.IsPowered == true || component.IsPowerRequiredForSignal == false)
        {
            if (args.Port == component.SpaceArtilleryFirePort && component.IsArmed == true)
            {
                if (TryComp<BatteryComponent>(uid, out var battery))
                {
                    if (component.IsPowered == true && battery.CurrentCharge >= component.PowerUseActive || component.IsPowerRequiredToFire == false)
                        TryFireArtillery(uid, component);
                    else
                        OnMalfunction(uid, component);
                }
            }
            if (args.Port == component.SpaceArtilleryToggleSafetyPort)
            {
                if (TryComp<CombatModeComponent>(uid, out var combat))
                {
                    if (combat.IsInCombatMode == false)
                    {
                        _combat.SetInCombatMode(uid, true, combat);
                        component.IsArmed = true;

                        if (component.IsCapableOfSendingSignal == true)
                            _deviceLink.SendSignal(uid, component.SpaceArtilleryDetectedSafetyChangePort, true);
                    }
                    else
                    {
                        _combat.SetInCombatMode(uid, false, combat);
                        component.IsArmed = false;

                        if (component.IsCapableOfSendingSignal == true)
                            _deviceLink.SendSignal(uid, component.SpaceArtilleryDetectedSafetyChangePort, true);
                    }
                }
            }
            if (args.Port == component.SpaceArtilleryOnSafetyPort)
            {
                if (TryComp<CombatModeComponent>(uid, out var combat))
                {
                    if (combat.IsInCombatMode == true && component.IsCapableOfSendingSignal == true)
                        _deviceLink.SendSignal(uid, component.SpaceArtilleryDetectedSafetyChangePort, true);

                    _combat.SetInCombatMode(uid, false, combat);
                    component.IsArmed = false;

                }
            }
            if (args.Port == component.SpaceArtilleryOffSafetyPort)
            {
                if (TryComp<CombatModeComponent>(uid, out var combat))
                {
                    if (combat.IsInCombatMode == false && component.IsCapableOfSendingSignal == true)
                        _deviceLink.SendSignal(uid, component.SpaceArtilleryDetectedSafetyChangePort, true);

                    _combat.SetInCombatMode(uid, true, combat);
                    component.IsArmed = true;
                }
            }
        }
        else
            OnMalfunction(uid, component);
    }


    private void OnApcChanged(EntityUid uid, SpaceArtilleryComponent component, ref PowerChangedEvent args)
    {
        if (TryComp<BatterySelfRechargerComponent>(uid, out var batteryCharger))
        {
            if (args.Powered)
            {
                component.IsCharging = true;
                batteryCharger.AutoRecharge = true;
                batteryCharger.AutoRechargeRate = component.PowerChargeRate;
            }
            else
            {
                component.IsCharging = false;
                batteryCharger.AutoRecharge = true;
                batteryCharger.AutoRechargeRate = component.PowerUsePassive * -1;

                if (TryComp<BatteryComponent>(uid, out var battery))
                    _battery.UseCharge(uid, component.PowerUsePassive, battery); //It is done so that BatterySelfRecharger will get start operating instead of being blocked by fully charged battery
            }
        }
    }


    private void OnBatteryChargeChanged(EntityUid uid, SpaceArtilleryComponent component, ref ChargeChangedEvent args)
    {
        if (args.Charge > 0)
        {
            component.IsPowered = true;
        }
        else
        {
            component.IsPowered = false;
        }

        if (TryComp<ApcPowerReceiverComponent>(uid, out var apcPowerReceiver) && TryComp<BatteryComponent>(uid, out var battery))
        {
            if (battery.IsFullyCharged == false)
            {
                apcPowerReceiver.Load = component.PowerUsePassive + component.PowerChargeRate;
            }
            else
            {
                apcPowerReceiver.Load = component.PowerUsePassive;
            }
        }
    }

    private void TryFireArtillery(EntityUid uid, SpaceArtilleryComponent component)
    {
        var xform = Transform(uid);

        if (!_gun.TryGetGun(uid, out var gunUid, out var gun))
        {
            OnMalfunction(uid, component);
            return;
        }

        var worldPosX = _xform.GetWorldPosition(uid).X;
        var worldPosY = _xform.GetWorldPosition(uid).Y;
        var worldRot = _xform.GetWorldRotation(uid) + Math.PI;
        var targetSpot = new Vector2(worldPosX - DISTANCE * (float) Math.Sin(worldRot), worldPosY + DISTANCE * (float) Math.Cos(worldRot));

        EntityCoordinates targetCordinates;
        targetCordinates = new EntityCoordinates(xform.MapUid!.Value, targetSpot);

        _gun.AttemptShoot(uid, gunUid, gun, targetCordinates);
    }


    ///TODO Fix empty cartridge allowing recoil to be activated
    ///TODO add check for args.FiredProjectiles
    private void OnShotEvent(EntityUid uid, SpaceArtilleryComponent component, AmmoShotEvent args)
    {
        if (args.FiredProjectiles.Count == 0)
        {
            OnMalfunction(uid, component);
            return;
        }

        if (TryComp<BatteryComponent>(uid, out var battery))
        {
            var worldPosX = _xform.GetWorldPosition(uid).X;
            var worldPosY = _xform.GetWorldPosition(uid).Y;
            var worldRot = _xform.GetWorldRotation(uid) + Math.PI;
            var targetSpot = new Vector2(worldPosX - DISTANCE * (float) Math.Sin(worldRot), worldPosY + DISTANCE * (float) Math.Cos(worldRot));

            var xformGridUid = Transform(uid).GridUid;

            if (component.IsCapableOfSendingSignal == true)
                _deviceLink.SendSignal(uid, component.SpaceArtilleryDetectedFiringPort, true);

            if (component.IsPowerRequiredToFire == true)
            {
                _battery.UseCharge(uid, component.PowerUseActive, battery);
            }
        }
    }

    private void OnEmptyShotEvent(EntityUid uid, SpaceArtilleryComponent component, OnEmptyGunShotEvent args)
    {
        OnMalfunction(uid, component);
    }

    private void OnMalfunction(EntityUid uid, SpaceArtilleryComponent component)
    {
        if (component.IsCapableOfSendingSignal == true)
            _deviceLink.SendSignal(uid, component.SpaceArtilleryDetectedMalfunctionPort, true);
    }

    private void OnProjectileHit(EntityUid uid, ShipWeaponProjectileComponent component, ProjectileHitEvent hitEvent)
    {
        var grid = Transform(hitEvent.Target).GridUid;
        if (grid == null)
            return;

        var players = Filter.Empty();
        players.AddInGrid((EntityUid) grid);

        foreach (var player in players.Recipients)
        {
            if (player.AttachedEntity is not EntityUid playerEnt)
                continue;

            var vector = _xform.GetWorldPosition(uid) - _xform.GetWorldPosition(playerEnt);

            _recoilSystem.KickCamera(playerEnt, vector.Normalized() * (float) hitEvent.Damage.GetTotal() / BIG_DAMAGE * BIG_DAMGE_KICK);
        }
    }
}
