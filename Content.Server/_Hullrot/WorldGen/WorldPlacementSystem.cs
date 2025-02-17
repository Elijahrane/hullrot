using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server._Hullrot.Worldgen.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.EntitySerialization;


namespace Content.Server._Hullrot.Worldgen;

/// <summary>
/// This system places objects in the world as defined by a <see cref="WorldPlacementPrototype"/>
/// </summary>
public sealed partial class WorldPlacementSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    private ISawmill _sawmill = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WorldPlacementComponent, ComponentInit>(OnInit);
    }

    private void OnInit(EntityUid uid, WorldPlacementComponent component, ComponentInit args)
    {
        if (!_prototypeManager.TryIndex<WorldPlacementPrototype>(component.Prototype, out var placementProto))
        {
            _sawmill.Error("Failed to load world placement prototype " + component.Prototype);
            return;
        }

        if (placementProto.Maps != null)
            LoadMaps(placementProto.Maps);
    }

    private void LoadMaps(List<string> maps)
    {
        foreach (var map in maps)
        {
            if (!_prototypeManager.TryIndex<WorldPlacementMapPrototype>(map, out var placementProto))
            {
                _sawmill.Error("Failed to index WorldPlacementMapPrototype " + map);
                continue;
            }

            if (!_prototypeManager.TryIndex<GameMapPrototype>(placementProto.Map, out var mapProto))
            {
                _sawmill.Error("Failed to index GameMapPrototype " + map);
                continue;
            }

            _gameTicker.MergeGameMap(mapProto, _gameTicker.DefaultMap, offset: placementProto.Pos);
        }
    }
}
