using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Server.GameTicking.Events;
using Content.Server.Shuttles;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Hullrot.Shipyard;
using Content.Shared.Access.Components;
using Content.Shared.GameTicking;
using Robust.Server.GameObjects;
using Robust.Server.Maps;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Hullrot;


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
    public MapId ShipyardMap { get; private set; }

    public override void Initialize()
    {
        _sawmill = Logger.GetSawmill("shipyard");
        SubscribeLocalEvent<RoundStartingEvent>(InitializeShipyard);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<ShipyardComponent, EntInsertedIntoContainerMessage>(OnItemSlotChanged);
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

    private void OnItemSlotChanged(EntityUid uid, ShipyardComponent component, ContainerModifiedMessage args)
    {
        if (!TryComp(args.Entity, out ShipVoucherComponent? shipVoucher))
            return;
        if (!TryComp(args.Entity, out IdCardComponent? idCard))
            return;
        if (!TryComp(uid, out TransformComponent? transform))
            return;
        if (!TryGetAndDockShuttle(transform.ParentUid,shipVoucher.ShuttlePath.ToString(), out var shuttle, out var dockingConfig))
            return;
    }

    /// <summary>
    /// Adds a ship to the shipyard and attempts to ftl-dock it to the given station
    /// </summary>
    /// <param name="stationUid">The ID of the station to dock the shuttle to</param>
    /// <param name="shuttlePath">The path to the shuttle file to load. Must be a grid file!</param>

    public bool TryGetAndDockShuttle(EntityUid stationUid, string shuttlePath, [NotNullWhen(true)] out ShuttleComponent? shuttle, out DockingConfig? config)
    {
        config = null;
        if (!TryComp<StationDataComponent>(stationUid, out var stationData) || !TryAddShuttle(shuttlePath, out var shuttleGrid) || !TryComp<ShuttleComponent>(shuttleGrid, out shuttle))
        {
            shuttle = null;
            return false;
        }

        var targetGrid = _station.GetLargestGrid(stationData);

        if (targetGrid == null)
        {
            Del(shuttleGrid);
            shuttle = null;
            return false;
        }

        _sawmill.Info($"Shuttle {shuttlePath} was spawned at {ToPrettyString((EntityUid) stationUid)}");
        _shuttle.TryFTLDock(shuttleGrid.Value, shuttle, targetGrid.Value);

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



}

