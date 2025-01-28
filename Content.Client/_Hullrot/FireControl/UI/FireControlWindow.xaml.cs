using Content.Client.UserInterface.Controls;
using Content.Shared._Hullrot.FireControl;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.XAML;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Hullrot.FireControl.UI;

[GenerateTypedNameReferences]
public sealed partial class FireControlWindow : FancyWindow
{
    public Action? OnServerRefresh;
    public FireControlWindow()
    {
        RobustXamlLoader.Load(this);
        Title = Loc.GetString("fire-control-window-title");
        RefreshButton.OnPressed += _ => OnServerRefresh?.Invoke();
    }

    public void UpdateStatus(FireControlConsoleBoundInterfaceState state)
    {
        if (state.Connected)
        {
            // RefreshButton.Disabled = true;
            ServerStatus.Text = Loc.GetString("fire-control-window-connected");
        }
        else
        {
            RefreshButton.Disabled = false;
            ServerStatus.Text = Loc.GetString("fire-control-window-disconnected");
        }

        foreach (var controllable in state.FireControllables)
        {
            var button = new Button();
            button.Text = controllable.Name;
            ControllablesBox.AddChild(button);
        }
    }
}
