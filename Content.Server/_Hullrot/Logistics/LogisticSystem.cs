using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
    private List<int> AlreadyGeneratedKeys = new();
    private readonly List<DirectionFlag> connectionDirs = new (4){
        DirectionFlag.North, DirectionFlag.South, DirectionFlag.East, DirectionFlag.West};

    private EntityQuery<LogisticPipeComponent> logisticQuery;

    private Dictionary<int, LogisticNetwork> networks = new();
    /// <inheritdoc/>
    public override void Initialize()
    {
        logisticQuery = new();
        SubscribeLocalEvent<LogisticPipeComponent,ComponentInit>(OnPipeCreation);
    }

    public void MergeLogisticNetworks(LogisticNetwork into, LogisticNetwork target)
    {
        var StorageRecordsByPrototypeID = new Dictionary<string, List<LogisticNetwork.StorageRecordById>>();
        var LogisticRequestsByRequester = new Dictionary<EntityUid, List<LogisticNetwork.EntityRequest>>();
        /// Its a union because we could have logistic bridges present
        var Nodes = into.ConnectedNodes.Union(target.ConnectedNodes).ToList();
        var PipeCount = Nodes.Count;
        foreach (var (prototypeId, LogisticRecord) in into.itemsById)
        {
            if (StorageRecordsByPrototypeID.ContainsKey(prototypeId))
                StorageRecordsByPrototypeID[prototypeId].Add(LogisticRecord);
            else
                StorageRecordsByPrototypeID.Add(prototypeId, new List<LogisticNetwork.StorageRecordById>(1){LogisticRecord});
        }
        foreach (var (prototypeId, LogisticRecord) in target.itemsById)
        {
            if (StorageRecordsByPrototypeID.ContainsKey(prototypeId))
                StorageRecordsByPrototypeID[prototypeId].Add(LogisticRecord);
            else
                StorageRecordsByPrototypeID.Add(prototypeId, new List<LogisticNetwork.StorageRecordById>(1) { LogisticRecord });
        }

        foreach (var request in into.logisticRequests)
        {
            if(LogisticRequestsByRequester.ContainsKey(request.requester))
                LogisticRequestsByRequester[request.requester].Add(request);
            else
            {
                LogisticRequestsByRequester.Add(request.requester, new List<LogisticNetwork.EntityRequest>(1){request});
            }
        }

        foreach (var request in target.logisticRequests)
        {
            if (LogisticRequestsByRequester.ContainsKey(request.requester))
                LogisticRequestsByRequester[request.requester].Add(request);
            else
            {
                LogisticRequestsByRequester.Add(request.requester, new List<LogisticNetwork.EntityRequest>(1) { request });
            }
        }
        
    }

    public void QueryLogisticNetwork(LogisticNetwork target, string prototypeId)
    {
        
    }
    public void OnPipeCreation(EntityUid pipe, LogisticPipeComponent component, ComponentInit args)
    {

    }
    public int generateNetworkIdentifier()
    {
        var key = _random.GetRandom().Next();
        while(AlreadyGeneratedKeys.Contains(key))
            key = _random.GetRandom().Next();
        AlreadyGeneratedKeys.Add(key);
        return key;
    }

    public bool removeNetworkIdentifier(int id)
    {
        if (!AlreadyGeneratedKeys.Contains(id))
            return false;
        AlreadyGeneratedKeys.Remove(id);
        return true;
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
        if (networks[firstComponent.NetworkId].PipeCount > networks[secondComponent.NetworkId].PipeCount)

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
