using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.DeadSpace.Ninja;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaInfoConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly NinjaInfoScannerSystem _scannerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaInfoConsoleComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<NinjaInfoConsoleComponent, NinjaInfoScannerScanMessage>(OnScanMessage);
        SubscribeLocalEvent<NinjaInfoConsoleComponent, NinjaInfoScannerEjectMessage>(OnEjectMessage);
        SubscribeLocalEvent<NinjaInfoConsoleComponent, NinjaInfoScannerTeleportMessage>(OnTeleportMessage);
        SubscribeLocalEvent<NinjaInfoConsoleComponent, EntInsertedIntoContainerMessage>(OnScannerContainerModified);
        SubscribeLocalEvent<NinjaInfoConsoleComponent, EntRemovedFromContainerMessage>(OnScannerContainerModified);
    }

    private EntityUid? GetLinkedScanner(EntityUid console)
    {
        if (!TryComp<DeviceLinkSinkComponent>(console, out var sinkComp))
            return null;

        foreach (var source in sinkComp.LinkedSources)
        {
            if (Exists(source) && HasComp<NinjaInfoScannerComponent>(source))
                return source;
        }

        return null;
    }

    private void OnScannerContainerModified(
        Entity<NinjaInfoConsoleComponent> ent,
        ref EntInsertedIntoContainerMessage args)
    {
        OnScannerContainerModified(ent.Owner, args.Container);
    }

    private void OnScannerContainerModified(
        Entity<NinjaInfoConsoleComponent> ent,
        ref EntRemovedFromContainerMessage args)
    {
        OnScannerContainerModified(ent.Owner, args.Container);
    }

    private void OnScannerContainerModified(EntityUid console, BaseContainer container)
    {
        if (GetLinkedScanner(console) is not { } scanner ||
            container.Owner != scanner)
        {
            return;
        }

        UpdateUserInterface(console);
    }

    private void OnActivateInWorld(
        Entity<NinjaInfoConsoleComponent> ent,
        ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        var scanner = GetLinkedScanner(ent.Owner);
        if (scanner == null)
        {
            Say(ent.Owner, "ninja-info-phrase-no-scanner-linked");
            return;
        }

        _ui.OpenUi(ent.Owner, NinjaInfoScannerUiKey.Key, args.User);
        UpdateUserInterface(ent.Owner);
        args.Handled = true;
    }

    private void Say(EntityUid speaker, string phrase)
    {
        _chatSystem.TrySendInGameICMessage(
            speaker,
            Loc.GetString(phrase),
            InGameICChatType.Speak,
            ChatTransmitRange.Normal,
            true
        );
    }

    public void UpdateUserInterface(EntityUid consoleUid)
    {
        NetEntity? contained = null;

        if (GetLinkedScanner(consoleUid) is { } scanner &&
            TryComp<NinjaInfoScannerComponent>(scanner, out var scannerComp) &&
            _container.TryGetContainer(scanner, scannerComp.ContainerId, out var container) &&
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

        _scannerSystem.TryStartScan(scanner, ent.Owner);
    }

    private void OnEjectMessage(
        Entity<NinjaInfoConsoleComponent> ent,
        ref NinjaInfoScannerEjectMessage args)
    {
        if (GetLinkedScanner(ent.Owner) is not { } scanner)
            return;

        _scannerSystem.TryEjectTarget(scanner, ent.Owner);
    }

    private void OnTeleportMessage(
        Entity<NinjaInfoConsoleComponent> ent,
        ref NinjaInfoScannerTeleportMessage args)
    {
        if (GetLinkedScanner(ent.Owner) is not { } scanner)
            return;

        _scannerSystem.TryTeleportTarget(scanner, ent.Owner);
    }
}