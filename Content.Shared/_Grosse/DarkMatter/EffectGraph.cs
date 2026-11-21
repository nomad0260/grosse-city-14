using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.DarkMatter;

/// <summary>
/// The value carried by a port in an effect graph.
/// </summary>
[NetSerializable, Serializable]
public enum EffectValueType : byte
{
    Number,
    Boolean,
    Entity,
    EntityList,
    Vector,
}

[NetSerializable, Serializable]
public sealed class EffectPortInfo
{
    public readonly string Id;
    public readonly string Label;
    public readonly EffectValueType Type;
    public readonly bool Required;

    public EffectPortInfo(string id, string label, EffectValueType type, bool required = true)
    {
        Id = id;
        Label = label;
        Type = type;
        Required = required;
    }
}

[NetSerializable, Serializable]
public sealed class EffectParameterInfo
{
    public readonly string Id;
    public readonly string Label;
    public readonly float Default;
    public readonly float Min;
    public readonly float Max;
    public readonly float Step;

    public EffectParameterInfo(string id, string label, float defaultValue, float min, float max, float step = 1f)
    {
        Id = id;
        Label = label;
        Default = defaultValue;
        Min = min;
        Max = max;
        Step = step;
    }
}

/// <summary>
/// A single palette entry. Adding an operation means adding an entry here and a handler in
/// <c>EffectGraphSystem</c>; the node editor itself does not need to know about it.
/// </summary>
public sealed class EffectOperationInfo
{
    public readonly string Id;
    public readonly string Label;
    public readonly string Category;
    public readonly string Description;
    public readonly EffectPortInfo[] Inputs;
    public readonly EffectPortInfo[] Outputs;
    public readonly EffectParameterInfo[] Parameters;

    /// <summary>
    /// Execution family. Several catalog entries can share one handler (aliases with a
    /// different label or parameter set), while the interpreter switches on this value.
    /// </summary>
    public readonly string Handler;

    public EffectOperationInfo(
        string id,
        string label,
        string category,
        string description,
        EffectPortInfo[] inputs,
        EffectPortInfo[] outputs,
        EffectParameterInfo[]? parameters = null,
        string? handler = null)
    {
        Id = id;
        Label = label;
        Category = category;
        Description = description;
        Inputs = inputs;
        Outputs = outputs;
        Parameters = parameters ?? Array.Empty<EffectParameterInfo>();
        Handler = handler ?? id;
    }

    public EffectPortInfo? FindInput(string id)
    {
        return Inputs.FirstOrDefault(port => port.Id == id);
    }

    public EffectPortInfo? FindOutput(string id)
    {
        return Outputs.FirstOrDefault(port => port.Id == id);
    }
}

/// <summary>
/// A node stores only an operation id and numeric parameters. It does not get a prototype per
/// combination, which keeps the system data-driven.
/// </summary>
[DataDefinition, NetSerializable, Serializable]
public sealed partial class EffectGraphNode
{
    [DataField]
    public int Id;

    [DataField]
    public string Operation = string.Empty;

    [DataField]
    public Vector2 Position;

    [DataField]
    public Dictionary<string, float> Parameters = new();

    public EffectGraphNode Clone()
    {
        return new EffectGraphNode
        {
            Id = Id,
            Operation = Operation,
            Position = Position,
            Parameters = new Dictionary<string, float>(Parameters ?? new Dictionary<string, float>()),
        };
    }
}

[DataDefinition, NetSerializable, Serializable]
public sealed partial class EffectGraphConnection
{
    [DataField]
    public int FromNode;

    [DataField]
    public string FromPort = string.Empty;

    [DataField]
    public int ToNode;

    [DataField]
    public string ToPort = string.Empty;

    public EffectGraphConnection Clone()
    {
        return new EffectGraphConnection
        {
            FromNode = FromNode,
            FromPort = FromPort,
            ToNode = ToNode,
            ToPort = ToPort,
        };
    }
}

/// <summary>
/// Serializable physical law. This object is the source of truth that gets copied into a crystal;
/// editing an archived formula later must not mutate existing crystals.
/// </summary>
[DataDefinition, NetSerializable, Serializable]
public sealed partial class EffectGraph
{
    public const int CurrentVersion = 1;

    [DataField]
    public int Version = CurrentVersion;

    [DataField]
    public List<EffectGraphNode> Nodes = new();

    [DataField]
    public List<EffectGraphConnection> Connections = new();

    public EffectGraph Clone()
    {
        var clone = new EffectGraph { Version = Version };
        clone.Nodes.AddRange((Nodes ?? new List<EffectGraphNode>()).Where(node => node != null).Select(node => node.Clone()));
        clone.Connections.AddRange((Connections ?? new List<EffectGraphConnection>())
            .Where(connection => connection != null)
            .Select(connection => connection.Clone()));
        return clone;
    }

    public EffectGraphNode? FindNode(int id)
    {
        return Nodes.FirstOrDefault(node => node.Id == id);
    }

    public int NextNodeId()
    {
        var id = 1;
        foreach (var node in Nodes)
            id = Math.Max(id, node.Id + 1);

        return id;
    }

    public void RemoveNode(int id)
    {
        Nodes.RemoveAll(node => node.Id == id);
        Connections.RemoveAll(connection => connection.FromNode == id || connection.ToNode == id);
    }
}

/// <summary>
/// The complete operation palette. Ports are deliberately typed so the editor and server can
/// reject nonsensical graphs before any effect is executed.
/// </summary>
public static class EffectOperationCatalog
{
    public const int MaxNodes = 128;
    public const int MaxConnections = 320;
    public const int MaxDepth = 40;

    private static readonly EffectPortInfo NumberA = new("a", "A", EffectValueType.Number);
    private static readonly EffectPortInfo NumberB = new("b", "B", EffectValueType.Number);
    private static readonly EffectPortInfo NumberOut = new("value", "Значение", EffectValueType.Number);
    private static readonly EffectPortInfo BoolOut = new("value", "Результат", EffectValueType.Boolean);
    private static readonly EffectPortInfo EntityOut = new("value", "Сущность", EffectValueType.Entity);
    private static readonly EffectPortInfo ListOut = new("value", "Список", EffectValueType.EntityList);
    private static readonly EffectPortInfo VectorOut = new("value", "Вектор", EffectValueType.Vector);

    private static readonly EffectPortInfo EntityIn = new("entity", "Сущность", EffectValueType.Entity);
    private static readonly EffectPortInfo OtherEntityIn = new("other", "Другая", EffectValueType.Entity);
    private static readonly EffectPortInfo ListIn = new("entities", "Список", EffectValueType.EntityList);
    private static readonly EffectPortInfo AmountIn = new("amount", "Величина", EffectValueType.Number);
    private static readonly EffectPortInfo TriggerIn = new("trigger", "Условие", EffectValueType.Boolean, false);
    private static readonly EffectPortInfo NumberC = new("c", "C", EffectValueType.Number);
    private static readonly EffectPortInfo BoolA = new("a", "A", EffectValueType.Boolean);
    private static readonly EffectPortInfo BoolB = new("b", "B", EffectValueType.Boolean);
    private static readonly EffectPortInfo VectorA = new("a", "A", EffectValueType.Vector);
    private static readonly EffectPortInfo VectorB = new("b", "B", EffectValueType.Vector);
    private static readonly EffectPortInfo VectorIn = new("vector", "Вектор", EffectValueType.Vector);
    private static readonly EffectPortInfo ListB = new("list_b", "Список B", EffectValueType.EntityList);

    public static readonly IReadOnlyList<EffectOperationInfo> All = BuildAll();

    private static List<EffectOperationInfo> BuildAll()
    {
        var all = new List<EffectOperationInfo>
        {
        // Sources
        Op("constant", "Константа", "Источники", "Возвращает заданное число.",
            Array.Empty<EffectPortInfo>(), new[] { NumberOut },
            new[] { new EffectParameterInfo("value", "Число", 1f, -10000f, 10000f, 0.1f) }),
        Op("self", "Кристалл", "Источники", "Сам предмет, в который записана формула.",
            Array.Empty<EffectPortInfo>(), new[] { EntityOut }),
        Op("owner", "Владелец", "Источники", "Существо, создавшее кристалл.",
            Array.Empty<EffectPortInfo>(), new[] { EntityOut }),
        Op("get_position", "Координаты", "Пространство", "Мировые координаты сущности.",
            new[] { EntityIn }, new[] { VectorOut }),
        Op("find_entities", "Поиск сущностей", "Цели", "Ищет сущности вокруг кристалла.",
            Array.Empty<EffectPortInfo>(), new[] { ListOut },
            new[]
            {
                new EffectParameterInfo("radius", "Радиус", 5f, 0f, 30f, 0.5f),
                new EffectParameterInfo("through_walls", "Сквозь стены", 1f, 0f, 1f, 1f),
            }),

        // Selection
        Op("filter_living", "Только живые", "Цели", "Оставляет живых существ.",
            new[] { ListIn }, new[] { ListOut }),
        Op("filter_dead", "Только мёртвые", "Цели", "Оставляет мёртвые тела.",
            new[] { ListIn }, new[] { ListOut }),
        Op("filter_items", "Только предметы", "Цели", "Оставляет предметы.",
            new[] { ListIn }, new[] { ListOut }),
        Op("filter_structures", "Только структуры", "Цели", "Оставляет структуры.",
            new[] { ListIn }, new[] { ListOut }),
        Op("nearest", "Ближайший", "Цели", "Выбирает ближайшую сущность из списка.",
            new[] { ListIn, EntityIn }, new[] { EntityOut }),
        Op("random_target", "Случайная цель", "Цели", "Выбирает случайную сущность из списка.",
            new[] { ListIn }, new[] { EntityOut }),
        Op("count", "Количество", "Цели", "Считает элементы списка.",
            new[] { ListIn }, new[] { NumberOut }),

        // Math
        OpBinary("add", "Сложение", "Математика", "A + B."),
        OpBinary("subtract", "Вычитание", "Математика", "A - B."),
        OpBinary("multiply", "Умножение", "Математика", "A × B."),
        OpBinary("divide", "Деление", "Математика", "A ÷ B."),
        OpBinary("power", "Степень", "Математика", "A в степени B."),
        OpBinary("root", "Корень", "Математика", "Корень B-й степени из A."),
        OpBinary("min", "Минимум", "Математика", "Меньшее из A и B."),
        OpBinary("max", "Максимум", "Математика", "Большее из A и B."),
        OpUnary("abs", "Модуль", "Математика", "Абсолютное значение."),
        OpUnary("sin", "Синус", "Математика", "Синус A."),
        OpUnary("cos", "Косинус", "Математика", "Косинус A."),
        Op("clamp", "Ограничение", "Математика", "Ограничивает A диапазоном.",
            new[] { NumberA, new EffectPortInfo("min", "Мин", EffectValueType.Number), new EffectPortInfo("max", "Макс", EffectValueType.Number) },
            new[] { NumberOut }),
        Op("normalize", "Нормализация", "Математика", "Переводит A из диапазона в 0..1.",
            new[] { NumberA, new EffectPortInfo("min", "Мин", EffectValueType.Number), new EffectPortInfo("max", "Макс", EffectValueType.Number) },
            new[] { NumberOut }),
        Op("random", "Случайное", "Математика", "Случайное число в заданном диапазоне.",
            Array.Empty<EffectPortInfo>(), new[] { NumberOut },
            new[]
            {
                new EffectParameterInfo("min", "Мин", 0f, -10000f, 10000f, 0.1f),
                new EffectParameterInfo("max", "Макс", 1f, -10000f, 10000f, 0.1f),
            }),

        // Logic
        OpBinary("greater", "Больше", "Логика", "A > B.", EffectValueType.Boolean),
        OpBinary("less", "Меньше", "Логика", "A < B.", EffectValueType.Boolean),
        OpBinary("equal", "Равно", "Логика", "A = B.", EffectValueType.Boolean),
        OpBinary("not_equal", "Не равно", "Логика", "A ≠ B.", EffectValueType.Boolean),
        Op("and", "И", "Логика", "Логическое И.",
            new[] { new EffectPortInfo("a", "A", EffectValueType.Boolean), new EffectPortInfo("b", "B", EffectValueType.Boolean) },
            new[] { BoolOut }),
        Op("or", "ИЛИ", "Логика", "Логическое ИЛИ.",
            new[] { new EffectPortInfo("a", "A", EffectValueType.Boolean), new EffectPortInfo("b", "B", EffectValueType.Boolean) },
            new[] { BoolOut }),
        Op("not", "НЕ", "Логика", "Логическое НЕ.",
            new[] { new EffectPortInfo("a", "A", EffectValueType.Boolean) }, new[] { BoolOut }),
        Op("in_range", "В диапазоне", "Логика", "Проверяет, что A находится между B и C.",
            new[] { NumberA, NumberB, new EffectPortInfo("c", "Макс", EffectValueType.Number) },
            new[] { BoolOut }),

        // Space
        Op("distance", "Расстояние", "Пространство", "Расстояние между двумя сущностями.",
            new[] { EntityIn, OtherEntityIn }, new[] { NumberOut }),
        Op("direction", "Направление", "Пространство", "Единичный вектор от A к B.",
            new[] { EntityIn, OtherEntityIn }, new[] { VectorOut }),
        Op("in_radius", "В радиусе", "Пространство", "Проверяет расстояние между A и B.",
            new[] { EntityIn, OtherEntityIn, AmountIn }, new[] { BoolOut }),
        Op("filter_range", "В области", "Пространство", "Оставляет объекты рядом с опорной сущностью.",
            new[] { ListIn, EntityIn, AmountIn }, new[] { ListOut }),
        Op("line_of_sight", "Прямая видимость", "Пространство", "Проверяет отсутствие препятствий между A и B.",
            new[] { EntityIn, OtherEntityIn, AmountIn }, new[] { BoolOut }),
        Op("ignore_obstacles", "Без препятствий", "Пространство", "Помечает список как проходящий сквозь стены.",
            new[] { ListIn }, new[] { ListOut }),

        // Time and state
        Op("timer", "Таймер", "Время", "Возвращает истину один раз за заданный интервал.",
            Array.Empty<EffectPortInfo>(), new[] { BoolOut },
            new[] { new EffectParameterInfo("interval", "Интервал, с", 1f, 0.2f, 60f, 0.1f) }),
        Op("accumulate", "Накопитель", "Время", "Прибавляет значение к состоянию узла.",
            new[] { NumberA }, new[] { NumberOut },
            new[] { new EffectParameterInfo("limit", "Предел", 1000f, 1f, 10000f, 1f) }),

        // Effects
        Op("heal", "Лечение", "Воздействие", "Восстанавливает здоровье цели.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut },
            new[] { new EffectParameterInfo("efficiency", "Эффективность", 1f, 0f, 4f, 0.1f) }),
        Op("damage", "Повреждение", "Воздействие", "Наносит термический урон цели.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut },
            new[] { new EffectParameterInfo("efficiency", "Эффективность", 1f, 0f, 4f, 0.1f) }),
        Op("set_temperature", "Температура", "Воздействие", "Нагревает или охлаждает цель.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut }),
        Op("radiation", "Радиация", "Воздействие", "Облучает цель. Урон зависит от накопленной дозы.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut }),
        Op("impulse", "Импульс", "Воздействие", "Толкает цель от кристалла.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut }),
        Op("set_velocity", "Скорость", "Воздействие", "Задаёт скорость цели в направлении от кристалла.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut }),
        Op("move_entity", "Перемещение", "Воздействие", "Смещает цель от кристалла.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut }),
        Op("transfer_energy", "Передача энергии", "Энергия", "Заряжает батарею цели за счёт энергии кристалла.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut }),
        Op("light", "Освещение", "Воздействие", "Создаёт или изменяет источник света на цели.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut },
            new[] { new EffectParameterInfo("radius", "Радиус", 4f, 0f, 20f, 0.5f) }),
        Op("markers", "Маркеры", "Воздействие", "Отправляет владельцу координаты целей.",
            new[] { ListIn, TriggerIn }, new[] { BoolOut }),

        // Energy
        Op("consume_energy", "Затратить энергию", "Энергия", "Списывает энергию; возвращает успех.",
            new[] { AmountIn }, new[] { BoolOut }),
        Op("store_energy", "Накопить энергию", "Энергия", "Добавляет энергию в запас кристалла.",
            new[] { AmountIn }, new[] { NumberOut }),
        Op("energy", "Запас энергии", "Энергия", "Текущий запас энергии кристалла.",
            Array.Empty<EffectPortInfo>(), new[] { NumberOut }),
        };

        AddExtendedOperations(all);
        return all;
    }

    private static void AddExtendedOperations(List<EffectOperationInfo> list)
    {
        AddSources(list);
        AddMath(list);
        AddLogic(list);
        AddSpaceAndVectors(list);
        AddTime(list);
        AddTargets(list);
        AddPhysical(list);
        AddEnergy(list);
        AddBiology(list);
        AddInformation(list);
        AddMemory(list);
    }

    private static void AddSources(List<EffectOperationInfo> list)
    {
        list.Add(Op("health", "Повреждения", "Источники", "Суммарный урон цели (0 = не повреждена).",
            new[] { EntityIn }, new[] { NumberOut }));
        list.Add(Op("temperature", "Температура цели", "Источники", "Текущая температура цели.",
            new[] { EntityIn }, new[] { NumberOut }));
        list.Add(Op("velocity", "Скорость цели", "Источники", "Вектор линейной скорости цели.",
            new[] { EntityIn }, new[] { VectorOut }));
        list.Add(Op("self_stability", "Стабильность кристалла", "Источники", "Стабильность материала кристалла.",
            Array.Empty<EffectPortInfo>(), new[] { NumberOut }));
        list.Add(Op("self_density", "Плотность кристалла", "Источники", "Плотность материала кристалла.",
            Array.Empty<EffectPortInfo>(), new[] { NumberOut }));
        list.Add(Op("self_reactivity", "Реактивность кристалла", "Источники", "Реактивность материала кристалла.",
            Array.Empty<EffectPortInfo>(), new[] { NumberOut }));
        list.Add(Op("self_purity", "Чистота кристалла", "Источники", "Чистота материала кристалла.",
            Array.Empty<EffectPortInfo>(), new[] { NumberOut }));
        list.Add(Op("self_dark_matter", "Тёмная материя", "Источники", "Концентрация тёмной материи в кристалле.",
            Array.Empty<EffectPortInfo>(), new[] { NumberOut }));
        list.Add(Op("self_efficiency", "Эффективность кристалла", "Источники", "Множитель эффективности записанных воздействий.",
            Array.Empty<EffectPortInfo>(), new[] { NumberOut }));
        list.Add(Op("tick", "Номер такта", "Источники", "Счётчик выполнений этой формулы.",
            Array.Empty<EffectPortInfo>(), new[] { NumberOut }));
        list.Add(Op("time", "Время", "Источники", "Время существования кристалла в секундах.",
            Array.Empty<EffectPortInfo>(), new[] { NumberOut }));
    }

    private static void AddMath(List<EffectOperationInfo> list)
    {
        AddUnaryMath(list, ("tan", "Тангенс", "Тангенс A."),
            ("asin", "Арксинус", "Арксинус A."),
            ("acos", "Арккосинус", "Арккосинус A."),
            ("atan", "Арктангенс", "Арктангенс A."),
            ("sinh", "Гиперболический синус", "sinh(A)."),
            ("cosh", "Гиперболический косинус", "cosh(A)."),
            ("tanh", "Гиперболический тангенс", "tanh(A)."),
            ("floor", "Округление вниз", "Наибольшее целое не больше A."),
            ("ceil", "Округление вверх", "Наименьшее целое не меньше A."),
            ("round", "Округление", "Ближайшее целое."),
            ("trunc", "Отбросить дробь", "Целая часть A."),
            ("sign", "Знак", "-1, 0 или 1."),
            ("sqrt", "Квадратный корень", "Корень из A."),
            ("cbrt", "Кубический корень", "Корень третьей степени из A."),
            ("ln", "Натуральный логарифм", "ln(A)."),
            ("log10", "Десятичный логарифм", "log10(A)."),
            ("exp", "Экспонента", "e^A."),
            ("fract", "Дробная часть", "Дробная часть A."),
            ("negate", "Отрицание", "-A."),
            ("reciprocal", "Обратное число", "1/A."),
            ("saturate", "Насыщение", "Ограничивает A диапазоном 0..1."),
            ("clamp01", "Ограничить 0..1", "Ограничивает A диапазоном 0..1."));

        AddBinaryMath(list, ("atan2", "Арктангенс двух аргументов", "Угол между A и B."),
            ("hypot", "Гипотенуза", "sqrt(A² + B²)."),
            ("mod", "Остаток", "Остаток от деления A на B."),
            ("step", "Ступень", "1, если A ≥ B, иначе 0."),
            ("distance1d", "Расстояние по числу", "|A - B|."),
            ("angle_diff", "Разница углов", "Кратчайшая разница углов A и B."),
            ("average", "Среднее", "(A + B) / 2."),
            ("percentage", "Процент", "A / B, ограниченное 0..1."),
            ("ratio", "Отношение", "A / B без ограничения."),
            ("weighted", "Взвешенное", "A × B."));

        list.Add(Op("lerp", "Интерполяция", "Математика", "Линейная интерполяция A и B параметром C.",
            new[] { NumberA, NumberB, NumberC }, new[] { NumberOut }));
        list.Add(Op("smoothstep", "Плавный переход", "Математика", "Плавная интерполяция A и B параметром C.",
            new[] { NumberA, NumberB, NumberC }, new[] { NumberOut }));
        list.Add(Op("wrap", "Зацикливание", "Математика", "Переносит A в диапазон B..C.",
            new[] { NumberA, NumberB, NumberC }, new[] { NumberOut }));
        list.Add(Op("remap", "Переназначение", "Математика", "Переводит A из 0..1 в B..C.",
            new[] { NumberA, NumberB, NumberC }, new[] { NumberOut }));
    }

    private static void AddUnaryMath(List<EffectOperationInfo> list,
        params (string Id, string Label, string Description)[] entries)
    {
        foreach (var (id, label, description) in entries)
            list.Add(OpUnary(id, label, "Математика", description));
    }

    private static void AddBinaryMath(List<EffectOperationInfo> list,
        params (string Id, string Label, string Description)[] entries)
    {
        foreach (var (id, label, description) in entries)
            list.Add(OpBinary(id, label, "Математика", description));
    }

    private static void AddLogic(List<EffectOperationInfo> list)
    {
        list.Add(OpBoolBinary("xor", "Исключающее ИЛИ", "Истина, если ровно один вход истинен."));
        list.Add(OpBoolBinary("nand", "И-НЕ", "Отрицание логического И."));
        list.Add(OpBoolBinary("nor", "ИЛИ-НЕ", "Отрицание логического ИЛИ."));
        list.Add(OpBoolBinary("xnor", "Исключающее ИЛИ-НЕ", "Отрицание исключающего ИЛИ."));

        list.Add(OpNumberPredicate("is_zero", "Равно нулю", "Истина, если A = 0."));
        list.Add(OpNumberPredicate("is_positive", "Положительное", "Истина, если A > 0."));
        list.Add(OpNumberPredicate("is_negative", "Отрицательное", "Истина, если A < 0."));
        list.Add(OpNumberPredicate("is_finite", "Конечное", "Истина, если A — конечное число."));
        list.Add(Op("between", "Между", "Логика", "Истина, если A находится между B и C.",
            new[] { NumberA, NumberB, NumberC }, new[] { BoolOut }));

        list.Add(OpEntityPredicate("is_alive", "Живое", "Цель жива."));
        list.Add(OpEntityPredicate("is_dead", "Мёртвое", "Цель мертва."));
        list.Add(OpEntityPredicate("is_item", "Предмет", "Цель — предмет."));
        list.Add(OpEntityPredicate("is_structure", "Структура", "Цель — структура."));
        list.Add(OpEntityPredicate("has_health", "Имеет здоровье", "У цели есть компонент здоровья."));
        list.Add(OpEntityPredicate("entity_exists", "Существует", "Сущность ещё существует."));
        list.Add(Op("same_entity", "Та же сущность", "Логика", "A и B — одна и та же сущность.",
            new[] { EntityIn, OtherEntityIn }, new[] { BoolOut }));
        list.Add(Op("is_owner", "Это владелец", "Логика", "Сущность является владельцем кристалла.",
            new[] { EntityIn }, new[] { BoolOut }));
        list.Add(Op("always", "Всегда", "Логика", "Всегда истина.",
            Array.Empty<EffectPortInfo>(), new[] { BoolOut }));
        list.Add(Op("never", "Никогда", "Логика", "Всегда ложь.",
            Array.Empty<EffectPortInfo>(), new[] { BoolOut }));
    }

    private static EffectOperationInfo OpBoolBinary(string id, string label, string description)
    {
        return Op(id, label, "Логика", description, new[] { BoolA, BoolB }, new[] { BoolOut });
    }

    private static EffectOperationInfo OpNumberPredicate(string id, string label, string description)
    {
        return Op(id, label, "Логика", description, new[] { NumberA }, new[] { BoolOut });
    }

    private static EffectOperationInfo OpEntityPredicate(string id, string label, string description)
    {
        return Op(id, label, "Логика", description, new[] { EntityIn }, new[] { BoolOut });
    }

    private static void AddSpaceAndVectors(List<EffectOperationInfo> list)
    {
        list.Add(Op("vector_add", "Сложение векторов", "Пространство", "A + B.",
            new[] { VectorA, VectorB }, new[] { VectorOut }));
        list.Add(Op("vector_subtract", "Вычитание векторов", "Пространство", "A - B.",
            new[] { VectorA, VectorB }, new[] { VectorOut }));
        list.Add(Op("vector_scale", "Масштаб вектора", "Пространство", "A × B.",
            new[] { VectorA, NumberB }, new[] { VectorOut }));
        list.Add(Op("vector_length", "Длина вектора", "Пространство", "Длина A.",
            new[] { VectorA }, new[] { NumberOut }));
        list.Add(Op("vector_normalize", "Нормализовать вектор", "Пространство", "Вектор единичной длины.",
            new[] { VectorA }, new[] { VectorOut }));
        list.Add(Op("vector_dot", "Скалярное произведение", "Пространство", "A · B.",
            new[] { VectorA, VectorB }, new[] { NumberOut }));
        list.Add(Op("vector_cross", "Векторное произведение", "Пространство", "Псевдоскалярное произведение A × B.",
            new[] { VectorA, VectorB }, new[] { NumberOut }));
        list.Add(Op("vector_distance", "Расстояние векторов", "Пространство", "|A - B|.",
            new[] { VectorA, VectorB }, new[] { NumberOut }));
        list.Add(Op("vector_angle", "Угол векторов", "Пространство", "Угол между A и B в радианах.",
            new[] { VectorA, VectorB }, new[] { NumberOut }));
        list.Add(Op("vector_rotate", "Повернуть вектор", "Пространство", "Поворачивает A на B радиан.",
            new[] { VectorA, NumberB }, new[] { VectorOut }));
        list.Add(Op("vector_from_angle", "Вектор из угла", "Пространство", "Единичный вектор по углу A.",
            new[] { NumberA }, new[] { VectorOut }));
        list.Add(Op("get_velocity", "Скорость", "Пространство", "Линейная скорость сущности.",
            new[] { EntityIn }, new[] { VectorOut }));
        list.Add(Op("orbit_position", "Орбитальная точка", "Пространство", "Точка на орбите сущности.",
            new[] { EntityIn, AmountIn, NumberB }, new[] { VectorOut }));
        list.Add(Op("position_offset", "Смещение позиции", "Пространство", "Позиция сущности плюс вектор.",
            new[] { EntityIn, VectorIn }, new[] { VectorOut }));
        list.Add(Op("pull", "Притянуть", "Пространство", "Тянет цель к кристаллу.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut }));
        list.Add(Op("push", "Оттолкнуть", "Пространство", "Толкает цель от кристалла.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut }));
        list.Add(Op("teleport_entity", "Телепортировать", "Пространство", "Перемещает цель в точку.",
            new[] { EntityIn, VectorIn, TriggerIn }, new[] { BoolOut }));
        list.Add(Op("swap_positions", "Обмен позициями", "Пространство", "Меняет местами две сущности.",
            new[] { EntityIn, OtherEntityIn }, new[] { BoolOut }));
    }

    private static void AddTime(List<EffectOperationInfo> list)
    {
        list.Add(Op("cooldown", "Перезарядка", "Время", "Истина один раз за интервал.",
            Array.Empty<EffectPortInfo>(), new[] { BoolOut },
            new[] { new EffectParameterInfo("interval", "Интервал, с", 1f, 0.2f, 60f, 0.1f) }));
        list.Add(Op("delay", "Задержка", "Время", "Пропускает сигнал с задержкой.",
            new[] { TriggerIn }, new[] { BoolOut },
            new[] { new EffectParameterInfo("delay", "Задержка, с", 1f, 0.1f, 30f, 0.1f) }));
        list.Add(Op("pulse", "Импульс", "Время", "Держит сигнал заданное время.",
            new[] { TriggerIn }, new[] { BoolOut },
            new[] { new EffectParameterInfo("duration", "Длительность, с", 0.5f, 0.1f, 10f, 0.1f) }));
        list.Add(Op("once", "Один раз", "Время", "Истина только при первом выполнении.",
            Array.Empty<EffectPortInfo>(), new[] { BoolOut }));
        list.Add(Op("every_n", "Каждый N-й", "Время", "Истина каждый заданный интервал.",
            Array.Empty<EffectPortInfo>(), new[] { BoolOut },
            new[] { new EffectParameterInfo("period", "Период, с", 2f, 0.2f, 120f, 0.1f) }));
        list.Add(Op("lifetime", "Время жизни", "Время", "Секунды с первого выполнения.",
            Array.Empty<EffectPortInfo>(), new[] { NumberOut }));
        list.Add(Op("sequence", "Счётчик", "Время", "Увеличивается на 1 за выполнение.",
            Array.Empty<EffectPortInfo>(), new[] { NumberOut },
            new[] { new EffectParameterInfo("limit", "Предел", 100f, 1f, 10000f, 1f) }));
        list.Add(Op("phase", "Фаза", "Время", "Синусоидальная фаза 0..1.",
            Array.Empty<EffectPortInfo>(), new[] { NumberOut },
            new[] { new EffectParameterInfo("period", "Период, с", 4f, 0.2f, 120f, 0.1f) }));
        list.Add(Op("clock", "Мировое время", "Время", "Текущее время сервера в секундах.",
            Array.Empty<EffectPortInfo>(), new[] { NumberOut }));
        list.Add(Op("tick_count", "Счётчик тактов", "Время", "Сколько раз формула уже выполнялась.",
            Array.Empty<EffectPortInfo>(), new[] { NumberOut }));
    }

    private static void AddTargets(List<EffectOperationInfo> list)
    {
        list.Add(Op("farthest", "Самый дальний", "Цели", "Самая дальняя сущность из списка.",
            new[] { ListIn, EntityIn }, new[] { EntityOut }));
        list.Add(Op("first", "Первый", "Цели", "Первый элемент списка.",
            new[] { ListIn }, new[] { EntityOut }));
        list.Add(Op("last", "Последний", "Цели", "Последний элемент списка.",
            new[] { ListIn }, new[] { EntityOut }));
        list.Add(Op("limit", "Ограничить список", "Цели", "Оставляет не больше A элементов.",
            new[] { ListIn, AmountIn }, new[] { ListOut }));
        list.Add(Op("shuffle", "Перемешать", "Цели", "Случайный порядок списка.",
            new[] { ListIn }, new[] { ListOut }));
        list.Add(Op("sort_distance", "Сортировать по дистанции", "Цели", "Сортирует список по удалённости от опорной сущности.",
            new[] { ListIn, EntityIn }, new[] { ListOut }));
        list.Add(Op("deduplicate", "Убрать повторы", "Цели", "Удаляет дубликаты из списка.",
            new[] { ListIn }, new[] { ListOut }));
        list.Add(Op("merge_lists", "Объединить списки", "Цели", "Соединяет два списка.",
            new[] { ListIn, ListB }, new[] { ListOut }));
        list.Add(Op("exclude_self", "Исключить кристалл", "Цели", "Убирает кристалл из списка.",
            new[] { ListIn }, new[] { ListOut }));
        list.Add(Op("exclude_owner", "Исключить владельца", "Цели", "Убирает владельца из списка.",
            new[] { ListIn }, new[] { ListOut }));
        list.Add(Op("filter_damaged", "Только повреждённые", "Цели", "Оставляет существ с уроном.",
            new[] { ListIn }, new[] { ListOut }));
        list.Add(Op("filter_healthy", "Только здоровые", "Цели", "Оставляет существ без урона.",
            new[] { ListIn }, new[] { ListOut }));
        list.Add(Op("filter_players", "Только игроки", "Цели", "Оставляет игроков.",
            new[] { ListIn }, new[] { ListOut }));
        list.Add(Op("filter_friendly", "Только союзники", "Цели",
            "Оставляет существ, дружественных фракции владельца.", new[] { ListIn }, new[] { ListOut }));
        list.Add(Op("filter_hostile", "Только враги", "Цели",
            "Оставляет существ, враждебных фракции владельца.", new[] { ListIn }, new[] { ListOut }));
        list.Add(Op("list_contains", "Содержит", "Цели", "Есть ли сущность в списке.",
            new[] { ListIn, EntityIn }, new[] { BoolOut }));
    }

    private static void AddPhysical(List<EffectOperationInfo> list)
    {
        AddDamage(list, "burn", "Горение", "Воздействие", "Наносит термический урон.");
        AddDamage(list, "cold_damage", "Холод", "Воздействие", "Наносит урон холодом.");
        AddDamage(list, "shock", "Электрошок", "Воздействие", "Наносит электрический урон.");
        AddDamage(list, "cut", "Порез", "Воздействие", "Наносит режущий урон.");
        AddDamage(list, "pierce", "Прокол", "Воздействие", "Наносит колющий урон.");
        AddDamage(list, "blunt", "Удар", "Воздействие", "Наносит ушибающий урон.");
        AddDamage(list, "corrosive", "Коррозия", "Воздействие", "Наносит химический урон.");
        AddDamage(list, "poison_damage", "Яд", "Биология", "Наносит урон ядом.");
        AddDamage(list, "cellular_damage", "Клеточный урон", "Биология", "Повреждает клетки.");
        AddDamage(list, "asphyxiation_damage", "Удушье", "Биология", "Наносит урон удушьем.");
        AddDamage(list, "bloodloss_damage", "Кровопотеря", "Биология", "Вызывает кровопотерю.");
        AddDamage(list, "genetic_damage", "Генетический урон", "Биология", "Повреждает генетику.");
        AddDamage(list, "radiation_damage", "Радиационный урон", "Энергия", "Облучает цель.");
        list.Add(Op("heal_all", "Комплексное лечение", "Биология", "Восстанавливает здоровье цели.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut }, handler: "heal"));
        list.Add(Op("repair", "Ремонт", "Воздействие", "Восстанавливает структуру цели.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut }, handler: "heal"));
        list.Add(Op("break_structure", "Разрушить структуру", "Воздействие", "Наносит структурный урон.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut }, handler: "damage"));
        list.Add(Op("throw", "Бросить", "Воздействие", "Придаёт цели импульс от кристалла.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut }, handler: "impulse"));
        list.Add(Op("launch", "Запустить", "Воздействие", "Задаёт цели скорость от кристалла.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut }, handler: "set_velocity"));
    }

    private static void AddDamage(List<EffectOperationInfo> list, string id, string label,
        string category, string description)
    {
        list.Add(Op(id, label, category, description,
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut }));
    }

    private static void AddEnergy(List<EffectOperationInfo> list)
    {
        list.Add(Op("charge_battery", "Зарядить батарею", "Энергия", "Передаёт энергию батарее цели.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut }, handler: "transfer_energy"));
        list.Add(Op("discharge_battery", "Разрядить батарею", "Энергия", "Забирает энергию из батареи.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut }));
        list.Add(Op("battery_level", "Заряд батареи", "Энергия", "Текущий заряд батареи цели.",
            new[] { EntityIn }, new[] { NumberOut }));
        list.Add(Op("battery_max", "Ёмкость батареи", "Энергия", "Максимальный заряд батареи цели.",
            new[] { EntityIn }, new[] { NumberOut }));
        list.Add(Op("emp", "ЭМИ", "Энергия", "Разряжает батарею и наносит электроурон.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut }));
        list.Add(Op("absorb_radiation", "Поглотить радиацию", "Энергия", "Снимает радиационное повреждение.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut }));
        list.Add(Op("absorb_radiation_area", "Поглотить радиацию в области", "Энергия",
            "Снимает всё радиационное поражение со всех целей в списке.",
            new[] { ListIn, AmountIn, TriggerIn }, new[] { BoolOut }));
        list.Add(Op("overcharge", "Перезарядка", "Энергия", "Переполняет батарею с риском перегрева.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut }));
        list.Add(Op("power_output", "Мощность", "Энергия", "Текущая мощность батареи.",
            new[] { EntityIn }, new[] { NumberOut }, handler: "battery_level"));
    }

    private static void AddBiology(List<EffectOperationInfo> list)
    {
        AddDamage(list, "poison", "Отравить", "Биология", "Вводит яд.");
        AddDamage(list, "cure_poison", "Вылечить яд", "Биология", "Нейтрализует яд (отрицательный урон).");
        AddDamage(list, "bleed", "Вызвать кровотечение", "Биология", "Вызывает кровопотерю.");
        AddDamage(list, "stop_bleeding", "Остановить кровотечение", "Биология", "Останавливает кровопотерю.");
        AddDamage(list, "asphyxiate", "Удушить", "Биология", "Вызывает удушье.");
        AddDamage(list, "oxygenate", "Насытить кислородом", "Биология", "Снимает удушье.");
        list.Add(Op("revive", "Оживить", "Биология", "Переводит цель в живое состояние.",
            new[] { EntityIn, TriggerIn }, new[] { BoolOut }));
        list.Add(Op("kill", "Убить", "Биология", "Переводит цель в мёртвое состояние.",
            new[] { EntityIn, TriggerIn }, new[] { BoolOut }));
    }

    private static void AddInformation(List<EffectOperationInfo> list)
    {
        list.Add(Op("ping", "Пинг", "Информация", "Отмечает цели у владельца.",
            new[] { ListIn, TriggerIn }, new[] { BoolOut }, handler: "markers"));
        list.Add(Op("scan", "Сканирование", "Информация", "Отмечает цели у владельца.",
            new[] { ListIn, TriggerIn }, new[] { BoolOut }, handler: "markers"));
        list.Add(Op("reveal", "Раскрыть", "Информация", "Показывает цели сквозь стены.",
            new[] { ListIn, TriggerIn }, new[] { BoolOut }, handler: "markers"));
        list.Add(Op("track", "Отслеживать", "Информация", "Отправляет координаты целей.",
            new[] { ListIn, TriggerIn }, new[] { BoolOut }, handler: "markers"));
        list.Add(Op("distress", "Сигнал бедствия", "Информация", "Отмечает цели и предупреждает владельца.",
            new[] { ListIn, TriggerIn }, new[] { BoolOut }, handler: "markers"));
        list.Add(Op("notify", "Уведомить", "Информация", "Показывает сообщение владельцу.",
            new[] { EntityIn, TriggerIn }, new[] { BoolOut }));
        list.Add(Op("alarm", "Тревога", "Информация", "Включает аварийный свет на цели.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut }));
        list.Add(Op("beacon", "Маяк", "Информация", "Создаёт световой маяк на цели.",
            new[] { EntityIn, AmountIn, TriggerIn }, new[] { BoolOut }, handler: "light"));
    }

    private static void AddMemory(List<EffectOperationInfo> list)
    {
        list.Add(Op("memory_set", "Записать в память", "Память", "Сохраняет значение в памяти узла.",
            new[] { NumberA }, new[] { NumberOut }));
        list.Add(Op("memory_get", "Прочитать память", "Память", "Возвращает сохранённое значение.",
            Array.Empty<EffectPortInfo>(), new[] { NumberOut }));
        list.Add(Op("memory_add", "Прибавить к памяти", "Память", "Добавляет A к сохранённому значению.",
            new[] { NumberA }, new[] { NumberOut }));
        list.Add(Op("memory_subtract", "Вычесть из памяти", "Память", "Вычитает A из сохранённого значения.",
            new[] { NumberA }, new[] { NumberOut }));
        list.Add(Op("counter", "Счётчик памяти", "Память", "Увеличивает сохранённое значение на 1.",
            Array.Empty<EffectPortInfo>(), new[] { NumberOut }));
        list.Add(Op("latch", "Защёлка", "Память", "Запоминает истину до сброса.",
            new[] { TriggerIn }, new[] { BoolOut }));
        list.Add(Op("toggle", "Переключатель", "Память", "Переключает состояние по фронту.",
            new[] { TriggerIn }, new[] { BoolOut }));
        list.Add(Op("edge", "Фронт", "Память", "Истина только в момент включения сигнала.",
            new[] { TriggerIn }, new[] { BoolOut }));
    }

    private static readonly Dictionary<string, EffectOperationInfo> ById = BuildIndex();

    private static Dictionary<string, EffectOperationInfo> BuildIndex()
    {
        var index = new Dictionary<string, EffectOperationInfo>(StringComparer.Ordinal);
        foreach (var operation in All)
        {
            if (!index.TryAdd(operation.Id, operation))
                throw new InvalidOperationException($"Duplicate effect operation id: {operation.Id}");
        }

        return index;
    }

    public static EffectOperationInfo? Get(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        return ById.GetValueOrDefault(id);
    }

    public static IEnumerable<IGrouping<string, EffectOperationInfo>> Grouped()
    {
        return All.GroupBy(operation => operation.Category);
    }

    private static EffectOperationInfo Op(
        string id,
        string label,
        string category,
        string description,
        EffectPortInfo[] inputs,
        EffectPortInfo[] outputs,
        EffectParameterInfo[]? parameters = null,
        string? handler = null)
    {
        return new EffectOperationInfo(id, label, category, description, inputs, outputs, parameters, handler);
    }

    private static EffectOperationInfo OpBinary(
        string id,
        string label,
        string category,
        string description,
        EffectValueType output = EffectValueType.Number)
    {
        return Op(id, label, category, description, new[] { NumberA, NumberB },
            new[] { new EffectPortInfo("value", "Результат", output) });
    }

    private static EffectOperationInfo OpUnary(string id, string label, string category, string description)
    {
        return Op(id, label, category, description, new[] { NumberA }, new[] { NumberOut });
    }
}

/// <summary>
/// Pure graph validation shared by the client editor and the server. It never executes a graph.
/// </summary>
public static class EffectGraphValidator
{
    public static bool Validate(EffectGraph? graph, out List<string> errors)
    {
        errors = new List<string>();

        if (graph == null)
        {
            errors.Add("Формула отсутствует.");
            return false;
        }

        if (graph.Nodes == null || graph.Connections == null)
        {
            errors.Add("Формула повреждена: отсутствует список узлов или связей.");
            return false;
        }

        if (graph.Nodes.Any(node => node == null) || graph.Connections.Any(connection => connection == null))
        {
            errors.Add("Формула повреждена: список содержит пустую запись.");
            return false;
        }

        if (graph.Version <= 0 || graph.Version > EffectGraph.CurrentVersion)
            errors.Add($"Неподдерживаемая версия формулы: {graph.Version}.");

        if (graph.Nodes.Count == 0)
        {
            errors.Add("Формула пуста: добавьте хотя бы одну операцию.");
            return false;
        }

        if (graph.Nodes.Count > EffectOperationCatalog.MaxNodes)
        {
            errors.Add($"Слишком много узлов: {graph.Nodes.Count} из {EffectOperationCatalog.MaxNodes}.");
            return false;
        }

        if (graph.Connections.Count > EffectOperationCatalog.MaxConnections)
        {
            errors.Add($"Слишком много связей: {graph.Connections.Count} из {EffectOperationCatalog.MaxConnections}.");
            return false;
        }

        var nodes = new Dictionary<int, EffectGraphNode>();
        var duplicateNodeIds = false;
        foreach (var node in graph.Nodes)
        {
            if (!nodes.TryAdd(node.Id, node))
            {
                duplicateNodeIds = true;
                errors.Add($"Повторяющийся идентификатор узла {node.Id}.");
            }

            if (EffectOperationCatalog.Get(node.Operation) == null)
                errors.Add($"Неизвестная операция «{node.Operation}».");

            if (node.Parameters == null)
            {
                errors.Add($"Узел {node.Id}: параметры повреждены.");
                continue;
            }

            foreach (var parameter in node.Parameters)
            {
                var info = EffectOperationCatalog.Get(node.Operation)?.Parameters.FirstOrDefault(p => p.Id == parameter.Key);
                if (info == null)
                    errors.Add($"Узел {node.Id}: неизвестный параметр «{parameter.Key}».");
                else if (!float.IsFinite(parameter.Value) ||
                         parameter.Value < info.Min || parameter.Value > info.Max)
                    errors.Add($"Узел {node.Id}: «{info.Label}» вне диапазона {info.Min}..{info.Max}.");
            }
        }

        // The topology passes below index nodes by id; duplicate ids would throw instead of
        // returning the validation error we just collected.
        if (duplicateNodeIds)
            return false;

        var usedInputs = new HashSet<string>(StringComparer.Ordinal);
        var adjacency = new Dictionary<int, List<int>>();
        foreach (var node in graph.Nodes)
            adjacency[node.Id] = new List<int>();

        foreach (var connection in graph.Connections)
        {
            if (!nodes.TryGetValue(connection.FromNode, out var from) ||
                !nodes.TryGetValue(connection.ToNode, out var to))
            {
                errors.Add($"Связь {connection.FromNode} → {connection.ToNode} ссылается на удалённый узел.");
                continue;
            }

            var fromDefinition = EffectOperationCatalog.Get(from.Operation);
            var toDefinition = EffectOperationCatalog.Get(to.Operation);
            if (fromDefinition == null || toDefinition == null)
                continue;

            var fromPort = fromDefinition.FindOutput(connection.FromPort);
            var toPort = toDefinition.FindInput(connection.ToPort);
            if (fromPort == null)
            {
                errors.Add($"Узел {from.Id}: нет выхода «{connection.FromPort}».");
                continue;
            }

            if (toPort == null)
            {
                errors.Add($"Узел {to.Id}: нет входа «{connection.ToPort}».");
                continue;
            }

            if (fromPort.Type != toPort.Type)
            {
                errors.Add($"Связь {from.Id}.{fromPort.Label} → {to.Id}.{toPort.Label}: несовместимые типы.");
                continue;
            }

            var inputKey = $"{to.Id}:{toPort.Id}";
            if (!usedInputs.Add(inputKey))
                errors.Add($"Узел {to.Id}: вход «{toPort.Label}» подключён несколько раз.");

            adjacency[from.Id].Add(to.Id);
        }

        foreach (var node in graph.Nodes)
        {
            var definition = EffectOperationCatalog.Get(node.Operation);
            if (definition == null)
                continue;

            foreach (var input in definition.Inputs.Where(input => input.Required))
            {
                if (!usedInputs.Contains($"{node.Id}:{input.Id}"))
                    errors.Add($"Узел {node.Id}: обязательный вход «{input.Label}» не подключён.");
            }
        }

        // Kahn's algorithm both detects cycles and gives us a safe execution order.
        var indegree = graph.Nodes.ToDictionary(node => node.Id, _ => 0);
        foreach (var (_, targets) in adjacency)
        {
            foreach (var target in targets)
                indegree[target]++;
        }

        var queue = new Queue<int>(indegree.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        var visited = 0;
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            visited++;

            foreach (var target in adjacency[id])
            {
                if (--indegree[target] == 0)
                    queue.Enqueue(target);
            }
        }

        if (visited != graph.Nodes.Count)
            errors.Add("В формуле обнаружен цикл: вычисление невозможно.");

        if (visited == graph.Nodes.Count && graph.Nodes.Count > 1 && GetDepth(graph) > EffectOperationCatalog.MaxDepth)
            errors.Add($"Слишком большая глубина вычислений (максимум {EffectOperationCatalog.MaxDepth}).");

        return errors.Count == 0;
    }

    private static int GetDepth(EffectGraph graph)
    {
        var incoming = graph.Connections
            .GroupBy(connection => connection.ToNode)
            .ToDictionary(group => group.Key, group => group.Select(connection => connection.FromNode).ToList());

        var memo = new Dictionary<int, int>();

        int Visit(int id)
        {
            if (memo.TryGetValue(id, out var cached))
                return cached;

            var depth = 1;
            if (incoming.TryGetValue(id, out var parents))
            {
                foreach (var parent in parents)
                    depth = Math.Max(depth, Visit(parent) + 1);
            }

            memo[id] = depth;
            return depth;
        }

        return graph.Nodes.Count == 0 ? 0 : graph.Nodes.Max(node => Visit(node.Id));
    }
}
