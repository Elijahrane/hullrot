using Robust.Shared.Serialization;

namespace Content.Shared._Hullrot.Shipyard;

/// <summary>
/// This handles...
/// </summary>
[Serializable, NetSerializable]
public sealed class ShipyardSellMessage : BoundUserInterfaceMessage
{
    public ShipyardSellMessage()
    {
    }
}
[Serializable, NetSerializable]
public sealed class ShipyardBuyMessage : BoundUserInterfaceMessage
{
    public ShipyardBuyMessage()
    {
    }
}

