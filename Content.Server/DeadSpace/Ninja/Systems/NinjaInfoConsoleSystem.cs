using Content.Server.DeviceLinking.Systems;
using Content.Shared.DeadSpace.Ninja;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaInfoConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DeviceLinkSystem _deviceLink = default!;
    [Dependency] private readonly NinjaInfoScannerSystem _scannerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaInfoConsoleComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<NinjaInfoConsoleComponent, NinjaInfoScannerScanMessage>(OnScanMessage);
        SubscribeLocalEvent<NinjaInfoConsoleComponent, NinjaInfoScannerEjectMessage>(OnEjectMessage);
        SubscribeLocalEvent<NinjaInfoConsoleComponent, NinjaInfoScannerTeleportMessage>(OnTeleportMessage);
    }

    private EntityUid? GetLinkedScanner(EntityUid console)
    {
        if (TryComp<DeviceLinkSinkComponent>(console, out var sinkComp))
        {
            foreach (var source in sinkComp.LinkedSources)
            {
                if (HasComp<NinjaInfoScannerComponent>(source))
                    return source;
            }
        }

        if (TryComp<DeviceLinkSourceComponent>(console, out var sourceComp))
        {
            foreach (var sinks in sourceComp.Outputs.Values)
            {
                foreach (var sinkUid in sinks)
                {
                    if (HasComp<NinjaInfoScannerComponent>(sinkUid))
                        return sinkUid;
                }
            }
        }

        return null;
    }

    private void OnActivateInWorld(Entity<NinjaInfoConsoleComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        var scanner = GetLinkedScanner(ent.Owner);
        if (scanner == null)
        {
            _popup.PopupEntity(Loc.GetString("ninja-info-popup-no-scanner-linked"), ent.Owner, args.User);
            return;
        }

        _ui.OpenUi(ent.Owner, NinjaInfoScannerUiKey.Key, args.User);
        UpdateUserInterface(ent.Owner);
        args.Handled = true;
    }

    public void UpdateUserInterface(EntityUid consoleUid)
    {
        NetEntity? contained = null;

        if (GetLinkedScanner(consoleUid) is { } scanner &&
            _container.TryGetContainer(scanner, "entity_storage", out var container) &&
            container.ContainedEntities.Count > 0)
        {
            contained = GetNetEntity(container.ContainedEntities[0]);
        }

        _ui.SetUiState(consoleUid, NinjaInfoScannerUiKey.Key, new NinjaInfoScannerState(contained));
    }

    private void OnScanMessage(
        Entity<NinjaInfoConsoleComponent> ent,
        ref NinjaInfoScannerScanMessage args)
    {
        if (GetLinkedScanner(ent.Owner) is not { } scanner)
            return;

        _scannerSystem.TryStartScan(scanner, args.Actor);
    }

    private void OnEjectMessage(Entity<NinjaInfoConsoleComponent> ent, ref NinjaInfoScannerEjectMessage args)
    {
        if (GetLinkedScanner(ent.Owner) is not { } scanner ||
            !_container.TryGetContainer(scanner, "entity_storage", out var container) ||
            container.ContainedEntities.Count == 0)
            return;

        var target = container.ContainedEntities[0];
        _container.Remove(target, container);
        UpdateUserInterface(ent.Owner);
    }

    private void OnTeleportMessage(Entity<NinjaInfoConsoleComponent> ent, ref NinjaInfoScannerTeleportMessage args)
    {
        if (GetLinkedScanner(ent.Owner) is not { } scanner ||
            !_container.TryGetContainer(scanner, "entity_storage", out var container) ||
            container.ContainedEntities.Count == 0)
            return;

        var target = container.ContainedEntities[0];
        _container.Remove(target, container);

        var markers = new List<EntityUid>();
        var query = EntityQueryEnumerator<NinjaInfoTeleportMarkerComponent>();
        while (query.MoveNext(out var markerUid, out _))
        {
            markers.Add(markerUid);
        }

        if (markers.Count > 0)
        {
            var randomMarker = _random.Pick(markers);
            _transform.SetCoordinates(target, Transform(randomMarker).Coordinates);
        }

        UpdateUserInterface(ent.Owner);
    }
}