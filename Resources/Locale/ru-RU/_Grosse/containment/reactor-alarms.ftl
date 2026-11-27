# РТМ: предупреждения и аварийные оповещения.
# Источники: DarkCoreAlarmSystem.GetMessage, ContainmentConsoleWindow.BuildWarning /
# BuildContainmentWarning / BuildProjectorBreakdown / футер, CoolingBlockCoolantSystem.OnExamined,
# попапы ContainmentStabilizerSystem.
# Числа передаются уже отформатированными (в форке нет NUMBER()).
# Проценты приходят целыми числами 0–100, знак «%» стоит здесь, в тексте.

## Оповещения в чат (DarkCoreAlarmSystem), отправитель — reactor-sender.

reactor-sender = РТМ
reactor-assembly-report = Сборка РТМ: { $summary }

reactor-alarm-overload = РТМ. Перегрузка энергосети. Выработка { $output } кВт, нагрузка { $load } кВт. Требуется снижение нагрузки или повышение мощности ядра.
reactor-alarm-unstable = РТМ. Стабильность { $stability }/100, сдерживание { $integrity }/100. Активных эмиттеров { $active } из { $total }. Требуется проверка патронов, лучей и охлаждения.
reactor-alarm-expanding = РТМ. Расширение ядра { $expansion }/200, стабильность { $stability }/100. До распада установки { $seconds } с. Требуется повышение стабилизации.
reactor-alarm-compressing = РТМ. Сжатие { $compression }, плотность { $density }/200, мощность поля { $beam }% из { $maxBeam }%. До коллапса { $seconds } с. Требуется снижение сжатия или мощности поля.
reactor-alarm-containment-failure = РТМ. Потеря сдерживания. Сдерживание { $integrity }/100. До расплавления { $seconds } с. Персоналу покинуть отсек.
reactor-alarm-generic = РТМ. Параметры реактора вне допуска.

## Плашка предупреждения на консоли (BuildWarning).

reactor-warning-no-core = Нет связи с ядром. Проверьте питание терминала и расстояние до ядра.
reactor-warning-temperature-critical = КРИТИЧЕСКАЯ ТЕМПЕРАТУРА: оборудование разрушается. Немедленно остановите реактор.
reactor-warning-temperature-damage = Температура выше 700 K: эмиттеры и вспомогательные системы получают повреждения.
reactor-warning-temperature-emitters = Температура выше 500 K: эффективность эмиттеров снижена.
reactor-warning-temperature-check = Температура выше 300 K: проверьте охлаждение.
reactor-warning-compression-critical = Критическое сжатие. До коллапса { $seconds } с. Немедленно снизьте интенсивность эмиттеров или мощность поля.
reactor-warning-compression-high = Сжатие выше нормы: { $compression } / { $threshold }. Снизьте эмиттеры или замените Дигамму — коллапса пока нет.
reactor-warning-expanding = Ядро расширяется. Проверьте патроны и поднимите стабилизацию эмиттеров.
reactor-warning-containment-lost = Сдерживание потеряно. Реактор вот-вот расплавится.
reactor-warning-overload = Выработка ниже нагрузки сети. Снизьте нагрузку или поднимите мощность ядра.
reactor-warning-unstable = Стабильность ниже нормы. Проверьте патроны, включение и исправность эмиттеров.
reactor-warning-initialization = Идёт первичная стабилизация ядра.

## Панель сдерживания (BuildContainmentWarning).

reactor-containment-stopped = Реактор остановлен. Сдерживание не требуется.
reactor-containment-critical = Критическое сжатие. Снизьте эмиттеры или замените Дигамму.
reactor-containment-high = Сжатие выше нормы. Снизьте эмиттеры или замените Дигамму.
reactor-containment-low = Сдерживание ниже нормы. Проверьте поле и охлаждение.
reactor-containment-nominal = Сдерживание в норме.

## Подсказки по охлаждению.

reactor-cooling-stopped = Реактор остановлен. Охлаждение не требуется.
reactor-cooling-nominal = Охлаждение справляется с текущей тепловой нагрузкой.
reactor-cooling-insufficient = Охлаждение не успевает отводить тепло. Снизьте интенсивность эмиттеров.

## Экзамайн охладителя (CoolingBlockCoolantSystem.OnExamined).

reactor-cooling-block-empty = Бак пуст: залейте хладагент канистрой.
reactor-cooling-block-mixture = В баке смесь жидкостей: отвод снижен.
reactor-cooling-block-sump-full = Отстойник полон: слейте отработку, блок не охлаждает.
reactor-cooling-block-no-transfer = Блок не отводит тепло: проверьте жидкость, температуру бака и загрязнение.
reactor-cooling-block-pair = Один блок обслуживает один эмиттер — держите его ближе к своему эмиттеру.

## Предупреждения по эмиттерам и патронам (ContainmentStabilizerSystem).

reactor-emitter-cartridge-spent = Патрон выработан. Эмиттер остановлен, оставлен радиоактивный патрон.
reactor-emitter-cartridge-present = В эмиттере уже установлен патрон. Сначала извлеките его.
reactor-emitter-cartridge-depleted = Отработанный патрон не выдаёт луч. Утилизируйте его.
reactor-emitter-cartridge-installed = Установлен { $cartridge }-патрон.
reactor-emitter-cartridge-removed = Патрон извлечён, эмиттер остановлен.
reactor-emitter-repaired = Эмиттер отремонтирован: { $integrity }% исправности.

## Строка пар «эмиттер — охладитель» на консоли.

reactor-console-emitters-missing = Эмиттеры не найдены.
reactor-console-no-pairs = Нет пар «эмиттер — охладитель».
reactor-console-pairs = Пары: { $pairs }
reactor-console-beams-ok = все лучи работают
reactor-console-beams-partial = не все лучи работают

## Лампы состояния в футере консоли.

reactor-footer-core-none = Ядро: Нет связи
reactor-footer-core-active = Ядро: Активно
reactor-footer-core-off = Ядро: Остановлено
reactor-footer-emitters-none = Эмиттеры: Нет связи
reactor-footer-emitters-nominal = Эмиттеры: Норма
reactor-footer-emitters-check = Эмиттеры: Проверка
reactor-footer-cooling-none = Охлаждение: Нет связи
reactor-footer-cooling-nominal = Охлаждение: Норма
reactor-footer-cooling-attention = Охлаждение: Внимание
