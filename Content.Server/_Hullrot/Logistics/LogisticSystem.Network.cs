using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.Storage.Components;
using Content.Shared._Hullrot.Logistics;
using Content.Shared.Atmos;
using Content.Shared.Construction.Components;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Random;
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
    [Dependency] private readonly IRobustRandom _random = default!;
    private Dictionary<int, LogisticNetwork> networks = new();
    private List<int> AlreadyGeneratedKeys = new();
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
    #region NetworkDataUpdates

    public void updateNetworkStorageData(LogisticNetwork network)
    {
        network.itemsById = new Dictionary<string, StorageRecordById>();
        foreach (var storage in network.StorageNodes)
        {
            if (!_containers.TryGetContainer(storage, "entity_storage", out var storageContainer))
                continue;
            foreach (var thing in storageContainer.ContainedEntities)
            {
                var key = MetaData(thing).EntityPrototype?.ID;
                if (key is null)
                    continue;
                if (network.itemsById.ContainsKey(key))
                {
                    var data = network.itemsById[key];
                    data.TotalAmount++;
                    if (data.Providers.ContainsKey(thing))
                        data.Providers[thing]++;
                    else
                        data.Providers.Add(thing, 1);
                }
                else
                {
                    StorageRecordById data = new(key);
                    data.TotalAmount = 1;
                    data.Providers.Add(thing, 1);
                    network.itemsById.Add(key, data);
                }

            }
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
        network.logisticRequests = new Stack<EntityRequest>();
        network.StorageNodes = new List<EntityUid>();
        network.itemsById = new Dictionary<string, StorageRecordById>();
        network.RequesterNodes = new List<EntityUid>();
        foreach (var node in network.ConnectedNodes)
        {
            if (!TryComp<LogisticPipeComponent>(node, out var comp))
                continue;
            if (comp.isRequester)
                network.RequesterNodes.Add(node);
            if (comp.isStorage)
                network.StorageNodes.Add(node);
        }

        foreach (var storage in network.StorageNodes)
        {
            if (!TryComp<EntityStorageComponent>(storage, out var comp))
                continue;
            foreach (var thing in comp.Contents.ContainedEntities)
            {
                var key = MetaData(thing).EntityPrototype?.ID;
                if (key is null)
                    continue;
                if (network.itemsById.ContainsKey(key))
                {
                    var data = network.itemsById[key];
                    data.TotalAmount++;
                    if (data.Providers.ContainsKey(thing))
                        data.Providers[thing]++;
                    else
                        data.Providers.Add(thing, 1);
                }
                else
                {
                    StorageRecordById data = new(key);
                    data.TotalAmount = 1;
                    data.Providers.Add(thing, 1);
                    network.itemsById.Add(key, data);
                }

            }
        }
        foreach (var requester in network.RequesterNodes)
        {
            GetLogisticRequestsEvent data = new();
            RaiseLocalEvent(requester, data);
            foreach (var request in data.Requests)
                network.logisticRequests.Push(request);
        }
    }
    #endregion


}
