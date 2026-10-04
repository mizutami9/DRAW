import os
import sys

sys.stdout.reconfigure(encoding='utf-8')

def get_dir_size(path, max_depth=3, current_depth=0):
    total = 0
    sub_sizes = {}
    try:
        with os.scandir(path) as it:
            for entry in it:
                try:
                    if entry.is_file(follow_symlinks=False):
                        total += entry.stat(follow_symlinks=False).st_size
                    elif entry.is_dir(follow_symlinks=False):
                        sub_size = get_dir_size_fast(entry.path)
                        total += sub_size
                        if current_depth < max_depth:
                            sub_sizes[entry.name] = sub_size
                except (PermissionError, FileNotFoundError, OSError):
                    pass
    except (PermissionError, FileNotFoundError, OSError):
        pass
    return total, sub_sizes

def get_dir_size_fast(path):
    total = 0
    try:
        with os.scandir(path) as it:
            for entry in it:
                try:
                    if entry.is_file(follow_symlinks=False):
                        total += entry.stat(follow_symlinks=False).st_size
                    elif entry.is_dir(follow_symlinks=False):
                        total += get_dir_size_fast(entry.path)
                except (PermissionError, FileNotFoundError, OSError):
                    pass
    except (PermissionError, FileNotFoundError, OSError):
        pass
    return total

def scan_target(title, target_path, threshold_gb=1.0):
    print(f"\n==========================================")
    print(f"--- {title}: {target_path} ---")
    print(f"==========================================")
    if not os.path.exists(target_path):
        print("Path not found.")
        return
    
    entries = []
    try:
        for item in os.listdir(target_path):
            p = os.path.join(target_path, item)
            try:
                if os.path.isdir(p):
                    sz = get_dir_size_fast(p)
                    entries.append((item, sz, True))
                elif os.path.isfile(p):
                    sz = os.path.getsize(p)
                    entries.append((item, sz, False))
            except Exception:
                pass
    except Exception as e:
        print(f"Error accessing {target_path}: {e}")
        return

    entries.sort(key=lambda x: x[1], reverse=True)
    for name, sz, is_dir in entries:
        gb = sz / 1024 / 1024 / 1024
        if gb >= threshold_gb:
            kind = "[DIR]" if is_dir else "[FILE]"
            print(f"{kind} {name:<35s}: {gb:6.2f} GB")

if __name__ == "__main__":
    scan_target("C Drive Root", "C:\\", threshold_gb=2.0)
    scan_target("User Root (mizut)", r"C:\Users\mizut", threshold_gb=1.0)
    scan_target("AppData Local", r"C:\Users\mizut\AppData\Local", threshold_gb=1.0)
    scan_target("AppData Roaming", r"C:\Users\mizut\AppData\Roaming", threshold_gb=1.0)
    scan_target(".cache", r"C:\Users\mizut\.cache", threshold_gb=0.5)
    scan_target(".gemini", r"C:\Users\mizut\.gemini", threshold_gb=0.5)
    scan_target("workSpace", r"C:\workSpace", threshold_gb=1.0)
