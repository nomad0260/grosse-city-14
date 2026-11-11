using Robust.Shared.GameObjects;

namespace Content.Server.NPC.Components;

/// <summary>
/// Позволяет NPC добивать беспомощную (критическую) добычу руками: NPC бьёт
/// тело до тех пор, пока оно не развалится на гибы, после чего восстанавливает
/// <see cref="HealAmount"/> здоровья.
/// </summary>
[RegisterComponent]
public sealed partial class NpcDevourComponent : Component
{
    /// <summary>
    /// Сколько ударов нужно нанести, чтобы тело развалилось на гибы.
    /// </summary>
    [DataField]
    public int GibHits = 4;

    /// <summary>
    /// Сколько здоровья восстанавливается после добивания добычи.
    /// </summary>
    [DataField]
    public float HealAmount = 25f;

    /// <summary>
    /// Максимальная дистанция до добычи.
    /// </summary>
    [DataField]
    public float Range = 1.5f;

    /// <summary>
    /// Звук, который играет в момент, когда тело разваливается на гибы.
    /// </summary>
    [DataField]
    public Robust.Shared.Audio.SoundSpecifier? DevourSound;
}
