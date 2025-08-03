import subprocess
import sys
import os

scripts = [
    "publish_windows.py",
    "publish_windows_arm64.py",
    "publish_macos.py",
    "publish_macos_arm64.py",
    "publish_linux.py",
    "publish_linux_arm64.py"
]

def main():
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument('--version', default=None, help='Release version (for consistency)')
    args = parser.parse_args()
    publish_dir = os.path.dirname(os.path.abspath(__file__))
    base_dir = os.path.dirname(publish_dir)
    for script in scripts:
        print(f"Running {script}...")
        cmd = [sys.executable, os.path.join(publish_dir, script)]
        if args.version:
            cmd += ['--version', args.version]
        result = subprocess.run(cmd, cwd=base_dir)
        if result.returncode != 0:
            print(f"[ERROR] {script} failed.")
            sys.exit(result.returncode)
    print("All zips created.")

if __name__ == "__main__":
    main()
