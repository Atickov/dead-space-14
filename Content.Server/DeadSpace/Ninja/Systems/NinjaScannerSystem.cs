using System;
using System.Collections.Generic;
using Content.Shared.Actions;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Cloning;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaScannerSystem : EntitySystem
{
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private readonly SharedCloningSystem _cloning = default!;
    [Dependency] private readonly IdentitySystem _identity = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedChameleonClothingSystem _chameleon = default!;
    [Dependency] private readonly SharedSpaceNinjaSystem _ninja = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;

    private readonly Dictionary<EntityUid, EntityUid> _originalIdentities = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NinjaScannerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NinjaScannerComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<NinjaScannerComponent, SpiderOSPowerChangedEvent>(OnSpiderOSPowerChanged);
        SubscribeLocalEvent<NinjaScannerComponent, NinjaScanActionEvent>(OnScan);
        SubscribeLocalEvent<NinjaScannerComponent, NinjaOpenScannerActionEvent>(OnOpenUi);
        SubscribeLocalEvent<NinjaScannerComponent, NinjaApplyDisguiseMessage>(OnApplyDisguise);
        SubscribeLocalEvent<NinjaScannerComponent, NinjaResetDisguiseMessage>(OnResetDisguise);
        SubscribeLocalEvent<NinjaScannerComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<NinjaScannerComponent> ent, ref MapInitEvent args)
    {
        var (uid, comp) = ent;
        _actions.AddAction(uid, ref comp.ScanActionEntity, comp.ScanAction);
        _actions.AddAction(uid, ref comp.OpenUiActionEntity, comp.OpenUiAction);
        Dirty(uid, comp);
    }

    private void OnShutdown(Entity<NinjaScannerComponent> ent, ref ComponentShutdown args)
    {
        var performer = Transform(ent.Owner).ParentUid;
        if (performer.IsValid() && _originalIdentities.Remove(performer, out var original))
        {
            QueueDel(original);
        }
    }

    private void OnGetActions(Entity<NinjaScannerComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;

        if (!TryComp<SpiderOSComponent>(ent.Owner, out var os) || !os.SuitActivated)
            return;

        args.AddAction(ent.Comp.ScanActionEntity);
        args.AddAction(ent.Comp.OpenUiActionEntity);
    }

    private void OnSpiderOSPowerChanged(Entity<NinjaScannerComponent> ent, ref SpiderOSPowerChangedEvent args)
    {
        if (!args.Activated)
        {
            _actions.RemoveAction(ent.Comp.ScanActionEntity);
            _actions.RemoveAction(ent.Comp.OpenUiActionEntity);
        }
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

        if (!target.IsValid() || !Exists(target))
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

    private CloningSettingsPrototype? GetCloningSettings()
    {
        if (_prototype.TryIndex<CloningSettingsPrototype>("Changeling", out var settings))
            return settings;

        if (_prototype.TryIndex<CloningSettingsPrototype>("Cloning", out var fallback))
            return fallback;

        return null;
    }

    private void ApplyDisguise(Entity<NinjaScannerComponent> ent, EntityUid target, EntityUid performer)
    {
        var comp = ent.Comp;
        var settings = GetCloningSettings();

        if (!comp.IsDisguised)
        {
            SaveOriginalAppearance(performer, settings);
            comp.IsDisguised = true;
        }

        CopyFullIdentity(target, performer, settings);
        CopyChameleonClothing(target, performer);

        Dirty(ent);
        UpdateUi(ent);

        _popup.PopupEntity(Loc.GetString("ninja-scanner-disguise-success", ("target", MetaData(target).EntityName)), performer, performer);
    }

    private void SaveOriginalAppearance(EntityUid performer, CloningSettingsPrototype? settings)
    {
        if (_originalIdentities.ContainsKey(performer))
            return;

        var original = Spawn(null, MapCoordinates.Nullspace);

        CopyFullIdentity(performer, original, settings);

        _originalIdentities[performer] = original;
    }

    private void RevertDisguise(Entity<NinjaScannerComponent> ent, EntityUid performer)
    {
        var comp = ent.Comp;
        if (!comp.IsDisguised)
            return;

        var settings = GetCloningSettings();

        if (_originalIdentities.Remove(performer, out var original) && Exists(original))
        {
            CopyFullIdentity(original, performer, settings);
            QueueDel(original);
        }

        ResetChameleonClothing(performer);
        comp.IsDisguised = false;
        Dirty(ent);
        UpdateUi(ent);
        _popup.PopupEntity(Loc.GetString("ninja-scanner-disguise-reverted"), performer, performer);
    }

    private void CopyFullIdentity(EntityUid source, EntityUid target, CloningSettingsPrototype? settings)
    {
        if (HasComp<HumanoidAppearanceComponent>(source))
        {
            EnsureComp<HumanoidAppearanceComponent>(target);
            _humanoidAppearance.CloneAppearance(source, target);
        }

        if (settings != null)
        {
            _cloning.CloneComponents(source, target, settings);

            if (settings.CopyStatusEffects)
            {
                _cloning.CopyStatusEffects(
                    source,
                    target,
                    settings.StatusEffectWhitelist,
                    settings.StatusEffectBlacklist);
            }
        }

        _metaData.SetEntityName(target, Name(source), raiseEvents: false);
        _metaData.SetEntityDescription(target, Description(source));
        _identity.QueueIdentityUpdate(target);

        if (TryComp<HumanoidAppearanceComponent>(source, out var sourceHumanoid) && HasComp<InventoryComponent>(target))
        {
            _inventory.SetInventorySpecies(target, sourceHumanoid.Species);
        }

        CopyExtraIdentityComponents(source, target);
    }

    private void CopyExtraIdentityComponents(EntityUid source, EntityUid target)
    {
        var targetComps = new List<IComponent>(EntityManager.GetComponents(target));
        foreach (var comp in targetComps)
        {
            var name = comp.GetType().Name;
            if (IsIdentityComponent(name) && !HasComponentOfType(source, comp.GetType()))
            {
                EntityManager.RemoveComponent(target, comp);
            }
        }

        foreach (var comp in EntityManager.GetComponents(source))
        {
            var name = comp.GetType().Name;
            if (IsIdentityComponent(name))
            {
                if (_serialization.CreateCopy(comp, notNullableOverride: true) is Component copied)
                {
                    EntityManager.AddComponent(target, copied, overwrite: true);
                }
            }
        }
    }

    private static bool IsIdentityComponent(string name)
    {
        return name.Contains("TTS") ||
               name.Contains("TypingIndicator") ||
               name.Contains("Voice") ||
               name.Contains("Grammar") ||
               name.Contains("Speech") ||
               name.Contains("Dna");
    }

    private bool HasComponentOfType(EntityUid entity, Type type)
    {
        foreach (var comp in EntityManager.GetComponents(entity))
        {
            if (comp.GetType() == type)
                return true;
        }
        return false;
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