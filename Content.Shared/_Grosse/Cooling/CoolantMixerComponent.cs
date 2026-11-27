using Robust.Shared.GameObjects;

namespace Content.Shared._Grosse.Cooling;

/// <summary>
/// Смеситель теплоносителей РТМ: собирает жидкости в бак и показывает,
/// что даст смесь в охладителе. Свойства жидкостей берутся из той же таблицы,
/// что и у охладителя (<see cref="CoolingBlockCoolantComponent.Coolants"/>).
/// </summary>
[RegisterComponent]
public sealed partial class CoolantMixerComponent : Component
{
    /// <summary>Бак смесителя.</summary>
    [DataField]
    public string Solution = "solution";

    /// <summary>Теплоотвод блока, для которого считается прогноз.</summary>
    [DataField]
    public float BaseCoolingCapacity = DarkCoreCoolantMath.DefaultCoolingCapacity;

    /// <summary>Штраф за смешивание разных теплоносителей, как у охладителя.</summary>
    [DataField]
    public float MixturePenalty = 0.9f;

    /// <summary>Базовый расход жидкости в секунду, как у охладителя.</summary>
    [DataField]
    public float BaseUse = 0.5f;
}
