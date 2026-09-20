using System.Linq;
using Content.Shared.DeadSpace.Ninja;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Chemistry.Components;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaInfoScannerSystem : SharedNinjaInfoScannerSystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly NinjaInfoObjectiveSystem _objectiveSystem = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _scanEndTimes = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaInfoScannerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<NinjaInfoScannerComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<NinjaInfoScannerComponent, EntInsertedIntoContainerMessage>(OnContainerInserted);
        SubscribeLocalEvent<NinjaInfoScannerComponent, EntRemovedFromContainerMessage>(OnContainerRemoved);
        SubscribeLocalEvent<NinjaInfoScannerComponent, NinjaInfoScannerScanMessage>(OnScanMessage);
        SubscribeLocalEvent<NinjaInfoScannerComponent, NinjaInfoScannerEjectMessage>(OnEjectMessage);
        SubscribeLocalEvent<NinjaInfoScannerComponent, NinjaInfoScannerTeleportMessage>(OnTeleportMessage);
        SubscribeLocalEvent<NinjaInfoScannerComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NinjaInfoScannerComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsScanning)
                continue;

            if (!_scanEndTimes.TryGetValue(uid, out var endTime))
                continue;

            if (_timing.CurTime < endTime)
                continue;

            FinishScan((uid, comp));
        }
    }

    private void OnInit(
        Entity<NinjaInfoScannerComponent> ent,
        ref ComponentInit args)
    {
        _container.EnsureContainer<ContainerSlot>(
            ent.Owner,
            ent.Comp.ContainerId);

        UpdateVisualState(ent);
    }

    private void OnShutdown(
        Entity<NinjaInfoScannerComponent> ent,
        ref ComponentShutdown args)
    {
        _scanEndTimes.Remove(ent.Owner);
    }

    private void OnActivateInWorld(
        Entity<NinjaInfoScannerComponent> ent,
        ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        _ui.OpenUi(
            ent.Owner,
            NinjaInfoScannerUiKey.Key,
            args.User);

        UpdateUserInterface(ent);

        args.Handled = true;
    }

    private void OnContainerInserted(
        Entity<NinjaInfoScannerComponent> ent,
        ref EntInsertedIntoContainerMessage args)
    {
        if (ent.Comp.IsScanning)
            return;

        UpdateVisualState(ent);
        UpdateUserInterface(ent);
    }

    private void OnContainerRemoved(
        Entity<NinjaInfoScannerComponent> ent,
        ref EntRemovedFromContainerMessage args)
    {
        if (ent.Comp.IsScanning)
        {
            FinishScan(ent);
            return;
        }

        UpdateVisualState(ent);
        UpdateUserInterface(ent);
    }

    private void OnScanMessage(
        Entity<NinjaInfoScannerComponent> ent,
        ref NinjaInfoScannerScanMessage args)
    {
        if (ent.Comp.IsScanning)
            return;

        if (!_container.TryGetContainer(
                ent.Owner,
                ent.Comp.ContainerId,
                out var container) ||
            container.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(
                Loc.GetString("ninja-info-popup-no-target"),
                ent.Owner,
                args.Actor);

            return;
        }

        var target = container.ContainedEntities[0];

        if (!Exists(target) ||
            !_mobState.IsAlive(target))
        {
            _popup.PopupEntity(
                Loc.GetString("ninja-info-popup-not-alive"),
                ent.Owner,
                args.Actor);

            return;
        }

        StartScan(ent, target, args.Actor);
    }

    public bool TryStartScan(EntityUid scanner, EntityUid actor)
    {
        if (!TryComp<NinjaInfoScannerComponent>(scanner, out var comp))
        {
            return false;
        }

        if (comp.IsScanning)
            return false;

        if (!_container.TryGetContainer(scanner, comp.ContainerId, out var container) || container.ContainedEntities.Count == 0)
        {
            return false;
        }

        var target = container.ContainedEntities[0];

        if (!Exists(target) || !_mobState.IsAlive(target))
        {
            return false;
        }

        StartScan((scanner, comp), target, actor);

        return true;
    }

    private void StartScan(Entity<NinjaInfoScannerComponent> ent, EntityUid target, EntityUid actor)
    {
        ent.Comp.IsScanning = true;
        ent.Comp.ScanningEntity = target;
        ent.Comp.VisualState = NinjaInfoScannerVisualState.Scan;

        _scanEndTimes[ent.Owner] =
            _timing.CurTime +
            TimeSpan.FromSeconds(ent.Comp.ScanTime);

        Dirty(ent);

        if (TryComp<SolutionComponent>(target, out var solution))
        {
            _solutionContainer.TryAddReagent(
                new Entity<SolutionComponent>(target, solution),
                new ReagentQuantity(
                    ent.Comp.ScanReagent,
                    FixedPoint2.New(ent.Comp.ScanReagentAmount)),
                out _);
        }

        UpdateVisualState(ent);
        UpdateUserInterface(ent);

        _popup.PopupEntity(Loc.GetString("ninja-info-popup-scan-started"), ent.Owner, actor);
    }

    private void FinishScan(Entity<NinjaInfoScannerComponent> ent)
    {
        _scanEndTimes.Remove(ent.Owner);

        if (!ent.Comp.IsScanning)
            return;

        var target = ent.Comp.ScanningEntity;

        ent.Comp.IsScanning = false;
        ent.Comp.ScanningEntity = null;

        if (target is not { } targetUid || !Exists(targetUid) || !_container.TryGetContainer(ent.Owner, ent.Comp.ContainerId, out var container) || !container.ContainedEntities.Any(x => x == targetUid))
        {
            ent.Comp.VisualState = NinjaInfoScannerVisualState.Open;

            Dirty(ent);
            UpdateVisualState(ent);
            UpdateUserInterface(ent);

            return;
        }

        if (!_mobState.IsAlive(targetUid))
        {
            ent.Comp.VisualState = NinjaInfoScannerVisualState.Closed;

            Dirty(ent);
            UpdateVisualState(ent);
            UpdateUserInterface(ent);

            return;
        }

        if (_objectiveSystem.TryScanEntity(targetUid))
        {
            _popup.PopupEntity(Loc.GetString("ninja-info-popup-scan-success"), ent.Owner, ent.Owner);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("ninja-info-popup-scan-fail"), ent.Owner, ent.Owner);
        }

        ent.Comp.VisualState = NinjaInfoScannerVisualState.Closed;

        Dirty(ent);

        UpdateVisualState(ent);
        UpdateUserInterface(ent);
    }

    private void OnEjectMessage(Entity<NinjaInfoScannerComponent> ent, ref NinjaInfoScannerEjectMessage args)
    {
        if (ent.Comp.IsScanning)
            return;

        if (!_container.TryGetContainer(ent.Owner, ent.Comp.ContainerId, out var container) || container.ContainedEntities.Count == 0)
        {
            return;
        }

        var target = container.ContainedEntities[0];

        _container.Remove(
            target,
            container);

        UpdateVisualState(ent);
        UpdateUserInterface(ent);
    }

    private void OnTeleportMessage(Entity<NinjaInfoScannerComponent> ent, ref NinjaInfoScannerTeleportMessage args)
    {
        if (ent.Comp.IsScanning)
            return;

        if (!_container.TryGetContainer(ent.Owner, ent.Comp.ContainerId, out var container) || container.ContainedEntities.Count == 0)
        {
            return;
        }

        var target = container.ContainedEntities[0];

        _container.Remove(
            target,
            container);

        var markers = new List<EntityUid>();
        var query =
            EntityQueryEnumerator<NinjaInfoTeleportMarkerComponent>();

        while (query.MoveNext(out var markerUid, out _))
        {
            markers.Add(markerUid);
        }

        if (markers.Count > 0)
        {
            var randomMarker = _random.Pick(markers);

            _transform.SetCoordinates(
                target,
                Transform(randomMarker).Coordinates);
        }

        UpdateVisualState(ent);
        UpdateUserInterface(ent);
    }

    private void UpdateVisualState(Entity<NinjaInfoScannerComponent> ent)
    {
        if (ent.Comp.IsScanning)
        {
            ent.Comp.VisualState = NinjaInfoScannerVisualState.Scan;
            Dirty(ent);
            return;
        }

        if (_container.TryGetContainer(ent.Owner, ent.Comp.ContainerId, out var container) && container.ContainedEntities.Count > 0)
        {
            ent.Comp.VisualState =
                NinjaInfoScannerVisualState.Closed;

            Dirty(ent);

            return;
        }

        ent.Comp.VisualState =
            NinjaInfoScannerVisualState.Open;

        Dirty(ent);
    }

    private void UpdateUserInterface(Entity<NinjaInfoScannerComponent> ent)
    {
        NetEntity? contained = null;

        if (_container.TryGetContainer(ent.Owner, ent.Comp.ContainerId, out var container) && container.ContainedEntities.Count > 0)
        {
            contained = GetNetEntity(container.ContainedEntities[0]);
        }

        _ui.SetUiState(ent.Owner, NinjaInfoScannerUiKey.Key, new NinjaInfoScannerState(contained));
    }
}