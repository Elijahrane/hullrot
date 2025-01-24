using Content.Shared._Hullrot.Logistics;
using Robust.Shared.Serialization;

namespace Content.Server._Hullrot.Logistics;

public sealed class LogisticsSupplyItemsEvent : EntityEventArgs
{
    public List<EntityUid> items;
    public List<EntityUid> taken;
    public int amountTaken;

    public LogisticsSupplyItemsEvent(List<EntityUid> items)
    {
        this.items = items;
        taken = new List<EntityUid>();
    }
}

public sealed class GetLogisticRequestsEvent : EntityEventArgs
{
    public List<LogisticNetwork.LogisticCommand> Requests = new();
}
public class GetLogisticStorageContents : EntityEventArgs
{
    public List<Tuple<string, int>> PrototypeAmountAvailable = new();
}
public class GetLogisticsStorageSpaceAvailableEvent : EntityEventArgs
{
    public int space = 0;
    public string? prototypeId;

    public GetLogisticsStorageSpaceAvailableEvent()
    {
    }
    public GetLogisticsStorageSpaceAvailableEvent(string prototype)
    {
        this.prototypeId = prototype;
    }


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
