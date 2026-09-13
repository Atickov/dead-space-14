using Content.Shared.Actions;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Inventory.Events;

namespace Content.Shared.DeadSpace.Ninja.Systems;

public abstract class SharedNinjaCloakSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpaceNinjaComponent, ToggleCloakNinjaEvent>(OnNinjaToggleCloak);
        SubscribeLocalEvent<NinjaCloakComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<NinjaCloakComponent, GotUnequippedEvent>(OnUnequipped);
    }


    private void OnNinjaToggleCloak(Entity<SpaceNinjaComponent> ent, ref ToggleCloakNinjaEvent args)
    {
        args.Handled = true;
        if (ent.Comp.Suit is not { } suitUid)
            return;

        if (!TryComp<NinjaCloakComponent>(suitUid, out var cloak))
            return;

        cloak.Enabled = !cloak.Enabled;
        Dirty(suitUid, cloak);

        AfterToggleCloak(ent, suitUid, cloak);
    }
    protected virtual void AfterToggleCloak(Entity<SpaceNinjaComponent> ent, EntityUid suitUid, NinjaCloakComponent cloak) { }

    private void OnGetActions(Entity<NinjaCloakComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;

        args.AddAction(ent.Comp.ActionEntity);
    }

    private void OnUnequipped(Entity<NinjaCloakComponent> ent, ref GotUnequippedEvent args)
    {
        if (!ent.Comp.Enabled)
            return;

        ent.Comp.Enabled = false;
        Dirty(ent);
    }
}