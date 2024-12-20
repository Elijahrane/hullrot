using System.Diagnostics.CodeAnalysis;
using Content.Shared._Hullrot.Logistics;
using Content.Shared.Atmos;
using Content.Shared.Random;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._Hullrot.Logistics;

/// <summary>
/// This handles...
/// </summary>
public sealed class LogisticSystem : EntitySystem
{
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    private Random? generatorInstance;
    private List<int> AlreadyGeneratedKeys = new();
    private readonly List<DirectionFlag> connectionDirs = new (4){
        DirectionFlag.North, DirectionFlag.South, DirectionFlag.East, DirectionFlag.West};

    private EntityQuery<LogisticPipeComponent> logisticQuery;

    private List<LogisticNetwork> networks = new List<LogisticNetwork>();
    /// <inheritdoc/>
    public override void Initialize()
    {
        logisticQuery = new();
        generatorInstance = _random.GetRandom();
    }

    public int generateNetworkIdentifier()
    {
        var key = generatorInstance?.Next();
        while(AlreadyGeneratedKeys.Contains(key))
            key = generatorInstance?.Next();
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

    private void UpdateLogisticPipeAppearance(EntityUid targetPipe, LogisticPipeComponent component)
    {
        var connectionCount = 0;
        foreach(var (dir,uid) in component.Connected)
        {
            if(uid is null)
                continue;
            connectionCount++;

        }

        switch (connectionCount)
        {
            case 0:
                return;
            case 1:
                return;
            case 2:
                return;
            case 3:
                return;
            case 4:
                return;
        }
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
                if ((pipe.Item2.connectionDirs & getReverseDir(dir)) == DirectionFlag.None)
                    continue;
                if (pipe.Item2.Connected[reverseDir] is not null)
                    continue;
                ConnectPipes(targetPipe, pipe.Item1, dir, pipeComponent, pipe.Item2);
                break;

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
