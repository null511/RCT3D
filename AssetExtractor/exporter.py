import subprocess
import sys
import re


class Exporter:
    # Point to your OpenRCT2 executable or command alias
    OPENRCT2_BIN = "C:/Program Files/OpenRCT2/openrct2.exe"


    @staticmethod
    def unpack_all(dat_path: str, raw_png: str):
        return Exporter.__execute("sprite", "unpack", dat_path, raw_png)

    @staticmethod
    def export_index(dat_path: str, idx: int, raw_png: str):
        return Exporter.__execute("sprite", "export", dat_path, str(idx), raw_png)

    @staticmethod
    def __execute(*args: str):
        cmd = [Exporter.OPENRCT2_BIN, *args]
        
        try:
            result = subprocess.run(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE, text=True)

            if result.returncode == 0:
                return True
            
            err_msg = result.stderr.strip()
            
            # Check if it failed safely because the index is just empty/unused in g1.dat
            # (OpenRCT2 usually prints "Failed to export sprite" or similar for invalid indices)
            # if "failed" in err_msg.lower() or "invalid" in err_msg.lower() or not os.path.exists(raw_png):
            if "failed" in err_msg.lower() or "invalid" in err_msg.lower():
                return False

            if re.search(r'Sprite \#\d+ does not exist in sprite file\.', err_msg):
                return False
            
            print(f"\n[!] OpenRCT2 process crashed with code {result.returncode}", file=sys.stderr)
            print(f"Error details:\n{err_msg}", file=sys.stderr)
            sys.exit(result.returncode)
        except FileNotFoundError:
            print(f"\n[!] Critical Error: Could not find or execute the binary at '{Exporter.OPENRCT2_BIN}'.", file=sys.stderr)
            print("Please double check your OPENRCT2_BIN path setting configuration.", file=sys.stderr)
            sys.exit(1)
