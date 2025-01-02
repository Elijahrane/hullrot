using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.Storage.Components;
using Content.Shared._Hullrot.Logistics;
using Content.Shared.Atmos;
using Content.Shared.Construction.Components;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Random;
using Content.Shared.Storage.Components;
using JetBrains.Annotations;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using static Content.Shared._Hullrot.Logistics.LogisticNetwork;

namespace Content.Server._Hullrot.Logistics;

/// <summary>
/// This handles...
/// </summary>
public sealed partial class LogisticSystem : EntitySystem
{
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly ContainerSystem _containers = default!;
    [Dependency] private readonly AnchorableSystem _anchoring = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    private Dictionary<int, LogisticNetwork> networks = new();
    private List<int> AlreadyGeneratedKeys = new();
    public const string StorageContainerString = "entity_storage";

    private readonly List<DirectionFlag> connectionDirs = new (4){
        DirectionFlag.North, DirectionFlag.South, DirectionFlag.East, DirectionFlag.West};


    /// <inheritdoc/>
    public override void Initialize()
    {
        #region Storage Subscription
        SubscribeLocalEvent<LogisticCargoDataComponent, ComponentStartup>(OnCargoTrackerStartup);
        SubscribeLocalEvent<LogisticCargoDataComponent, AnchorStateChangedEvent>(OnCargoTrackerAnchorChange);
        SubscribeLocalEvent<LogisticCargoDataComponent, StorageAfterCloseEvent>();
        #endregion

        #region Pipe Subscriptions
        SubscribeLocalEvent<LogisticPipeComponent, ComponentInit>(OnPipeInit);
        SubscribeLocalEvent<LogisticPipeComponent, ComponentStartup>(OnPipeStartup);
        SubscribeLocalEvent<LogisticPipeComponent, AnchorStateChangedEvent>(OnAnchorChange);
        #endregion 
    }
    #region Pipes
    #region Event Subscribers
    public void OnPipeInit(EntityUid pipe, LogisticPipeComponent component, ComponentInit args)
    {
        foreach (var connectionDir in connectionDirs)
        {
            if ((connectionDir & component.connectionDirs) != DirectionFlag.None)
            {
                component.Connected.Add(connectionDir, null);
            }

        }
    }

    public void OnPipeStartup(EntityUid pipe, LogisticPipeComponent component, ComponentStartup args)
    {
        component.hasStarted = true;
        if (Transform(pipe).Anchored)
            ConnectNearby(pipe, component);
        if (component.NetworkId == 0)
        {
            createNetwork(pipe, component);
        }
    }
    public void OnAnchorChange(EntityUid entity, LogisticPipeComponent pipeComponent, AnchorStateChangedEvent args)
    {
        if (!pipeComponent.hasStarted)
            return;
        if (args.Anchored == false)
            DisconnectFromAllPipes(entity, pipeComponent);
        else
            ConnectNearby(entity, pipeComponent);
    }
    #endregion
    #region Helpers
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
    

    private IEnumerable<Tuple<EntityUid, LogisticPipeComponent>> LogisticPipesInDirection(Vector2i pos, DirectionFlag pipeDir, MapGridComponent grid, EntityUid mapUid)
    {
        var offsetPos = pos.Offset(DirectionExtensions.AsDir(pipeDir));
        foreach (var entity in _mapSystem.GetAnchoredEntities(mapUid, grid, offsetPos))
        {
            if(!TryComp<LogisticPipeComponent>(entity, out var container))
                continue;

            yield return new (entity, container);
        }
    }

    #endregion
    #region Visuals
    private void UpdateLogisticPipeAppearance(EntityUid targetPipe, LogisticPipeComponent component)
    {
        if (component.isStorage | component.isRequester)
            return;
        var connectionCount = 0;
        var connectedDirs = DirectionFlag.None;
        foreach(var dir in connectionDirs)
        {
            if ((dir & component.connectionDirs) == DirectionFlag.None)
                continue;
            if (component.Connected[dir] is null)
                continue;
            connectionCount++;
            connectedDirs |= dir;
        }
        var transComp = Transform(targetPipe);
        _appearance.SetData(targetPipe, LogisticVisualLayout.way0, false);
        _appearance.SetData(targetPipe, LogisticVisualLayout.way1, false);
        _appearance.SetData(targetPipe, LogisticVisualLayout.way2, false);
        _appearance.SetData(targetPipe, LogisticVisualLayout.way3, false);
        _appearance.SetData(targetPipe, LogisticVisualLayout.way4, false);

        switch (connectionCount)
        {
            case 0:
                _appearance.SetData(targetPipe, LogisticVisualLayout.way0, true);
                return;
            case 1:
                switch(connectedDirs)
                {
                    case DirectionFlag.South:
                    case DirectionFlag.North:
                        _appearance.SetData(targetPipe, LogisticVisualLayout.way1, true);
                        transComp.LocalRotation = DirectionExtensions.ToAngle(Direction.North);
                        return;
                    case DirectionFlag.East:
                    case DirectionFlag.West:
                        _appearance.SetData(targetPipe, LogisticVisualLayout.way1, true);
                        transComp.LocalRotation = DirectionExtensions.ToAngle(Direction.East);
                        return;

                }
                _appearance.SetData(targetPipe, LogisticVisualLayout.way1, true);
                return;
            case 2:
                switch(connectedDirs)
                {
                    case DirectionFlag.SouthEast:
                        _appearance.SetData(targetPipe, LogisticVisualLayout.way2, true);
                        transComp.LocalRotation = DirectionExtensions.ToAngle(Direction.East);
                        return;
                    case DirectionFlag.NorthWest:
                        _appearance.SetData(targetPipe, LogisticVisualLayout.way2, true);
                        transComp.LocalRotation = DirectionExtensions.ToAngle(Direction.West);
                        return;
                    case DirectionFlag.SouthWest:
                        _appearance.SetData(targetPipe, LogisticVisualLayout.way2, true);
                        transComp.LocalRotation = DirectionExtensions.ToAngle(Direction.South);
                        return;
                    case DirectionFlag.NorthEast:
                        _appearance.SetData(targetPipe, LogisticVisualLayout.way2, true);
                        transComp.LocalRotation = DirectionExtensions.ToAngle(Direction.North);
                        return;
                    case (DirectionFlag.North | DirectionFlag.South):
                        _appearance.SetData(targetPipe, LogisticVisualLayout.way1, true);
                        transComp.LocalRotation = DirectionExtensions.ToAngle(Direction.North);
                        return;
                    case (DirectionFlag.East | DirectionFlag.West):
                        _appearance.SetData(targetPipe, LogisticVisualLayout.way1, true);
                        transComp.LocalRotation = DirectionExtensions.ToAngle(Direction.East);
                        return;
                    default:
                        return;
                }
            case 3:
                _appearance.SetData(targetPipe, LogisticVisualLayout.way3, true);
                if ((connectedDirs & DirectionFlag.North) != DirectionFlag.None && (connectedDirs & DirectionFlag.South) != DirectionFlag.None)
                    if ((connectedDirs & DirectionFlag.East) != DirectionFlag.None)
                        transComp.LocalRotation = DirectionExtensions.ToAngle(Direction.East);
                    else
                        transComp.LocalRotation = DirectionExtensions.ToAngle(Direction.West);
                else
                    if ((connectedDirs & DirectionFlag.South) != DirectionFlag.None)
                        transComp.LocalRotation = DirectionExtensions.ToAngle(Direction.South);
                    else
                        transComp.LocalRotation = DirectionExtensions.ToAngle(Direction.North);
                return;
            case 4:
                _appearance.SetData(targetPipe, LogisticVisualLayout.way4, true);
                return;
            default:
                return;
        }
    }
    #endregion
    #region Pipe Connecting
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
        if (firstComponent.NetworkId == 0)
        {
            firstComponent.NetworkId = secondComponent.NetworkId;
            AddPipeToNetwork(firstPipe, networks[secondComponent.NetworkId]);
        }
        else if (secondComponent.NetworkId == 0)
        {
            secondComponent.NetworkId = firstComponent.NetworkId;
            AddPipeToNetwork(secondPipe, networks[firstComponent.NetworkId]);
        }

        else if (firstComponent.NetworkId != secondComponent.NetworkId)
        {
            MergeLogisticNetworks(networks[firstComponent.NetworkId], networks[secondComponent.NetworkId]);
        }

        UpdateLogisticPipeAppearance(firstPipe, firstComponent);
        UpdateLogisticPipeAppearance(secondPipe, secondComponent);
    }

    private void ConnectNearby(EntityUid targetPipe, LogisticPipeComponent pipeComponent)
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
            foreach (var pipe in LogisticPipesInDirection(localCoordinates, dir, mapGrid, transComp.GridUid.Value))
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
    #endregion
    #region Pipe Disconnecting
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
        rebuildNetworkData(networks[firstComponent.NetworkId]);
        rebuildNetworkData(networks[secondComponent.NetworkId]);
        UpdateLogisticPipeAppearance(firstPipe, firstComponent);
        UpdateLogisticPipeAppearance(secondPipe, secondComponent);
    }

    public void DisconnectFromAllPipes(EntityUid entity, LogisticPipeComponent pipeComponent)
    {
        var transComp = Transform(entity);
        if (transComp.GridUid is null)
            return;
        if (Deleted(transComp.GridUid))
            return;
        if (!TryComp<MapGridComponent>(transComp.GridUid, out var mapGrid))
            return;
        var localCoordinates = _transformSystem.GetGridOrMapTilePosition(entity, transComp);
        foreach (var dir in connectionDirs)
        {
            if ((dir & pipeComponent.connectionDirs) == DirectionFlag.None)
                continue;
            if (pipeComponent.Connected[dir] is null)
                continue;
            foreach (var pipe in LogisticPipesInDirection(localCoordinates, dir, mapGrid, transComp.GridUid.Value))
            {
                var reverseDir = getReverseDir(dir);
                if ((pipe.Item2.connectionDirs & getReverseDir(dir)) == DirectionFlag.None)
                    continue;
                if (pipe.Item2.Connected[reverseDir] is null)
                    continue;
                if (pipe.Item2.Connected[reverseDir] != entity)
                    continue;
                DisconnectPipes(entity, pipe.Item1, dir, pipeComponent, pipe.Item2);
                break;

            }
        }

        RemovePipeFromNetwork(entity, networks[pipeComponent.NetworkId]);
        pipeComponent.NetworkId = 0;
    }
    #endregion
    #endregion

    #region Networks
    #region Network Identifiers
    public int generateNetworkIdentifier()
    {
        var key = _random.GetRandom().Next();
        while (AlreadyGeneratedKeys.Contains(key))
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
    #endregion
    #region Create Network
    public int createNetwork(EntityUid pipe, LogisticPipeComponent component)
    {
        var networkId = generateNetworkIdentifier();
        var network = new LogisticNetwork();
        network.ConnectedNodes.Add(pipe);
        network.PipeCount = 1;
        network.NetworkId = networkId;
        component.NetworkId = networkId;
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
            AddPipeToNetwork(uid, network);
        }

        return networkId;
    }
    #endregion
    #region Add/Remove Pipe to Network
    public void AddPipeToNetwork(EntityUid pipe, LogisticNetwork network)
    {
        if (!TryComp<LogisticPipeComponent>(pipe, out var comp))
            return;
        comp.NetworkId = network.NetworkId;
        comp.network = network;
        network.ConnectedNodes.Add(pipe);
        network.PipeCount++;
        if (comp.isStorage)
        {
            network.StorageNodes.Add(pipe);
            updateNetworkStorageData(network);
        }
        if (comp.isRequester)
        {
            network.RequesterNodes.Add(pipe);
            updateNetworkRequestData(network);
        }
        _chat.ChatMessageToAll(Shared.Chat.ChatChannel.OOC, $"{pipe} added to {network.NetworkId} network", $"{pipe} added to {network.NetworkId} network", pipe, false, false);
    }
    public void RemovePipeFromNetwork(EntityUid pipe, LogisticNetwork network)
    {
        network.ConnectedNodes.Remove(pipe);
        network.PipeCount--;
        if (network.PipeCount == 0)
            removeNetworkIdentifier(network.NetworkId);
        network.Dispose();

        _chat.ChatMessageToAll(Shared.Chat.ChatChannel.OOC, $"{pipe} removed from {network.NetworkId} network", $"{pipe} removed from {network.NetworkId} network", pipe, false, false);
    }

    #endregion
    #region Network Merging
    public void MergeLogisticNetworks(LogisticNetwork into, LogisticNetwork target)
    {
        var Nodes = into.ConnectedNodes.Union(target.ConnectedNodes).ToList();
        _chat.ChatMessageToAll(Shared.Chat.ChatChannel.OOC, $"Merging {target.NetworkId} into {into.NetworkId}. Count before merge {into.ConnectedNodes.Count}, after {Nodes.Count}", $"Merging {target.NetworkId} into {into.NetworkId}. Count before merge {into.ConnectedNodes.Count}, after {Nodes.Count}", Nodes[0], false, false);
        foreach (var pipe in Nodes)
        {
            if (!TryComp<LogisticPipeComponent>(pipe, out var pipeComp))
                continue;
            pipeComp.NetworkId = into.NetworkId;
            pipeComp.network = into;
        }
        into.ConnectedNodes = Nodes;

        into.PipeCount = Nodes.Count;
        target.Dispose();
        rebuildNetworkData(into);

    }
    #endregion
    #region Network Data Updates

    public void updateNetworkStorageData(LogisticNetwork network)
    {
        foreach (var storage in network.StorageNodes)
        {
            EnsureComp<LogisticCargoDataComponent>(storage, out var storageComp);
            var localEntries = getStorageContentsData(storage);
            storageComp.CargoEntries = localEntries;
            setNetworkStorageData(storage, localEntries, network);
        }
    }

    public void updateNetworkRequestData(LogisticNetwork network)
    {
        network.logisticRequests = new Stack<EntityRequest>();
        foreach (var requester in network.RequesterNodes)
        {
            GetLogisticRequestsEvent data = new();
            RaiseLocalEvent(requester, data);
            foreach (var request in data.Requests)
                network.logisticRequests.Push(request);
        }
    }

    public void rebuildNetworkData(LogisticNetwork network)
    {
        updateNetworkStorageData(network);
        updateNetworkRequestData(network);
    }
    #endregion
    #endregion

    #region Storage

    public void setNetworkStorageData(EntityUid from, Dictionary<string, List<EntityUid>> entries, LogisticNetwork network)
    {
        var dataComp = EnsureComp<LogisticCargoDataComponent>(from);
        foreach (var (key,contentss) in dataComp.CargoEntries)
        {
            if (entries.ContainsKey(key))
                continue;
            var storageRecord = network.itemsById[key];
            storageRecord.TotalAmount -= storageRecord.Providers[from];
            storageRecord.Providers.Remove(from);
            network.itemsById[key] = storageRecord;
        }
        foreach (var (key, items) in entries)
        {
            if (!network.itemsById.ContainsKey(key))
            {
                network.itemsById.Add(key, new StorageRecordById(key));
            }

            var storageRecord = network.itemsById[key];

            if (!storageRecord.Providers.ContainsKey(from))
            {
                storageRecord.Providers.Add(from, 0);
            }

            storageRecord.TotalAmount += items.Count - storageRecord.Providers[from];
            storageRecord.Providers[from] = items.Count;
            network.itemsById[key] = storageRecord;
        }

        dataComp.CargoEntries = entries;

    }

    public Dictionary<string, List<EntityUid>> getStorageContentsData(EntityUid target)
    {
        var localEntries = new Dictionary<string, List<EntityUid>>();
        if (!_containers.TryGetContainer(target, StorageContainerString, out var storageContainer))
            return localEntries;
        foreach (var thing in storageContainer.ContainedEntities)
        {
            var key = MetaData(thing).EntityPrototype?.ID;
            if (key is null)
                continue;
            if (!localEntries.ContainsKey(key))
                localEntries.Add(key, new List<EntityUid>());
            localEntries[key].Add(thing);
        }

        return localEntries;
    }

    public void refreshNetworkStorageDataForTarget(EntityUid target)
    {

    }

    #endregion

    #region Requests

    #endregion
}
