using Content.Shared.Actions;
using Content.Shared.DeadSpace.Ninja.Components;

namespace Content.Shared.DeadSpace.Ninja.Systems;
public abstract class SharedNinjaHolographicClonesSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaHolographicClonesComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NinjaHolographicClonesComponent, GetItemActionsEvent>(OnGetActions);
    }

    private void OnMapInit(Entity<NinjaHolographicClonesComponent> ent, ref MapInitEvent args)
    {
        var (uid, comp) = ent;
        _actions.AddAction(uid, ref comp.HolographicClonesActionEntity, comp.HolographicClonesAction);
        Dirty(uid, comp);
    }

    private void OnGetActions(Entity<NinjaHolographicClonesComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;
        args.AddAction(ent.Comp.HolographicClonesActionEntity);
    }
}