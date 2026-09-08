using Content.Shared.Emp;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Ninja.Components;

namespace Content.Shared.DeadSpace.Ninja.Systems;

public sealed class SharedNinjaEmpAbilitySystem : EntitySystem
{
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedEmpSystem _emp = default!;
    [Dependency] private readonly SharedSpaceNinjaSystem _ninja = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaEmpAbilityComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NinjaEmpAbilityComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<NinjaEmpAbilityComponent, NinjaEmpEvent>(OnEmp);
    }

    private void OnMapInit(Entity<NinjaEmpAbilityComponent> ent, ref MapInitEvent args)
    {
        var (uid, comp) = ent;
        _actionContainer.EnsureAction(uid, ref comp.EmpActionEntity, comp.EmpAction);
        Dirty(uid, comp);
    }

    private void OnGetActions(Entity<NinjaEmpAbilityComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;
        args.AddAction(ent.Comp.EmpActionEntity);
    }

    private void OnEmp(Entity<NinjaEmpAbilityComponent> ent, ref NinjaEmpEvent args)
    {
        if (!_ninja.TryUseCharge(args.Performer, ent.Comp.Charge))
            return;

        args.Handled = true;
        var (uid, comp) = ent;
        _emp.EmpPulse(Transform(uid).Coordinates, comp.EmpRange, comp.EmpConsumption, comp.EmpDuration, args.Performer);
    }
}
