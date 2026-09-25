// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Ninja.Components;
using Robust.Shared.Containers;

namespace Content.Shared.DeadSpace.Ninja.Systems;

public abstract class SharedNinjaDisguiseSystem : EntitySystem
{
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaDisguiseComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NinjaDisguiseComponent, GetItemActionsEvent>(OnGetItemActions);

        SubscribeLocalEvent<NinjaDisguiseComponent, SpiderOSPowerChangedEvent>(OnSpiderOSPowerChanged);
    }

    private void OnMapInit(Entity<NinjaDisguiseComponent> ent, ref MapInitEvent args)
    {
        var (uid, comp) = ent;
        _actionContainer.EnsureAction(uid, ref comp.ActionScanEntity, comp.ActionScan);
        _actionContainer.EnsureAction(uid, ref comp.ActionMenuEntity, comp.ActionMenu);
        Dirty(uid, comp);
    }

    private void OnSpiderOSPowerChanged(Entity<NinjaDisguiseComponent> ent, ref SpiderOSPowerChangedEvent args)
    {
        if (!args.Activated)
        {
            _actions.RemoveAction(ent.Comp.ActionScanEntity);
            _actions.RemoveAction(ent.Comp.ActionMenuEntity);
            var revealed = new NinjaDisguiseRevealedEvent();
            RaiseLocalEvent(ent, ref revealed);
        }
    }

    private void OnGetItemActions(Entity<NinjaDisguiseComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;

        args.AddAction(ent.Comp.ActionScanEntity);
        args.AddAction(ent.Comp.ActionMenuEntity);
    }

    public bool TryGetActiveDisguise(EntityUid wearer, [NotNullWhen(true)] out EntityUid suitUid,
        [NotNullWhen(true)] out NinjaDisguiseComponent? comp)
    {
        suitUid = EntityUid.Invalid;
        comp = null!;

        if (!TryComp<ContainerManagerComponent>(wearer, out var containers))
            return false;

        foreach (var container in containers.Containers.Values)
        {
            if (container.Count != 1)
                continue;

            var item = container.ContainedEntities[0];
            if (!item.IsValid() || !TryComp(item, out comp) || !comp.Disguised)
                continue;

            suitUid = item;
            return true;
        }

        return false;
    }
}