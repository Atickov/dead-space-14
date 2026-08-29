using JetBrains.Annotations;
using Content.Shared.DeadSpace.Ninja.Components;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.Ninja.UI;

[UsedImplicitly]
public sealed class SpiderOSWindowBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private SpiderOSWindow? _window;

    public SpiderOSWindowBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new SpiderOSWindow();

        _window.OnModuleSelected += (tier, category) =>
        {
            SendMessage(new SpiderOSSelectModuleMessage(tier, category));
        };

        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not SpiderOSBoundUserInterfaceState cState)
            return;

        _window.UpdateState(cState.LockedTiers, cState.SelectedModules);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _window?.Close();
        _window = null;
    }
}