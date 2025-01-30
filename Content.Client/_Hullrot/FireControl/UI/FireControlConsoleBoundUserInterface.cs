using Content.Shared._Hullrot.FireControl;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Map;

namespace Content.Client._Hullrot.FireControl.UI;

[UsedImplicitly]
public sealed class FireControlConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private FireControlWindow? _window;

    public FireControlConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<FireControlWindow>();

        _window.OnServerRefresh += OnRefreshServer;

        _window.Radar.OnRadarClick += (coords) =>
        {
            var netCoords = EntMan.GetNetCoordinates(coords);
            SendFireMessage(netCoords);
        };

        _window.Radar.DefaultCursorShape = Control.CursorShape.Crosshair;
    }

    private void OnRefreshServer()
    {
        SendMessage(new FireControlConsoleRefreshServerMessage());
    }

    private void SendFireMessage(NetCoordinates coordinates)
    {
        if (_window == null)
            return;

        var selected = new List<NetEntity>();
        foreach (var button in _window.WeaponsList)
        {
            if (button.Value.Pressed)
                selected.Add(button.Key);
        }

        if (selected.Count > 0)
            SendMessage(new FireControlConsoleFireMessage(selected, coordinates));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not FireControlConsoleBoundInterfaceState castState)
            return;

        _window?.UpdateStatus(castState);
    }
}
