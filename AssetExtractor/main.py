import os
import zipfile

from PIL import Image
import configuration
from trees import trees
from exporter import Exporter
from square import isometric_to_square


def run():
    # Extract game sprites
    print("Extracting sprites...")

    if not os.path.exists(configuration.RAW_DIR):
        os.makedirs(configuration.RAW_DIR)

    # extract_g1()
    # extract_paths()
    trees.extract()

    print("\nExtraction complete.")
    print("\nProcessing sprites...")

    # Convert isometric sprites to square textures
    # square_terrain()

    trees.process()

    print("\nProcessing complete!")


def extract_g1():
    GAME_G1_PATH = os.path.join(configuration.GAME_PATH, "Data", "g1.dat")
    RAW_DIR_G1 = os.path.join(configuration.RAW_DIR, "g1")

    start_index = 0
    end_index = 35000

    print(f"\nExtracting sprites from {GAME_G1_PATH} across indices {start_index} to {end_index}...")

    if not os.path.exists(RAW_DIR_G1):
        os.makedirs(RAW_DIR_G1)

    for idx in range(start_index, end_index + 1):
        raw_png = os.path.join(RAW_DIR_G1, f"sprite_{idx}.png")

        if not os.path.exists(raw_png):
            Exporter.export_index(GAME_G1_PATH, idx, raw_png)
    

def extract_paths():
    extract_path("rct1.footpath_surface.dirt.parkobj", "Dirt")


def extract_path(obj_name, raw_name):
    # GAME_OBJ_PATH = os.path.join(GAME_PATH, "ObjData", obj_name)
    GAME_OBJ_PATH = os.path.join("C:\\Program Files\\OpenRCT2\\data\\object\\rct1\\footpath_surface", obj_name)
    RAW_DIR_PATH = os.path.join(configuration.RAW_DIR, "Paths", raw_name)

    print(f"\nExtracting sprites from {obj_name}...")

    if not os.path.exists(RAW_DIR_PATH):
        os.makedirs(RAW_DIR_PATH)

    with zipfile.ZipFile(GAME_OBJ_PATH, 'r') as archive:
        archive.extract("images.dat", RAW_DIR_PATH)

    tmp = os.path.join(RAW_DIR_PATH, "images.dat")
    Exporter.unpack_all(tmp, RAW_DIR_PATH)
    

# def extract_range(game_path, raw_dir, start_index, end_index):
#     print(f"\nExtracting sprites from {game_path} across indices {start_index} to {end_index}...")

#     if not os.path.exists(raw_dir):
#         os.makedirs(raw_dir)

#     for idx in range(start_index, end_index + 1):
#         raw_png = os.path.join(raw_dir, f"sprite_{idx}.png")

#         if not os.path.exists(raw_png):
#             Exporter.export_sprite(game_path, idx, raw_png)

#     print("\nExtraction complete.")


def square_terrain():
    OUTPUT_DIR_TERRAIN = os.path.join(configuration.OUTPUT_DIR, "terrain")
    if not os.path.exists(OUTPUT_DIR_TERRAIN):
        os.makedirs(OUTPUT_DIR_TERRAIN)

    print(f"Scanning raw images for 64x63 isometric floor tiles...")

    square_terrain_tile(1987, "sand_brown")
    square_terrain_tile(2044, "sand_red")
    square_terrain_tile(2101, "sand")
    square_terrain_tile(2158, "ice")
    square_terrain_tile(2215, "grass_clumps")
    square_terrain_tile(2329, "martian")
    square_terrain_tile(2386, "chequerboard")
    square_terrain_tile(2500, "dirt")
    square_terrain_tile(2557, "rock")
    square_terrain_tile(2716, "grass_0")
    square_terrain_tile(2754, "grass_1a")
    square_terrain_tile(2792, "grass_1b")
    square_terrain_tile(2830, "grass_1c")
    square_terrain_tile(2868, "grass_1d")
    square_terrain_tile(2906, "grass_2a")
    square_terrain_tile(2944, "grass_2b")
    square_terrain_tile(2982, "grass_2c")
    square_terrain_tile(3020, "grass_2d")

    print("\nAll flattened terrain tiles are saved.")


def square_terrain_tile(idx, name):
    raw_png = os.path.join(configuration.RAW_DIR, "g1", f"sprite_{idx}.png")
    final_png = os.path.join(configuration.OUTPUT_DIR, "terrain", f"{name}.png")

    try:
        with Image.open(raw_png) as iso_img:
            isometric_to_square(iso_img, final_png);

    except Exception as e:
        print(f" [!] Processing failed on asset index #{idx}: {e}")


if __name__ == "__main__":
    run()
