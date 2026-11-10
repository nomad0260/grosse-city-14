using Content.Client.Changelog;
using Content.Client.UserInterface.Systems.EscapeMenu;
using Content.Client.UserInterface.Systems.Guidebook;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Collections;
using Robust.Shared.Configuration;

namespace Content.Client.Info
{
    public sealed class LinkBanner : BoxContainer
    {
        private readonly IConfigurationManager _cfg;

        private ValueList<(CVarDef<string> cVar, Button button)> _infoLinks;

        private readonly GridContainer _buttons;

        public LinkBanner()
        {
            _buttons = new GridContainer
            {
                Columns = 3,
                HSeparationOverride = 4,
                VSeparationOverride = 4,
                HorizontalExpand = true,
            };
            AddChild(_buttons);

            var uriOpener = IoCManager.Resolve<IUriOpener>();
            _cfg = IoCManager.Resolve<IConfigurationManager>();

            var rulesButton = new Button() {Text = Loc.GetString("server-info-rules-button"), HorizontalExpand = true};
            rulesButton.OnPressed += args => new RulesAndInfoWindow().Open();
            _buttons.AddChild(rulesButton);

            AddInfoButton("server-info-discord-button", CCVars.InfoLinksDiscord);
            AddInfoButton("server-info-website-button", CCVars.InfoLinksWebsite);
            AddInfoButton("server-info-wiki-button", CCVars.InfoLinksWiki);
            AddInfoButton("server-info-forum-button", CCVars.InfoLinksForum);
            AddInfoButton("server-info-telegram-button", CCVars.InfoLinksTelegram);

            var guidebookController = UserInterfaceManager.GetUIController<GuidebookUIController>();
            var guidebookButton = new Button { Text = Loc.GetString("server-info-guidebook-button"), HorizontalExpand = true };
            guidebookButton.OnPressed += _ =>
            {
                guidebookController.ToggleGuidebook();
            };
            _buttons.AddChild(guidebookButton);

            var changelogButton = new ChangelogButton { HorizontalExpand = true };
            changelogButton.OnPressed += args => UserInterfaceManager.GetUIController<ChangelogUIController>().ToggleWindow();
            _buttons.AddChild(changelogButton);

            void AddInfoButton(string loc, CVarDef<string> cVar)
            {
                var button = new Button { Text = Loc.GetString(loc), HorizontalExpand = true };
                button.OnPressed += _ => uriOpener.OpenUri(_cfg.GetCVar(cVar));
                _buttons.AddChild(button);
                _infoLinks.Add((cVar, button));
            }
        }

        /// <summary>
        /// Перестраивает ссылки в заданное число колонок, чтобы подписи не вылезали за край.
        /// </summary>
        public void SetCompact(int columns)
        {
            _buttons.Columns = Math.Clamp(columns, 1, 3);
        }

        protected override void EnteredTree()
        {
            // LinkBanner is constructed before the client even connects to the server due to UI refactor stuff.
            // We need to update these buttons when the UI is shown.

            base.EnteredTree();

            foreach (var (cVar, link) in _infoLinks)
            {
                link.Visible = _cfg.GetCVar(cVar) != "";
            }
        }
    }
}
