using System;
using System.Reflection;
using System.Runtime.InteropServices;
using VRCFaceTracking.Core.OSC;

namespace VRCFaceTracking.Avalonia.iOS;

/// <summary>
/// fti_osc is statically linked into the app executable on iOS, but Core imports it as
/// <c>[DllImport("fti_osc")]</c>, which would otherwise look for a dylib that doesn't exist.
/// Point those imports at the main program instead.
/// </summary>
internal static class FtiOscResolver
{
    private const string LibraryName = "fti_osc";

    public static void Register() =>
        NativeLibrary.SetDllImportResolver(typeof(fti_osc).Assembly, Resolve);

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath) =>
        libraryName == LibraryName ? NativeLibrary.GetMainProgramHandle() : IntPtr.Zero;
}
