/// Copyright Rane (elijahrane@gmail.com) 2025
/// All rights reserved.

using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Power;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Map;

namespace Content.Server._Hullrot.FireControl;

public sealed partial class FireControlSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly GunSystem _gun = default!;


    /// My fire control system replaces the functionality of point cannons.
    /// I'll keep my notes here on how it works.
    /// 1) Ships have a central fire control server that keeps track of the available remote weapons (turrets, drones, etc)
    /// 2) The server leases control of these weapons to targeting consoles, etc, allowing multiple targeting consoles on a ship
    /// 3) Targeting consoles generally have RTS style selection controls in addition to tags
    /// 4) Control groups from that are not diegetic and lets players personalize them

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FireControlServerComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<FireControlServerComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<FireControllableComponent, PowerChangedEvent>(OnControllablePowerChanged);
        SubscribeLocalEvent<FireControllableComponent, ComponentShutdown>(OnControllableShutdown);

        InitializeConsole();
    }

    private void OnPowerChanged(EntityUid uid, FireControlServerComponent component, PowerChangedEvent args)
    {
        if (args.Powered)
            TryConnect(uid, component);
        else
            Disconnect(uid, component);
    }

    private void OnShutdown(EntityUid uid, FireControlServerComponent component, ComponentShutdown args)
    {
        Disconnect(uid, component);
    }

    private void OnControllablePowerChanged(EntityUid uid, FireControllableComponent component, PowerChangedEvent args)
    {
        if (args.Powered)
            TryRegister(uid, component);
        else
            Unregister(uid, component);
    }

    private void OnControllableShutdown(EntityUid uid, FireControllableComponent component, ComponentShutdown args)
    {
        Unregister(uid, component);
    }

    private void Disconnect(EntityUid server, FireControlServerComponent? component = null)
    {
        if (!Resolve(server, ref component))
            return;

        if (!Exists(component.ConnectedGrid) || !TryComp<FireControlGridComponent>(component.ConnectedGrid, out var controlGrid))
            return;

        if (controlGrid.ControllingServer == server)
        {
            controlGrid.ControllingServer = null;
            RemComp<FireControlGridComponent>((EntityUid)component.ConnectedGrid);
        }

        foreach (var controllable in(component.Controlled))
            Unregister(controllable);

        foreach (var console in component.Consoles)
            UnregisterConsole(console);
    }

    public void RefreshControllables(EntityUid grid, FireControlGridComponent? component = null)
    {
        if (!Resolve(grid, ref component))
            return;

        if (component.ControllingServer == null || !TryComp<FireControlServerComponent>(component.ControllingServer, out var server))
            return;

        var query = EntityQueryEnumerator<FireControllableComponent>();

        while (query.MoveNext(out var controllable, out var controlComp))
        {
            if (_xform.GetGrid(controllable) == grid)
                TryRegister(controllable, controlComp);
        }

        foreach (var console in server.Consoles)
            UpdateUi(console);
    }

    private bool TryConnect(EntityUid server, FireControlServerComponent? component = null)
    {
        if (!Resolve(server, ref component))
            return false;

        var grid = _xform.GetGrid(server);

        if (grid == null)
            return false;

        var controlGrid = EnsureComp<FireControlGridComponent>((EntityUid)grid);

        if (controlGrid.ControllingServer != null)
            return false;

        controlGrid.ControllingServer = server;
        component.ConnectedGrid = grid;

        RefreshControllables((EntityUid)grid, controlGrid);

        return true;
    }

    private void Unregister(EntityUid controllable, FireControllableComponent? component = null)
    {
        if (!Resolve(controllable, ref component))
            return;

        if (component.ControllingServer == null || !TryComp<FireControlServerComponent>(component.ControllingServer, out var controlComp))
            return;

        controlComp.Controlled.Remove(controllable);
        component.ControllingServer = null;
    }

    private bool TryRegister(EntityUid controllable, FireControllableComponent? component = null)
    {
        if (!Resolve(controllable, ref component))
            return false;

        var gridServer = TryGetGridServer(controllable);

        if (gridServer.ServerComponent == null)
            return false;

        if (gridServer.ServerComponent.Controlled.Add(controllable))
        {
            component.ControllingServer = gridServer.ServerUid;
            return true;
        }
        else
        {
            return false;
        }

    }

    private (EntityUid? ServerUid, FireControlServerComponent? ServerComponent) TryGetGridServer(EntityUid uid)
    {
        var grid = _xform.GetGrid(uid);

        if (grid == null)
            return (null, null);

        if (!TryComp<FireControlGridComponent>(grid, out var controlGrid))
            return (null, null);

        if (controlGrid.ControllingServer == null || !TryComp<FireControlServerComponent>(controlGrid.ControllingServer, out var server))
            return (null, null);

        return (controlGrid.ControllingServer, server);
    }

    public void FireWeapons(EntityUid server, List<NetEntity> weapons, NetCoordinates coordinates, FireControlServerComponent? component = null)
    {
        if (!Resolve(server, ref component))
            return;

        foreach (var weapon in weapons)
        {
            var localWeapon = GetEntity(weapon);
            if (!component.Controlled.Contains(localWeapon))
                continue;

            if (!TryComp<GunComponent>(localWeapon, out var gun))
                continue;

            _gun.AttemptShoot(localWeapon, localWeapon, gun, GetCoordinates(coordinates));
        }
    }
}

/// <summary>
/// Server queries entities, they return their e.g. fire control tags, ammo status, etc
/// </summary>
public sealed class FireControllableStatusReportEvent : EntityEventArgs
{
    public List<(string type, string content)> StatusReports = new();
}
