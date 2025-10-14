using System;
using System.Globalization;
using System.Threading.Tasks;
using VRCFaceTracking.Avalonia.Assets;
using VRCFaceTracking.Contracts.Services;
using VRCFaceTracking.Core.Contracts.Services;

namespace VRCFaceTracking.Services;

public class LanguageSelectorService(ILocalSettingsService localSettingsService) : ILanguageSelectorService
{
    public const string DefaultLanguage = "DefaultLanguage";

    private const string SettingsKey = "AppBackgroundRequestedLanguage";

    public string Language { get; set; } = DefaultLanguage;

    public async Task InitializeAsync()
    {
        Language = await LoadLanguageFromSettingsAsync();
        await SetRequestedLanguageAsync();
        await Task.CompletedTask;
    }

    public async Task SetLanguageAsync(string language)
    {
        Language = language;
        await SetRequestedLanguageAsync();
        await SaveLanguageInSettingsAsync(Language);
    }

    public Task SetRequestedLanguageAsync()
    {
        // Map language keys from UI to proper CultureInfo names used by resource files.
        // The UI uses "zh" as the ComboBox item name, but resource file is named "Resources.zh-cn.resx",
        // so request the specific culture "zh-CN". For default, use the system two-letter ISO name.
        var cultureName = Language == DefaultLanguage ? CultureInfo.CurrentCulture.TwoLetterISOLanguageName : Language;
        if (string.Equals(cultureName, "zh", StringComparison.OrdinalIgnoreCase))
        {
            cultureName = "zh-CN";
        }
        Resources.Culture = new CultureInfo(cultureName);
        return Task.CompletedTask;
    }

    private async Task<string> LoadLanguageFromSettingsAsync()
    {
        return await localSettingsService.ReadSettingAsync(SettingsKey, DefaultLanguage);
    }

    private async Task SaveLanguageInSettingsAsync(string langauge)
    {
        await localSettingsService.SaveSettingAsync(SettingsKey, langauge);
    }
}
