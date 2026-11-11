using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server.NPC.Pathfinding;
namespace Content.Server._Grosse.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Простой оператор который удаляет указанный ключ из blackboard.
/// Используется для сброса состояния между циклами патруля.
/// </summary>
public sealed partial class BlackboardRemoveOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;

    /// <summary>
    /// Ключ который нужно удалить из blackboard.
    /// </summary>
    [DataField("key", required: true)]
    public string Key = string.Empty;

    /// <summary>
    /// Дополнительные ключи для удаления (опционально).
    /// </summary>
    [DataField("additionalKeys")]
    public List<string>? AdditionalKeys = null;

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        // Удаляем основной ключ
        if (!string.IsNullOrEmpty(Key))
        {
            blackboard.Remove<object>(Key);
        }

        // Удаляем дополнительные ключи если указаны
        if (AdditionalKeys != null)
        {
            foreach (var additionalKey in AdditionalKeys)
            {
                if (!string.IsNullOrEmpty(additionalKey))
                {
                    blackboard.Remove<object>(additionalKey);
                }
            }
        }

        return HTNOperatorStatus.Finished;
    }
}
