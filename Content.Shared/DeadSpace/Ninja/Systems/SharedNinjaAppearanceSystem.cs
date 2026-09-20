using Content.Shared.DeadSpace.Ninja.Components;

namespace Content.Shared.DeadSpace.Ninja.Systems;

public abstract class SharedNinjaAppearanceSystem : EntitySystem
{
    public void SetAppearance(Entity<NinjaAppearanceComponent> ent, NinjaColorway colorway, bool helmetShown)
    {
        SetAppearance(ent, colorway, helmetShown, ent.Comp.Style);
    }

    public void SetAppearance(Entity<NinjaAppearanceComponent> ent, NinjaColorway colorway, bool helmetShown,
        NinjaStyle style)
    {
        var comp = ent.Comp;
        comp.Colorway = colorway;
        comp.HelmetShown = helmetShown;
        comp.ScarfShown = !helmetShown;
        comp.Style = style;
        Dirty(ent, comp);
        OnHelmetHiddenChanged(ent, helmetShown);
    }

    protected virtual void OnHelmetHiddenChanged(Entity<NinjaAppearanceComponent> ent, bool shown)
    {
    }
}