using Content.Shared.Actions;
using Content.Shared.DeadSpace.Ninja.Components;

namespace Content.Shared.DeadSpace.Ninja.Systems;

public abstract class SharedNinjaAppearanceSystem : EntitySystem
{
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedSpaceNinjaSystem _ninja = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaAppearanceComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NinjaAppearanceComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<NinjaAppearanceComponent, CycleNinjaColorEvent>(OnCycleColor);
        SubscribeLocalEvent<NinjaAppearanceComponent, ToggleNinjaScarfEvent>(OnToggleScarf);
        SubscribeLocalEvent<NinjaAppearanceComponent, ToggleNinjaHelmetEvent>(OnToggleHelmet);
    }

    private void OnMapInit(Entity<NinjaAppearanceComponent> ent, ref MapInitEvent args)
    {
        var (uid, comp) = ent;
        _actionContainer.EnsureAction(uid, ref comp.CycleColorActionEntity, comp.CycleColorAction);
        _actionContainer.EnsureAction(uid, ref comp.ToggleScarfActionEntity, comp.ToggleScarfAction);
        _actionContainer.EnsureAction(uid, ref comp.ToggleHelmetActionEntity, comp.ToggleHelmetAction);
        Dirty(uid, comp);
    }

    private void OnGetItemActions(Entity<NinjaAppearanceComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands || !_ninja.IsNinja(args.User))
            return;

        var comp = ent.Comp;
        args.AddAction(ref comp.CycleColorActionEntity, comp.CycleColorAction);
        args.AddAction(ref comp.ToggleScarfActionEntity, comp.ToggleScarfAction);
        args.AddAction(ref comp.ToggleHelmetActionEntity, comp.ToggleHelmetAction);
    }

    private void OnCycleColor(Entity<NinjaAppearanceComponent> ent, ref CycleNinjaColorEvent args)
    {
        if (args.Handled)
            return;

        ent.Comp.Colorway = (NinjaColorway)(((int)ent.Comp.Colorway + 1) % 3);
        Dirty(ent, ent.Comp);
        args.Handled = true;
    }

    private void OnToggleScarf(Entity<NinjaAppearanceComponent> ent, ref ToggleNinjaScarfEvent args)
    {
        if (args.Handled)
            return;

        ent.Comp.ScarfShown = !ent.Comp.ScarfShown;
        Dirty(ent, ent.Comp);
        args.Handled = true;
    }

    private void OnToggleHelmet(Entity<NinjaAppearanceComponent> ent, ref ToggleNinjaHelmetEvent args)
    {
        if (args.Handled)
            return;

        ent.Comp.HelmetShown = !ent.Comp.HelmetShown;
        Dirty(ent, ent.Comp);
        OnHelmetHiddenChanged(ent, ent.Comp.HelmetShown);
        args.Handled = true;
    }

    protected virtual void OnHelmetHiddenChanged(Entity<NinjaAppearanceComponent> ent, bool shown)
    {
    }
}