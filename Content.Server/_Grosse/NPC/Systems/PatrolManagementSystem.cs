using Content.Server.NPC.Components;
using Content.Server._Grosse.NPC.Components;
using Content.Shared.NPC.Components;
using Content.Shared._Grosse.NPC.Components;
using Robust.Shared.Random;
using Robust.Shared.Map;
using System.Linq;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Systems;
using Content.Server.NPC.Pathfinding;

namespace Content.Server._Grosse.NPC.Systems;

/// <summary>
/// Система управления патрулированием NPC
/// </summary>
public sealed partial class PatrolManagementSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PatrolComponent, ComponentStartup>(OnPatrolStartup);
    }

    private void OnPatrolStartup(EntityUid uid, PatrolComponent component, ComponentStartup args)
    {
        // При появлении NPC находим все точки его патруля
        FindPatrolPoints(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Обновляем всех патрулирующих NPC
        var query = EntityQueryEnumerator<PatrolComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var patrol, out var xform))
        {
            UpdatePatrol(uid, patrol, xform, frameTime);
        }
    }

    /// <summary>
    /// Находит все точки патруля для NPC
    /// </summary>
    private void FindPatrolPoints(EntityUid uid, PatrolComponent component)
    {
        component.Points.Clear();

        // Ищем все точки патруля с нужной группой
        var query = EntityQueryEnumerator<PatrolPointComponent, TransformComponent>();
        var points = new List<(EntityUid uid, int order)>();

        while (query.MoveNext(out var pointUid, out var point, out var _))
        {
            if (point.Group == component.Group)
            {
                var order = point.Order ?? 999;
                points.Add((pointUid, order));
            }
        }

        // Сортируем по порядку
        points.Sort((a, b) => a.order.CompareTo(b.order));

        // Сохраняем в компонент
        component.Points = points.Select(p => p.uid).ToList();
        component.CurrentPointIndex = 0;

        Log.Info($"Patrol: Found {component.Points.Count} points for group '{component.Group}' for entity {ToPrettyString(uid)}");
    }

    private void UpdatePatrol(EntityUid uid, PatrolComponent patrol, TransformComponent xform, float frameTime)
    {
        if (patrol.Points.Count == 0)
            return;

        // Если ждём на точке
        if (patrol.WaitTimer > 0)
        {
            patrol.WaitTimer -= frameTime;
            return;
        }

        // Получаем текущую целевую точку
        var targetPoint = patrol.Points[patrol.CurrentPointIndex];

        var targetXform = Transform(targetPoint);

        // Проверяем, достиг ли NPC точки
        var distance = (xform.WorldPosition - targetXform.WorldPosition).Length();

        if (distance < 0.5f) // Достиг точки
        {
            // Получаем время ожидания на этой точке
            if (TryComp<PatrolPointComponent>(targetPoint, out var pointComp))
            {
                patrol.WaitTimer = pointComp.WaitTime;
            }

            // Переходим к следующей точке
            MoveToNextPoint(patrol);
        }
    }

    private void MoveToNextPoint(PatrolComponent patrol)
    {
        if (patrol.Loop)
        {
            // По кругу
            patrol.CurrentPointIndex = (patrol.CurrentPointIndex + 1) % patrol.Points.Count;
        }
        else
        {
            // Туда-обратно
            if (patrol.Forward)
            {
                patrol.CurrentPointIndex++;
                if (patrol.CurrentPointIndex >= patrol.Points.Count - 1)
                {
                    patrol.Forward = false;
                    patrol.CurrentPointIndex = patrol.Points.Count - 1;
                }
            }
            else
            {
                patrol.CurrentPointIndex--;
                if (patrol.CurrentPointIndex <= 0)
                {
                    patrol.Forward = true;
                    patrol.CurrentPointIndex = 0;
                }
            }
        }
    }

    /// <summary>
    /// Получить текущую целевую точку патруля
    /// </summary>
    public EntityUid? GetCurrentTarget(EntityUid uid, PatrolComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return null;

        if (component.Points.Count == 0)
            return null;

        return component.Points[component.CurrentPointIndex];
    }

    /// <summary>
    /// Получить координаты текущей целевой точки
    /// </summary>
    public EntityCoordinates? GetCurrentTargetCoordinates(EntityUid uid, PatrolComponent? component = null)
    {
        var target = GetCurrentTarget(uid, component);
        if (target == null)
            return null;

        return Transform(target.Value).Coordinates;
    }

    /// <summary>
    /// Принудительно обновить список точек патруля
    /// </summary>
    public void RefreshPatrolPoints(EntityUid uid, PatrolComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        FindPatrolPoints(uid, component);
    }
}
