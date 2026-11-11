using Content.Shared.NPC.Events;
using Content.Shared._Grosse.NPC.Events;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using System.Numerics;
using Content.Client.NPC;

namespace Content.Client._Grosse.NPC;

/// <summary>
/// Клиентская система для отрисовки патрульных маршрутов.
/// </summary>
public sealed partial class PatrolVisualizerSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    private PatrolVisualizerOverlay? _patrolOverlay;
    private PatrolVisualizerDataMessage? _currentData;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PatrolVisualizerDataMessage>(OnPatrolData);
    }

    private void OnPatrolData(PatrolVisualizerDataMessage msg)
    {
        _currentData = msg;

        // Если данных нет - выключаем overlay
        if (msg.Groups.Count == 0)
        {
            DisableOverlay();
            return;
        }

        // Включаем overlay если ещё не включён
        EnableOverlay();
    }

    private void EnableOverlay()
    {
        if (_patrolOverlay != null)
            return;

        _patrolOverlay = new PatrolVisualizerOverlay(this);
        _overlay.AddOverlay(_patrolOverlay);
    }

    private void DisableOverlay()
    {
        if (_patrolOverlay == null)
            return;

        _overlay.RemoveOverlay(_patrolOverlay);
        _patrolOverlay = null;
    }

    public PatrolVisualizerDataMessage? GetCurrentData() => _currentData;
}

/// <summary>
/// Overlay для отрисовки линий между патрульными точками.
/// </summary>
public sealed partial class PatrolVisualizerOverlay : Overlay
{
    private readonly PatrolVisualizerSystem _system;
    private readonly IEntityManager _entManager;
    private readonly IEyeManager _eyeManager;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public PatrolVisualizerOverlay(PatrolVisualizerSystem system)
    {
        _system = system;
        _entManager = IoCManager.Resolve<IEntityManager>();
        _eyeManager = IoCManager.Resolve<IEyeManager>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var data = _system.GetCurrentData();
        if (data == null)
            return;

        var handle = args.WorldHandle;
        var xformSys = _entManager.System<SharedTransformSystem>();

        foreach (var group in data.Groups)
        {
            if (group.Points.Count == 0)
                continue;

            // Определяем цвет для группы (хэш от имени группы)
            var color = GetGroupColor(group.GroupName);

            // Рисуем линии между точками
            for (var i = 0; i < group.Points.Count; i++)
            {
                var currentPoint = group.Points[i];
                var nextPoint = group.Points[(i + 1) % group.Points.Count];

                if (!_entManager.TryGetEntity(currentPoint.Entity, out var currentEntity) ||
                    !_entManager.TryGetEntity(nextPoint.Entity, out var nextEntity))
                    continue;

                var currentPos = xformSys.GetWorldPosition(currentEntity.Value);
                var nextPos = xformSys.GetWorldPosition(nextEntity.Value);

                // Рисуем линию
                handle.DrawLine(currentPos, nextPos, color);

                // Рисуем стрелку в середине линии
                var midPoint = (currentPos + nextPos) / 2f;
                var direction = (nextPos - currentPos).Normalized();
                DrawArrow(handle, midPoint, direction, color);

                // Рисуем маркер точки
                DrawPointMarker(handle, currentPos, color);
            }
        }
    }

    private void DrawArrow(DrawingHandleWorld handle, Vector2 position, Vector2 direction, Color color)
    {
        var arrowSize = 0.3f;

        // Вычисляем точки стрелки
        var perpendicular = new Vector2(-direction.Y, direction.X);
        var left = position - direction * arrowSize + perpendicular * arrowSize * 0.5f;
        var right = position - direction * arrowSize - perpendicular * arrowSize * 0.5f;

        handle.DrawLine(position, left, color);
        handle.DrawLine(position, right, color);
    }

    private void DrawPointMarker(DrawingHandleWorld handle, Vector2 position, Color color)
    {
        // Рисуем крестик в точке патруля
        var size = 0.2f;
        handle.DrawLine(position + new Vector2(-size, -size), position + new Vector2(size, size), color);
        handle.DrawLine(position + new Vector2(-size, size), position + new Vector2(size, -size), color);
    }

    private Color GetGroupColor(string groupName)
    {
        // Генерируем цвет на основе хэша имени группы
        var hash = groupName.GetHashCode();
        var hue = (hash & 0xFF) / 255f;

        return Color.FromHsv(new Vector4(hue, 0.8f, 0.9f, 1f));
    }
}
