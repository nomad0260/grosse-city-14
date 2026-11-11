using Robust.Shared.GameObjects;

namespace Content.Server.NPC.Components;

/// <summary>
/// Компонент для NPC, который патрулирует
/// </summary>
[RegisterComponent]
public sealed partial class PatrolComponent : Component
{
    /// <summary>
    /// Название группы патруля (например "security" или "janitor")
    /// </summary>
    [DataField("group")]
    public string Group = "default";

    /// <summary>
    /// Текущая точка патруля (индекс в списке)
    /// </summary>
    [ViewVariables]
    public int CurrentPointIndex = 0;

    /// <summary>
    /// Список точек патруля (EntityUid точек)
    /// </summary>
    [ViewVariables]
    public List<EntityUid> Points = new();

    /// <summary>
    /// Идти по кругу или туда-обратно
    /// </summary>
    [DataField("loop")]
    public bool Loop = true;

    /// <summary>
    /// Направление движения (для туда-обратно)
    /// </summary>
    [ViewVariables]
    public bool Forward = true;

    /// <summary>
    /// Время ожидания на текущей точке
    /// </summary>
    [ViewVariables]
    public float WaitTimer = 0f;
}
