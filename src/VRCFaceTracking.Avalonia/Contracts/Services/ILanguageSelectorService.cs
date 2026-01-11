using System.Threading.Tasks;
using System;
using Avalonia.Styling;

namespace VRCFaceTracking.Contracts.Services;

public interface ILanguageSelectorService
{
    string Language
    {
        get;
    }

    event EventHandler? LanguageChanged;

    Task InitializeAsync();

    Task SetLanguageAsync(string language);

    Task SetRequestedLanguageAsync();
}
