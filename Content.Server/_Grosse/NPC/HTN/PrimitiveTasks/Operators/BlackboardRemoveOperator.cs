using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._Grosse.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Удаляет указанные ключи из blackboard. Используется для сброса состояния
/// между циклами патруля.
/// </summary>
public sealed partial class BlackboardRemoveOperator : HTNOperator
{
    [DataField("key", required: true)]
    public string Key = string.Empty;

    [DataField("additionalKeys")]
    public List<string>? AdditionalKeys;

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        RemoveKey(blackboard, Key);

        if (AdditionalKeys != null)
        {
            for (var i = 0; i < AdditionalKeys.Count; i++)
                RemoveKey(blackboard, AdditionalKeys[i]);
        }

        return HTNOperatorStatus.Finished;
    }

    private static void RemoveKey(NPCBlackboard blackboard, string key)
    {
        if (string.IsNullOrEmpty(key))
            return;

        // Хранимый тип заранее неизвестен: Remove<object> проходит assertion
        // для любого значения и удаляет ключ целиком.
        blackboard.Remove<object>(key);
    }
}
