#nullable enable

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using VRCFaceTracking.Core.Models;

namespace VRCFaceTracking.Avalonia.Models;

/// <summary>
/// A tracking module as the front end sees it: upstream metadata plus install state, whether the user
/// enabled it, and its position in the user-defined load order. Upstream VRCFaceTracking models none of these.
/// </summary>
public class ModuleEntry : InstallableTrackingModule, INotifyPropertyChanged
{
    private InstallState _installationState;
    private bool _instantiatable = true;
    private int _order;

    public event PropertyChangedEventHandler? PropertyChanged;

    [JsonIgnore]
    public InstallState InstallationState
    {
        get => _installationState;
        set
        {
            if (SetField(ref _installationState, value))
            {
                OnPropertyChanged(nameof(IsInstalled));
            }
        }
    }

    [JsonIgnore]
    public bool IsInstalled => InstallationState == InstallState.Installed;

    /// <summary>Whether the lib manager should try to initialize this module.</summary>
    public bool Instantiatable
    {
        get => _instantiatable;
        set => SetField(ref _instantiatable, value);
    }

    /// <summary>Position in the load order; lower values are initialized first.</summary>
    public int Order
    {
        get => _order;
        set => SetField(ref _order, value);
    }

    // Order and Instantiatable live in ModuleLoadOrder.json. They're still read from module.json, where older
    // builds stored them, but never written back: upstream's ModuleInstaller owns module.json and rewrites it on update.
#pragma warning disable CA1822 // Newtonsoft only discovers instance ShouldSerialize* methods
    public bool ShouldSerializeOrder() => false;
    public bool ShouldSerializeInstantiatable() => false;

    // Older builds wrote "Local"; upstream renamed it to IsLocal.
    [JsonProperty("Local")]
    private bool LegacyLocal
    {
        get => IsLocal;
        set => IsLocal |= value;
    }

    public bool ShouldSerializeLegacyLocal() => false;
#pragma warning restore CA1822

    /// <summary>Key used to persist load-order preferences. Legacy modules all share Guid.Empty, so fall back to the DLL name.</summary>
    [JsonIgnore]
    public string LoadOrderKey => ModuleId != System.Guid.Empty ? ModuleId.ToString() : $"local:{DllFileName}";

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
