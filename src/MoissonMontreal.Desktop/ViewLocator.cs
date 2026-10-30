using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using MoissonMontreal.Desktop.ViewModels;

namespace MoissonMontreal.Desktop;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    /// <summary>
    /// Creates the view that matches a ViewModel (for example <c>HomeViewModel</c> → <c>HomeView</c>).
    /// </summary>
    /// <param name="param">The ViewModel to show.</param>
    /// <returns>The matching view, a "Not Found" text if there is none, or <c>null</c> if <paramref name="param"/> is <c>null</c>.</returns>
    public Control? Build(object? param)
    {
        if (param is null)
            return null;
        
        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }
        
        return new TextBlock { Text = "Not Found: " + name };
    }

    /// <summary>
    /// Tells Avalonia whether this locator can show the given object.
    /// </summary>
    /// <param name="data">The object to show.</param>
    /// <returns><c>true</c> if the object is a ViewModel.</returns>
    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
