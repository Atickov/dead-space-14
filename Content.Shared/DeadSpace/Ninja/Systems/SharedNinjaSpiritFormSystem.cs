using Content.Shared.Actions;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Interaction.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared.DeadSpace.Ninja.Systems;

public abstract class SharedNinjaSpiritFormSystem : EntitySystem
{
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedSpaceNinjaSystem _ninja = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaSpiritFormComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NinjaSpiritFormComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<NinjaSpiritFormComponent, NinjaSpiritFormEvent>(OnSpiritAction);

        SubscribeLocalEvent<NinjaSpiritFormComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<NinjaSpiritFormComponent, UseAttemptEvent>(OnUseAttempt);
        SubscribeLocalEvent<NinjaSpiritFormComponent, InteractionAttemptEvent>(OnInteractAttempt);
    }

    private void OnMapInit(
        Entity<NinjaSpiritFormComponent> ent,
        ref MapInitEvent args)
    {
        _actionContainer.EnsureAction(
            ent,
            ref ent.Comp.SpiritFormActionEntity,
            ent.Comp.SpiritFormAction);

        Dirty(ent);
    }

    private void OnGetActions(
        Entity<NinjaSpiritFormComponent> ent,
        ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;

        args.AddAction(ent.Comp.SpiritFormActionEntity);
    }

    private void OnSpiritAction(
        Entity<NinjaSpiritFormComponent> ent,
        ref NinjaSpiritFormEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.SpiritFormActive)
        {
            DeactivateSpirit(ent, args.Performer);
        }
        else
        {
            if (!TryGetBatteryMax(args.Performer, out var maxCharge))
                return;

            var cost = maxCharge * ent.Comp.EnergyDrainPercent;

            if (!_ninja.HasCharge(args.Performer, cost))
                return;

            ActivateSpirit(ent, args.Performer);
        }

        args.Handled = true;
    }

    public void ActivateSpirit(
        Entity<NinjaSpiritFormComponent> ent,
        EntityUid user)
    {
        if (!TryGetBatteryMax(user, out var maxCharge))
            return;

        var cost = maxCharge * ent.Comp.EnergyDrainPercent;

        if (!_ninja.TryUseCharge(user, cost))
            return;

        ent.Comp.SpiritFormActive = true;
        ent.Comp.DrainAccumulator = 0f;

        Dirty(ent);

        _audio.PlayPredicted(
            ent.Comp.ActivateSound,
            ent,
            user);

        SetSpiritCollision(user, true);
        SetSpiritAppearance(ent, true);
    }

    public void DeactivateSpirit(
        Entity<NinjaSpiritFormComponent> ent,
        EntityUid user)
    {
        if (!ent.Comp.SpiritFormActive)
            return;

        ent.Comp.SpiritFormActive = false;
        ent.Comp.DrainAccumulator = 0f;

        Dirty(ent);

        _audio.PlayPredicted(
            ent.Comp.DeactivateSound,
            ent,
            user);

        SetSpiritCollision(user, false);
        SetSpiritAppearance(ent, false);
    }

    protected virtual void SetSpiritCollision(
        EntityUid user,
        bool phasing)
    {
    }

    protected virtual void SetSpiritAppearance(
        Entity<NinjaSpiritFormComponent> ent,
        bool phasing)
    {
    }

    private void OnAttackAttempt(
        Entity<NinjaSpiritFormComponent> ent,
        ref AttackAttemptEvent args)
    {
        if (ent.Comp.SpiritFormActive)
            args.Cancel();
    }

    private void OnUseAttempt(
        Entity<NinjaSpiritFormComponent> ent,
        ref UseAttemptEvent args)
    {
        if (ent.Comp.SpiritFormActive)
            args.Cancel();
    }

    private void OnInteractAttempt(
        Entity<NinjaSpiritFormComponent> ent,
        ref InteractionAttemptEvent args)
    {
        if (ent.Comp.SpiritFormActive)
            args.Cancelled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NinjaSpiritFormComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.SpiritFormActive)
                continue;

            // Charge drain and forced deactivation are server-authoritative.
            // On the client TryUseCharge/TryGetBatteryMax always fail, which would
            // predictively deactivate the form every second until the next server state.
            if (_net.IsClient)
                continue;

            comp.DrainAccumulator += frameTime;

            if (comp.DrainAccumulator < 1f)
                continue;

            comp.DrainAccumulator -= 1f;

            var xform = Transform(uid);

            if (!xform.ParentUid.IsValid())
            {
                DeactivateSpirit((uid, comp), xform.ParentUid);
                continue;
            }

            var user = xform.ParentUid;

            if (!TryGetBatteryMax(user, out var maxCharge))
            {
                DeactivateSpirit((uid, comp), user);
                continue;
            }

            var cost = maxCharge * comp.EnergyDrainPercent;

            if (!_ninja.TryUseCharge(user, cost))
            {
                DeactivateSpirit((uid, comp), user);
            }
        }
    }

    private bool TryGetBatteryMax(
        EntityUid user,
        out float maxCharge)
    {
        maxCharge = 0f;

        if (!TryComp<SpaceNinjaComponent>(user, out var ninja))
            return false;

        if (ninja.Suit is not { } suit)
            return false;

        return TryGetSuitBatteryMax(suit, out maxCharge);
    }

    protected virtual bool TryGetSuitBatteryMax(
        EntityUid suit,
        out float maxCharge)
    {
        maxCharge = 0f;
        return false;
    }
}