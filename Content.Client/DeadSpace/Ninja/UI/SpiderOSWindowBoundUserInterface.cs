using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.Ninja.UI;

public sealed class SpiderOSWindowBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    [ViewVariables]
    private SpiderOSWindow? _window;

    public SpiderOSWindowBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        IoCManager.InjectDependencies(this);

        _window = new SpiderOSWindow();

        _window.OnModuleSelected += (tier, category) =>
        {
            SendMessage(new SpiderOSSelectModuleMessage(tier, category));
        };

        _window.OnAppearanceChanged += (colorway, helmet) =>
        {
            SendMessage(new SpiderOSSetAppearanceMessage(colorway, helmet));
        };

        _window.OnSuitPowerChanged += activated =>
        {
            SendMessage(new SpiderOSSetSuitPowerMessage(activated));
        };

        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not SpiderOSBoundUserInterfaceState cState)
            return;

        if (!_proto.TryIndex(cState.Skills, out SpiderOSPrototype? proto))
            return;

        _window.UpdateState(
            cState.LockedTiers,
            cState.SelectedModules,
            cState.ActivatedTiers,
            proto.AllSkills,
            cState.PendingColorway,
            cState.PendingHelmet,
            cState.SuitActivated);
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