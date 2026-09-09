using Content.Shared.DeadSpace.Ninja.Components;

namespace Content.Shared.DeadSpace.Ninja.Systems;

public abstract class SharedNinjaAppearanceSystem : EntitySystem
{
    public void SetAppearance(Entity<NinjaAppearanceComponent> ent, NinjaColorway colorway, bool helmetShown)
    {
        var comp = ent.Comp;
        comp.Colorway = colorway;
        comp.HelmetShown = helmetShown;
        comp.ScarfShown = !helmetShown;
        Dirty(ent, comp);
        OnHelmetHiddenChanged(ent, helmetShown);
    }

    protected virtual void OnHelmetHiddenChanged(Entity<NinjaAppearanceComponent> ent, bool shown)
    {
    }
}