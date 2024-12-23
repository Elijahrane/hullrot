using Content.Client._Hullrot.Shipyard.UI;
using Content.Shared._Hullrot.Shipyard;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Hullrot.Shipyard.BUI;

public sealed class ShipyardBoundUserInterface : BoundUserInterface
{

    [Dependency] private readonly EntityManager _entityManager = default!;
    private readonly SharedShipyardSystem? _shipyardSystem;
    private ShipyardMenu? _menu;

    public ShipyardBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();
        _menu = new ShipyardMenu(this);
        _menu.OpenCentered();
        _menu.OnSellShip += SellShip;
        _menu.OnBuyShip += BuyShip;
        ShipyardState();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing) return;

        _menu?.Dispose();
    }

    private void SellShip(BaseButton.ButtonEventArgs args)
    {
        SendMessage(new ShipyardSellMessage());
    }

    private void BuyShip(BaseButton.ButtonEventArgs args)
    {
        SendMessage(new ShipyardBuyMessage());
    }
/*
    protected void ShipyardState()
    {


        if(!_entityManager.TryGetComponent<ShipyardComponent>(Owner, out var shipyard))
            return;

        bool lpcpresent = _entityManager.TryGetComponent<ShipVoucherComponent>(shipyard.TargetLPCSlot.Item, out var lpc);
        bool deedpresent = _entityManager.TryGetComponent<ShuttleDeedComponent>(shipyard.TargetDeedSlot.Item, out var deed);
        var name = (lpc is not null) ? lpc.Name : string.Empty;
        var description = (lpc is not null) ? lpc.Description : string.Empty;
        var shipyardstate = new ShipyardBoundUserInterfaceState(
            deedpresent,
            lpcpresent,
            name,
            description,
            0,
            0);


    }*/
}
