{
  # All these inputs need to be kept in sync with the local submodules!!!!!!!!!
  inputs = {
    vrcft = {
      flake = false;
      url = "github:dfgHiatus/VRCFaceTracking/13d051a24743717c5eb9b14b39f149fe82bc1073";
    };

    hyperText = {
      flake = false;
      url = "github:dfgHiatus/HyperText.Avalonia/8c511c818c8936eb575e6fad224279746cd102da";
    };

    desktopNotifications = {
      flake = false;
      url = "github:dfgHiatus/DesktopNotifications/e69ae5bdc3144d47bfdbf47c962954406aeb0c92";
    };
  };
  inputs.nixpkgs.url = "nixpkgs/nixos-unstable";

  outputs = { self, nixpkgs, vrcft, desktopNotifications, hyperText, ... }: let
    systems = ["x86_64-linux" "aarch64-linux" "aarch64-darwin"];
    forAllSystems = nixpkgs.lib.genAttrs systems;
    pkgsFor = system: import nixpkgs {
      inherit system;
    };
  in {
    packages = forAllSystems (system: let
      pkgs = pkgsFor system;
      dotnet = pkgs.dotnetCorePackages.dotnet_8;
    in {
      default = pkgs.buildDotnetModule rec {
        version = "1.1.0.0";
        pname = "vrchatfacetracking";

        buildInputs = with pkgs; [
          pkg-config fontconfig
          xorg.libX11 xorg.libSM xorg.libICE
          (pkgs.callPackage ./nix/simpleosc.nix {})
        ];

        src = ./.;

        postUnpack = ''
          cp -r ${vrcft} $sourceRoot/src/VRCFaceTracking
          cp -r ${hyperText} $sourceRoot/src/HyperText.Avalonia
          cp -r ${desktopNotifications} $sourceRoot/src/DesktopNotifications

          # Fix permissions cause nix is dumb and doesnt set them correctly
          find $sourceRoot/src -type d -exec chmod 755 {} \;
          find $sourceRoot/src -type f -exec chmod 644 {} \;
        '';

        postFixup = ''
          mv $out/bin/VRCFaceTracking.Avalonia.Desktop $out/bin/vrchatfacetracking
        '';

        dotnetSdk = dotnet.sdk;
        # Nuget deps is borked :/
        nugetDeps = ./nix/deps.json;
        dotnetRuntime = dotnet.runtime;
        dotnetInstallFlags = ["--framework net8.0"];
        executables = ["VRCFaceTracking.Avalonia.Desktop"];
        projectFile = "src/VRCFaceTracking.Avalonia.Desktop/VRCFaceTracking.Avalonia.Desktop.csproj";

        meta = with pkgs.lib; {
          license = licenses.asl20;
          platforms = platforms.linux;
          mainProgram = "vrchatfacetracking";
          homepage = "https://github.com/dfgHiatus/VRCFaceTracking.Avalonia";
          description = "A cross-platform VRCFaceTracking GUI made with Avalonia";
        };
      };
    });
    devShells = forAllSystems (system: let
      pkgs = pkgsFor system;
      dotnet_sdk = pkgs.dotnet-sdk_8;
    in {
      default = pkgs.mkShell rec {
        buildInputs = with pkgs; [
          dotnet_sdk fontconfig icu
          xorg.libX11 xorg.libSM xorg.libICE
          (pkgs.callPackage ./nix/simpleosc.nix {})
        ];

        shellHook = ''
          export DOTNET_ROOT="${dotnet_sdk}"
          # Fucks knows why dotnet looks in PATH instead of LD_LIBRARY_PATH
          export PATH="$PATH:${builtins.toString (pkgs.lib.makeLibraryPath buildInputs)}";
          export LD_LIBRARY_PATH="$LD_LIBRARY_PATH:${builtins.toString (pkgs.lib.makeLibraryPath buildInputs)}";
        '';
      };
    });
  };
}
