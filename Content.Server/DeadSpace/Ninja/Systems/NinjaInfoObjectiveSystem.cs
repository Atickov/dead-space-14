using System.Linq;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Server.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaInfoObjectiveSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaInfoConditionComponent, ObjectiveAssignedEvent>(OnObjectiveAssigned);
        SubscribeLocalEvent<NinjaInfoConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnObjectiveAssigned(Entity<NinjaInfoConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        var comp = ent.Comp;
        var onlineCount = _playerManager.PlayerCount;
        comp.TargetCount = Math.Max(1, onlineCount / comp.PlayersPerTarget);
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

        var baseDescription = Loc.GetString("objective-condition-secret-info-description");
        var fullDescription = Loc.GetString("ninja-info-objective-description",
            ("base", baseDescription),
            ("job", jobTitle),
            ("count", comp.TargetCount));

        _metaData.SetEntityDescription(ent, fullDescription);
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

    public bool TryScanEntity(EntityUid scannedBody)
    {
        if (!_mobState.IsAlive(scannedBody))
            return false;

        var query = EntityQueryEnumerator<NinjaInfoConditionComponent>();
        var scannedAny = false;

        while (query.MoveNext(out _, out var comp))
        {
            if (comp.ScannedEntities.Contains(scannedBody))
                continue;

            if (!_mind.TryGetMind(scannedBody, out var mindId, out _))
                continue;

            if (!_jobs.MindTryGetJob(mindId, out var jobProto))
                continue;

            if (jobProto.ID == comp.TargetJobId)
            {
                comp.ScannedEntities.Add(scannedBody);
                comp.CorrectScans++;
                scannedAny = true;
            }
        }

        return scannedAny;
    }
}