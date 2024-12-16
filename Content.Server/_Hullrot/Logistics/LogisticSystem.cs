using Content.Server.NodeContainer;
using Content.Server.NodeContainer.Nodes;
using Content.Shared._Hullrot.Logistics;
using Content.Shared.Atmos;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Server._Hullrot.Logistics;

/// <summary>
/// This handles...
/// </summary>
public sealed class LogisticSystem : EntitySystem
{
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    private readonly List<DirectionFlag> connectionDirs = new (4){
        DirectionFlag.North, DirectionFlag.South, DirectionFlag.East, DirectionFlag.West};

    private EntityQuery<LogisticPipeComponent> logisticQuery;
    /// <inheritdoc/>
    public override void Initialize()
    {
        logisticQuery = new();
    }



    private void UpdateLogisticPipeAppearance(EntityUid targetPipe, LogisticPipeComponent pipeComponent)
    {
        var transComp = Transform(targetPipe);
        if (transComp.GridUid is null)
            return;
        if (Deleted(transComp.GridUid))
            return;
        if (!TryComp<MapGridComponent>(transComp.GridUid, out var mapGrid))
            return;
        var localCoordinates = _transformSystem.GetGridOrMapTilePosition(targetPipe, transComp);
        foreach (var dir in connectionDirs)
        {
            if ((dir & pipeComponent.connectionDirs) == DirectionFlag.None)
                continue;
            foreach (var pipe in LogisticPipesInDirection(localCoordinates, dir, mapGrid))
            {

            }
        }
    }


    private IEnumerable<LogisticPipeComponent> LogisticPipesInDirection(Vector2i pos, DirectionFlag pipeDir, MapGridComponent grid)
    {
        var offsetPos = pos.Offset(DirectionExtensions.AsDir(pipeDir));

        foreach (var entity in grid.GetAnchoredEntities(offsetPos))
        {
            if (!logisticQuery.TryGetComponent(entity, out var container))
                continue;

            yield return container;
        }
    }
}
