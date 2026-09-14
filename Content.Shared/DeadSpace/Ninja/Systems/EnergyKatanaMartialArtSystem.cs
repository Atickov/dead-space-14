using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Hands;
using Content.Shared.Inventory.Events;
using Content.Shared.Weapons.Reflect;

namespace Content.Shared.DeadSpace.Ninja.Systems;

public sealed class EnergyKatanaMartialArtSystem : EntitySystem
{
    [Dependency] private readonly SharedSpaceNinjaSystem _ninja = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EnergyKatanaMartialArtComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<EnergyKatanaMartialArtComponent, GotEquippedHandEvent>(OnEquippedHand);
        SubscribeLocalEvent<EnergyKatanaMartialArtComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<EnergyKatanaMartialArtComponent, GotUnequippedHandEvent>(OnUnequippedHand);
        SubscribeLocalEvent<EnergyKatanaMartialArtComponent, EnergyKatanaMartialArtActiveEvent>(OnMartialArtActive);
    }

    private void OnEquipped(Entity<EnergyKatanaMartialArtComponent> ent, ref GotEquippedEvent args)
    {
        if (IsMartialArtActive(args.Equipee))
            Boost(ent);
    }

    private void OnEquippedHand(Entity<EnergyKatanaMartialArtComponent> ent, ref GotEquippedHandEvent args)
    {
        if (IsMartialArtActive(args.User))
            Boost(ent);
    }

    private void OnUnequipped(Entity<EnergyKatanaMartialArtComponent> ent, ref GotUnequippedEvent args)
    {
        Restore(ent);
    }

    private void OnUnequippedHand(Entity<EnergyKatanaMartialArtComponent> ent, ref GotUnequippedHandEvent args)
    {
        Restore(ent);
    }

    private void OnMartialArtActive(Entity<EnergyKatanaMartialArtComponent> ent, ref EnergyKatanaMartialArtActiveEvent args)
    {
        if (args.Active)
            Boost(ent);
        else
            Restore(ent);
    }

    public void Boost(Entity<EnergyKatanaMartialArtComponent> ent)
    {
        if (ent.Comp.Boosted)
            return;

        if (!TryComp<ReflectComponent>(ent, out var reflect))
            return;

        ent.Comp.OriginalReflectProb = reflect.ReflectProb;
        ent.Comp.Boosted = true;
        Dirty(ent);

        reflect.ReflectProb += ent.Comp.ReflectProbBonus;
        Dirty(ent, reflect);
    }

    public void Restore(Entity<EnergyKatanaMartialArtComponent> ent)
    {
        if (!ent.Comp.Boosted)
            return;

        if (TryComp<ReflectComponent>(ent, out var reflect))
        {
            reflect.ReflectProb = ent.Comp.OriginalReflectProb;
            Dirty(ent, reflect);
        }

        ent.Comp.Boosted = false;
        Dirty(ent);
    }

    private bool IsMartialArtActive(EntityUid user)
    {
        if (!_ninja.NinjaQuery.TryComp(user, out var ninja) || ninja.Suit is not { } suit)
            return false;

        return TryComp<NinjaMartialArtComponent>(suit, out var martial) && martial.Active;
    }
}

[ByRefEvent]
public readonly record struct EnergyKatanaMartialArtActiveEvent(bool Active);