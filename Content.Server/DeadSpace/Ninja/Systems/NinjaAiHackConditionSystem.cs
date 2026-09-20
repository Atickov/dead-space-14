using Content.Shared.DeadSpace.Ninja;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaAiHackConditionSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaAiHackConditionComponent, ObjectiveAssignedEvent>(OnObjectiveAssigned);
        SubscribeLocalEvent<NinjaAiHackConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
        SubscribeLocalEvent<NinjaAiHackEvent>(OnAiHacked);
    }

    private void OnObjectiveAssigned(Entity<NinjaAiHackConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        ent.Comp.Mind = args.MindId;

        if (args.Mind.OwnedEntity is { } player)
        {
            EnsureComp<NinjaAiHackComponent>(player);
        }
    }

    private void OnAiHacked(ref NinjaAiHackEvent args)
    {
        var query = EntityQueryEnumerator<NinjaAiHackConditionComponent>();
        while (query.MoveNext(out _, out var condition))
        {
            _mind.TryGetMind(args.Ninja, out var ninjaMindId, out _);

            if (condition.Mind == args.Ninja || condition.Mind == ninjaMindId)
            {
                condition.Hacked = true;
            }
        }
    }

    private void OnGetProgress(Entity<NinjaAiHackConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = ent.Comp.Hacked ? 1f : 0f;
    }
}