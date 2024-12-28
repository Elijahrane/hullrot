using System.Threading.Tasks;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Shared._Hullrot.Logistics;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class LogisticPipeComponent : Component
{
    [DataField, ViewVariables]
    public DirectionFlag connectionDirs =
        DirectionFlag.East | DirectionFlag.West | DirectionFlag.North | DirectionFlag.South;

    [DataField, ViewVariables]
    public Dictionary<DirectionFlag, EntityUid?> Connected = new();

    [DataField, ViewVariables]
    public LogisticNodeType nodeFlags = LogisticNodeType.Transporter;

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
[Flags]
public enum LogisticNodeType
{
    Transporter,
    Storage,
    Requester
}


public class GetLogisticRequestsEvent : EntityEventArgs
{
    public List<LogisticNetwork.EntityRequest> Requests = new();
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
        public int TotalAmount = 0;
        public Dictionary<EntityUid, int> Providers = new();
    }

    public class StorageRecordById : StorageRecord
    {
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
    }

    #endregion

    public Stack<EntityRequest> logisticRequests = new();

    public Dictionary<string, StorageRecordById> itemsById = new();

    public List<EntityUid> ConnectedNodes = new();

    public List<EntityUid> StorageNodes = new();

    public List<EntityUid> RequesterNodes = new();



    public int PipeCount = 0;

    public int NetworkId = 0;

}


