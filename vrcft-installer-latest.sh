#!/bin/bash

# Check OS compatibility
if [[ "$OSTYPE" != "linux-gnu"* ]] && [[ "$OSTYPE" != "darwin"* ]]; then
    echo "Error: This script is only compatible with Linux and macOS."
    exit 1
fi

# Minimum required .NET 8 version
min_version="8.0.404"

# Check for .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET is not installed. Please install .NET 8.0.404 or higher."
    exit 1
fi

# Get installed .NET versions and filter for .NET 8 only
installed_versions=$(dotnet --list-sdks | awk '{print $1}' | grep -E '^8\.')

# Check if any .NET 8 version is installed
if [[ -z "$installed_versions" ]]; then
    echo "Error: No .NET 8 version installed. Please install .NET 8.0.404 or higher."
    exit 1
fi

# Get the highest available .NET 8 version
latest_version=$(echo "$installed_versions" | sort -V | tail -n 1)

# Check if the latest installed version is at least the minimum required version
if [[ "$(printf '%s\n%s' "$min_version" "$latest_version" | sort -V | head -n 1)" != "$min_version" ]]; then
    echo "Error: Installed .NET version ($latest_version) is lower than the required ($min_version)."
    exit 1
fi

echo "Using .NET version: $latest_version"

install_dir="$HOME/.local/share/VRCFaceTracking"
bin_dir="$HOME/.local/bin"
build_project="src/VRCFaceTracking.Avalonia.Desktop/VRCFaceTracking.Avalonia.Desktop.csproj"


# Function to get the latest commit hash
get_latest_commit() {
    git fetch origin main
    git rev-parse origin/main
}

# Function to get the current commit hash
get_current_commit() {
    git rev-parse HEAD
}

# Function to check for updates
check_for_updates() {
    echo "Checking for updates..."
    git fetch origin main
    local_commit=$(get_current_commit)
    remote_commit=$(get_latest_commit)

    if [ "$local_commit" != "$remote_commit" ]; then
        echo "New version available"
        echo "Current commit: ${local_commit:0:8}"
        echo "Latest commit: ${remote_commit:0:8}"
        echo "Updating to the latest version..."
        git checkout main
        git pull origin main
        git submodule update --init --recursive
        return 0  # Update available
    else
        echo "VRCFT is already at the latest commit: ${local_commit:0:8}"
        return 1  # No updates
    fi
}

# Detect OS and architecture for build
detect_platform() {
    case "$(uname -s)" in
        Darwin)
            if [[ "$(uname -m)" == "arm64" ]]; then
                echo "osx-arm64"
            else
                echo "osx-64"
            fi
            ;;
        Linux)
            if [[ "$(uname -m)" == "aarch64" ]]; then
                echo "linux-arm64"
            else
                echo "linux-x64"
            fi
            ;;
        *)
            echo "unsupported"
            ;;
    esac
}

# Function to build the project
build_project() {
    platform=$(detect_platform)
    if [[ "$platform" == "unsupported" ]]; then
        echo "Error: Unsupported platform."
        exit 1
    fi

    echo "Building VRCFT for $platform..."
    
    case "$platform" in
        osx-64)
            dotnet publish "$build_project" -r osx-64 -c "MacOS Release" --self-contained -f net8.0
            ;;
        osx-arm64)
            dotnet publish "$build_project" -r osx-arm64 -c "MacOS Release" --self-contained -f net8.0
            ;;
        linux-x64)
            dotnet publish "$build_project" -r linux-x64 -c "Linux Release" --self-contained -f net8.0
            ;;
        linux-arm64)
            dotnet publish "$build_project" -r linux-arm64 -c "Linux Release" --self-contained -f net8.0
            ;;
    esac

    echo "Build complete!"
}

# Ensure we're in the correct directory
cd "$install_dir"

# Check for updates and rebuild if necessary
if check_for_updates; then
    echo "Repository updated. Building new release..."
    build_project
else
    echo "No updates found. Using existing build."
fi

# Locate and run the built VRCFT app
vrcft_bin=$(find "$install_dir/src/VRCFaceTracking.Avalonia.Desktop/bin" -type f -name "VRCFaceTracking.Avalonia.Desktop" | head -n 1)
if [[ -x "$vrcft_bin" ]]; then
    echo "Starting VRCFT..."
    "$vrcft_bin"
else
    echo "Error: VRCFT binary not found. Please run the installer again."
    exit 1
fi
