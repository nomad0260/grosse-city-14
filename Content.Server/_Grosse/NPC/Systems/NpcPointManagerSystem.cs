using System;
using System.Numerics;
using Content.Server._Grosse.NPC.Components;
using Content.Shared._Grosse.NPC.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Grosse.NPC.Systems;

/// <summary>
/// Единый реестр точек интереса NPC: патрульных (<see cref="PatrolPointComponent"/>)
/// и стратегических (<see cref="StrategicPointComponent"/>).
/// <para>
/// Реестр поддерживается событиями компонентов, а не обходом мира: поиск точки —
/// это проход по маленькому списку одной группы/тега. Тиковой работы у системы нет
/// вообще, поэтому она не влияет на производительность при сотнях NPC.
/// </para>
/// <para>
/// Слоты точки живут на самом компоненте (<c>Slots</c>), потому что их читают тесты
/// и VV. Просроченные резервации и кулдауны снимаются лениво — в момент, когда слот
/// в следующий раз запрашивают. Отдельный <c>Update</c> для этого не нужен.
/// </para>
/// </summary>
public sealed class NpcPointManagerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly Dictionary<string, List<EntityUid>> _patrolByGroup = new();
    private readonly Dictionary<string, List<EntityUid>> _strategicByTag = new();

    /// <summary>
    /// Больше стольких кандидатов за одно планирование не перебираем: даже если
    /// на карте сотни точек, NPC выбирает из ближайших по приоритету.
    /// </summary>
    private const int MaxCandidates = 48;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PatrolPointComponent, ComponentStartup>(OnPatrolStartup);
        SubscribeLocalEvent<PatrolPointComponent, ComponentShutdown>(OnPatrolShutdown);
        SubscribeLocalEvent<StrategicPointComponent, ComponentStartup>(OnStrategicStartup);
        SubscribeLocalEvent<StrategicPointComponent, ComponentShutdown>(OnStrategicShutdown);
        SubscribeLocalEvent<NpcPointTrackerComponent, EntityTerminatingEvent>(OnTrackerTerminating);
    }

    #region Registry

    private void OnPatrolStartup(EntityUid uid, PatrolPointComponent component, ComponentStartup args)
    {
        EnsureSlots(component.Slots, component.Capacity);
        Add(_patrolByGroup, GetGroup(component), uid);
    }

    private void OnPatrolShutdown(EntityUid uid, PatrolPointComponent component, ComponentShutdown args)
    {
        Remove(_patrolByGroup, GetGroup(component), uid);
    }

    private void OnStrategicStartup(EntityUid uid, StrategicPointComponent component, ComponentStartup args)
    {
        EnsureSlots(component.Slots, component.Capacity);

        if (component.Tags.Count == 0)
        {
            Add(_strategicByTag, string.Empty, uid);
            return;
        }

        foreach (var tag in component.Tags)
            Add(_strategicByTag, tag, uid);
    }

    private void OnStrategicShutdown(EntityUid uid, StrategicPointComponent component, ComponentShutdown args)
    {
        if (component.Tags.Count == 0)
        {
            Remove(_strategicByTag, string.Empty, uid);
            return;
        }

        foreach (var tag in component.Tags)
            Remove(_strategicByTag, tag, uid);
    }

    private void OnTrackerTerminating(EntityUid uid, NpcPointTrackerComponent tracker, ref EntityTerminatingEvent args)
    {
        // NPC умер/удалён, пока стоял на точке: слоты нельзя оставлять за ним.
        ReleaseAll(uid, tracker);
    }

    /// <summary>
    /// Переиндексирует патрульную точку после изменения её группы.
    /// Индекс обновляется только по событиям компонента, поэтому правка
    /// <c>Group</c> вручную (VV, админ-команда) требует явного вызова.
    /// </summary>
    public void ReindexPatrolPoint(EntityUid uid)
    {
        List<string>? emptied = null;

        foreach (var (key, list) in _patrolByGroup)
        {
            if (list.Remove(uid) && list.Count == 0)
                (emptied ??= new List<string>(2)).Add(key);
        }

        if (emptied != null)
        {
            for (var i = 0; i < emptied.Count; i++)
                _patrolByGroup.Remove(emptied[i]);
        }

        if (TryComp<PatrolPointComponent>(uid, out var component))
            Add(_patrolByGroup, GetGroup(component), uid);
    }

    private static string GetGroup(PatrolPointComponent component)
    {
        return string.IsNullOrEmpty(component.Group) ? "default" : component.Group;
    }

    private static void Add(Dictionary<string, List<EntityUid>> index, string key, EntityUid uid)
    {
        if (!index.TryGetValue(key, out var list))
        {
            list = new List<EntityUid>(8);
            index[key] = list;
        }

        if (!list.Contains(uid))
            list.Add(uid);
    }

    private static void Remove(Dictionary<string, List<EntityUid>> index, string key, EntityUid uid)
    {
        if (!index.TryGetValue(key, out var list))
            return;

        list.Remove(uid);

        if (list.Count == 0)
            index.Remove(key);
    }

    private static void EnsureSlots(List<PointSlot> slots, int capacity)
    {
        capacity = Math.Max(1, capacity);

        while (slots.Count < capacity)
            slots.Add(new PointSlot(slots.Count));

        // Ёмкость уменьшили: лишние свободные слоты убираем, занятые оставляем,
        // чтобы не потерять владельца.
        for (var i = slots.Count - 1; i >= capacity; i--)
        {
            if (slots[i].IsFree())
                slots.RemoveAt(i);
        }

        for (var i = 0; i < slots.Count; i++)
            slots[i].Index = i;
    }

    #endregion

    #region Patrol

    /// <summary>
    /// Выдаёт следующую патрульную точку и резервирует на ней слот.
    /// Возвращает <c>null</c>, если подходящих точек нет — это нормальный
    /// результат, ветка HTN просто уступит место следующей.
    /// </summary>
    public EntityUid? TryClaimNextPatrolPoint(EntityUid npc, string group, EntityUid? currentPoint, string? route = null)
    {
        if (!TryComp<NpcPointTrackerComponent>(npc, out var tracker))
            return null;

        if (!_patrolByGroup.TryGetValue(string.IsNullOrEmpty(group) ? "default" : group, out var points) || points.Count == 0)
            return null;

        var now = _timing.CurTime;

        // Сначала пробуем точку, на которой NPC уже стоит: если после перепланирования
        // она всё ещё свободна для него, нет смысла идти куда-то ещё.
        if (currentPoint is { } current &&
            Exists(current) &&
            TryComp<PatrolPointComponent>(current, out var currentComp) &&
            MatchesRoute(currentComp, route) &&
            TryReserveSlot(npc, now, currentComp.Slots, currentComp.ReservationTimeout, currentComp.SpreadRadius))
        {
            return current;
        }

        var currentOrder = currentPoint is { } cur &&
                           Exists(cur) &&
                           TryComp<PatrolPointComponent>(cur, out var curComp)
            ? curComp.Order
            : null;

        EntityUid? best = null;
        var bestPriority = int.MinValue;
        var bestOrder = int.MaxValue;
        var bestDistance = float.MaxValue;

        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];

            if (!Exists(point) || !TryComp<PatrolPointComponent>(point, out var component))
                continue;

            if (!MatchesRoute(component, route))
                continue;

            if (!HasFreeSlot(component.Slots, now))
                continue;

            if (currentPoint == point)
                continue;

            // Priority из YAML задаёт приоритет движения: точки с более высоким
            // приоритетом рассматриваются раньше остальных.
            if (component.Priority < bestPriority)
                continue;

            if (component.Priority > bestPriority)
            {
                bestPriority = component.Priority;
                bestOrder = int.MaxValue;
                bestDistance = float.MaxValue;
                best = null;
            }

            // Маршрут с порядком: идём строго по возрастанию Order и заворачиваемся.
            if (currentOrder is { } order && component.Order is { } candidateOrder)
            {
                var normalized = candidateOrder < order ? candidateOrder + int.MaxValue : candidateOrder;

                if (normalized < bestOrder)
                {
                    bestOrder = normalized;
                    best = point;
                }

                continue;
            }

            // Без порядка: ближайшая точка, чтобы NPC не бегал через полкарты.
            var distance = GetSquaredDistance(npc, point);

            if (!best.HasValue || distance < bestDistance)
            {
                bestDistance = distance;
                best = point;
            }
        }

        if (best is { } chosen &&
            TryComp<PatrolPointComponent>(chosen, out var chosenComp) &&
            TryReserveSlot(npc, now, chosenComp.Slots, chosenComp.ReservationTimeout, chosenComp.SpreadRadius))
        {
            tracker.LastPointRequestTime = now;
            return chosen;
        }

        // Все точки заняты: NPC всё равно идёт к ближайшей, а не встаёт посреди
        // коридора. Слот в этом случае не резервируется — точка общая.
        if (FindBusyPatrolPoint(npc, points, route, currentPoint) is { } busyPoint)
        {
            tracker.LastPointRequestTime = now;
            return busyPoint;
        }

        return null;
    }

    private EntityUid? FindBusyPatrolPoint(EntityUid npc, List<EntityUid> points, string? route, EntityUid? currentPoint)
    {
        EntityUid? best = null;
        var bestDistance = float.MaxValue;

        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];

            if (point == currentPoint || !Exists(point) || !TryComp<PatrolPointComponent>(point, out var component))
                continue;

            if (!MatchesRoute(component, route))
                continue;

            var distance = GetSquaredDistance(npc, point);

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = point;
        }

        return best;
    }

    public void ReleasePatrolPoint(EntityUid point, EntityUid npc)
    {
        if (!Exists(point) || !TryComp<PatrolPointComponent>(point, out var component))
            return;

        ReleaseSlot(point, npc, component.Slots, component.CooldownTime);
    }

    private static bool MatchesRoute(PatrolPointComponent point, string? route)
    {
        // Точка без маршрута доступна любому маршруту группы.
        return string.IsNullOrEmpty(point.Route) || string.IsNullOrEmpty(route) || point.Route == route;
    }

    /// <summary>
    /// Назначает NPC маршрут внутри группы. Маршрут детерминирован для NPC,
    /// чтобы весь отряд не выбрал один и тот же путь, и не менялся при перепланировании.
    /// </summary>
    public string? AutoAssignRoute(EntityUid npc, string group)
    {
        if (!_patrolByGroup.TryGetValue(string.IsNullOrEmpty(group) ? "default" : group, out var points))
            return null;

        List<string>? routes = null;

        for (var i = 0; i < points.Count; i++)
        {
            if (!Exists(points[i]) || !TryComp<PatrolPointComponent>(points[i], out var component))
                continue;

            if (string.IsNullOrEmpty(component.Route))
                continue;

            routes ??= new List<string>(4);

            if (!routes.Contains(component.Route))
                routes.Add(component.Route);
        }

        if (routes == null || routes.Count == 0)
            return null;

        routes.Sort(StringComparer.Ordinal);
        return routes[(int)(npc.Id % (uint)routes.Count)];
    }

    #endregion

    #region Strategic

    /// <summary>
    /// Кандидаты, отсортированные по приоритету. Фильтры: активность, фракция,
    /// теги и наличие свободного слота.
    /// </summary>
    public List<(EntityUid uid, StrategicPointComponent comp)> GetStrategicPointCandidates(
        EntityUid npc,
        string? faction,
        HashSet<string> requiredTags)
    {
        var result = new List<(EntityUid, StrategicPointComponent)>(8);

        if (requiredTags.Count == 0)
            return result;

        var now = _timing.CurTime;
        var seen = new HashSet<EntityUid>();

        foreach (var tag in requiredTags)
        {
            if (!_strategicByTag.TryGetValue(tag, out var points))
                continue;

            for (var i = 0; i < points.Count && result.Count < MaxCandidates; i++)
            {
                var point = points[i];

                if (!seen.Add(point))
                    continue;

                if (!Exists(point) || !TryComp<StrategicPointComponent>(point, out var component))
                    continue;

                if (!component.IsActive)
                    continue;

                if (!string.IsNullOrEmpty(component.Faction) &&
                    !string.Equals(component.Faction, faction, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!HasFreeSlot(component.Slots, now))
                    continue;

                result.Add((point, component));
            }

            if (result.Count >= MaxCandidates)
                break;
        }

        result.Sort(static (a, b) =>
        {
            var priorityA = a.Item2.Priority * (a.Item2.IsActive ? a.Item2.ActivePriorityMultiplier : 1f);
            var priorityB = b.Item2.Priority * (b.Item2.IsActive ? b.Item2.ActivePriorityMultiplier : 1f);
            return priorityB.CompareTo(priorityA);
        });

        return result;
    }

    public bool TryReserveStrategicPoint(EntityUid point, EntityUid npc)
    {
        if (!Exists(point) || !TryComp<StrategicPointComponent>(point, out var component))
            return false;

        return TryReserveSlot(npc, _timing.CurTime, component.Slots, component.ReservationTimeout, component.SpreadRadius);
    }

    public void ReleaseStrategicPoint(EntityUid point, EntityUid npc)
    {
        if (!Exists(point) || !TryComp<StrategicPointComponent>(point, out var component))
            return;

        ReleaseSlot(point, npc, component.Slots, component.CooldownTime);
    }

    /// <summary>
    /// Позиция слота NPC относительно центра точки: NPC расходятся вокруг точки,
    /// а не встают все в один тайл.
    /// </summary>
    public bool TryGetSlotOffset(EntityUid point, EntityUid npc, out Vector2 offset)
    {
        offset = Vector2.Zero;

        var slots = GetSlots(point);

        if (slots == null)
            return false;

        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i].Owner != npc)
                continue;

            offset = slots[i].Offset;
            return true;
        }

        return false;
    }

    #endregion

    #region Slots

    public bool OccupySlot(EntityUid point, EntityUid npc)
    {
        var slots = GetSlots(point);

        if (slots == null)
            return false;

        if (TryMarkOwned(slots, npc))
            return true;

        // Резервации нет (например, точка выдана вручную) — резервируем и сразу занимаем.
        return TryReserveSlot(npc, _timing.CurTime, slots, 0f) && TryMarkOwned(slots, npc);
    }

    /// <summary>
    /// Переводит слот этого NPC в состояние «стоит на точке».
    /// </summary>
    private static bool TryMarkOwned(List<PointSlot> slots, EntityUid npc)
    {
        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i].Owner != npc)
                continue;

            slots[i].State = PointSlotState.Occupied;
            // Занятый слот не протухает: NPC уйдёт сам или умрёт.
            slots[i].ExpiresAt = TimeSpan.MaxValue;
            return true;
        }

        return false;
    }

    private List<PointSlot>? GetSlots(EntityUid point)
    {
        if (!Exists(point))
            return null;

        if (TryComp<PatrolPointComponent>(point, out var patrol))
            return patrol.Slots;

        if (TryComp<StrategicPointComponent>(point, out var strategic))
            return strategic.Slots;

        return null;
    }

    private static bool TryReserveSlot(
        EntityUid npc,
        TimeSpan now,
        List<PointSlot> slots,
        float timeoutSeconds,
        float spreadRadius = 0f)
    {
        // Свою резервацию переиспользуем: повторное планирование не должно отбирать
        // у NPC второй слот и не должно продлевать чужие резервации.
        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];

            if (slot.Owner != npc)
                continue;

            if (slot.State == PointSlotState.Occupied)
                return true;

            slot.State = PointSlotState.Reserved;
            slot.ExpiresAt = now + TimeSpan.FromSeconds(Math.Max(1f, timeoutSeconds));
            return true;
        }

        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];

            if (!TryNormalize(slot, now))
                continue;

            slot.State = PointSlotState.Reserved;
            slot.Owner = npc;
            slot.StartedAt = now;
            slot.ExpiresAt = now + TimeSpan.FromSeconds(Math.Max(1f, timeoutSeconds));
            slot.Offset = spreadRadius > 0f ? GetSlotOffset(i, slots.Count, spreadRadius) : Vector2.Zero;
            return true;
        }

        return false;
    }

    private static bool HasFreeSlot(List<PointSlot> slots, TimeSpan now)
    {
        for (var i = 0; i < slots.Count; i++)
        {
            if (IsAvailable(slots[i], now))
                return true;
        }

        return false;
    }

    private static bool IsAvailable(PointSlot slot, TimeSpan now)
    {
        return slot.State switch
        {
            PointSlotState.Free => true,
            PointSlotState.Cooldown or PointSlotState.Reserved or PointSlotState.Blocked => now >= slot.ExpiresAt,
            _ => false,
        };
    }

    /// <summary>
    /// Слот протух (резервация не дошла, кулдаун истёк) — сбрасываем его в Free.
    /// </summary>
    private static bool TryNormalize(PointSlot slot, TimeSpan now)
    {
        if (slot.State == PointSlotState.Free)
            return true;

        if (!IsAvailable(slot, now))
            return false;

        slot.State = PointSlotState.Free;
        slot.Owner = null;
        slot.ExpiresAt = TimeSpan.Zero;
        slot.Offset = Vector2.Zero;
        return true;
    }

    private void ReleaseSlot(EntityUid point, EntityUid npc, List<PointSlot> slots, float cooldownSeconds)
    {
        var now = _timing.CurTime;

        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];

            if (slot.Owner != npc)
                continue;

            slot.Owner = null;
            slot.Offset = Vector2.Zero;

            if (slot.State == PointSlotState.Occupied && cooldownSeconds > 0f)
            {
                slot.State = PointSlotState.Cooldown;
                slot.ExpiresAt = now + TimeSpan.FromSeconds(cooldownSeconds);
            }
            else
            {
                // Недошедшая резервация не должна блокировать точку кулдауном.
                slot.State = PointSlotState.Free;
                slot.ExpiresAt = TimeSpan.Zero;
            }

            return;
        }
    }

    private static Vector2 GetSlotOffset(int index, int capacity, float radius)
    {
        if (capacity <= 1)
            return Vector2.Zero;

        var angle = MathF.Tau * index / capacity;
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
    }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Освобождает всё, что NPC держал. Вызывается при удалении NPC.
    /// </summary>
    public void ReleaseAll(EntityUid npc, NpcPointTrackerComponent tracker)
    {
        if (tracker.CurrentPatrolPoint is { } patrol)
            ReleasePatrolPoint(patrol, npc);

        if (tracker.CurrentStrategicPoint is { } strategic)
            ReleaseStrategicPoint(strategic, npc);

        tracker.CurrentPatrolPoint = null;
        tracker.CurrentStrategicPoint = null;
    }

    private float GetSquaredDistance(EntityUid npc, EntityUid point)
    {
        if (!TryComp<TransformComponent>(npc, out var npcXform) || !TryComp<TransformComponent>(point, out var pointXform))
            return float.MaxValue;

        // Только в пределах одной карты: сравнивать локальные координаты разных
        // гридов бессмысленно, а NPC через Z-уровни ближайшую точку не выбирает.
        if (npcXform.MapID != pointXform.MapID)
            return float.MaxValue;

        return (_transform.GetWorldPosition(npcXform) - _transform.GetWorldPosition(pointXform)).LengthSquared();
    }

    #endregion
}
