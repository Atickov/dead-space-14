using Content.Shared.Clothing;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Hands.EntitySystems;

namespace Content.Shared.DeadSpace.Ninja.Systems;

public sealed class SharedNinjaMartialArtSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaMartialArtComponent, ClothingGotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<NinjaMartialArtComponent, ClothingGotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<NinjaMartialArtComponent, SpiderOSPowerChangedEvent>(OnSuitPowerChange);
        SubscribeLocalEvent<NinjaMartialArtComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnGotEquipped(Entity<NinjaMartialArtComponent> ent, ref ClothingGotEquippedEvent args)
    {
        if (TryComp<SpiderOSComponent>(ent, out var spiderOS))
            SetActive(args.Wearer, ent, spiderOS.SuitActivated);
        else
            SetActive(args.Wearer, ent, true);
    }

    private void OnGotUnequipped(Entity<NinjaMartialArtComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        SetActive(args.Wearer, ent, false);
    }

    private void OnSuitPowerChange(Entity<NinjaMartialArtComponent> ent, ref SpiderOSPowerChangedEvent args)
    {
        SetActive(args.Wearer, ent, args.Activated);
    }

    private void OnShutdown(Entity<NinjaMartialArtComponent> ent, ref ComponentShutdown args)
    {
        var user = Transform(ent).ParentUid;
        if (user == ent.Owner || !Exists(user))
            return;

        SetActive(user, ent, false);
    }

    private void SetActive(EntityUid user, Entity<NinjaMartialArtComponent> ent, bool active)
    {
        if (ent.Comp.Active == active)
            return;

        ent.Comp.Active = active;
        Dirty(ent);

        var ev = new EnergyKatanaMartialArtActiveEvent(active);
        foreach (var held in _hands.EnumerateHeld(user))
            RaiseLocalEvent(held, ref ev);
    }
}