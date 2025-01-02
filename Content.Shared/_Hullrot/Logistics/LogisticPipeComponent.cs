using System.Threading.Tasks;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Shared._Hullrot.Logistics;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent,Serializable, NetSerializable]
public sealed partial class LogisticPipeComponent : Component
{
    [DataField, ViewVariables]
    public DirectionFlag connectionDirs =
        DirectionFlag.East | DirectionFlag.West | DirectionFlag.North | DirectionFlag.South;

    [DataField, ViewVariables]
    public Dictionary<DirectionFlag, EntityUid?> Connected = new();
    [DataField, ViewVariables]
    public bool isStorage = false;
    [DataField, ViewVariables]
    public bool isRequester = false;
    /// <summary>
    /// necesarry becauase anchors are called before initialize startup and can't be checked for this in metadata SPCR 2024
    /// </summary>
    [DataField, ViewVariables]
    public bool hasStarted = false;


    [DataField, ViewVariables]
    public LogisticNetwork? network;

    [DataField, ViewVariables]
    public int NetworkId = 0;
}

[Serializable, NetSerializable]
public enum LogisticVisualLayout
{
    baseLayer,
    way0,
    way1,
    way2,
    way3,
    way4
}

public class TryInsertIntoLogisticStorageEvent : EntityEventArgs
{
    public EntityUid target;
}


public class GetLogisticRequestsEvent : EntityEventArgs
{
    public List<LogisticNetwork.EntityRequest> Requests = new();
}

public class GetLogisticStorageContents : EntityEventArgs
{
    public List<Tuple<string, int>> PrototypeAmountAvailable = new();
}

public class GetLogisticsStorageSpaceAvailable : EntityEventArgs
{
    public int space = 0;
}

public class GetLogisticsStorageSpaceTotal : EntityEventArgs
{
    public int space = 0;
}

public class LogisticsStorageContentsChange : EntityEventArgs
{
}

public class LogisticsStorageRetrieveItem : EntityEventArgs
{
}

        
public class LogisticNetwork : IDisposable
{
    #region InternalClasses
    public class EntityRequest
    {
        public readonly EntityUid requester;
        public int Count;

        public EntityRequest(EntityUid requester, int amount)
        {
            this.requester = requester;
            this.Count = amount;
        }

    }

    public class EntityRequestById : EntityRequest
    {
        public readonly string prototypeId;

        public EntityRequestById(EntityUid requester, int amount, string prototypeId) : base(requester, amount)
        {
            this.prototypeId = prototypeId;
        }
    }

    public class EntityRequestByName : EntityRequest
    {
        public readonly string name;

        public EntityRequestByName(EntityUid requester, int amount, string name) : base(requester, amount)
        {
            this.name = name;
        }
    }

    public class StorageRecord
    {
        [ViewVariables]
        public int TotalAmount = 0;
        [ViewVariables]
        public Dictionary<EntityUid, int> Providers = new();
    }

    public class StorageRecordById : StorageRecord
    {
        [ViewVariables]
        public string PrototypeId;

        public StorageRecordById(string id) : base()
        {
            PrototypeId = id;
        }
    }
    #endregion

    #region DisposalImplementation

    public void Dispose()
    {
        ConnectedNodes.Clear();
        StorageNodes.Clear();
        RequesterNodes.Clear();
        foreach (var (key, data) in itemsById)
        {
            data.Providers.Clear();
        }
        RelevantStorageRecordsForStorer.Clear();

    }

    #endregion
    [ViewVariables]
    public Stack<EntityRequest> LogisticRequestsStack
    {
        get
        {
            return new Stack<EntityRequest>(LogisticRequests);
        }
    }

    public List<EntityRequest> LogisticRequests = new();
    [ViewVariables]
    public Dictionary<string, StorageRecordById> itemsById = new();

    [ViewVariables]
    // network state data for each storage node.
    public Dictionary<EntityUid, List<string>> RelevantStorageRecordsForStorer = new();

    public Dictionary<EntityUid, List<EntityRequest>> RelevantRequestsForEntity = new();
    [ViewVariables]
    public List<EntityUid> ConnectedNodes = new();
    [ViewVariables]
    public List<EntityUid> StorageNodes = new();
    [ViewVariables]
    public List<EntityUid> RequesterNodes = new();
    


    [ViewVariables]
    public int PipeCount = 0;
    [ViewVariables]
    public int NetworkId = 0;

}


