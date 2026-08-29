using Content.Server.Humanoid;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Popups;
using Content.Shared.Actions;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Robust.Server.GameObjects;
using Content.Shared.DeadSpace.Ninja.Systems;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaScannerSystem : EntitySystem
{
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly NameModifierSystem _nameMod = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedChameleonClothingSystem _chameleon = default!;
    [Dependency] private readonly SharedSpaceNinjaSystem _ninja = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NinjaScannerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NinjaScannerComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<NinjaScannerComponent, NinjaScanActionEvent>(OnScan);
        SubscribeLocalEvent<NinjaScannerComponent, NinjaOpenScannerActionEvent>(OnOpenUi);
        SubscribeLocalEvent<NinjaScannerComponent, NinjaApplyDisguiseMessage>(OnApplyDisguise);
        SubscribeLocalEvent<NinjaScannerComponent, NinjaResetDisguiseMessage>(OnResetDisguise);
    }

    private void OnMapInit(Entity<NinjaScannerComponent> ent, ref MapInitEvent args)
    {
        var (uid, comp) = ent;
        _actions.AddAction(uid, ref comp.ScanActionEntity, comp.ScanAction);
        _actions.AddAction(uid, ref comp.OpenUiActionEntity, comp.OpenUiAction);
        Dirty(uid, comp);
    }

    private void OnGetActions(Entity<NinjaScannerComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;
        args.AddAction(ent.Comp.ScanActionEntity);
        args.AddAction(ent.Comp.OpenUiActionEntity);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NinjaScannerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsDisguised)
                continue;

            var performer = Transform(uid).ParentUid;
            if (!performer.IsValid() || !HasComp<HumanoidAppearanceComponent>(performer))
                continue;

            var energyCost = comp.DisguiseEnergyCost * frameTime;

            if (!_ninja.TryUseCharge(performer, energyCost))
            {
                _popup.PopupEntity(Loc.GetString("ninja-scanner-disguise-out-of-energy"), performer, performer, PopupType.SmallCaution);
                RevertDisguise((uid, comp), performer);
            }
        }
    }

    private void OnOpenUi(Entity<NinjaScannerComponent> ent, ref NinjaOpenScannerActionEvent args)
    {
        args.Handled = true;
        _ui.OpenUi(ent.Owner, NinjaScannerUiKey.Key, args.Performer);
        UpdateUi(ent);
    }

    private void OnScan(Entity<NinjaScannerComponent> ent, ref NinjaScanActionEvent args)
    {
        var target = args.Target;
        if (target == EntityUid.Invalid || !Exists(target))
            return;

        if (!HasComp<HumanoidAppearanceComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("ninja-scanner-invalid-target"), ent, args.Performer);
            return;
        }

        args.Handled = true;

        var name = MetaData(target).EntityName;
        var data = new NinjaScanData(name, GetNetEntity(target));

        ent.Comp.ScannedTargets.RemoveAll(d => d.Target == data.Target);
        ent.Comp.ScannedTargets.Insert(0, data);

        while (ent.Comp.ScannedTargets.Count > ent.Comp.MaxScans)
            ent.Comp.ScannedTargets.RemoveAt(ent.Comp.ScannedTargets.Count - 1);

        Dirty(ent);
        UpdateUi(ent);

        _popup.PopupEntity(Loc.GetString("ninja-scanner-scan-success", ("target", name)), ent, args.Performer);
    }

    private void OnApplyDisguise(Entity<NinjaScannerComponent> ent, ref NinjaApplyDisguiseMessage args)
    {
        var performer = args.Actor;
        var target = GetEntity(args.Target);

        if (!Exists(target))
            return;

        if (!ent.Comp.ScannedTargets.Exists(d => GetEntity(d.Target) == target))
            return;

        ApplyDisguise(ent, target, performer);
    }

    private void OnResetDisguise(Entity<NinjaScannerComponent> ent, ref NinjaResetDisguiseMessage args)
    {
        var performer = args.Actor;
        if (!ent.Comp.IsDisguised)
            return;

        RevertDisguise(ent, performer);
    }

    private void ApplyDisguise(Entity<NinjaScannerComponent> ent, EntityUid target, EntityUid performer)
    {
        var comp = ent.Comp;

        if (!comp.IsDisguised)
        {
            SaveOriginalAppearance(performer, comp);
            comp.IsDisguised = true;
        }

        if (TryComp<HumanoidAppearanceComponent>(target, out var targetHumanoid) && HasComp<HumanoidAppearanceComponent>(performer))
        {
            _humanoid.CloneAppearance(target, performer);

            if (HasComp<InventoryComponent>(performer))
            {
                _inventory.SetInventorySpecies(performer, targetHumanoid.Species);
            }
        }

        var targetName = _nameMod.GetBaseName(target);
        _metaData.SetEntityName(performer, targetName);

        CopyChameleonClothing(target, performer);

        Dirty(ent);
        UpdateUi(ent);

        _popup.PopupEntity(Loc.GetString("ninja-scanner-disguise-success", ("target", MetaData(target).EntityName)), performer, performer);
    }

    private void RevertDisguise(Entity<NinjaScannerComponent> ent, EntityUid performer)
    {
        var comp = ent.Comp;
        if (!comp.IsDisguised)
            return;

        RestoreOriginalAppearance(performer, comp);
        ResetChameleonClothing(performer);
        comp.IsDisguised = false;
        Dirty(ent);
        UpdateUi(ent);
        _popup.PopupEntity(Loc.GetString("ninja-scanner-disguise-reverted"), performer, performer);
    }

    private void SaveOriginalAppearance(EntityUid performer, NinjaScannerComponent comp)
    {
        comp.OriginalName = _nameMod.GetBaseName(performer);

        if (TryComp<HumanoidAppearanceComponent>(performer, out var humanoid))
        {
            comp.OriginalSpecies = humanoid.Species;
            comp.OriginalMarkings = new MarkingSet(humanoid.MarkingSet);
            comp.OriginalSkinColor = humanoid.SkinColor;
        }
    }

    private void RestoreOriginalAppearance(EntityUid performer, NinjaScannerComponent comp)
    {
        if (comp.OriginalName != null)
            _metaData.SetEntityName(performer, comp.OriginalName);

        if (TryComp<HumanoidAppearanceComponent>(performer, out var humanoid))
        {
            if (comp.OriginalSpecies != null)
            {
                _humanoid.SetSpecies(performer, comp.OriginalSpecies, humanoid: humanoid);

                if (HasComp<InventoryComponent>(performer))
                    _inventory.SetInventorySpecies(performer, comp.OriginalSpecies);
            }

            if (comp.OriginalSkinColor.HasValue)
                _humanoid.SetSkinColor(performer, comp.OriginalSkinColor.Value, humanoid: humanoid);

            if (comp.OriginalMarkings != null)
            {
                humanoid.MarkingSet = new MarkingSet(comp.OriginalMarkings);
                Dirty(performer, humanoid);
            }
        }
    }

    private void CopyChameleonClothing(EntityUid target, EntityUid performer)
    {
        if (!_inventory.TryGetSlots(performer, out var ninjaSlots))
            return;

        foreach (var slot in ninjaSlots)
        {
            if (!_inventory.TryGetSlotEntity(performer, slot.Name, out var ninjaItem) || !TryComp<ChameleonClothingComponent>(ninjaItem, out var chameleon))
                continue;

            if (_inventory.TryGetSlotEntity(target, slot.Name, out var targetItem) && MetaData(targetItem.Value).EntityPrototype?.ID is { } targetProto)
            {
                _chameleon.SetSelectedPrototype(ninjaItem.Value, targetProto, component: chameleon);
            }
            else
            {
                _chameleon.SetSelectedPrototype(ninjaItem.Value, null, component: chameleon);
            }
        }
    }

    private void ResetChameleonClothing(EntityUid performer)
    {
        if (!_inventory.TryGetSlots(performer, out var slots))
            return;

        foreach (var slot in slots)
        {
            if (!_inventory.TryGetSlotEntity(performer, slot.Name, out var ninjaItem))
                continue;

            if (TryComp<ChameleonClothingComponent>(ninjaItem, out var chameleon))
            {
                _chameleon.SetSelectedPrototype(ninjaItem.Value, null, component: chameleon);
            }
        }
    }

    private void UpdateUi(Entity<NinjaScannerComponent> ent)
    {
        if (!_ui.HasUi(ent.Owner, NinjaScannerUiKey.Key))
            return;

        _ui.SetUiState(ent.Owner, NinjaScannerUiKey.Key, new NinjaScannerBoundUserInterfaceState(ent.Comp.ScannedTargets, ent.Comp.IsDisguised));
    }
}