import os
import shutil
import subprocess

def run_dotnet_publish(runtime, config, self_contained, framework):
    """Run the dotnet publish command with the specified parameters."""
    cmd = [
        'dotnet', 'publish',
        '-r', runtime,
        '-c', config,
        '--self-contained' if self_contained else '',
        '-f', framework
    ]
    cmd = [part for part in cmd if part]  # Remove empty strings
    print(f"Running: {' '.join(cmd)}")
    subprocess.run(cmd, check=True)

def create_zip(source_dir, zip_name):
    """Create a zip file from the specified directory."""
    # Create zip file
    if os.path.exists(zip_name):
        os.remove(zip_name)

    # Check if directory has content
    if not os.listdir(source_dir):
        print(f"Warning: Directory {source_dir} is empty, skipping zip creation")
        return False

    shutil.make_archive(
        base_name=zip_name[:-4],  # Remove .zip extension
        format='zip',
        root_dir=source_dir,
        base_dir='./'
    )
    print(f"Created: {zip_name}")
    return True

def find_output_directory(bin_dir, framework, runtime):
    """Find the directory containing the published output for a specific framework and runtime."""
    # For Windows-specific frameworks
    if framework.startswith('net') and 'windows' in framework.lower():
        # Convert framework format to directory format (replace - with .)
        dir_framework = framework.replace('-', '.')

        # Check for direct match first
        publish_dir = os.path.join(bin_dir, dir_framework, runtime, 'publish')
        if os.path.exists(publish_dir) and os.listdir(publish_dir):
            return publish_dir

        # Try alternative paths for windows frameworks
        # First try with dots instead of dashes
        alt_path = os.path.join(bin_dir, framework.replace('-', '.'), runtime, 'publish')
        if os.path.exists(alt_path) and os.listdir(alt_path):
            return alt_path

        # Then try with just the base framework (net8.0)
        base_framework = framework.split('-')[0]
        alt_path = os.path.join(bin_dir, base_framework, runtime, 'publish')
        if os.path.exists(alt_path) and os.listdir(alt_path):
            return alt_path
    else:
        # For non-Windows frameworks
        publish_dir = os.path.join(bin_dir, framework, runtime, 'publish')
        if os.path.exists(publish_dir) and os.listdir(publish_dir):
            return publish_dir

    # If we couldn't find it with the specific approaches, do a more general search
    for root, dirs, files in os.walk(bin_dir):
        if root.endswith(os.path.join(runtime, 'publish')) and os.listdir(root):
            return root

    return None

def main():
    # Define the publish configurations
    configs = [
        # runtime, config_name, self_contained, framework
        ('win-x64', 'Windows Release', True, 'net8.0-windows10.0.17763.0'),
        ('win-arm64', 'Windows Release', True, 'net8.0-windows10.0.17763.0'),
        ('osx-x64', 'MacOS Release', True, 'net8.0'),
        ('osx-arm64', 'MacOS Release', True, 'net8.0'),
        ('linux-x64', 'Linux Release', True, 'net8.0'),
        ('linux-arm64', 'Linux Release', True, 'net8.0')
    ]

    # Current directory where the script is run
    current_dir = os.getcwd()

    for runtime, config, self_contained, framework in configs:
        # Run the publish command
        run_dotnet_publish(runtime, config, self_contained, framework)

        # Determine the bin directory
        bin_dir = os.path.join(current_dir, 'bin', config)

        # Find the output directory
        output_dir = find_output_directory(bin_dir, framework, runtime)

        if output_dir and os.listdir(output_dir):
            # Create zip filename
            clean_framework = framework.split('-')[0]  # Get base framework name like 'net8.0'
            # Place zip in installers/<runtime>/
            arch_folder = os.path.join(current_dir, 'installers', runtime)
            os.makedirs(arch_folder, exist_ok=True)
            zip_filename = os.path.join(arch_folder, f"VRCFaceTracking_Avalonia_{runtime}_{clean_framework}.zip")
            if create_zip(output_dir, zip_filename):
                print(f"Successfully created zip for {runtime} with framework {framework} at {zip_filename}")
        else:
            print(f"Could not find non-empty output directory for {runtime} with framework {framework} in {bin_dir}")
            # Print the directory structure to help debugging
            print("Available directories in bin folder:")
            for root, dirs, files in os.walk(bin_dir):
                print(f"  {root}")
                for d in dirs:
                    print(f"    - {d}")

if __name__ == "__main__":
    main()
