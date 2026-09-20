using System.Linq;
using Content.Server.Objectives.Systems;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaInfoObjectiveSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly NumberObjectiveSystem _number = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaInfoConditionComponent, ObjectiveAfterAssignEvent>(OnObjectiveAssigned);
        SubscribeLocalEvent<NinjaInfoConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnObjectiveAssigned(Entity<NinjaInfoConditionComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        var comp = ent.Comp;
        comp.TargetCount = Math.Max(1, _number.GetTarget(ent.Owner));
        var activeJobs = new List<(string JobId, JobPrototype Proto)>();

        var query = EntityQueryEnumerator<MindComponent>();
        while (query.MoveNext(out var mindId, out var mind))
        {
            if (mind.OwnedEntity is not { } body || !_mobState.IsAlive(body))
                continue;

            if (_jobs.MindTryGetJob(mindId, out var jobProto))
            {
                activeJobs.Add((jobProto.ID, jobProto));
            }
        }

        if (activeJobs.Count > 0)
        {
            var selectedJob = _random.Pick(activeJobs);
            comp.TargetJobId = selectedJob.JobId;
            comp.TargetJobTitle = Loc.GetString(selectedJob.Proto.Name);
        }
        else
        {
            var allJobs = _proto.EnumeratePrototypes<JobPrototype>().Where(j => j.SetPreference).ToList();
            if (allJobs.Count > 0)
            {
                var picked = _random.Pick(allJobs);
                comp.TargetJobId = picked.ID;
                comp.TargetJobTitle = Loc.GetString(picked.Name);
            }
        }

        var jobTitle = string.IsNullOrEmpty(comp.TargetJobTitle)
            ? Loc.GetString("ninja-info-job-unknown")
            : comp.TargetJobTitle;

        var fullDescription = Loc.GetString("ninja-info-objective-description",
            ("job", jobTitle),
            ("count", comp.TargetCount));

        _metaData.SetEntityDescription(ent, fullDescription, args.Meta);
        Dirty(ent.Owner, comp);
    }

    private void OnGetProgress(Entity<NinjaInfoConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var comp = ent.Comp;
        if (comp.TargetCount <= 0)
        {
            args.Progress = 1f;
            return;
        }

        args.Progress = Math.Clamp((float)comp.CorrectScans / comp.TargetCount, 0f, 1f);
    }

    public bool TryScanEntity(EntityUid scannedBody, EntityUid? actor)
    {
        if (!_mobState.IsAlive(scannedBody))
            return false;

        if (!_mind.TryGetMind(scannedBody, out var scannedMind, out _))
            return false;

        if (!_jobs.MindTryGetJob(scannedMind, out var jobProto))
            return false;

        if (actor is not { } actorUid || !_mind.TryGetMind(actorUid, out _, out var actorMind))
            return false;

        var scannedAny = false;

        foreach (var objective in actorMind.Objectives)
        {
            if (!TryComp<NinjaInfoConditionComponent>(objective, out var comp))
                continue;

            if (comp.ScannedEntities.Contains(scannedBody))
                continue;

            if (jobProto.ID != comp.TargetJobId)
                continue;

            comp.ScannedEntities.Add(scannedBody);
            if (comp.CorrectScans < comp.TargetCount)
                comp.CorrectScans++;

            Dirty(objective, comp);
            scannedAny = true;
        }

        return scannedAny;
    }
}