import json
import os
import re

from AssetExtractor import configuration
from AssetExtractor.exporter import Exporter


class trees:
    SOURCE_GAMES = ("rct2", "rct2ww", "rct2tt")
    OBJECT_TYPES = ("scenery_small", "scenery_large")
    DAT_PATTERN = re.compile(r"\$RCT2:OBJDATA/([^[]+)", re.IGNORECASE)

    @staticmethod
    def find_objects():
        object_dir = os.path.join(os.path.dirname(configuration.OPENRCT2_BIN), "data", "object")
        tree_objects = []

        for source_game in trees.SOURCE_GAMES:
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
                        or metadata.get("objectType") not in trees.OBJECT_TYPES
                        or properties.get("isTree") is not True
                    ):
                        continue

                    dat_files = []
                    for image_reference in metadata.get("images", []):
                        if not isinstance(image_reference, str):
                            continue

                        match = trees.DAT_PATTERN.search(image_reference)
                        if match:
                            dat_name = os.path.basename(match.group(1))
                            if dat_name not in dat_files:
                                dat_files.append(dat_name)

                    tree_objects.append((source_game, metadata, dat_files))

        return sorted(tree_objects, key=lambda item: (item[0], item[1].get("objectType", ""), item[1].get("id", "")))

    @staticmethod
    def extract():
        tree_objects = trees.find_objects()
        object_data_dir = os.path.join(configuration.GAME_PATH, "ObjData")
        output_dir = os.path.join(configuration.RAW_DIR, "Scenery", "Trees")
        os.makedirs(output_dir, exist_ok=True)
        exported_count = 0
        failed_count = 0
        skipped_count = 0

        if not tree_objects:
            print(" [!] No built-in RCT2 tree scenery objects were found in the OpenRCT2 object catalogs.")

        for source_game, metadata, dat_files in tree_objects:
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
                print(f" Extracting {source_game} tree {object_id} from {dat_name}...")
                if not Exporter.export_object(object_path, dat_output_dir):
                    print(f" [!] Sprite export failed for {object_id}: {object_path}")
                    failed_count += 1
                else:
                    exported_count += 1

        print(
            f"Tree sprite extraction: {len(tree_objects)} tree objects found, "
            f"{exported_count} DAT files exported, {failed_count} failed, {skipped_count} skipped."
        )

    @staticmethod
    def copy():
        # copy tree sprites from the RAW_DIR to the OUTPUT_DIR
        pass
