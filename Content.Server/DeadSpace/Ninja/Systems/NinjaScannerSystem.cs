using System.Collections.Generic;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Ninja;
using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaScannerSystem : EntitySystem
{
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private readonly IdentitySystem _identity = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedSpaceNinjaSystem _ninja = default!;

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

        if (performer.IsValid() &&
            _originalIdentities.Remove(performer, out var original))
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

            if (!performer.IsValid() ||
                !HasComp<HumanoidAppearanceComponent>(performer))
                continue;

            var energyCost = comp.DisguiseEnergyCost * frameTime;

            if (!_ninja.TryUseCharge(performer, energyCost))
            {
                _popup.PopupEntity(
                    Loc.GetString("ninja-scanner-disguise-out-of-energy"),
                    performer,
                    performer,
                    PopupType.SmallCaution);

                RevertDisguise((uid, comp), performer);
            }
        }
    }

    private void OnOpenUi(Entity<NinjaScannerComponent> ent, ref NinjaOpenScannerActionEvent args)
    {
        args.Handled = true;

        _ui.OpenUi(
            ent.Owner,
            NinjaScannerUiKey.Key,
            args.Performer);

        UpdateUi(ent);
    }

    private void OnScan(Entity<NinjaScannerComponent> ent, ref NinjaScanActionEvent args)
    {
        var target = args.Target;

        if (target == EntityUid.Invalid || !Exists(target))
            return;

        if (!HasComp<HumanoidAppearanceComponent>(target))
        {
            _popup.PopupEntity(
                Loc.GetString("ninja-scanner-invalid-target"),
                ent,
                args.Performer);

            return;
        }

        args.Handled = true;

        var name = MetaData(target).EntityName;
        var data = new NinjaScanData(
            name,
            GetNetEntity(target));

        ent.Comp.ScannedTargets.RemoveAll(d => d.Target == data.Target);
        ent.Comp.ScannedTargets.Insert(0, data);

        while (ent.Comp.ScannedTargets.Count > ent.Comp.MaxScans)
        {
            ent.Comp.ScannedTargets.RemoveAt(
                ent.Comp.ScannedTargets.Count - 1);
        }

        Dirty(ent);
        UpdateUi(ent);

        _popup.PopupEntity(
            Loc.GetString(
                "ninja-scanner-scan-success",
                ("target", name)),
            ent,
            args.Performer);
    }

    private void OnApplyDisguise(
        Entity<NinjaScannerComponent> ent,
        ref NinjaApplyDisguiseMessage args)
    {
        var performer = args.Actor;
        var target = GetEntity(args.Target);

        if (!target.IsValid() || !Exists(target))
            return;

        ApplyDisguise(ent, target, performer);
    }

    private void OnResetDisguise(
        Entity<NinjaScannerComponent> ent,
        ref NinjaResetDisguiseMessage args)
    {
        var performer = args.Actor;

        if (!ent.Comp.IsDisguised)
            return;

        RevertDisguise(ent, performer);
    }

    private void ApplyDisguise(
        Entity<NinjaScannerComponent> ent,
        EntityUid target,
        EntityUid performer)
    {
        var comp = ent.Comp;

        if (!comp.IsDisguised)
        {
            SaveOriginalAppearance(performer);
            comp.IsDisguised = true;
        }

        CopyVisualAppearance(target, performer);

        RaiseNetworkEvent(
            new NinjaSpriteEvent(
                GetNetEntity(target),
                GetNetEntity(performer)));

        Dirty(ent);
        UpdateUi(ent);

        _popup.PopupEntity(
            Loc.GetString(
                "ninja-scanner-disguise-success",
                ("target", MetaData(target).EntityName)),
            performer,
            performer);
    }

    private void SaveOriginalAppearance(EntityUid performer)
    {
        if (_originalIdentities.ContainsKey(performer))
            return;

        var original = Spawn(
            null,
            MapCoordinates.Nullspace);

        CopyVisualAppearance(
            performer,
            original);

        _originalIdentities[performer] = original;
    }

    private void RevertDisguise(
        Entity<NinjaScannerComponent> ent,
        EntityUid performer)
    {
        var comp = ent.Comp;

        if (!comp.IsDisguised)
            return;

        if (_originalIdentities.Remove(
                performer,
                out var original) &&
            Exists(original))
        {
            CopyVisualAppearance(
                original,
                performer);

            RaiseNetworkEvent(
                new NinjaSpriteEvent(
                    GetNetEntity(original),
                    GetNetEntity(performer),
                    true));

            QueueDel(original);
        }

        comp.IsDisguised = false;

        Dirty(ent);
        UpdateUi(ent);

        _popup.PopupEntity(
            Loc.GetString(
                "ninja-scanner-disguise-reverted"),
            performer,
            performer);
    }

    private void CopyVisualAppearance(
        EntityUid source,
        EntityUid target)
    {
        if (TryComp<HumanoidAppearanceComponent>(
                source,
                out var sourceHumanoid))
        {
            var targetHumanoid =
                EnsureComp<HumanoidAppearanceComponent>(
                    target);

            _humanoidAppearance.CloneAppearance(
                source,
                target,
                sourceHumanoid,
                targetHumanoid);

            Dirty(target, targetHumanoid);

            if (HasComp<InventoryComponent>(target))
            {
                _inventory.SetInventorySpecies(
                    target,
                    sourceHumanoid.Species);
            }
        }

        _metaData.SetEntityName(
            target,
            Name(source),
            raiseEvents: false);

        _metaData.SetEntityDescription(
            target,
            Description(source));

        _identity.QueueIdentityUpdate(target);
    }

    private void UpdateUi(Entity<NinjaScannerComponent> ent)
    {
        if (!_ui.HasUi(
                ent.Owner,
                NinjaScannerUiKey.Key))
            return;

        _ui.SetUiState(
            ent.Owner,
            NinjaScannerUiKey.Key,
            new NinjaScannerBoundUserInterfaceState(
                ent.Comp.ScannedTargets,
                ent.Comp.IsDisguised));
    }
}