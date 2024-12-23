using Content.Shared.Containers.ItemSlots;


namespace Content.Shared._Hullrot.Shipyard;



public abstract class SharedShipyardSystem : EntitySystem
{

    [Dependency] protected readonly ItemSlotsSystem _itemSlotsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShipyardComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ShipyardComponent, ComponentRemove>(OnComponentRemove);
    }

    private void OnComponentInit(EntityUid uid, ShipyardComponent component, ComponentInit args)
    {
        _itemSlotsSystem.AddItemSlot(uid, ShipyardComponent.TargetDeedCardSlotId, component.TargetDeedSlot);
        _itemSlotsSystem.AddItemSlot(uid, ShipyardComponent.TargetLPCSlotId, component.TargetLPCSlot);
    }

    private void OnComponentRemove(EntityUid uid, ShipyardComponent component, ComponentRemove args)
    {
        _itemSlotsSystem.RemoveItemSlot(uid, component.TargetDeedSlot);
        _itemSlotsSystem.RemoveItemSlot(uid, component.TargetLPCSlot);
    }
}
