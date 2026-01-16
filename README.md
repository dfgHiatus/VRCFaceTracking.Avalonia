
<img width="1212" alt="eye_candy" src="https://github.com/user-attachments/assets/b74e44ed-1015-4b33-868b-7c69de717ccb" />

# VRCFaceTracking.Avalonia

VRCFaceTracking.Avalonia is a cross-platform version of [VRCFaceTracking](https://github.com/benaclejames/VRCFaceTracking) that can be run on Windows, macOS and Linux. It strives to be as feature complete and similar to the original app as possible.

## Installation
### Windows
Head to the releases section and [download the latest installer](https://github.com/dfgHiatus/VRCFaceTracking.Avalonia/releases/latest). 

You may be prompted to download the .NET runtime for desktop apps, install it if needs be.

### Linux
Head to the releases section and [download the latest appimage](https://github.com/dfgHiatus/VRCFaceTracking.Avalonia/releases/latest). 

You may also be prompted to download the .NET runtime for desktop apps here too.

*Some Linux users have reported that toggling OSC on and off can soft-crash the application. If this effects you, use [this version](https://github.com/dfgHiatus/VRCFaceTracking.Avalonia/releases/tag/v1.0.0.0).*

### macOS (and alt. Linux method)
Install `git` and a version of the `dotnet-sdk` (.NET 8, 8.0.404 or higher).

Then copy, paste, and run the following script in a terminal of your choice:

```bash
bash -c "$(curl -fsSL https://gist.githubusercontent.com/dfgHiatus/7b3d87100536db3a74914517d770cc7b/raw/db69ad092e43023b24fd88482e350313adc7c33b/vrcft-installer-latest.sh)"
```

Once it finishes installing, you can update and run the VRCFaceTracking.Avalonia app by typing `vrcft` into your terminal.

## Module Compatibility

While VRCFaceTracking.Avalonia can be run on Windows, macOS and Linux, as it currently stands modules (on the VRCFaceTracking Module Registry) are compiled with the intention of being run on Windows. The below chart outlines what modules are comptatible with what operating systems:

*For inaccuracies with this table, please open an issue.*

| Module Name                     | Windows Compatibility | macOS Compatibility    | Linux Compatibility | Author(s)               | Version | Last Updated | Module Description                                                                                                      |
|---------------------------------|-----------------------|------------------------|---------------------|-------------------------|---------|--------------|-------------------------------------------------------------------------------------------------------------------------|
| ETVR Tracking Module            | ✅                     | ✅                    | ✅                 | Lorow with ETVR Team    | 0.0.5   | 2024-02-19   | The module translates the eye tracking data from ETVR to VRCFT and vice versa.                                          |
| SteamLink VRCFT Module          | ✅                     | ❌                    | ❌                 | Ykeara                  | 1.0.6   | 2023-12-17   | VRCFT eye and face tracking module for SteamLink on Meta Quest devices.                                                 |
| cympleFaceTracking              | ✅                     | ❌                    | ❌                 | Dominocs                | 1.0     | 2024-03-10   | VRCFaceTracking module for project Cymple.                                                                              |
| ALVR Module                     | ✅                     | ✅                    | ✅                 | zarik5                  | 1.2.0   | 2024-01-26   | VRCFaceTracking module for ALVR support.                                                                                |
| LiveLink                        | ✅                     | ⚠️                    | ✅                 | Dazbme and tkya         | 1.1.0   | 2023-07-24   | This module allows Perfect Sync (ARKit) data streamed from the Unreal LiveLink iOS app to be used with VRCFaceTracking. |
| ALXR Local Module               | ✅                     | ✅                    | ✅                 | korejan                 | 1.5.0   | 2025-02-25   | Provides eye and/or facial tracking for PC OpenXR runtimes via libalxr.                                                 |
| Meta Quest Link Module          | ✅                     | ❌                    | ❌                 | TofuLemon, azmidi       | 1.4.0   | 2025-02-08   | Provides face tracking for the Quest Pro over Link/AirLink for use with VRCFT.                                          |
| Virtual Desktop                 | ✅                     | ❌                    | ❌                 | Virtual Desktop, Inc.   | 1.3     | 2024-01-30   | Provides face and eye tracking for the Quest Pro when using Virtual Desktop.                                            |
| VRCFT-Babble                    | ✅                     | ✅                    | ✅                 | dfgHiatus               | 2.1.1   | 2023-10-22   | Project Babble face tracking for VRCFaceTracking v5.                                                                    |
| Pico4SAFTExtTrackingModule      | ✅                     | ⚠️                    | ⚠️️                 | miranda1000, azmidi     | 1.5.1   | 2023-05-05   | Provides eye and face tracking compatibility with the Pico Streaming Assistant.                                         |
| ALXR Remote Module              | ✅                     | ✅                    | ✅                 | korejan                 | 1.3.2   | 2023-12-14   | Provides OpenXR eye and/or facial tracking for standalone & wired headsets via remote ALXR clients.                     |
| SRanipalTrackingModule          | ✅                     | ❌                    | ❌                 | VRCFT Team              | 1.5     | 2024-02-11   | Provides gaze tracking data for SRanipal to interact with VRCFT.                                                        |
| VRCFTPimaxModule                | ✅                     | ❌                    | ❌                 | dfgHiatus               | 1.0     | 2023-04-20   | Droolon Pi 1 eye tracking for VRCFaceTracking v5.                                                                       |
| VRCFTVarjoModule                | ✅                     | ❌                    | ❌                 | Chickenbread & M3gagluk | 4.10    | 2023-04-27   | Provides eye tracking data for Varjo to interact with VRCFT.                                                            |
| ViveStreamingFaceTrackingModule | ✅                     | ❌                    | ❌                 | HTC Corp.               | 1.7     | 2024-11-12   | Streams facial tracking data from Vive Focus 3 and Vive XR Elite to VRCFT's Unified Expressions.                        |
| VRCFTOmniceptModule             | ✅                     | ❌                    | ❌                 | 200Tigersbloxed         | 1.4.0   | 2023-05-23   | Implements support for the HP Reverb G2 Omnicept Eye Tracking with their Glia SDK.                                      |
| MeowFaceTrackingModule          | ✅                     | ✅                    | ✅                 | azmidi                  | 1.3     | 2023-04-20   | Use the MeowFace Android app with VRCFaceTracking!                                                                      |
| iFacialMocap Tracking Module    | ✅                     | ⚠️                    | ✅                 | Shuisho                 | 1.0.5   | 2024-03-20   | Provides face tracking data from iFacialMocap for use with VRCFT.                                                      |

Where:

| ✅          | ⚠️            | ❌              |
|------------|---------------|----------------|
| Compatible | Needs Testing | Not Compatible |

## Building

If you want to build from source, clone this repo and open the `.sln`/`.csproj` files in an editor of your choice. I have only tested and built this on Visual Studio 2022 and Rider 2024.3.5, but it should work with other IDEs.

## Credits

- [benaclejames's](https://github.com/benaclejames) [VRCFaceTracking](https://github.com/benaclejames/VRCFaceTracking)
- [MammaMiaDev's](https://github.com/MammaMiaDev) [avaloniaui-the-series](https://github.com/MammaMiaDev/avaloniaui-the-series)
