using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.UIpatch;

/// <summary>
/// Lobby-only visual language: dark side panels, dossier typography and controls.
/// These live in the theme rather than in a stylesheet local to <c>LobbyGui</c>,
/// so the lobby keeps a consistent Combine-inspired look.
/// </summary>
[CommonSheetlet]
public sealed class LobbySheetlet<T> : Sheetlet<T> where T : PalettedStylesheet
{
    public const string Panel = "LobbyPanel";
    public const string MenuPanel = "PvpMenuPanel";
    public const string Divider = "LobbyDivider";
    public const string Accent = "LobbyAccent";
    public const string ActionDock = "LobbyActionDock";
    public const string TopBar = "LobbyTopBar";
    public const string TopBarDivider = "LobbyTopBarDivider";
    public const string CharacterCard = "LobbyCharacterCard";
    public const string DossierPortrait = "LobbyDossierPortrait";
    public const string DossierHeader = "LobbyDossierHeader";
    public const string DossierCode = "LobbyDossierCode";
    public const string DossierName = "LobbyDossierName";
    public const string DossierMetaLabel = "LobbyDossierMetaLabel";
    public const string DossierMetaValue = "LobbyDossierMetaValue";
    public const string DossierPortraitTop = "LobbyDossierPortraitTop";
    public const string DossierStatus = "LobbyDossierStatus";
    public const string Hint = "LobbyHint";
    public const string ButtonPrimary = "LobbyButtonPrimary";
    public const string ButtonSecondary = "LobbyButtonSecondary";
    public const string ButtonDanger = "LobbyButtonDanger";
    public const string ButtonBottomBar = "LobbyButtonBottomBar";
    public const string TextTitle = "LobbyTextTitle";
    public const string TextSection = "LobbyTextSection";
    public const string TextBody = "LobbyTextBody";
    public const string TextMuted = "LobbyTextMuted";
    public const string TextAccent = "LobbyTextAccent";
    public const string TextTime = "LobbyTextTime";
    public const string TextMode = "LobbyTextMode";
    public const string TextButton = "LobbyTextButton";
    public const string TextReady = "LobbyTextReady";
    public const string TextMono = "LobbyTextMono";
    public const string TopServerName = "LobbyTopServerName";
    public const string TopBarSub = "LobbyTopBarSub";
    public const string TopBarTime = "LobbyTopBarTime";
    public const string Icon = "LobbyIcon";
    public const string TopBarText = "LobbyTopBarText";
    public const string TopBarTextSecondary = "LobbyTopBarTextSecondary";
    public const string TopBarIcon = "LobbyTopBarIcon";
    public const string TopBarIconSmall = "LobbyTopBarIconSmall";
    public const string TabContainer = "LobbyTabContainer";

    // Deployment terminal layout
    public const string HeroTitle = "LobbyHeroTitle";
    public const string HeroSub = "LobbyHeroSub";
    public const string MenuIndex = "LobbyMenuIndex";
    public const string MenuIndexActive = "LobbyMenuIndexActive";
    public const string MenuLabel = "LobbyMenuLabel";
    public const string MenuLabelActive = "LobbyMenuLabelActive";
    public const string DeployHeading = "LobbyDeployHeading";
    public const string DeployRowKey = "LobbyDeployRowKey";
    public const string DeployRowValue = "LobbyDeployRowValue";
    public const string DeployAction = "LobbyDeployAction";
    public const string DeployDesc = "LobbyDeployDesc";
    public const string StatusValue = "LobbyStatusValue";
    public const string NodeStatus = "LobbyNodeStatus";
    public const string MonoSmall = "LobbyMonoSmall";
    public const string ShiftTimer = "LobbyShiftTimer";
    public const string VersionLine = "LobbyVersionLine";
    public const string BuildLine = "LobbyBuildLine";
    public const string PlayersValue = "LobbyPlayersValue";
    public const string PlayersLabel = "LobbyPlayersLabel";
    public const string BottomHint = "LobbyBottomHint";
    public const string Motto = "LobbyMotto";
    public const string StatLabel = "LobbyStatLabel";
    public const string StatValue = "LobbyStatValue";
    public const string FileCode = "LobbyFileCode";
    public const string NodeDot = "LobbyNodeDot";
    public const string PortraitFrame = "LobbyPortraitFrame";
    public const string ObserveText = "LobbyObserveText";
    public const string DeployStatus = "LobbyDeployStatus";
    public const string DeployReason = "LobbyDeployReason";
    public const string SetupHeader = "LobbySetupHeader";
    public const string SetupTitle = "LobbySetupTitle";
    public const string SetupBreadcrumb = "LobbySetupBreadcrumb";

    // Обучение при входе в лобби
    public const string TutorialDim = "LobbyTutorialDim";
    public const string TutorialPanel = "LobbyTutorialPanel";
    public const string TutorialKicker = "LobbyTutorialKicker";
    public const string TutorialTitle = "LobbyTutorialTitle";
    public const string TutorialStep = "LobbyTutorialStep";
    public const string TutorialSection = "LobbyTutorialSection";
    public const string TutorialKey = "LobbyTutorialKey";
    public const string TutorialKeyText = "LobbyTutorialKeyText";
    public const string TutorialQuote = "LobbyTutorialQuote";
    public const string TutorialQuoteText = "LobbyTutorialQuoteText";
    public const string TutorialText = "LobbyTutorialText";
    public const string TutorialLead = "LobbyTutorialLead";
    public const string TutorialRail = "LobbyTutorialRail";
    public const string TutorialRailEdge = "LobbyTutorialRailEdge";
    public const string TutorialNavLabel = "LobbyTutorialNavLabel";
    public const string TutorialNavLabelActive = "LobbyTutorialNavLabelActive";

    // Graphite / Steel / Amber / Alert палитра
    private static readonly Color PanelBg = Color.FromHex("#151817").WithAlpha(0.92f);      // Graphite
    private static readonly Color PanelBgSecondary = Color.FromHex("#1B1E1D").WithAlpha(0.92f);
    private static readonly Color PanelBorder = Color.FromHex("#777970").WithAlpha(0.45f);  // Steel
    private static readonly Color AccentRed = Color.FromHex("#9E3937");                      // Alert
    private static readonly Color AccentAmber = Color.FromHex("#D13239");                    // Primary Red Accent
    private static readonly Color AccentRedGlow = Color.FromHex("#D13239").WithAlpha(0.45f);
    private static readonly Color MenuBg = Color.FromHex("#1D201F").WithAlpha(0.92f);
    private static readonly Color DividerColor = Color.FromHex("#777970").WithAlpha(0.35f);
    private static readonly Color TextPrimary = Color.FromHex("#ECEEEB");
    private static readonly Color TextSecondary = Color.FromHex("#777970");                  // Steel
    private static readonly Color TextDisabled = Color.FromHex("#50534D");
    private static readonly Color ButtonHover = Color.FromHex("#D13239").WithAlpha(0.14f);
    private static readonly Color ButtonPressed = Color.FromHex("#9E3937").WithAlpha(0.25f);
    private static readonly Color ButtonDark = Color.FromHex("#151817").WithAlpha(0.90f);
    private static readonly Color ButtonDarkBorder = Color.FromHex("#777970").WithAlpha(0.45f);

    // Базовые шрифты с кириллицей — для всего, что приходит из локализации
    // или от игрока (имена, роли, сервер, таймер).
    private const string LobbyFontSemiBoldPath = "/Fonts/NotoSans/NotoSans-Bold.ttf";
    private const string LobbyFontBoldPath = "/Fonts/NotoSans/NotoSans-Bold.ttf";
    private const string LobbyMonoPath = "/Fonts/RobotoMono/RobotoMono-Regular.ttf";

    // Техническая типографика лобби (OFL, Resources/Fonts/_GC14): Rajdhani и
    // Share Tech Mono. У них нет кириллицы, поэтому они используются только для
    // статичных английских подписей и цифр.
    private const string LobbyFontDisplayPath = "/Fonts/_GC14/Rajdhani-Bold.ttf";
    private const string LobbyFontTechSemiBoldPath = "/Fonts/_GC14/Rajdhani-SemiBold.ttf";
    private const string LobbyFontTechMediumPath = "/Fonts/_GC14/Rajdhani-Medium.ttf";
    private const string LobbyTechMonoPath = "/Fonts/_GC14/ShareTechMono-Regular.ttf";

    public override StyleRule[] GetRules(T sheet, object config)
    {
        return
        [
            // Основные панели с угловатым дизайном и красными акцентами
            E<PanelContainer>()
                .Class(Panel)
                .Panel(new RoundedStyleBox
                {
                    BackgroundColor = PanelBg,
                    BorderColor = PanelBorder,
                    BorderThickness = 1f,  // Тонкая граница
                    CornerRadius = 2f,     // Минимальное скругление
                    Padding = new Thickness(12f, 10f, 12f, 10f),
                }),
            // Верхняя статусная панель
            E<PanelContainer>()
                .Class(TopBar)
                .Panel(new RoundedStyleBox
                {
                    BackgroundColor = PanelBg.WithAlpha(0.90f),
                    BorderColor = PanelBorder.WithAlpha(0.6f),
                    BorderThickness = 1f, // Тонкая нижняя граница (визуально)
                    CornerRadius = 0f,
                }),
            // Разделитель в верхней панели
            E<PanelContainer>()
                .Class(TopBarDivider)
                .Panel(new StyleBoxFlat(AccentRed.WithAlpha(0.4f))),
            // Карточка персонажа
            E<PanelContainer>()
                .Class(CharacterCard)
                .Panel(new RoundedStyleBox
                {
                    BackgroundColor = PanelBgSecondary,
                    BorderColor = PanelBorder,
                    BorderThickness = 1f,
                    CornerRadius = 2f,
                    Padding = new Thickness(0f),
                }),
            // Досье: портретная рамка
            E<PanelContainer>()
                .Class(DossierPortrait)
                .Panel(new RoundedStyleBox
                {
                    BackgroundColor = Color.FromHex("#0A0B0D"),
                    BorderColor = PanelBorder,
                    BorderThickness = 1f,
                    CornerRadius = 0f,
                    Padding = new Thickness(0f),
                }),
            // Тонкие серые разделители
            E<PanelContainer>()
                .Class(Divider)
                .Panel(new StyleBoxFlat(DividerColor)),
            // Красный акцент - короткая линия под заголовком
            E<PanelContainer>()
                .Class(Accent)
                .Panel(new StyleBoxFlat(AccentRed)),
            // Док-панель действий с красной рамкой
            E<PanelContainer>()
                .Class(ActionDock)
                .Panel(new RoundedStyleBox
                {
                    BackgroundColor = PanelBg,
                    BorderColor = AccentRed,
                    BorderThickness = 2f,  // Яркая красная рамка
                    CornerRadius = 2f,     // Угловатый дизайн
                    Padding = new Thickness(12f, 12f, 12f, 12f),
                }),
            // Подсказка внизу слева
            E<PanelContainer>()
                .Class(Hint)
                .Panel(new RoundedStyleBox
                {
                    BackgroundColor = PanelBg.WithAlpha(0.90f),
                    BorderColor = PanelBorder.WithAlpha(0.7f),
                    BorderThickness = 1f,
                    CornerRadius = 3f,
                    Padding = new Thickness(10f, 6f, 10f, 6f),
                }),
            // Вложенные меню
            E<PanelContainer>()
                .Class(MenuPanel)
                .Panel(new RoundedStyleBox
                {
                    BackgroundColor = MenuBg,
                    BorderColor = PanelBorder,
                    BorderThickness = 1f,
                    CornerRadius = 2f,
                    Padding = new Thickness(10f, 8f, 10f, 8f),
                }),

            // === СТИЛИ ТЕКСТА И ИКОНОК ===

            // Иконки в информационных блоках
            E<Label>()
                .Class(Icon)
                .Font(sheet.BaseFont.GetFont(14)),

            // Верхняя панель: основной текст
            E<Label>()
                .Class(TopBarText)
                .Font(sheet.BaseFont.GetFont(13))
                .FontColor(TextPrimary),

            // Верхняя панель: вторичный текст
            E<Label>()
                .Class(TopBarTextSecondary)
                .Font(sheet.BaseFont.GetFont(12))
                .FontColor(TextSecondary),

            // Верхняя панель: иконка логотипа
            E<Label>()
                .Class(TopBarIcon)
                .Font(sheet.BaseFont.GetFont(18))
                .FontColor(AccentRed),

            // Верхняя панель: маленькая иконка
            E<Label>()
                .Class(TopBarIconSmall)
                .Font(sheet.BaseFont.GetFont(12))
                .FontColor(TextSecondary),

            // === ТЕКСТ ЛОББИ ===
            E<Label>()
                .Class(TextTitle)
                .Font(ResCache.GetFont(LobbyFontSemiBoldPath, 22))
                .FontColor(TextPrimary),
            E<Label>()
                .Class(TextSection)
                .Font(ResCache.GetFont(LobbyFontSemiBoldPath, 15))
                .FontColor(TextPrimary),
            E<Label>()
                .Class(TextBody)
                .Font(sheet.BaseFont.GetFont(13))
                .FontColor(Color.FromHex("#C3C8CE")),
            // Описание режима — RichTextLabel, поэтому нужен отдельный селектор,
            // иначе оно рисуется дефолтным шрифтом темы.
            E<RichTextLabel>()
                .Class(TextBody)
                .Font(sheet.BaseFont.GetFont(13))
                .Prop(Label.StylePropertyFontColor, Color.FromHex("#C3C8CE")),
            E<Label>()
                .Class(TextMuted)
                .Font(sheet.BaseFont.GetFont(12))
                .FontColor(TextSecondary),
            E<Label>()
                .Class(TextAccent)
                .Font(ResCache.GetFont(LobbyFontSemiBoldPath, 12))
                .FontColor(AccentAmber),
            E<Label>()
                .Class(TextTime)
                .Font(ResCache.GetFont(LobbyFontSemiBoldPath, 14))
                .FontColor(AccentRed),
            E<Label>()
                .Class(TextMode)
                .Font(ResCache.GetFont(LobbyFontSemiBoldPath, 15))
                .FontColor(AccentRed),
            E<Label>()
                .Class(TextButton)
                .Font(ResCache.GetFont(LobbyFontSemiBoldPath, 13))
                .FontColor(Color.FromHex("#D7DADE")),
            E<Label>()
                .Class(TextReady)
                .Font(ResCache.GetFont(LobbyFontBoldPath, 13))
                .FontColor(Color.FromHex("#F2F3F5")),
            E<Label>()
                .Class(TopServerName)
                .Font(ResCache.GetFont(LobbyFontSemiBoldPath, 16))
                .FontColor(TextPrimary),
            E<Label>()
                .Class(TopBarSub)
                .Font(sheet.BaseFont.GetFont(12))
                .FontColor(TextSecondary),
            E<Label>()
                .Class(TopBarTime)
                .Font(sheet.BaseFont.GetFont(12))
                .FontColor(TextPrimary),
            // Моноширинный шрифт для кодов и часов: цифры не «прыгают» при смене.
            // Правило идёт после TopBarTime, иначе базовый шрифт перекрывает его.
            E<Label>()
                .Class(TextMono)
                .Font(ResCache.GetFont(LobbyMonoPath, 12)),

            // === DEPLOYMENT TERMINAL ===
            E<Label>()
                .Class(HeroTitle)
                .Font(ResCache.GetFont(LobbyFontDisplayPath, 36))
                .FontColor(TextPrimary),
            E<Label>()
                .Class(HeroSub)
                .Font(ResCache.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", 12))
                .FontColor(TextSecondary),
            E<Label>()
                .Class(MenuIndex)
                .Font(ResCache.GetFont(LobbyTechMonoPath, 26))
                .FontColor(TextSecondary),
            E<Label>()
                .Class(MenuIndexActive)
                .Font(ResCache.GetFont(LobbyTechMonoPath, 26))
                .FontColor(AccentAmber),
            E<Label>()
                .Class(MenuLabel)
                .Font(ResCache.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", 19))
                .FontColor(Color.FromHex("#B5B8B0")),
            E<Label>()
                .Class(MenuLabelActive)
                .Font(ResCache.GetFont(LobbyFontBoldPath, 20))
                .FontColor(Color.FromHex("#F5F6F4")),
            E<Label>()
                .Class(DeployHeading)
                .Font(ResCache.GetFont(LobbyFontBoldPath, 14))
                .FontColor(AccentAmber),
            E<Label>()
                .Class(DeployRowKey)
                .Font(ResCache.GetFont(LobbyMonoPath, 11))
                .FontColor(Color.FromHex("#9FA29B")),
            E<Label>()
                .Class(DeployRowValue)
                .Font(ResCache.GetFont(LobbyMonoPath, 12))
                .FontColor(TextPrimary),
            E<Label>()
                .Class(DeployAction)
                .Font(ResCache.GetFont(LobbyFontBoldPath, 22))
                .FontColor(Color.FromHex("#F5F6F4")),
            E<Label>()
                .Class(DeployDesc)
                .Font(sheet.BaseFont.GetFont(9))
                .FontColor(TextSecondary),
            E<Label>()
                .Class(StatusValue)
                .Font(ResCache.GetFont(LobbyMonoPath, 10))
                .FontColor(Color.FromHex("#C3C8CE")),
            E<Label>()
                .Class(NodeStatus)
                .Font(ResCache.GetFont(LobbyFontBoldPath, 12))
                .FontColor(Color.FromHex("#63C77B")),
            E<Label>()
                .Class(MonoSmall)
                .Font(ResCache.GetFont(LobbyMonoPath, 10))
                .FontColor(Color.FromHex("#8A9098")),
            E<Label>()
                .Class(ShiftTimer)
                .Font(ResCache.GetFont(LobbyMonoPath, 13))
                .FontColor(Color.FromHex("#E8A33D")),
            E<Label>()
                .Class(VersionLine)
                .Font(ResCache.GetFont(LobbyMonoPath, 11))
                .FontColor(Color.FromHex("#7E848B")),
            // Version / build / server lines under the node bar.
            E<Label>()
                .Class(BuildLine)
                .Font(ResCache.GetFont(LobbyMonoPath, 11))
                .FontColor(Color.FromHex("#9BA1A8")),
            E<Label>()
                .Class(PlayersValue)
                .Font(ResCache.GetFont(LobbyTechMonoPath, 14))
                .FontColor(Color.FromHex("#EAEAEA")),
            E<Label>()
                .Class(PlayersLabel)
                .Font(ResCache.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", 13))
                .FontColor(Color.FromHex("#C3C8CE")),
            E<Label>()
                .Class(BottomHint)
                .Font(ResCache.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", 13))
                .FontColor(Color.FromHex("#C2C5BD")),
            E<Label>()
                .Class(Motto)
                .Font(ResCache.GetFont(LobbyFontTechSemiBoldPath, 12))
                .FontColor(Color.FromHex("#7E848B")),
            E<Label>()
                .Class(StatLabel)
                .Font(ResCache.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", 10))
                .FontColor(TextSecondary),
            E<Label>()
                .Class(StatValue)
                .Font(ResCache.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", 13))
                .FontColor(TextPrimary),
            E<Label>()
                .Class(FileCode)
                .Font(ResCache.GetFont(LobbyTechMonoPath, 9))
                .FontColor(TextSecondary),
            E<Label>()
                .Class(ObserveText)
                .Font(ResCache.GetFont(LobbyFontSemiBoldPath, 10))
                .FontColor(Color.FromHex("#969BA2")),
            E<PanelContainer>()
                .Class(NodeDot)
                .Panel(new StyleBoxFlat(Color.FromHex("#63C77B"))),
            // Статус очереди и причина блокировки деплоя под плашкой.
            E<Label>()
                .Class(DeployStatus)
                .Font(ResCache.GetFont(LobbyMonoPath, 10))
                .FontColor(AccentAmber),
            E<Label>()
                .Class(DeployReason)
                .Font(ResCache.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", 11))
                .FontColor(Color.FromHex("#C85A57")),
            // Шапка редактора персонала в языке лобби (полупрозрачная под анимированный фон).
            E<PanelContainer>()
                .Class(SetupHeader)
                .Panel(new RoundedStyleBox
                {
                    BackgroundColor = Color.FromHex("#111413").WithAlpha(0.58f),
                    BorderColor = Color.FromHex("#777970").WithAlpha(0.35f),
                    BorderThickness = 1f,
                    CornerRadius = 0f,
                    Padding = new Thickness(0f),
                }),
            E<Label>()
                .Class(SetupTitle)
                .Font(sheet.BaseFont.GetFont(15))
                .FontColor(TextPrimary),
            E<Label>()
                .Class(SetupBreadcrumb)
                .Font(ResCache.GetFont(LobbyFontDisplayPath, 18))
                .FontColor(AccentAmber),
            E<PanelContainer>()
                .Class(PortraitFrame)
                .Panel(new RoundedStyleBox
                {
                    BackgroundColor = Color.Transparent,
                    BorderColor = Color.Transparent,
                    BorderThickness = 0f,
                    CornerRadius = 0f,
                    Padding = new Thickness(0f),
                }),

            // === ДОСЬЕ ===
            E<Label>()
                .Class(DossierHeader)
                .Font(ResCache.GetFont(LobbyFontSemiBoldPath, 18))
                .FontColor(TextPrimary),
            E<Label>()
                .Class(DossierCode)
                .Font(sheet.BaseFont.GetFont(10))
                .FontColor(TextSecondary),
            E<Label>()
                .Class(DossierName)
                .Font(ResCache.GetFont(LobbyFontBoldPath, 19))
                .FontColor(TextPrimary),
            E<Label>()
                .Class(DossierMetaLabel)
                .Font(ResCache.GetFont(LobbyFontSemiBoldPath, 10))
                .FontColor(Color.FromHex("#7E848B")),
            E<Label>()
                .Class(DossierMetaValue)
                .Font(ResCache.GetFont(LobbyFontSemiBoldPath, 13))
                .FontColor(Color.FromHex("#D7DADE")),
            E<Label>()
                .Class(DossierPortraitTop)
                .Font(ResCache.GetFont(LobbyFontSemiBoldPath, 10))
                .FontColor(TextSecondary),
            E<Label>()
                .Class(DossierStatus)
                .Font(ResCache.GetFont(LobbyFontSemiBoldPath, 10))
                .FontColor(AccentRed),

            // === ОБУЧЕНИЕ В ЛОББИ (Graphite / Steel / Red в стиле основного меню) ===
            E<PanelContainer>()
                .Class(TutorialDim)
                .Panel(new StyleBoxFlat(Color.FromHex("#0A0C0B").WithAlpha(0.84f))),
            E<PanelContainer>()
                .Class(TutorialPanel)
                .Panel(new RoundedStyleBox
                {
                    BackgroundColor = Color.FromHex("#151817").WithAlpha(0.96f),
                    BorderColor = Color.FromHex("#777970").WithAlpha(0.45f),
                    BorderThickness = 1f,
                    CornerRadius = 0f,
                    Padding = new Thickness(0f),
                }),
            E<PanelContainer>()
                .Class(TutorialRail)
                .Panel(new RoundedStyleBox
                {
                    BackgroundColor = Color.FromHex("#111413").WithAlpha(0.98f),
                    BorderColor = Color.Transparent,
                    BorderThickness = 0f,
                    CornerRadius = 0f,
                    Padding = new Thickness(16f, 18f, 14f, 16f),
                }),
            E<PanelContainer>()
                .Class(TutorialRailEdge)
                .Panel(new StyleBoxFlat(Color.FromHex("#777970").WithAlpha(0.35f))),
            E<Label>()
                .Class(TutorialNavLabel)
                .Font(ResCache.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", 12))
                .FontColor(Color.FromHex("#9FA29B")),
            E<Label>()
                .Class(TutorialNavLabelActive)
                .Font(ResCache.GetFont(LobbyFontSemiBoldPath, 12))
                .FontColor(Color.FromHex("#F5F6F4")),
            E<Label>()
                .Class(TutorialKicker)
                .Font(ResCache.GetFont(LobbyMonoPath, 11))
                .FontColor(AccentAmber),
            E<Label>()
                .Class(TutorialTitle)
                .Font(ResCache.GetFont(LobbyFontBoldPath, 20))
                .FontColor(TextPrimary),
            E<Label>()
                .Class(TutorialStep)
                .Font(ResCache.GetFont(LobbyMonoPath, 12))
                .FontColor(AccentAmber),
            E<Label>()
                .Class(TutorialSection)
                .Font(ResCache.GetFont(LobbyFontBoldPath, 12))
                .FontColor(AccentAmber),
            E<PanelContainer>()
                .Class(TutorialKey)
                .Panel(new RoundedStyleBox
                {
                    BackgroundColor = Color.FromHex("#1C201F"),
                    BorderColor = Color.FromHex("#777970").WithAlpha(0.45f),
                    BorderThickness = 1f,
                    CornerRadius = 0f,
                    Padding = new Thickness(8f, 3f, 8f, 3f),
                }),
            E<Label>()
                .Class(TutorialKeyText)
                .Font(ResCache.GetFont(LobbyMonoPath, 11))
                .FontColor(TextPrimary)
                .AlignMode(Label.AlignMode.Center),
            E<PanelContainer>()
                .Class(TutorialQuote)
                .Panel(new RoundedStyleBox
                {
                    BackgroundColor = Color.FromHex("#191C1B").WithAlpha(0.95f),
                    BorderColor = Color.FromHex("#D13239").WithAlpha(0.65f),
                    BorderThickness = 1f,
                    CornerRadius = 0f,
                    Padding = new Thickness(14f, 10f, 14f, 10f),
                }),
            E<RichTextLabel>()
                .Class(TutorialQuoteText)
                .Font(sheet.BaseFont.GetFont(13))
                .Prop(Label.StylePropertyFontColor, Color.FromHex("#E4E6E3")),
            E<RichTextLabel>()
                .Class(TutorialText)
                .Font(sheet.BaseFont.GetFont(13))
                .Prop(Label.StylePropertyFontColor, Color.FromHex("#D0D3CC")),
            E<Label>()
                .Class(TutorialText)
                .Font(sheet.BaseFont.GetFont(13))
                .FontColor(Color.FromHex("#D0D3CC")),
            E<RichTextLabel>()
                .Class(TutorialLead)
                .Font(sheet.BaseFont.GetFont(15))
                .Prop(Label.StylePropertyFontColor, TextPrimary),

            // Подсказки тоже RichTextLabel: они переносятся по ширине окна.
            E<RichTextLabel>()
                .Class(TextMuted)
                .Font(sheet.BaseFont.GetFont(12))
                .Prop(Label.StylePropertyFontColor, TextSecondary),

            // Тёмная кнопка для UICommandButton (кнопки Combine рисуются сами)
            E<ContainerButton>()
                .Class(ButtonSecondary)
                .Box(new RoundedStyleBox
                {
                    BackgroundColor = ButtonDark,
                    BorderColor = ButtonDarkBorder,
                    BorderThickness = 1f,
                    CornerRadius = 0f,
                }),

            // Полупрозрачный TabContainer для редактора персонажа и маркировок
            E<TabContainer>()
                .Class(TabContainer)
                .Prop(Robust.Client.UserInterface.Controls.TabContainer.StylePropertyPanelStyleBox, new StyleBoxFlat
                {
                    BackgroundColor = Color.FromHex("#151817").WithAlpha(0.26f),
                    BorderColor = Color.FromHex("#777970").WithAlpha(0.30f),
                    BorderThickness = new Thickness(1f),
                    ContentMarginLeftOverride = 6,
                    ContentMarginRightOverride = 6,
                    ContentMarginTopOverride = 6,
                    ContentMarginBottomOverride = 6,
                })
                .Prop(Robust.Client.UserInterface.Controls.TabContainer.StylePropertyTabStyleBox, new StyleBoxFlat
                {
                    BackgroundColor = Color.FromHex("#1B1F1E").WithAlpha(0.68f),
                    BorderColor = Color.FromHex("#D13239").WithAlpha(0.90f),
                    BorderThickness = new Thickness(0f, 2f, 0f, 0f),
                    ContentMarginLeftOverride = 12,
                    ContentMarginRightOverride = 12,
                    ContentMarginTopOverride = 6,
                    ContentMarginBottomOverride = 6,
                })
                .Prop(Robust.Client.UserInterface.Controls.TabContainer.StylePropertyTabStyleBoxInactive, new StyleBoxFlat
                {
                    BackgroundColor = Color.FromHex("#121514").WithAlpha(0.38f),
                    BorderColor = Color.FromHex("#777970").WithAlpha(0.24f),
                    BorderThickness = new Thickness(1f, 1f, 1f, 0f),
                    ContentMarginLeftOverride = 12,
                    ContentMarginRightOverride = 12,
                    ContentMarginTopOverride = 6,
                    ContentMarginBottomOverride = 6,
                })
                .Prop(Robust.Client.UserInterface.Controls.TabContainer.stylePropertyTabFontColor, TextPrimary)
                .Prop(Robust.Client.UserInterface.Controls.TabContainer.StylePropertyTabFontColorInactive, TextSecondary),
        ];
    }
}
