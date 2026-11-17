# Пиксельный шрифт для Mini Station

## Статус

В папке лежит **Pixeloid Sans** (OFL, кириллица, латиница, греческий) — зеркало с GitHub releases.

## Какой шрифт скачать

Рекомендуется **Pixeloid Sans** (OFL, есть кириллица, латиница, греческий):

- Зеркало (прямые `.ttf`): https://github.com/ChewKeanHo/visuals-fonts-pixeloid/releases/tag/v1.0.0  
  Скачайте `Pixeloid.Sans.ttf`, `Pixeloid.Sans-Bold.ttf`, `Pixeloid.Mono.ttf` и переименуйте (см. таблицу ниже).
- Официально: https://ggbot.itch.io/pixeloid-font — кнопка **Download**, архив `Pixeloid_Font_1_0.zip`
- Зеркало: https://www.dafont.com/pixeloid-sans.font — файлы `PixeloidSans.ttf` и `PixeloidSans-Bold.ttf`

Альтернатива с кириллицей: **GohuFont** — https://github.com/koema/gohufont (папка `font-ttf`, файл `GohuFontuni14.ttf`).  
Если используете Gohu, переименуйте файлы в `PixeloidSans.ttf` / `PixeloidSans-Bold.ttf` или поменяйте пути в `Content.Client/Resources/MiniFonts.cs`.

## Куда положить файлы

Скопируйте из архива в **эту папку** (`Resources/Fonts/_Mini/Pixeloid/`):

| Файл из архива      | Имя в репозитории        |
|---------------------|--------------------------|
| `PixeloidSans.ttf`  | `PixeloidSans.ttf`       |
| `PixeloidSans-Bold.ttf` | `PixeloidSans-Bold.ttf` |
| `PixeloidMono.ttf`  | `PixeloidMono.ttf` (опционально, для «моноширинного» UI) |

Также положите сюда `OFL.txt` из архива (лицензия).

Пути в игре: `/Fonts/_Mini/Pixeloid/PixeloidSans.ttf` и т.д.

## После установки

Пересоберите клиент. Весь UI использует `MiniFonts` в `Content.Client/Resources/MiniFonts.cs`.  
Для эмодзи и редких символов остаётся запасной Noto Symbols (уже есть в сборке).
