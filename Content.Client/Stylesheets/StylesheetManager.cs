using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Reflection;

namespace Content.Client.Stylesheets
{
    public sealed partial class StylesheetManager : IStylesheetManager
    {
        [Dependency] private ILogManager _logManager = default!;
        [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private IReflectionManager _reflection = default!;

        [Dependency]
        private IResourceCache
            _resCache = default!; // TODO: REMOVE (obsolete; used to construct StyleNano/StyleSpace)

        public Stylesheet SheetNanotrasen { get; private set; } = default!;
        public Stylesheet SheetSystem { get; private set; } = default!;

        [Obsolete("Update to use SheetNanotrasen instead")]
        public Stylesheet SheetNano { get; private set; } = default!;

        [Obsolete("Update to use SheetSystem instead")]
        public Stylesheet SheetSpace { get; private set; } = default!;

        private Dictionary<string, Stylesheet> Stylesheets { get; set; } = default!;

        private bool _initialized;

        public bool TryGetStylesheet(string name, [MaybeNullWhen(false)] out Stylesheet stylesheet)
        {
            return Stylesheets.TryGetValue(name, out stylesheet);
        }

        public HashSet<Type> UnusedSheetlets { get; private set; } = [];

        public void Initialize()
        {
            var sawmill = _logManager.GetSawmill("style");
            sawmill.Debug("Initializing Stylesheets...");
            var sw = Stopwatch.StartNew();

            // add all sheetlets to the hashset
            var tys = _reflection.FindTypesWithAttribute<CommonSheetletAttribute>();
            UnusedSheetlets = [..tys];

            Stylesheets = new Dictionary<string, Stylesheet>();
            SheetNanotrasen = Init(new NanotrasenStylesheet(new BaseStylesheet.NoConfig(), this));
            SheetSystem = Init(new SystemStylesheet(new BaseStylesheet.NoConfig(), this));
            SheetNano = new StyleNano(_resCache).Stylesheet; // TODO: REMOVE (obsolete)
            SheetSpace = new StyleSpace(_resCache).Stylesheet; // TODO: REMOVE (obsolete)

            _userInterfaceManager.Stylesheet = SheetNanotrasen;

            // warn about unused sheetlets
            if (UnusedSheetlets.Count > 0)
            {
                var sheetlets = UnusedSheetlets.AsEnumerable()
                    .Take(5)
                    .Select(t => t.FullName ?? "<could not get FullName>")
                    .ToArray();
                sawmill.Error($"There are unloaded sheetlets: {string.Join(", ", sheetlets)}");
            }

            _initialized = true;
            sawmill.Debug($"Initialized {_styleRuleCount} style rules in {sw.Elapsed}");
        }

        public void ReloadSheets()
        {
            // Nothing to reload while the initial build is still in progress.
            if (!_initialized)
                return;

            var sawmill = _logManager.GetSawmill("style");
            var sw = Stopwatch.StartNew();

            Stylesheets.Clear();
            _styleRuleCount = 0;

            SheetNanotrasen = Init(new NanotrasenStylesheet(new BaseStylesheet.NoConfig(), this));
            SheetSystem = Init(new SystemStylesheet(new BaseStylesheet.NoConfig(), this));

#pragma warning disable CS0618 // Legacy sheets are still exposed through TryGetStylesheet.
            SheetNano = new StyleNano(_resCache).Stylesheet;
            SheetSpace = new StyleSpace(_resCache).Stylesheet;
#pragma warning restore CS0618

            // Assigning a fresh instance makes the UI re-apply styles to the whole tree.
            _userInterfaceManager.Stylesheet = SheetNanotrasen;

            sawmill.Debug($"Reloaded {_styleRuleCount} style rules in {sw.Elapsed}");
        }

        private int _styleRuleCount;

        private Stylesheet Init(BaseStylesheet baseSheet)
        {
            Stylesheets.Add(baseSheet.StylesheetName, baseSheet.Stylesheet);
            _styleRuleCount += baseSheet.Stylesheet.Rules.Count;
            return baseSheet.Stylesheet;
        }
    }
}
