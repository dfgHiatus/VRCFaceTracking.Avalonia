#!/bin/bash

# Check if running on a supported operating system
if [[ "$OSTYPE" != "linux-gnu"* ]] && [[ "$OSTYPE" != "darwin"* ]]; then
    echo "Error: This script is only compatible with Linux and macOS."
    exit 1
fi

# Check for .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET is not installed. Please install .NET 8.0."
    exit 1
fi

# Ensure required .NET version is available
required_version="8.0"
installed_versions=$(dotnet --list-sdks | awk '{print $1}')

if ! echo "$installed_versions" | grep -q "$required_version"; then
    echo "Error: Required .NET version ($required_version) is not installed."
    echo "Installed versions:"
    echo "$installed_versions"
    exit 1
fi

# Define installation directories
install_dir="$HOME/.local/share/vrcft"
bin_dir="$HOME/.local/bin"

# Create installation directory if it doesn't exist
mkdir -p "$install_dir" "$bin_dir"

# Function to get the latest commit hash
get_latest_commit() {
    git fetch origin main
    git rev-parse origin/main
}

# Function to get the current commit hash
get_current_commit() {
    git rev-parse HEAD
}

# Function to update the repository
update_repo() {
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
        echo "VRCFT has been updated successfully to commit ${remote_commit:0:8}!"
    else
        echo "VRCFT is already up to date: ${local_commit:0:8}"
    fi
}

# Ensure we're in the correct directory
cd "$install_dir"

# Clone repository if not installed
if ! [ -d ".git" ]; then
    echo "Installing VRCFT..."
    git clone --recurse-submodules https://github.com/dfgHiatus/VRCFaceTracking.Avalonia "$install_dir"
    cd "$install_dir" || exit
else
    cd "$install_dir" || exit
    update_repo
fi

# Build and run the app if there were updates
if [ "$local_commit" != "$remote_commit" ]; then
    echo "Building VRCFT..."
    dotnet publish src/VRCFaceTracking.Avalonia.Desktop/VRCFaceTracking.Avalonia.Desktop.csproj -c Release -r "$(uname -m)-$(uname | tr '[:upper:]' '[:lower:]')" --self-contained false -f net8.0
fi

# Run the latest build
echo "Starting VRCFT..."
"$install_dir/src/VRCFaceTracking.Avalonia.Desktop/bin/Release/net8.0/$(uname -m)-$(uname | tr '[:upper:]' '[:lower:]')/publish/VRCFaceTracking.Avalonia.Desktop"
