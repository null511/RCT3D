import configuration
import json
import os
import re

from exporter import Exporter
from PIL import Image


class Vegetation:
    SOURCE_GAMES = ("rct2", "rct2ww", "rct2tt")
    OBJECT_TYPES = ("scenery_small", "scenery_large")
    VEGETATION_CURSORS = {"CURSOR_TREE_DOWN", "CURSOR_FLOWER_DOWN"}
    DAT_PATTERN = re.compile(r"\$RCT2:OBJDATA/([^[]+)", re.IGNORECASE)

    @staticmethod
    def run():
        # Extract game sprites
        print("Extracting sprites...")

        if not os.path.exists(configuration.RAW_DIR):
            os.makedirs(configuration.RAW_DIR)

        Vegetation.extract()

        print("\nExtraction complete.")
        print("\nProcessing sprites...")

        Vegetation.process()

        print("\nProcessing complete!")

    @staticmethod
    def find_objects():
        object_dir = os.path.join(os.path.dirname(configuration.OPENRCT2_BIN), "data", "object")
        vegetation_objects = []

        for source_game in Vegetation.SOURCE_GAMES:
            source_dir = os.path.join(object_dir, source_game)
            if not os.path.isdir(source_dir):
                print(f" [!] Built-in {source_game} object catalog not found: {source_dir}")
                continue

            for root, dirs, files in os.walk(source_dir):
                dirs.sort()
                for filename in sorted(files):
                    if not filename.lower().endswith(".json"):
                        continue

                    metadata_path = os.path.join(root, filename)
                    try:
                        with open(metadata_path, "r", encoding="utf-8") as metadata_file:
                            metadata = json.load(metadata_file)
                    except (OSError, json.JSONDecodeError) as error:
                        print(f" [!] Could not read object metadata {metadata_path}: {error}")
                        continue

                    source_games = metadata.get("sourceGame", [])
                    if isinstance(source_games, str):
                        source_games = (source_games,)

                    properties = metadata.get("properties", {})
                    if (
                        source_game not in source_games
                        or metadata.get("objectType") not in Vegetation.OBJECT_TYPES
                        or not (
                            properties.get("isTree") is True
                            or properties.get("cursor") in Vegetation.VEGETATION_CURSORS
                        )
                    ):
                        continue

                    dat_files = []
                    for image_reference in metadata.get("images", []):
                        if not isinstance(image_reference, str):
                            continue

                        match = Vegetation.DAT_PATTERN.search(image_reference)
                        if match:
                            dat_name = os.path.basename(match.group(1))
                            if dat_name not in dat_files:
                                dat_files.append(dat_name)

                    vegetation_objects.append((source_game, metadata, dat_files))

        return sorted(vegetation_objects, key=lambda item: (item[0], item[1].get("objectType", ""), item[1].get("id", "")))

    @staticmethod
    def extract():
        vegetation_objects = Vegetation.find_objects()
        object_data_dir = os.path.join(configuration.GAME_PATH, "ObjData")
        output_dir = os.path.join(configuration.RAW_DIR, "Scenery", "Trees")
        os.makedirs(output_dir, exist_ok=True)
        exported_count = 0
        failed_count = 0
        skipped_count = 0

        if not vegetation_objects:
            print(" [!] No built-in RCT2 tree or shrub scenery objects were found in the OpenRCT2 object catalogs.")

        for source_game, metadata, dat_files in vegetation_objects:
            object_id = str(metadata.get("id") or "unknown-object")
            safe_object_id = re.sub(r"[^A-Za-z0-9._-]+", "_", object_id)
            if not dat_files:
                print(f" [!] No RCT2 DAT image reference found for {object_id}")
                skipped_count += 1
                continue

            for dat_name in dat_files:
                object_path = os.path.join(object_data_dir, dat_name)
                dat_output_dir = os.path.join(output_dir, safe_object_id, os.path.splitext(dat_name)[0])
                if not os.path.isfile(object_path):
                    print(f" [!] Missing DAT file for {object_id}: {object_path}")
                    skipped_count += 1
                    continue

                os.makedirs(dat_output_dir, exist_ok=True)
                print(f" Extracting {source_game} vegetation {object_id} from {dat_name}...")
                if not Exporter.export_object(object_path, dat_output_dir):
                    print(f" [!] Sprite export failed for {object_id}: {object_path}")
                    failed_count += 1
                else:
                    exported_count += 1

        print(
            f"Vegetation sprite extraction: {len(vegetation_objects)} tree/shrub objects found, "
            f"{exported_count} DAT files exported, {failed_count} failed, {skipped_count} skipped."
        )

    @staticmethod
    def _project_tile(tile, direction):
        x = tile.get("x", 0)
        y = tile.get("y", 0)
        z = tile.get("z", 0)
        if direction == 1:
            x, y = y, -x
        elif direction == 2:
            x, y = -x, -y
        elif direction == 3:
            x, y = -y, x

        screen_y = ((x + y) >> 1) - z
        return y - x, screen_y, screen_y

    @staticmethod
    def _compose_large_tree(metadata, raw_object_dir, direction):
        offsets_path = os.path.join(raw_object_dir, "sprite_offsets.json")
        try:
            with open(offsets_path, "r", encoding="utf-8") as offsets_file:
                sprite_offsets = json.load(offsets_file)
        except (OSError, json.JSONDecodeError) as error:
            print(f" [!] Could not read sprite offsets for {metadata.get('id')}: {error}")
            return None

        tiles = metadata.get("properties", {}).get("tiles", [])
        fragments = []
        for sequence, tile in enumerate(tiles):
            image_index = 4 + (sequence << 2) + direction
            sprite_info = sprite_offsets.get(str(image_index))
            if not isinstance(sprite_info, dict):
                print(f" [!] Missing sprite offset for {metadata.get('id')} image {image_index}")
                return None

            try:
                sprite_path = os.path.join(raw_object_dir, sprite_info["file"])
                with Image.open(sprite_path) as source:
                    sprite = source.convert("RGBA")
            except (OSError, KeyError) as error:
                print(f" [!] Could not open sprite for {metadata.get('id')} image {image_index}: {error}")
                return None

            screen_x, screen_y, depth = Vegetation._project_tile(tile, direction)
            origin_x = screen_x + sprite_info["x"]
            origin_y = screen_y + sprite_info["y"]
            fragments.append((depth, sequence, origin_x, origin_y, sprite))

        if not fragments:
            print(f" [!] No tile sprites found for {metadata.get('id')}")
            return None

        left = min(fragment[2] for fragment in fragments)
        top = min(fragment[3] for fragment in fragments)
        right = max(fragment[2] + fragment[4].width for fragment in fragments)
        bottom = max(fragment[3] + fragment[4].height for fragment in fragments)
        composite = Image.new("RGBA", (right - left, bottom - top), (0, 0, 0, 0))

        for _, _, x, y, sprite in sorted(fragments, key=lambda fragment: (fragment[0], fragment[1])):
            composite.alpha_composite(sprite, (x - left, y - top))

        return composite

        # content_bounds = composite.getchannel("A").getbbox()
        # if content_bounds is None:
        #     print(f" [!] Composed image is fully transparent for {metadata.get('id')}")
        #     return None

        # return composite.crop(content_bounds)

    @staticmethod
    def _compose_direction_atlas(north, west, background=(0, 0, 0, 0)):
        cell_width = max(north.width, west.width)
        cell_height = max(north.height, west.height)
        atlas = Image.new("RGBA", (cell_width * 2, cell_height), background)
        north_position = ((cell_width - north.width) // 2, (cell_height - north.height) // 2)
        west_position = (cell_width + (cell_width - west.width) // 2, (cell_height - west.height) // 2)
        atlas.alpha_composite(north, north_position)
        atlas.alpha_composite(west, west_position)
        return atlas

    @staticmethod
    def _write_companion_atlases(output_dir, output_name, north_size, west_size):
        for suffix, fallback in (("_n", (128, 128, 255, 255)), ("_s", (255, 0, 0, 0))):
            companions = []
            has_companion = False
            for direction_name, expected_size in (("north", north_size), ("west", west_size)):
                source_path = os.path.join(output_dir, f"{output_name}-{direction_name}{suffix}.png")
                if os.path.isfile(source_path):
                    has_companion = True
                    try:
                        with Image.open(source_path) as source:
                            companion = source.convert("RGBA")
                    except OSError as error:
                        print(f" [!] Could not open vegetation {suffix} companion {source_path}: {error}")
                        companion = Image.new("RGBA", expected_size, fallback)

                    if companion.size != expected_size:
                        print(f" [!] Vegetation {suffix} companion {source_path} is {companion.width}x{companion.height}, expected {expected_size[0]}x{expected_size[1]}.")
                        companion = Image.new("RGBA", expected_size, fallback)
                else:
                    companion = Image.new("RGBA", expected_size, fallback)

                companions.append(companion)

            output_path = os.path.join(output_dir, f"{output_name}{suffix}.png")
            if not has_companion:
                if os.path.isfile(output_path):
                    os.remove(output_path)
                continue

            try:
                atlas = Vegetation._compose_direction_atlas(companions[0], companions[1], background=fallback)
                atlas.save(output_path)
            except OSError as error:
                print(f" [!] Could not save vegetation {suffix} atlas for {output_name}: {error}")

    @staticmethod
    def process():
        vegetation_objects = Vegetation.find_objects()
        raw_root = os.path.join(configuration.RAW_DIR, "Scenery", "Trees")
        output_dir = os.path.join(configuration.OUTPUT_DIR, "trees")
        os.makedirs(output_dir, exist_ok=True)
        small_composed = 0
        large_composed = 0
        failed_count = 0
        skipped_count = 0

        for _, metadata, dat_files in vegetation_objects:
            object_id = str(metadata.get("id") or "unknown-object")
            safe_object_id = re.sub(r"[^A-Za-z0-9._-]+", "_", object_id)
            object_type = metadata.get("objectType")
            if not dat_files:
                print(f" [!] No extracted DAT sources found for {object_id}")
                skipped_count += 1
                continue

            for dat_name in dat_files:
                dat_stem = os.path.splitext(dat_name)[0]
                raw_object_dir = os.path.join(raw_root, safe_object_id, dat_stem)
                output_name = safe_object_id
                if len(dat_files) > 1:
                    output_name = f"{output_name}-{dat_stem}"

                if object_type == "scenery_small":
                    north_path = os.path.join(raw_object_dir, "1.png")
                    west_path = os.path.join(raw_object_dir, "0.png")
                    if not os.path.isfile(north_path) or not os.path.isfile(west_path):
                        missing_path = north_path if not os.path.isfile(north_path) else west_path
                        print(f" [!] Missing vegetation direction sprite for {object_id}: {missing_path}")
                        failed_count += 1
                        continue

                    try:
                        with Image.open(north_path) as source:
                            north = source.convert("RGBA")
                        with Image.open(west_path) as source:
                            west = source.convert("RGBA")
                        atlas = Vegetation._compose_direction_atlas(north, west)
                        atlas.save(os.path.join(output_dir, f"{output_name}.png"))
                    except OSError as error:
                        print(f" [!] Could not compose vegetation atlas for {object_id}: {error}")
                        failed_count += 1
                        continue

                    Vegetation._write_companion_atlases(output_dir, output_name, north.size, west.size)
                    small_composed += 1
                    continue

                if object_type != "scenery_large":
                    skipped_count += 1
                    continue

                direction_sprites = []
                for direction in (1, 0):
                    composite = Vegetation._compose_large_tree(metadata, raw_object_dir, direction)
                    if composite is None:
                        failed_count += 1
                    direction_sprites.append(composite)

                if any(sprite is None for sprite in direction_sprites):
                    continue

                north, west = direction_sprites
                try:
                    atlas = Vegetation._compose_direction_atlas(north, west)
                    atlas.save(os.path.join(output_dir, f"{output_name}.png"))
                except OSError as error:
                    print(f" [!] Could not save vegetation atlas for {object_id}: {error}")
                    failed_count += 1
                    continue

                Vegetation._write_companion_atlases(output_dir, output_name, north.size, west.size)
                large_composed += 1

        print(
            f"Vegetation image output: {small_composed} small-tree/shrub atlases composed, "
            f"{large_composed} large-tree/shrub atlases composed, {failed_count} failed, {skipped_count} skipped."
        )
