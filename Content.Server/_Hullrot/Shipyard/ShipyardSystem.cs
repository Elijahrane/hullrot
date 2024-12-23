using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Cargo.Systems;
using Content.Server.GameTicking.Events;
using Content.Server.Mind;
using Content.Server.Shuttles;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Hullrot.Shipyard;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Server.Maps;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.Server._Hullrot.Shipyard;


/// <summary>
/// This handles...
/// </summary>
public sealed class ShipyardSystem : SharedShipyardSystem
{
    private ISawmill _sawmill = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly SharedMapSystem _mapping = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MetaDataSystem _metaSystem = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly DockingSystem _docking = default!;
    public MapId ShipyardMap { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("shipyard");
        SubscribeLocalEvent<RoundStartingEvent>(InitializeShipyard);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<ShipyardComponent, EntInsertedIntoContainerMessage>(OnItemSlotChanged);
        SubscribeLocalEvent<ShipyardComponent, ShipyardSellMessage>(OnSellMessage);
        SubscribeLocalEvent<ShipyardComponent, ShipyardBuyMessage>(OnBuyMessage);
    }


    private void InitializeShipyard(RoundStartingEvent round)
    {
        var mapnode = _mapping.CreateMap();
        if (!TryComp(mapnode, out MapComponent? map))
            return;
        _metaSystem.SetEntityName(mapnode, "Shipspawningdimension"); //naming it so its identifiable
        ShipyardMap = map.MapId;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        if (!_mapManager.MapExists(ShipyardMap))
            return;

        _mapManager.DeleteMap(ShipyardMap);
    }

    public void OnSellMessage(EntityUid uid, ShipyardComponent component, ShipyardSellMessage args)
    {
        if (!TryComp(component.TargetDeedSlot.Item, out ShuttleDeedComponent? deedComponent))
            return;
        if (!TryComp(uid, out TransformComponent? transform))
            return;
        if (!TryComp(transform.ParentUid, out StationMemberComponent? stationmember))
            return;
        if (deedComponent.ShuttleUid == null)
            return;
        if (!TrySellShuttle(transform.ParentUid, deedComponent.ShuttleUid.Value, out var sale))
            return;
        _entityManager.DeleteEntity(component.TargetDeedSlot.Item);
    }

    public void OnBuyMessage(EntityUid uid, ShipyardComponent component, ShipyardBuyMessage args)
    {
        if (!TryComp(component.TargetLPCSlot.Item, out ShipVoucherComponent? shipVoucher))
            return;
        if (!TryComp(uid, out TransformComponent? transform))
            return;
        if (!TryComp(transform.ParentUid, out StationMemberComponent? stationmember))
            return;
        if (!TryGetAndDockShuttle(stationmember.Station,shipVoucher.ShuttlePath.ToString(), out var shuttle, out var dockingConfig))
            return;

        SpawnAndPopulateDeed(transform, shuttle, shipVoucher);

        _entityManager.DeleteEntity(component.TargetLPCSlot.Item);
    }

    private void OnItemSlotChanged(EntityUid uid, ShipyardComponent component, ContainerModifiedMessage args)
    {
        TryComp(component.TargetLPCSlot.Item, out ShipVoucherComponent? shipVoucher);
        TryComp(component.TargetDeedSlot.Item, out ShuttleDeedComponent? deedComponent);

    }


    /// <summary>
    /// Checks a shuttle to make sure that it is docked to the given station, and that there are no lifeforms aboard. Then it appraises the grid, outputs to the server log, and deletes the grid
    /// </summary>
    /// <param name="stationUid">The ID of the station that the shuttle is docked to</param>
    /// <param name="shuttleUid">The grid ID of the shuttle to be appraised and sold</param>
    public bool TrySellShuttle(EntityUid stationUid, EntityUid shuttleUid, out int sale)
    {
        sale = 0;
        var shuttleconnections = _docking.GetDocks(shuttleUid);
        var stationconnections = _docking.GetDocks(stationUid);

        if (shuttleconnections.Count > 1)
        {
            _sawmill.Warning($"shuttle is docked to multiple grids");
            return false;
        }

        var match = (from shuttle in shuttleconnections
            from station in stationconnections
            where shuttle.Comp.DockedWith == station.Owner
            select new { Shuttle = shuttle, Station = station }).FirstOrDefault();
        if (match == null)
        {
            _sawmill.Warning($"shuttle is not docked with this station");
            return false;
        }

        var mobQuery = GetEntityQuery<MobStateComponent>();
        var xformQuery = GetEntityQuery<TransformComponent>();

        if (FoundOrganics(shuttleUid, mobQuery, xformQuery))
        {
            _sawmill.Warning($"organics on board");
            return false;
        }

        sale = (int) _pricing.AppraiseGrid(shuttleUid);
        _entityManager.DeleteEntity(shuttleUid);
        _sawmill.Info($"Sold shuttle {shuttleUid} for {sale}");
        return true;
    }


    /// <summary>
    /// Adds a ship to the shipyard and attempts to ftl-dock it to the given station
    /// </summary>
    /// <param name="stationUid">The ID of the station to dock the shuttle to</param>
    /// <param name="shuttlePath">The path to the shuttle file to load. Must be a grid file!</param>
    public bool TryGetAndDockShuttle(EntityUid stationUid,
        string shuttlePath,
        [NotNullWhen(true)] out EntityUid? shuttlegrid,
        out DockingConfig? config)
    {
        config = null;
        if (!TryComp<StationDataComponent>(stationUid, out var stationData) ||
            !TryAddShuttle(shuttlePath, out shuttlegrid) ||
            !TryComp<ShuttleComponent>(shuttlegrid, out var shuttlecomp))
        {
            shuttlegrid = null;
            return false;
        }

        var targetGrid = _station.GetLargestGrid(stationData);

        if (targetGrid == null)
        {
            Del(shuttlegrid);
            shuttlegrid = null;
            return false;
        }

        _sawmill.Info($"Shuttle {shuttlePath} was spawned at {ToPrettyString((EntityUid)stationUid)}");
        if (!_shuttle.TryFTLDock(shuttlegrid.Value, shuttlecomp, targetGrid.Value))
        {
            shuttlegrid = null;
            return false;
        }


        return true;


    }


    /// <summary>
    /// Loads a shuttle into the ShipyardMap from a file path
    /// </summary>
    /// <param name="shuttlePath">The path to the grid file to load. Must be a grid file!</param>
    /// <param name="shuttleGrid">The UID of the spawned grid</param>
    /// <returns>Returns the EntityUid of the shuttle</returns>
    private bool TryAddShuttle(string shuttlePath, out EntityUid? shuttleGrid)
    {
        shuttleGrid = null;
        var loadOptions = new MapLoadOptions();

        if (!_map.TryLoad(ShipyardMap, shuttlePath, out var grids, loadOptions))
            return false;

        if (grids.Count == 1)
        {
            shuttleGrid = grids[0];

            return true;
        }

        foreach (var grid in grids) //too many grids
        {
            _entityManager.DeleteEntity(grid);
        }

        return false;
    }

    private void SpawnAndPopulateDeed(TransformComponent spawnertransform, EntityUid? shuttle, ShipVoucherComponent? shipVoucher)
    {
        var spawnedDeed = _entityManager.SpawnEntity("ShuttleOwnershipChip", spawnertransform.Coordinates);
        var deedComponent = EnsureComp<ShuttleDeedComponent>(spawnedDeed);
        if (shipVoucher is null || shuttle is null)
            return;
        if (!TryRenameShuttle(shuttle, shipVoucher.Name, shipVoucher.NameSuffix))
            return;
        deedComponent.ShuttleUid = shuttle;
        deedComponent.ShuttleName = shipVoucher.Name;
        deedComponent.ShuttleNameSuffix = shipVoucher.NameSuffix;
    }

    private bool TryRenameShuttle(EntityUid? shuttle, string name, string prefix)
    {
            if (shuttle is null)
                return false;
            _metaSystem.SetEntityName(shuttle.Value, string.Join(' ', prefix, name));
            return true;
    }

    public bool FoundOrganics(EntityUid uid, EntityQuery<MobStateComponent> mobQuery, EntityQuery<TransformComponent> xformQuery)
    {
        var xform = xformQuery.GetComponent(uid);
        var childEnumerator = xform.ChildEnumerator;

        while (childEnumerator.MoveNext(out var child))
        {
            if (mobQuery.TryGetComponent(child, out var mobState)
                && !_mobState.IsDead(child, mobState)
                && _mind.TryGetMind(child, out var mind, out var mindComp)
                && !_mind.IsCharacterDeadIc(mindComp)
                || FoundOrganics(child, mobQuery, xformQuery))
                return true;
        }

        return false;
    }

}

