using Content.Server.NPC.Components;
using Content.Server._Grosse.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Shared.NPC.Components;
using Content.Shared._Grosse.NPC.Components;
using Robust.Shared.Log;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;
using System.Numerics;
using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Systems;
using Content.Server.NPC.Pathfinding;

namespace Content.Server._Grosse.NPC.Systems;

/// <summary>
/// Центральный менеджер для управления патрульными и стратегическими точками.
/// Контролирует резервацию, занятие и освобождение слотов.
/// </summary>
public sealed partial class NpcPointManagerSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    private readonly ISawmill _sawmill = Logger.GetSawmill("npc.pointmanager");

    public override void Initialize()
    {
        base.Initialize();

        // Инициализация слотов при создании точек
        SubscribeLocalEvent<PatrolPointComponent, ComponentStartup>(OnPatrolPointStartup);
        SubscribeLocalEvent<StrategicPointComponent, ComponentStartup>(OnStrategicPointStartup);

        // Освобождение слотов при удалении NPC с трекером
        SubscribeLocalEvent<NpcPointTrackerComponent, EntityTerminatingEvent>(OnTrackerTerminating);

        _sawmill.Info("NpcPointManagerSystem initialized!");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Проверяем истекшие резервации и кулдауны
        CheckExpiredReservations();
        CheckExpiredCooldowns();
    }

    #region Инициализация

    private void OnPatrolPointStartup(EntityUid uid, PatrolPointComponent component, ComponentStartup args)
    {
        InitializeSlots(component.Slots, component.Capacity);
    }

    private void OnStrategicPointStartup(EntityUid uid, StrategicPointComponent component, ComponentStartup args)
    {
        InitializeSlots(component.Slots, component.Capacity);
    }

    /// <summary>
    /// Слоты создаются на ComponentStartup, но точка может быть заспавнена
    /// (или загружена с картой) до подписки системы — тогда список пуст и
    /// точка навсегда считается «занятой». Восстанавливаем лениво.
    /// </summary>
    private void EnsureSlots(EntityUid point, StrategicPointComponent comp)
    {
        if (comp.Slots.Count > 0 || comp.Capacity <= 0)
            return;

        InitializeSlots(comp.Slots, comp.Capacity);
        Log.Warning($"Strategic point {ToPrettyString(point)} had no slots, initialized {comp.Capacity}");
    }

    private void InitializeSlots(List<PointSlot> slots, int capacity)
    {
        slots.Clear();
        for (int i = 0; i < capacity; i++)
        {
            slots.Add(new PointSlot(i));
        }
    }

    #endregion

    #region Патрульные точки

    /// <summary>
    /// Попытаться зарезервировать следующую точку патруля
    /// </summary>
    public EntityUid? TryClaimNextPatrolPoint(EntityUid npc, string group, EntityUid? currentPoint, string? route = null)
    {
        _sawmill.Info($"TryClaimNextPatrolPoint called: npc={npc}, group='{group}', route='{route}', currentPoint={currentPoint}");

        // Находим все точки группы с учётом маршрута
        var points = new List<(EntityUid uid, PatrolPointComponent comp, float distance)>();

        var npcXform = Transform(npc);
        var npcPos = _transform.GetWorldPosition(npcXform);

        var query = EntityQueryEnumerator<PatrolPointComponent, TransformComponent>();
        while (query.MoveNext(out var pointUid, out var point, out var pointXform))
        {
            // Фильтр по группе
            if (point.Group != group)
                continue;

            // Фильтр по маршруту
            if (!string.IsNullOrEmpty(route))
            {
                // NPC привязан к маршруту - берём только точки с таким же route (или без route)
                if (!string.IsNullOrEmpty(point.Route) && point.Route != route)
                    continue;
            }
            else
            {
                // NPC без маршрута - берём только точки БЕЗ маршрута
                if (!string.IsNullOrEmpty(point.Route))
                    continue;
            }

            var distance = (_transform.GetWorldPosition(pointXform) - npcPos).Length();
            points.Add((pointUid, point, distance));
            _sawmill.Info($"Found patrol point {pointUid} in group '{group}' route '{point.Route}' at distance {distance}");
        }

        if (points.Count == 0)
        {
            _sawmill.Warning($"No patrol points found for group '{group}' route '{route}'!");
            return null;
        }

        // Сортируем по order
        var ordered = points
            .Where(p => p.comp.Order != null)
            .OrderBy(p => p.comp.Order!.Value)
            .ToList();

        if (ordered.Count == 0)
        {
            // Нет порядка - выбираем случайную свободную
            var available = points.Where(p => HasFreeSlot(p.uid, p.comp.Slots)).ToList();
            if (available.Count == 0)
                return null;

            var chosen = _random.Pick(available);
            return TryReserveSlot(chosen.uid, npc, chosen.comp.Slots, chosen.comp.ReservationTimeout)
                ? chosen.uid
                : null;
        }

        // Ищем следующую точку после текущей
        int currentIndex = -1;
        if (currentPoint != null)
        {
            currentIndex = ordered.FindIndex(p => p.uid == currentPoint);
        }

        // Пробуем точки по порядку начиная со следующей
        for (int i = 0; i < ordered.Count; i++)
        {
            int index = (currentIndex + 1 + i) % ordered.Count;
            var candidate = ordered[index];

            if (HasFreeSlot(candidate.uid, candidate.comp.Slots) || IsOwnedByNpc(candidate.uid, npc, candidate.comp.Slots))
            {
                if (TryReserveSlot(candidate.uid, npc, candidate.comp.Slots, candidate.comp.ReservationTimeout))
                    return candidate.uid;
            }
        }

        return null;
    }

    /// <summary>
    /// Освободить точку патруля
    /// </summary>
    public void ReleasePatrolPoint(EntityUid point, EntityUid npc)
    {
        if (!TryComp<PatrolPointComponent>(point, out var comp))
            return;

        ReleaseSlot(point, npc, comp.Slots, comp.CooldownTime);
    }

    #endregion

    #region Стратегические точки

    /// <summary>
    /// Список подходящих стратегических точек, отсортированный по убыванию приоритета.
    /// Слоты не резервирует: вызывающий сам выбирает первую достижимую точку.
    /// </summary>
    public List<(EntityUid uid, StrategicPointComponent comp)> GetStrategicPointCandidates(
        EntityUid npc, string? faction, HashSet<string> requiredTags)
    {
        var candidates = new List<(EntityUid uid, StrategicPointComponent comp, float score)>();

        var npcXform = Transform(npc);
        var npcPos = _transform.GetWorldPosition(npcXform);

        var query = EntityQueryEnumerator<StrategicPointComponent, TransformComponent>();
        while (query.MoveNext(out var pointUid, out var point, out var pointXform))
        {
            EnsureSlots(pointUid, point);

            // Фильтры
            if (!point.IsActive)
                continue;

            if (faction != null && point.Faction != null && point.Faction != faction)
                continue;

            if (requiredTags.Count > 0 && !point.Tags.Overlaps(requiredTags))
                continue;

            if (!HasFreeSlot(pointUid, point.Slots) && !IsOwnedByNpc(pointUid, npc, point.Slots))
                continue;

            // Вычисляем score. Расстояние между разными картами бессмысленно,
            // поэтому точки на своей карте получают приоритет.
            var sameMap = pointXform.MapID == npcXform.MapID;
            var distance = sameMap
                ? (_transform.GetWorldPosition(pointXform) - npcPos).Length()
                : 0f;
            var priority = point.Priority * (point.IsActive ? point.ActivePriorityMultiplier : 1.0f);

            // Чем ближе и важнее - тем лучше
            var score = priority * 10.0f - distance * 0.1f;

            // Разбредание: уже занятая точка теряет привлекательность, чтобы
            // толпа распределялась по разным точкам, а не липла к одной.
            score -= point.Slots.Count(s => !s.IsFree()) * 20.0f;

            if (!sameMap)
                score -= 1000f;

            candidates.Add((pointUid, point, score));
        }

        return candidates
            .OrderByDescending(c => c.score)
            .Select(c => (c.uid, c.comp))
            .ToList();
    }

    /// <summary>
    /// Попытаться зарезервировать стратегическую точку
    /// </summary>
    public EntityUid? TryClaimStrategicPoint(EntityUid npc, string? faction, HashSet<string> requiredTags)
    {
        foreach (var (pointUid, _) in GetStrategicPointCandidates(npc, faction, requiredTags))
        {
            if (TryReserveStrategicPoint(pointUid, npc))
                return pointUid;
        }

        return null;
    }

    /// <summary>
    /// Зарезервировать слот конкретной стратегической точки.
    /// </summary>
    public bool TryReserveStrategicPoint(EntityUid point, EntityUid npc)
    {
        if (!TryComp<StrategicPointComponent>(point, out var comp))
            return false;

        EnsureSlots(point, comp);
        return TryReserveSlot(point, npc, comp.Slots, comp.ReservationTimeout, comp.SpreadRadius);
    }

    /// <summary>
    /// Короткая сводка: сколько точек в мире и почему они не подошли NPC.
    /// Нужна для диагностики «пишет, что точек нет, а они есть».
    /// </summary>
    public string DescribeStrategicPoints(EntityUid npc, string? faction, HashSet<string> requiredTags)
    {
        int total = 0, inactive = 0, tagMismatch = 0, factionMismatch = 0, full = 0, onOtherMap = 0;
        var others = new List<string>();

        var npcXform = Transform(npc);

        var query = EntityQueryEnumerator<StrategicPointComponent, TransformComponent>();
        while (query.MoveNext(out var pointUid, out var point, out var pointXform))
        {
            total++;
            EnsureSlots(pointUid, point);

            if (pointXform.MapID != npcXform.MapID)
                onOtherMap++;

            if (!point.IsActive)
            {
                inactive++;
                continue;
            }

            if (faction != null && point.Faction != null && point.Faction != faction)
            {
                factionMismatch++;
                continue;
            }

            if (requiredTags.Count > 0 && !point.Tags.Overlaps(requiredTags))
            {
                tagMismatch++;
                continue;
            }

            if (!HasFreeSlot(pointUid, point.Slots) && !IsOwnedByNpc(pointUid, npc, point.Slots))
            {
                full++;
                continue;
            }

            others.Add($"{ToPrettyString(pointUid)}: tags={string.Join(",", point.Tags)} active={point.IsActive} slots={point.Slots.Count}");
        }

        var result = $"всего точек={total}, неактивных={inactive}, чужие теги={tagMismatch}, чужая фракция={factionMismatch}, без свободных слотов={full}, на другой карте={onOtherMap}";

        if (others.Count > 0)
            result += $"; подходящие без пути: {string.Join(" | ", others)}";

        return result;
    }

    /// <summary>
    /// Освободить стратегическую точку
    /// </summary>
    public void ReleaseStrategicPoint(EntityUid point, EntityUid npc)
    {
        if (!TryComp<StrategicPointComponent>(point, out var comp))
            return;

        ReleaseSlot(point, npc, comp.Slots, comp.CooldownTime);
    }

    /// <summary>
    /// Активировать стратегические точки с тегом
    /// </summary>
    public void ActivateStrategicPoints(string tag, bool active = true)
    {
        var query = EntityQueryEnumerator<StrategicPointComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Tags.Contains(tag))
            {
                comp.IsActive = active;
                Log.Info($"Strategic point {ToPrettyString(uid)} {(active ? "activated" : "deactivated")}");
            }
        }
    }

    #endregion

    #region Управление слотами

    private bool HasFreeSlot(EntityUid point, List<PointSlot> slots)
    {
        return slots.Any(s => s.IsFree());
    }

    private bool IsOwnedByNpc(EntityUid point, EntityUid npc, List<PointSlot> slots)
    {
        return slots.Any(s => s.IsOwnedBy(npc));
    }

    private bool TryReserveSlot(EntityUid point, EntityUid npc, List<PointSlot> slots, float timeoutSeconds, float spreadRadius = 0f)
    {
        // Проверяем, может уже есть слот у этого NPC
        var existing = slots.FirstOrDefault(s => s.IsOwnedBy(npc));
        if (existing != null)
        {
            // Если слот уже ЗАНЯТ (Occupied) - продлеваем
            // Если только зарезервирован - НЕ продлеваем, чтобы не блокировать plan execution
            if (existing.State == PointSlotState.Occupied)
            {
                existing.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(timeoutSeconds);
                Log.Debug($"Extended occupied slot {existing.Index} on point {ToPrettyString(point)} for NPC {ToPrettyString(npc)}");
                return true;
            }

            // Слот зарезервирован но не занят - возвращаем успех без изменений
            Log.Debug($"NPC {ToPrettyString(npc)} already has reserved slot {existing.Index} on point {ToPrettyString(point)}, not extending");
            return true;
        }

        // Ищем свободный слот
        var freeSlot = slots.FirstOrDefault(s => s.IsFree());
        if (freeSlot == null)
            return false;

        // Резервируем
        freeSlot.State = PointSlotState.Reserved;
        freeSlot.Owner = npc;
        freeSlot.StartedAt = _timing.CurTime;
        freeSlot.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(timeoutSeconds);

        // Своя позиция в округе точки: NPC расходятся, а не встают в один тайл.
        freeSlot.Offset = GetSlotOffset(freeSlot.Index, slots.Count, spreadRadius);

        // Добавляем трекер для автоматической очистки
        EnsureComp<NpcPointTrackerComponent>(npc);

        Log.Debug($"Reserved slot {freeSlot.Index} on point {ToPrettyString(point)} for NPC {ToPrettyString(npc)}");
        return true;
    }

    /// <summary>
    /// Позиция слота относительно центра точки.
    /// </summary>
    public bool TryGetSlotOffset(EntityUid point, EntityUid npc, out Vector2 offset)
    {
        offset = Vector2.Zero;

        if (!TryComp<StrategicPointComponent>(point, out var comp))
            return false;

        EnsureSlots(point, comp);

        var slot = comp.Slots.FirstOrDefault(s => s.IsOwnedBy(npc));
        if (slot == null)
            return false;

        offset = slot.Offset;
        return true;
    }

    /// <summary>
    /// Раскладывает слоты по спирали (золотой угол), чтобы соседние NPC
    /// не выстраивались в линию и не занимали один тайл.
    /// </summary>
    private static Vector2 GetSlotOffset(int index, int capacity, float radius)
    {
        if (radius <= 0f || capacity <= 1)
            return Vector2.Zero;

        var angle = index * 2.399963f;
        var r = radius * MathF.Sqrt((index + 0.5f) / capacity);

        return new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r);
    }

    /// <summary>
    /// Занять слот (NPC пришёл на точку)
    /// </summary>
    public bool OccupySlot(EntityUid point, EntityUid npc)
    {
        List<PointSlot>? slots = null;

        if (TryComp<PatrolPointComponent>(point, out var patrol))
            slots = patrol.Slots;
        else if (TryComp<StrategicPointComponent>(point, out var strategic))
            slots = strategic.Slots;

        if (slots == null)
            return false;

        if (TryComp<StrategicPointComponent>(point, out var strategicComp))
            EnsureSlots(point, strategicComp);

        var slot = slots.FirstOrDefault(s => s.IsOwnedBy(npc));
        if (slot == null)
            return false;

        slot.State = PointSlotState.Occupied;
        slot.StartedAt = _timing.CurTime;
        slot.ExpiresAt = TimeSpan.MaxValue; // Занят до явного освобождения

        Log.Debug($"Occupied slot {slot.Index} on point {ToPrettyString(point)} by NPC {ToPrettyString(npc)}");
        return true;
    }

    private void ReleaseSlot(EntityUid point, EntityUid npc, List<PointSlot> slots, float cooldownSeconds)
    {
        var slot = slots.FirstOrDefault(s => s.IsOwnedBy(npc));
        if (slot == null)
            return;

        slot.State = PointSlotState.Cooldown;
        slot.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(cooldownSeconds);

        Log.Debug($"Released slot {slot.Index} on point {ToPrettyString(point)} from NPC {ToPrettyString(npc)}");
    }

    #endregion

    #region Очистка

    private void CheckExpiredReservations()
    {
        var now = _timing.CurTime;

        // Патрульные точки
        var patrolQuery = EntityQueryEnumerator<PatrolPointComponent>();
        while (patrolQuery.MoveNext(out var uid, out var comp))
        {
            CheckSlotsExpiration(uid, comp.Slots, now);
        }

        // Стратегические точки
        var strategicQuery = EntityQueryEnumerator<StrategicPointComponent>();
        while (strategicQuery.MoveNext(out var uid, out var comp))
        {
            CheckSlotsExpiration(uid, comp.Slots, now);
        }
    }

    private void CheckExpiredCooldowns()
    {
        var now = _timing.CurTime;

        var patrolQuery = EntityQueryEnumerator<PatrolPointComponent>();
        while (patrolQuery.MoveNext(out var uid, out var comp))
        {
            foreach (var slot in comp.Slots.Where(s => s.State == PointSlotState.Cooldown && s.ExpiresAt <= now))
            {
                slot.State = PointSlotState.Free;
                slot.Owner = null;
            }
        }

        var strategicQuery = EntityQueryEnumerator<StrategicPointComponent>();
        while (strategicQuery.MoveNext(out var uid, out var comp))
        {
            foreach (var slot in comp.Slots.Where(s => s.State == PointSlotState.Cooldown && s.ExpiresAt <= now))
            {
                slot.State = PointSlotState.Free;
                slot.Owner = null;
            }
        }
    }

    private void CheckSlotsExpiration(EntityUid point, List<PointSlot> slots, TimeSpan now)
    {
        foreach (var slot in slots.Where(s => s.State == PointSlotState.Reserved && s.ExpiresAt <= now))
        {
            Log.Warning($"Reservation expired for slot {slot.Index} on point {ToPrettyString(point)}, owner: {slot.Owner}");
            slot.State = PointSlotState.Free;
            slot.Owner = null;
        }
    }

    private void OnTrackerTerminating(EntityUid npc, NpcPointTrackerComponent tracker, ref EntityTerminatingEvent ev)
    {
        // Освобождаем все слоты этого NPC
        var patrolQuery = EntityQueryEnumerator<PatrolPointComponent>();
        while (patrolQuery.MoveNext(out var uid, out var comp))
        {
            ReleaseSlot(uid, npc, comp.Slots, 0f);
        }

        var strategicQuery = EntityQueryEnumerator<StrategicPointComponent>();
        while (strategicQuery.MoveNext(out var uid, out var comp))
        {
            ReleaseSlot(uid, npc, comp.Slots, 0f);
        }
    }

    #endregion

    #region Утилиты

    /// <summary>
    /// Автоматически назначает маршрут NPC на основе доступных точек.
    /// Выбирает наименее загруженный маршрут в группе.
    /// </summary>
    public string? AutoAssignRoute(EntityUid npc, string group)
    {
        // Находим все уникальные маршруты в группе
        var routes = new Dictionary<string, int>(); // route -> количество NPC на нём

        var query = EntityQueryEnumerator<PatrolPointComponent>();
        while (query.MoveNext(out var pointUid, out var point))
        {
            if (point.Group != group)
                continue;

            // Точки без маршрута доступны всем - пропускаем
            if (string.IsNullOrEmpty(point.Route))
                continue;

            if (!routes.ContainsKey(point.Route))
                routes[point.Route] = 0;
        }

        if (routes.Count == 0)
        {
            _sawmill.Info($"No routes found in group '{group}' - NPC will use common points");
            return null; // Нет специфичных маршрутов - используем общие точки
        }

        // Подсчитываем NPC на каждом маршруте
        var trackerQuery = EntityQueryEnumerator<NpcPointTrackerComponent>();
        while (trackerQuery.MoveNext(out var trackerUid, out var tracker))
        {
            if (tracker.PatrolGroup == group && !string.IsNullOrEmpty(tracker.PatrolRoute))
            {
                if (routes.ContainsKey(tracker.PatrolRoute))
                    routes[tracker.PatrolRoute]++;
            }
        }

        // Выбираем наименее загруженный маршрут
        var leastLoaded = routes.OrderBy(kvp => kvp.Value).First();
        _sawmill.Info($"Auto-assigned route '{leastLoaded.Key}' (load: {leastLoaded.Value} NPCs) from {routes.Count} available routes");

        return leastLoaded.Key;
    }

    /// <summary>
    /// Получить информацию о слотах точки
    /// </summary>
    public (int total, int free, int reserved, int occupied) GetPointStatus(EntityUid point)
    {
        List<PointSlot>? slots = null;

        if (TryComp<PatrolPointComponent>(point, out var patrol))
            slots = patrol.Slots;
        else if (TryComp<StrategicPointComponent>(point, out var strategic))
            slots = strategic.Slots;

        if (slots == null)
            return (0, 0, 0, 0);

        return (
            slots.Count,
            slots.Count(s => s.IsFree()),
            slots.Count(s => s.IsReserved()),
            slots.Count(s => s.IsOccupied())
        );
    }

    #endregion
}
