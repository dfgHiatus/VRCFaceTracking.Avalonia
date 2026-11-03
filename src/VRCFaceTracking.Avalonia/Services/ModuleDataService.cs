using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VRCFaceTracking.Avalonia.Models;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Helpers;
using VRCFaceTracking.Core.Models;

namespace VRCFaceTracking.Core.Services;

public class ModuleDataService : IModuleDataService
{
    private List<ModuleEntry>? _remoteModules;
    private readonly Dictionary<Guid, int> _ratingCache = new();

    private readonly IIdentityService _identityService;
    private readonly ILogger<ModuleDataService> _logger;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _loadOrderLock = new(1, 1);

    private const string BaseUrl = "https://registry.vrcft.io/";

    // Front-end only: upstream VRCFaceTracking initializes modules alphabetically and has no notion of load order.
    private static readonly string LoadOrderFile = Path.Combine(Utils.PersistentDataDirectory, "ModuleLoadOrder.json");

    public ModuleDataService(IIdentityService identityService, ILogger<ModuleDataService> logger)
    {
        _identityService = identityService;
        _logger = logger;
        // The registry is a third-party host. Never let a single request stall the UI for 100s.
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _httpClient.BaseAddress = new Uri(BaseUrl);
    }

    private async Task<IEnumerable<ModuleEntry>> AllModules()
    {
        try
        {
            // This is where we make the actual request to the API at modules and get the list of modules.
            var response = await _httpClient.GetAsync("modules");
            if (!response.IsSuccessStatusCode)
            {
                return new List<ModuleEntry>();
            }

            var content = await response.Content.ReadAsStringAsync();
            return await Json.ToObjectAsync<List<ModuleEntry>>(content);
        }
        catch (Exception e)
        {
            _logger.LogWarning("Exception trying to get modules from module registry: {e}", e.Message);
            return new List<ModuleEntry>();
        }
    }

    public async Task<IEnumerable<InstallableTrackingModule>> GetRemoteModules() => await GetRemoteModuleEntries();

    public async Task<IReadOnlyList<ModuleEntry>> GetRemoteModuleEntries()
    {
        _remoteModules ??= new List<ModuleEntry>(await AllModules());

        return _remoteModules;
    }

    public async Task IncrementDownloadsAsync(TrackingModuleMetadata moduleMetadata)
    {
        // Send a PATCH request to the downloads endpoint with the module ID in the body
        var rating = new RatingObject
        { UserId = _identityService.GetUniqueUserId(), ModuleId = moduleMetadata.ModuleId.ToString() };
        var content = new StringContent(JsonConvert.SerializeObject(rating), Encoding.UTF8, "application/json");
        var response = await _httpClient.PatchAsync("downloads", content);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            return;
        }

        _logger.LogError("Failed to increment downloads for {ModuleId}. Status code: {StatusCode}", moduleMetadata.ModuleId, response.StatusCode);
    }

    private void CheckForLegacyModules()
    {
        if (!Directory.Exists(Utils.CustomLibsDirectory))
        {
            Directory.CreateDirectory(Utils.CustomLibsDirectory);
        }

        var moduleDlls = Directory.GetFiles(Utils.CustomLibsDirectory, "*.dll");

        foreach (var moduleDll in moduleDlls)
        {
            var moduleFolder = Path.Combine(Utils.CustomLibsDirectory, Path.GetFileNameWithoutExtension(moduleDll));
            if (!Directory.Exists(moduleFolder))
            {
                Directory.CreateDirectory(moduleFolder);
            }

            var moduleJsonPath = Path.Combine(moduleFolder, "module.json");
            if (!File.Exists(moduleJsonPath))
            {
                var newMeta = new InstallableTrackingModule
                {
                    DllFileName = Path.GetFileName(moduleDll),
                    ModuleId = Guid.Empty,
                    ModuleName = Path.GetFileNameWithoutExtension(moduleDll),
                    ModuleDescription = "Legacy module",
                    AuthorName = "Local",
                    ModulePageUrl = "file:///" + Path.GetDirectoryName(moduleDll),
                    IsLocal = true,
                };
                var moduleJson = JsonConvert.SerializeObject(newMeta);
                File.WriteAllText(moduleJsonPath, moduleJson);
                continue;
            }
        }
    }

    public async Task<int?> GetMyRatingAsync(TrackingModuleMetadata moduleMetadata)
    {
        if (_ratingCache.TryGetValue(moduleMetadata.ModuleId, out var async))
        {
            _logger.LogDebug("Rating for {ModuleId} was cached as {Rating}", moduleMetadata.ModuleId, async);
            return async;
        }

        var rating = new RatingObject
        { UserId = _identityService.GetUniqueUserId(), ModuleId = moduleMetadata.ModuleId.ToString() };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri("rating", UriKind.Relative),
                Content = new StringContent(JsonConvert.SerializeObject(rating), Encoding.UTF8, "application/json"),
            });
        }
        catch (Exception e)
        {
            // Registry unreachable or slow: a missing rating is not fatal.
            _logger.LogWarning("Could not fetch rating for module {ModuleId}: {Message}", moduleMetadata.ModuleId, e.Message);
            return null;
        }

        // Deserialize the input content but extract the Rating property. Be careful though, we might 404 if the user hasn't rated the module yet.
        if (response.StatusCode != HttpStatusCode.OK)
        {
            _logger.LogDebug("Failed to get user rating for module {ModuleId}", moduleMetadata.ModuleId);
            return null;
        }

        var ratingResponse = await Json.ToObjectAsync<RatingObject>(await response.Content.ReadAsStringAsync());

        _logger.LogDebug("Rating for {ModuleId} was {Rating}. Caching...", moduleMetadata.ModuleId, ratingResponse.Rating);
        _ratingCache[moduleMetadata.ModuleId] = ratingResponse.Rating;
        return ratingResponse.Rating;
    }

    public async Task SetMyRatingAsync(TrackingModuleMetadata moduleMetadata, int rating)
    {
        // Same format as get but we PUT this time
        var ratingObject = new RatingObject { UserId = _identityService.GetUniqueUserId(), ModuleId = moduleMetadata.ModuleId.ToString(), Rating = rating };

        var content = new StringContent(JsonConvert.SerializeObject(ratingObject), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PutAsync("rating", content);
        }
        catch (Exception e)
        {
            // Ratings are a nicety; never let a network hiccup take the app down.
            _logger.LogWarning("Could not set rating for module {ModuleId}: {Message}", moduleMetadata.ModuleId, e.Message);
            return;
        }

        if (response.StatusCode != HttpStatusCode.OK)
        {
            _logger.LogError("Failed to set rating for {ModuleId} to {Rating}. Status code: {StatusCode}", moduleMetadata.ModuleId, rating, response.StatusCode);
            return;
        }
        _logger.LogDebug("Rating for {ModuleId} was set to {Rating}. Caching...", moduleMetadata.ModuleId, rating);
        _ratingCache[moduleMetadata.ModuleId] = rating;
    }

    public IEnumerable<InstallableTrackingModule> GetInstalledModules() => GetInstalledModuleEntries();

    // Loose DLLs are migrated into module folders by CheckForLegacyModules, so GetInstalledModules already returns them.
    public IEnumerable<InstallableTrackingModule> GetLegacyModules() => [];

    /// <summary>All installed modules, sorted by the user's load order.</summary>
    public IReadOnlyList<ModuleEntry> GetInstalledModuleEntries()
    {
        if (!Directory.Exists(Utils.CustomLibsDirectory))
        {
            Directory.CreateDirectory(Utils.CustomLibsDirectory);
        }

        CheckForLegacyModules();

        // Check each folder in our CustomModulesDir folder and see if it has a module.json file.
        // If it does, deserialize it and add it to the list of installed modules.
        var installedModules = new List<ModuleEntry>();
        var moduleFolders = Directory.GetDirectories(Utils.CustomLibsDirectory);
        foreach (var moduleFolder in moduleFolders)
        {
            var moduleJsonPath = Path.Combine(moduleFolder, "module.json");
            if (!File.Exists(moduleJsonPath))
            {
                continue;
            }

            var moduleJson = File.ReadAllText(moduleJsonPath);
            try
            {
                var module = JsonConvert.DeserializeObject<ModuleEntry>(moduleJson);
                var guidFolderFound = Directory.GetDirectories(Utils.CustomLibsDirectory, module.ModuleId.ToString(), SearchOption.TopDirectoryOnly).Length > 0;
                module.AssemblyLoadPath = guidFolderFound ? Path.Combine(moduleFolder, module.DllFileName) : Path.Combine(Utils.CustomLibsDirectory, module.DllFileName);
                module.InstallationState = InstallState.Installed;
                installedModules.Add(module);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to deserialize module.json for {ModuleFolder}", moduleFolder);
            }
        }

        // Saved preferences win; modules without one keep any order an older build wrote into module.json.
        var preferences = ReadLoadOrder();
        foreach (var module in installedModules)
        {
            if (preferences.TryGetValue(module.LoadOrderKey, out var preference))
            {
                module.Order = preference.Order;
                module.Instantiatable = preference.Enabled;
            }
        }

        return installedModules
            .OrderBy(m => m.Order)
            .ThenBy(m => m.ModuleName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task SaveInstalledModulesDataAsync(IEnumerable<ModuleEntry> modulesToSave)
    {
        var preferences = modulesToSave.ToDictionary(
            m => m.LoadOrderKey,
            m => new ModuleLoadPreference(m.Order, m.Instantiatable));

        await _loadOrderLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(Utils.PersistentDataDirectory);
            await File.WriteAllTextAsync(LoadOrderFile, JsonConvert.SerializeObject(preferences, Formatting.Indented));
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Could not save module load order");
        }
        finally
        {
            _loadOrderLock.Release();
        }
    }

    private Dictionary<string, ModuleLoadPreference> ReadLoadOrder()
    {
        try
        {
            if (File.Exists(LoadOrderFile))
            {
                return JsonConvert.DeserializeObject<Dictionary<string, ModuleLoadPreference>>(File.ReadAllText(LoadOrderFile)) ?? [];
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Could not read module load order, falling back to defaults");
        }

        return [];
    }

    private sealed record ModuleLoadPreference(int Order, bool Enabled);
}
