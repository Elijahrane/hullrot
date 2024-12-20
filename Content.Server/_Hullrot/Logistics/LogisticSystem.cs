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

    public DirectionFlag getReverseDir(DirectionFlag dir)
    {
        switch (dir)
        {
            case DirectionFlag.East :
                return DirectionFlag.West;
            case DirectionFlag.West :
                return DirectionFlag.East;
            case DirectionFlag.North:
                return DirectionFlag.South;
            case DirectionFlag.South:
                return DirectionFlag.North;
            default:
                return DirectionFlag.None;
        }
    }

    private void ConnectPipes(EntityUid firstPipe, EntityUid secondPipe, DirectionFlag firstDir,LogisticPipeComponent? firstComponent, LogisticPipeComponent? secondComponent)

    {
        if(firstComponent is null)
            TryComp(firstPipe, out firstComponent);
        if(secondComponent is null)
            TryComp(secondPipe, out secondComponent);
        if (firstComponent is null || secondComponent is null)
            return;
        firstComponent.Connected[firstDir] = secondPipe;
        secondComponent.Connected[getReverseDir(firstDir)] = firstPipe;
        UpdateLogisticPipeAppearance(firstPipe, firstComponent);
        UpdateLogisticPipeAppearance(secondPipe, secondComponent);


    }



    private void CheckConnections(EntityUid targetPipe, LogisticPipeComponent pipeComponent)
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
            if (pipeComponent.Connected[dir] is not null)
                continue;
            foreach (var pipe in LogisticPipesInDirection(localCoordinates, dir, mapGrid))
            {
                var reverseDir = getReverseDir(dir);
                if((pipe.connectionDirs & getReverseDir(dir)) == DirectionFlag.None)
                    continue;
                if (pipe.Connected[reverseDir] is not null)
                    continue;
                ConnectPipes(targetPipe, pipe);
                

            }
        }
    }


    private IEnumerable<Tuple<EntityUid, LogisticPipeComponent>> LogisticPipesInDirection(Vector2i pos, DirectionFlag pipeDir, MapGridComponent grid)
    {
        var offsetPos = pos.Offset(DirectionExtensions.AsDir(pipeDir));

        foreach (var entity in grid.GetAnchoredEntities(offsetPos))
        {
            if (!logisticQuery.TryGetComponent(entity, out var container))
                continue;

            yield return new (entity, container);
        }
    }
}
