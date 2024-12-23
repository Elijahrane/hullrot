using Robust.Shared.Serialization;

namespace Content.Shared._Hullrot.Shipyard;

[Serializable, NetSerializable]
public sealed class ShipyardBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool isShuttleDeedPresent;
    public bool isShipVoucherPresent;
    public string shipVoucherName;
    public string shipVoucherDescription;
    public int accountBalance;
    public int shuttleValue;



    public ShipyardBoundUserInterfaceState(
    bool isshuttledeedpresent,
    bool isshipvoucherpresent,
    string shipvouchername,
    string shipvoucherdescription,
    int accountbalance,
    int shuttlevalue)
    {
        isShuttleDeedPresent = isshuttledeedpresent;
        isShipVoucherPresent = isshipvoucherpresent;
        shipVoucherName = shipvouchername;
        shipVoucherDescription = shipvoucherdescription;
        accountBalance = accountbalance;
        shuttleValue = shuttlevalue;
    }
}
