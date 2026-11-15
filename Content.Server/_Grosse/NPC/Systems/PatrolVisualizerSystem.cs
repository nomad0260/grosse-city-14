using System.Linq;
using Content.Shared._Grosse.NPC.Components;
using Content.Shared._Grosse.NPC.Events;
using Robust.Shared.Player;

namespace Content.Server._Grosse.NPC.Systems;

/// <summary>
/// Собирает патрульные маршруты и отправляет их клиенту для отрисовки.
/// </summary>
public sealed partial class PatrolVisualizerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestPatrolVisualizerMessage>(OnRequestVisualization);
    }

    private void OnRequestVisualization(RequestPatrolVisualizerMessage msg, EntitySessionEventArgs args)
    {
        // Пустое сообщение выключает overlay на клиенте.
        var data = msg.Enabled
            ? CollectPatrolData(msg.PatrolGroup)
            : new PatrolVisualizerDataMessage();

        RaiseNetworkEvent(data, args.SenderSession);
    }

    public void SendVisualizationData(PatrolVisualizerDataMessage data, ICommonSession session)
    {
        RaiseNetworkEvent(data, session);
    }

    /// <summary>
    /// Собирает точки по группам. Публичный, потому что та же выборка нужна
    /// админ-команде — раньше она была скопирована туда целиком.
    /// </summary>
    public PatrolVisualizerDataMessage CollectPatrolData(string? patrolGroup)
    {
        var message = new PatrolVisualizerDataMessage();
        var groupedPoints = new Dictionary<string, List<(EntityUid uid, PatrolPointComponent comp)>>();

        var query = EntityQueryEnumerator<PatrolPointComponent>();
        while (query.MoveNext(out var uid, out var point))
        {
            if (patrolGroup != null && point.Group != patrolGroup)
                continue;

            if (!groupedPoints.TryGetValue(point.Group, out var list))
            {
                list = new List<(EntityUid, PatrolPointComponent)>();
                groupedPoints[point.Group] = list;
            }

            list.Add((uid, point));
        }

        foreach (var (groupName, points) in groupedPoints)
        {
            var groupData = new PatrolGroupData { GroupName = groupName };

            foreach (var (uid, point) in points.OrderBy(p => p.comp.Order ?? int.MaxValue))
            {
                groupData.Points.Add(new PatrolPointData
                {
                    Entity = GetNetEntity(uid),
                    Coordinates = GetNetCoordinates(Transform(uid).Coordinates),
                    Order = point.Order,
                    WaitTime = point.WaitTime,
                    LookAround = point.LookAround,
                });
            }

            if (groupData.Points.Count > 0)
                message.Groups.Add(groupData);
        }

        return message;
    }
}
