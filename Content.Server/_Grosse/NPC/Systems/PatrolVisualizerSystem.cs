using Content.Server.NPC.Components;
using Content.Server._Grosse.NPC.Components;
using Content.Shared.NPC.Components;
using Content.Shared._Grosse.NPC.Components;
using Content.Shared.NPC.Events;
using Content.Shared._Grosse.NPC.Events;
using Robust.Shared.Map;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using System.Linq;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Systems;
using Content.Server.NPC.Pathfinding;

namespace Content.Server._Grosse.NPC.Systems;

/// <summary>
/// Серверная система для отправки данных визуализации патрулей
/// </summary>
public sealed partial class PatrolVisualizerSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestPatrolVisualizerMessage>(OnRequestVisualization);
    }

    private void OnRequestVisualization(RequestPatrolVisualizerMessage msg, EntitySessionEventArgs args)
    {
        if (!msg.Enabled)
        {
            // Отправляем пустое сообщение для отключения визуализации
            RaiseNetworkEvent(new PatrolVisualizerDataMessage(), args.SenderSession);
            return;
        }

        // Собираем данные о патрулях
        var data = CollectPatrolData(msg.PatrolGroup);
        RaiseNetworkEvent(data, args.SenderSession);
    }

    /// <summary>
    /// Публичный метод для отправки данных визуализации
    /// </summary>
    public void SendVisualizationData(PatrolVisualizerDataMessage data, ICommonSession session)
    {
        RaiseNetworkEvent(data, session);
    }

    private PatrolVisualizerDataMessage CollectPatrolData(string? patrolGroup)
    {
        var message = new PatrolVisualizerDataMessage();

        // Собираем данные по группам патрулей
        var groupedPoints = new Dictionary<string, List<(EntityUid uid, PatrolPointComponent comp)>>();

        // Находим все точки патруля
        var query = EntityQueryEnumerator<PatrolPointComponent>();
        while (query.MoveNext(out var uid, out var point))
        {
            // Фильтруем по группе если указана
            if (patrolGroup != null && point.Group != patrolGroup)
                continue;

            if (!groupedPoints.ContainsKey(point.Group))
                groupedPoints[point.Group] = new();

            groupedPoints[point.Group].Add((uid, point));
        }

        // Формируем данные для каждой группы
        foreach (var (groupName, points) in groupedPoints)
        {
            var groupData = new PatrolGroupData
            {
                GroupName = groupName,
                Points = new List<PatrolPointData>()
            };

            // Сортируем по order
            var sortedPoints = points.OrderBy(p => p.comp.Order ?? 999).ToList();

            foreach (var entry in sortedPoints)
            {
                var uid = entry.uid;
                var point = entry.comp;

                var xform = Transform(uid);

                groupData.Points.Add(new PatrolPointData
                {
                    Entity = GetNetEntity(uid),
                    Coordinates = GetNetCoordinates(xform.Coordinates),
                    Order = point.Order,
                    WaitTime = point.WaitTime,
                    LookAround = point.LookAround
                });
            }

            if (groupData.Points.Count > 0)
                message.Groups.Add(groupData);
        }

        Log.Info($"PatrolVisualizer: Collected {message.Groups.Count} patrol groups" +
                 (patrolGroup != null ? $" for group '{patrolGroup}'" : ""));

        return message;
    }
}
