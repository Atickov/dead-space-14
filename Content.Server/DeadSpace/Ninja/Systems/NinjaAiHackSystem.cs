using Content.Server.DeadSpace.Ninja.Systems;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Content.Server.Silicons.Laws;
using Content.Server.Chat.Managers;
using Robust.Shared.Player;

namespace Content.Server.Ninja.Systems;

public sealed class NinjaAiHackSystem : EntitySystem
{
    [Dependency] private readonly NinjaGlovesSystem _gloves = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SiliconLawSystem _lawSystem = default!;
    [Dependency] private readonly IonStormSystem _ionStorm = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaAiHackComponent, BeforeInteractHandEvent>(OnBeforeInteractHand);
        SubscribeLocalEvent<NinjaAiHackComponent, NinjaAiHackDoAfterEvent>(OnDoAfter);
    }

    private void OnBeforeInteractHand(EntityUid uid, NinjaAiHackComponent comp, BeforeInteractHandEvent args)
    {
        if (args.Handled || !HasComp<SiliconLawUpdaterComponent>(args.Target))
            return;

        if (!_gloves.AbilityCheck(uid, args, out var target))
            return;

        if (TryComp<ActorComponent>(target, out var actor))
        {
            var hackAnnouncement = Loc.GetString("ninja-ai-hack-started");
            _chatManager.DispatchServerMessage(actor.PlayerSession, hackAnnouncement);
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, uid, comp.Delay, new NinjaAiHackDoAfterEvent(), target: target, used: uid, eventTarget: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            MovementThreshold = 0.5f,
            CancelDuplicate = false
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs))
            args.Handled = true;
    }

    private void OnDoAfter(EntityUid uid, NinjaAiHackComponent comp, NinjaAiHackDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target == null)
            return;
        args.Handled = true;

        var hackedLaws = _ionStorm.GenerateIonLaws(3);
        var success = false;
        var hackAnnouncement = Loc.GetString("ninja-ai-hack-announcement");

        var query = EntityQueryEnumerator<SiliconLawBoundComponent>();
        while (query.MoveNext(out var targetUid, out _))
        {
            EnsureComp<SiliconLawProviderComponent>(targetUid);
            _lawSystem.SetLaws(hackedLaws, targetUid);

            if (TryComp<ActorComponent>(targetUid, out var actor))
            {
                _chatManager.DispatchServerMessage(actor.PlayerSession, hackAnnouncement);
            }

            success = true;
        }
        var providerQuery = EntityQueryEnumerator<SiliconLawProviderComponent>();
        while (providerQuery.MoveNext(out var providerUid, out _))
        {
            _lawSystem.SetLaws(hackedLaws, providerUid);
        }
        if (success)
        {
            RemComp<NinjaAiHackComponent>(uid);
            var ev = new NinjaAiHackEvent(args.User, args.Target.Value);
            RaiseLocalEvent(ref ev);
        }
    }
}