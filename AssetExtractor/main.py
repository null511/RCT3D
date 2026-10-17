import os
import subprocess
import sys
from PIL import Image
from square import isometric_to_square

# ==========================================
# ⚙️ CONFIGURATION - ADJUST PATHS HERE
# ==========================================
# Point to your OpenRCT2 executable or command alias
OPENRCT2_BIN = "C:/Program Files/OpenRCT2/openrct2.exe" 

# Absolute path to your base game asset file
G1_DAT_PATH = "Z:/SteamLibrary/steamapps/common/Rollercoaster Tycoon 2/Data/g1.dat" 

RAW_DIR = "./bin/raw"

# Output directory for your final 3D-ready engine assets
OUTPUT_DIR = "./bin/squared"

# Index boundaries for core flat terrain surfaces (Grass, Dirt, Desert Sand, Checkered)
START_SCAN_INDEX = 0
END_SCAN_INDEX = 35000

# ==========================================

def run():
    extract_all()
    square_terrain()

    print("\nProcessing complete!")


def extract_all():
    if not os.path.exists(RAW_DIR):
        os.makedirs(RAW_DIR)

    print(f"Scanning g1.dat for 64x32 isometric floor tiles across indices {START_SCAN_INDEX} to {END_SCAN_INDEX}...")

    for idx in range(START_SCAN_INDEX, END_SCAN_INDEX + 1):
        raw_png = os.path.join(RAW_DIR, f"sprite_{idx}.png")

        if os.path.exists(raw_png):
            continue

        # 1. Direct individual asset extraction via fixed OpenRCT2 syntax
        # usage: openrct2 sprite export <spritefile> <idx> <output>
        cmd = [OPENRCT2_BIN, "sprite", "export", G1_DAT_PATH, str(idx), raw_png]
        
        try:
            # Capturing stderr allows us to inspect the failure reason instead of masking it
            result = subprocess.run(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE, text=True)
            
            # If the binary errored out, inspect why
            if result.returncode != 0:
                err_msg = result.stderr.strip()
                
                # Check if it failed safely because the index is just empty/unused in g1.dat
                # (OpenRCT2 usually prints "Failed to export sprite" or similar for invalid indices)
                if "failed" in err_msg.lower() or "invalid" in err_msg.lower() or not os.path.exists(raw_png):
                    continue
                else:
                    # Throw any unrelated system errors, paths issues, or internal engine failures
                    print(f"\n[!] OpenRCT2 process crashed at index #{idx} with code {result.returncode}", file=sys.stderr)
                    print(f"Error details:\n{err_msg}", file=sys.stderr)
                    sys.exit(result.returncode)
        except FileNotFoundError:
            print(f"\n[!] Critical Error: Could not find or execute the binary at '{OPENRCT2_BIN}'.", file=sys.stderr)
            print("Please double check your OPENRCT2_BIN path setting configuration.", file=sys.stderr)
            sys.exit(1)
        
    print("\nExtraction complete.")


def square_terrain():
    if not os.path.exists(OUTPUT_DIR):
        os.makedirs(OUTPUT_DIR)

    print(f"Scanning raw images for 64x63 isometric floor tiles...")

    square(1987, "sand_brown")
    square(2044, "sand_red")
    square(2101, "sand")
    square(2158, "ice")
    square(2215, "grass_clumps")
    square(2329, "martian")
    square(2386, "chequerboard")
    square(2500, "dirt")
    square(2557, "rock")
    square(2792, "grass")

    print("\nAll flattened terrain tiles are saved.")


def square(idx, name):
    raw_png = os.path.join(RAW_DIR, f"sprite_{idx}.png")
    final_png = os.path.join(OUTPUT_DIR, f"{name}.png")

    if not os.path.exists(raw_png):
        return

    try:
        with Image.open(raw_png) as iso_img:
            isometric_to_square(iso_img, final_png);

    except Exception as e:
        print(f" [!] Processing failed on asset index #{idx}: {e}")


if __name__ == "__main__":
    run()
