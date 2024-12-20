using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Hullrot.Logistics;
using Content.Shared.Atmos;
using Content.Shared.Random;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using static Content.Shared._Hullrot.Logistics.LogisticNetwork;

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

    public HashSet<EntityUid> getAllPipesConnectedToPoint(EntityUid target , LogisticPipeComponent component)
    {
        var NextIteration = new Stack<LogisticPipeComponent>();
        NextIteration.Push(component);
        var returnSet = new HashSet<EntityUid>(){target};
        while (NextIteration.TryPop(out var next))
        {
            foreach (var (direction, nextPipe ) in next.Connected)
            {
                if (nextPipe is null)
                    continue;
                if (!TryComp<LogisticPipeComponent>(nextPipe, out var pipeComp))
                    continue;
                if (returnSet.Contains(nextPipe.Value))
                    continue;

                returnSet.Add(nextPipe.Value);
                NextIteration.Push(pipeComp);

            }
        }

        return returnSet;
    }

    public void MergeLogisticNetworks(LogisticNetwork into, LogisticNetwork target)
    {
        var StorageRecordsByPrototypeID = new Dictionary<string, List<StorageRecordById>>();
        var LogisticRequestsByRequester = new Dictionary<EntityUid, List<EntityRequest>>();
        /// Its a union because we could have logistic bridges present
        var Nodes = into.ConnectedNodes.Union(target.ConnectedNodes).ToList();
        var PipeCount = Nodes.Count;
        foreach (var (prototypeId, LogisticRecord) in into.itemsById)
        {
            if (StorageRecordsByPrototypeID.ContainsKey(prototypeId))
                StorageRecordsByPrototypeID[prototypeId].Add(LogisticRecord);
            else
                StorageRecordsByPrototypeID.Add(prototypeId, new List<StorageRecordById>(1){LogisticRecord});
        }
        foreach (var (prototypeId, LogisticRecord) in target.itemsById)
        {
            if (StorageRecordsByPrototypeID.ContainsKey(prototypeId))
                StorageRecordsByPrototypeID[prototypeId].Add(LogisticRecord);
            else
                StorageRecordsByPrototypeID.Add(prototypeId, new List<StorageRecordById>(1) { LogisticRecord });
        }

        foreach (var request in into.logisticRequests)
        {
            if(LogisticRequestsByRequester.ContainsKey(request.requester))
                LogisticRequestsByRequester[request.requester].Add(request);
            else
            {
                LogisticRequestsByRequester.Add(request.requester, new List<EntityRequest>(1){request});
            }
        }

        foreach (var request in target.logisticRequests)
        {
            if (LogisticRequestsByRequester.ContainsKey(request.requester))
                LogisticRequestsByRequester[request.requester].Add(request);
            else
            {
                LogisticRequestsByRequester.Add(request.requester, new List<EntityRequest>(1) { request });
            }
        }

        var replacementStorage = new Dictionary<string, StorageRecordById>();
        foreach (var (prototype, list) in StorageRecordsByPrototypeID)
        {
            var unifiedRecords = new StorageRecordById(prototype);
            foreach (var record in list)
            {
                foreach (var (storageEntity, amount) in record.Providers)
                {
                    if (unifiedRecords.Providers.ContainsKey(storageEntity))
                        continue;
                    unifiedRecords.Providers.Add(storageEntity, amount);
                    unifiedRecords.TotalAmount += amount;
                }
            }
            replacementStorage.Add(prototype, unifiedRecords);
        }

        var replacementRequestStack = new Stack<EntityRequest>();
        foreach(var request in into.logisticRequests)
            replacementRequestStack.Push(request);
        foreach(var request in target.logisticRequests)
            replacementRequestStack.Push(request);
        into.logisticRequests = replacementRequestStack;
        into.itemsById = replacementStorage;
        into.ConnectedNodes = Nodes;
        into.PipeCount = PipeCount;

    }

    public int PipeConnectionCount(LogisticPipeComponent pipeComp)
    {
        var counter = 0;
        foreach (var (dir, entity) in pipeComp.Connected)
        {
            if (entity is not null)
                counter++;
        }

        return counter;
    }

    public void RemovePipeFromNetwork(EntityUid pipe, LogisticNetwork network)
    {
        network.ConnectedNodes.Remove(pipe);
        network.PipeCount--;
    }

    public void AddPipeToNetwork(EntityUid pipe, LogisticNetwork network)
    {
        network.ConnectedNodes.Add(pipe);
        network.PipeCount++;
    }
    public void QueryLogisticNetwork(LogisticNetwork target, string prototypeId)
    {
        
    }
    public void OnPipeCreation(EntityUid pipe, LogisticPipeComponent component, ComponentInit args)
    {
        CheckConnections(pipe, component);
        if (component.NetworkId == 0)
        {
            createNetwork(pipe, component);
        }
    }

    public int createNetwork(EntityUid pipe, LogisticPipeComponent component)
    {
        var networkId = generateNetworkIdentifier();
        var network = new LogisticNetwork();
        network.ConnectedNodes.Add(pipe);
        network.PipeCount = 1;
        network.NetworkId = networkId;
        networks.Add(networkId, network);
        return networkId;
    }

    public int createNetwork(HashSet<EntityUid> pipes)
    {
        var networkId = generateNetworkIdentifier();
        var network = new LogisticNetwork();
        network.NetworkId = networkId;
        networks.Add(networkId, network);
        foreach (var uid in pipes)
        {
            if (!TryComp<LogisticPipeComponent>(uid, out var comp))
                continue;
            network.ConnectedNodes.Add(uid);
            network.PipeCount++;
            comp.NetworkId = networkId;
        }

        return networkId;
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
        if(firstComponent.NetworkId == 0)
            AddPipeToNetwork(firstPipe, networks[secondComponent.NetworkId]);
        else if(secondComponent.NetworkId == 0)
            AddPipeToNetwork(secondPipe, networks[firstComponent.NetworkId]);
        if (firstComponent.NetworkId != secondComponent.NetworkId)
            MergeLogisticNetworks(networks[firstComponent.NetworkId], networks[secondComponent.NetworkId]);

        UpdateLogisticPipeAppearance(firstPipe, firstComponent);
        UpdateLogisticPipeAppearance(secondPipe, secondComponent);
    }

    private void DisconnectPipes(EntityUid firstPipe,
        EntityUid secondPipe,
        DirectionFlag firstDir,
        LogisticPipeComponent? firstComponent,
        LogisticPipeComponent? secondComponent)
    {
        if (firstComponent is null)
            TryComp(firstPipe, out firstComponent);
        if (secondComponent is null)
            TryComp(secondPipe, out secondComponent);
        if (firstComponent is null || secondComponent is null)
            return;
        var network = networks[firstComponent.NetworkId];
        firstComponent.Connected[firstDir] = null;
        var firstCount = PipeConnectionCount(firstComponent);
        secondComponent.Connected[getReverseDir(firstDir)] = null;
        var secondCount = PipeConnectionCount(secondComponent);
        if(firstCount < 1)
        {
            RemovePipeFromNetwork(firstPipe, network);
            createNetwork(firstPipe, firstComponent);
        }
        else if (secondCount < 1)
        {
            RemovePipeFromNetwork(secondPipe, network);
            createNetwork(secondPipe, secondComponent);
        }
        else
        {
            var firstPipeCount = getAllPipesConnectedToPoint(firstPipe, firstComponent);
            var secondPipeCount = getAllPipesConnectedToPoint(secondPipe, secondComponent);
            if (firstPipeCount.Count > secondPipeCount.Count)
            {
                RemovePipeFromNetwork(secondPipe, network);
                createNetwork(secondPipeCount);
            }
            else
            {
                RemovePipeFromNetwork(firstPipe, network);
                createNetwork(firstPipeCount);
            }
        }
        UpdateLogisticPipeAppearance(firstPipe, firstComponent);
        UpdateLogisticPipeAppearance(secondPipe, secondComponent);
    }

    private void UpdateLogisticPipeAppearance(EntityUid targetPipe, LogisticPipeComponent component)
    {
        var connectionCount = PipeConnectionCount(component);

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
