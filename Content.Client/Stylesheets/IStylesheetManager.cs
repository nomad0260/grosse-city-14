using System.Diagnostics.CodeAnalysis;
using Robust.Client.UserInterface;

namespace Content.Client.Stylesheets;

public interface IStylesheetManager
{
    /// Nanotrasen styles: the default style! Use this for most UIs
    Stylesheet SheetNanotrasen { get; }

    ///
    /// System styles: use this for any admin / debug menus, and any odds and ends (like the changelog for some reason)
    ///
    Stylesheet SheetSystem { get; }


    [Obsolete("Update to use SheetNanotrasen instead")]
    Stylesheet SheetNano { get; }

    [Obsolete("Update to use SheetSystem instead")]
    Stylesheet SheetSpace { get; }

    /// get a stylesheet by name
    public bool TryGetStylesheet(string name, [MaybeNullWhen(false)]  out Stylesheet stylesheet);

    void Initialize();

    /// <summary>
    /// Rebuilds every stylesheet from scratch. Style rules hold concrete <see cref="Robust.Client.Graphics.Font"/>
    /// instances, so anything that changes which fonts are used (the selectable UI font style)
    /// has to rebuild them for the change to reach the rest of the UI.
    /// </summary>
    void ReloadSheets();

    ///
    /// Sheetlets marked with CommonSheetlet that have not satisfied the type constraints of any stylesheet
    ///
    public HashSet<Type> UnusedSheetlets { get; }
}
