import os
import shutil
import subprocess
import sys

def check_vpk():
    if shutil.which("vpk") is None:
        print("[ERROR] Velopack CLI (vpk) is not installed or not in PATH. Please install it from https://github.com/velopack/velopack.")
        sys.exit(1)

def run_dotnet_publish():
    cmd = [
        'dotnet', 'publish',
        '-r', 'win-arm64',
        '-c', 'Windows Release',
        '--self-contained',
        '-f', 'net8.0-windows10.0.17763.0'
    ]
    print(f"Running: {' '.join(cmd)}")
    subprocess.run(cmd, check=True)

def create_zip(output_dir, zip_name):
    if os.path.exists(zip_name):
        os.remove(zip_name)
    shutil.make_archive(base_name=zip_name[:-4], format='zip', root_dir=output_dir, base_dir='./')
    print(f"Created: {zip_name}")

def make_installer(output_dir, installer_dir, version):
    cmd = [
        "vpk", "[win]", "pack",
        "--packId", "VRCFaceTracking.Avalonia",
        "--packVersion", version,
        "--packDir", output_dir,
        "--mainExe", "VRCFaceTracking.Avalonia.Desktop.exe",
        "--outputDir", installer_dir
    ]
    print(f"Running: {' '.join(cmd)}")
    subprocess.run(cmd, check=True)

def main():
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument('--version', default='1.0.0', help='Release version for installer')
    args = parser.parse_args()
    version = args.version
    check_vpk()
    base_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    bin_dir = os.path.join(base_dir, 'bin', 'Windows Release')
    framework = 'net8.0-windows10.0.17763.0'
    runtime = 'win-arm64'
    publish_dir = os.path.join(bin_dir, framework, runtime, 'publish')
    run_dotnet_publish()
    if os.path.exists(publish_dir) and os.listdir(publish_dir):
        arch_folder = os.path.join(base_dir, 'installers', 'win-arm64')
        os.makedirs(arch_folder, exist_ok=True)
        make_installer(publish_dir, arch_folder, version)
        print(f"Installer created in: {arch_folder}")
    else:
        print(f"[ERROR] Publish directory not found or empty: {publish_dir}")

if __name__ == "__main__":
    main()
