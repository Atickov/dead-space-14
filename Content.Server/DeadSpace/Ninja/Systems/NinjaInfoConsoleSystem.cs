using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.DeadSpace.Ninja;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.UserInterface;
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

        SubscribeLocalEvent<NinjaInfoConsoleComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<NinjaInfoConsoleComponent, AfterActivatableUIOpenEvent>(OnUiOpened);
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

    private void OnUiOpenAttempt(
        Entity<NinjaInfoConsoleComponent> ent,
        ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (GetLinkedScanner(ent.Owner) != null)
            return;

        args.Cancel();
        if (!args.Silent)
            Say(ent.Owner, "ninja-info-phrase-no-scanner-linked");
    }

    private void OnUiOpened(
        Entity<NinjaInfoConsoleComponent> ent,
        ref AfterActivatableUIOpenEvent args)
    {
        UpdateUserInterface(ent.Owner);
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

        _scannerSystem.TryStartScan(scanner, args.Actor);
    }

    private void OnEjectMessage(
        Entity<NinjaInfoConsoleComponent> ent,
        ref NinjaInfoScannerEjectMessage args)
    {
        if (GetLinkedScanner(ent.Owner) is not { } scanner)
            return;

        _scannerSystem.TryEjectTarget(scanner, args.Actor);
    }

    private void OnTeleportMessage(
        Entity<NinjaInfoConsoleComponent> ent,
        ref NinjaInfoScannerTeleportMessage args)
    {
        if (GetLinkedScanner(ent.Owner) is not { } scanner)
            return;

        _scannerSystem.TryTeleportTarget(scanner, args.Actor);
    }
}