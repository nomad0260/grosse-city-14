using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;

namespace Content.Server._Grosse.NPC.Components;

/// <summary>
/// Позволяет NPC добивать беспомощную (критическую) добычу: NPC бьёт тело,
/// пока оно не умрёт, затем <see cref="TearTime"/> секунд разрывает его на
/// части, съедает несколько выпавших органов и восстанавливает здоровье.
/// </summary>
[RegisterComponent]
public sealed partial class NpcDevourComponent : Component
{
    /// <summary>
    /// Сколько секунд NPC разрывает мёртвое тело на части.
    /// </summary>
    [DataField]
    public float TearTime = 15f;

    /// <summary>
    /// Сколько выпавших органов NPC съедает после разрывания тела.
    /// </summary>
    [DataField]
    public int OrgansToEat = 2;

    /// <summary>
    /// Сколько здоровья восстанавливается после поедания добычи.
    /// </summary>
    [DataField]
    public float HealAmount = 25f;

    /// <summary>
    /// Тип урона, который снимается при лечении. Вынесен в YAML, чтобы не
    /// зашивать «Slash» строкой в системе.
    /// </summary>
    [DataField]
    public string HealDamageType = "Slash";

    /// <summary>
    /// Максимальная дистанция до добычи.
    /// </summary>
    [DataField]
    public float Range = 1.5f;

    /// <summary>
    /// Звук, который играет в момент поедания органов.
    /// </summary>
    [DataField]
    public SoundSpecifier? DevourSound;

    /// <summary>
    /// Тело, которое NPC сейчас разрывает на части.
    /// </summary>
    [ViewVariables]
    public EntityUid? TearTarget;

    /// <summary>
    /// Активное действие разрывания тела.
    /// </summary>
    [ViewVariables]
    public DoAfterId? TearId;
}
