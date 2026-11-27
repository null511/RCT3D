import configuration
import json
import os
import sys
import tempfile
import zipfile

from PIL import Image
from exporter import Exporter
from square import flat_path_to_square


class Paths:
    INVISIBLE_IDS = frozenset((
        "openrct2.footpath_surface.invisible",
        "openrct2.footpath_surface.queue_invisible",
    ))

    @staticmethod
    def export_all():
        object_root = os.path.join(os.path.dirname(configuration.OPENRCT2_BIN), "data", "object")

        try:
            object_paths = []
            for group in sorted(os.scandir(object_root), key=lambda entry: entry.name):
                if not group.is_dir():
                    continue

                surface_dir = os.path.join(group.path, "footpath_surface")

                if os.path.isdir(surface_dir):
                    object_paths.extend(
                        entry.path for entry in sorted(os.scandir(surface_dir), key=lambda entry: entry.name)
                        if entry.is_file() and entry.name.lower().endswith((".parkobj", ".json"))
                    )

            if not object_paths:
                raise ValueError(f"No footpath surface objects found under {object_root}")
        except (OSError, ValueError) as error:
            print(f" [!] Could not discover path objects: {error}", file=sys.stderr)
            return False

        success = True
        seen_ids = set()
        for object_path in object_paths:
            try:
                metadata = Paths.read_metadata(object_path)
                object_id = metadata.get("id")
                if (not isinstance(object_id, str) or not object_id or object_id in (".", "..")
                        or os.path.basename(object_id) != object_id or metadata.get("objectType") != "footpath_surface"):
                    raise ValueError(f"Invalid footpath surface metadata: {object_path}")

                if object_id in Paths.INVISIBLE_IDS:
                    continue

                output_key = os.path.normcase(object_id)

                if output_key in seen_ids:
                    raise ValueError(f"Duplicate path output directory for object ID: {object_id}")

                seen_ids.add(output_key)
                properties = metadata.get("properties", {})

                if not isinstance(properties, dict) or type(properties.get("isQueue", False)) is not bool:
                    raise ValueError(f"Invalid path properties: {object_path}")

                Paths.export(object_id, object_path, properties.get("isQueue", False))
            except (OSError, ValueError, KeyError, zipfile.BadZipFile) as error:
                print(f" [!] Path export failed for {object_path}: {error}", file=sys.stderr)
                success = False

        return success

    @staticmethod
    def read_metadata(object_path):
        if object_path.lower().endswith(".parkobj"):
            with zipfile.ZipFile(object_path) as archive, archive.open("object.json") as source:
                metadata = json.load(source)
        else:
            with open(object_path, "r", encoding="utf-8") as source:
                metadata = json.load(source)
        if not isinstance(metadata, dict):
            raise ValueError(f"Invalid object metadata: {object_path}")
        return metadata

    @staticmethod
    def export(object_id, object_path, is_queue):
        raw_dir = os.path.join(configuration.RAW_DIR, "Paths", object_id)
        output_dir = os.path.join(configuration.OUTPUT_DIR, "path", object_id)
        os.makedirs(raw_dir, exist_ok=True)

        print(f"Extracting {object_id} from {object_path}...")

        try:
            exported = Exporter.export_object(object_path, raw_dir)
        except SystemExit as error:
            raise ValueError(f"OpenRCT2 export terminated with code {error.code}: {object_path}") from error
        if not exported:
            raise ValueError(f"OpenRCT2 could not export {object_path}")

        Paths.export_flat_textures(object_id, raw_dir, output_dir, is_queue)

    @staticmethod
    def export_flat_textures(object_id, raw_dir, output_dir, is_queue):
        offsets_path = os.path.join(raw_dir, "sprite_offsets.json")
        with open(offsets_path, "r", encoding="utf-8") as source:
            offsets = json.load(source)
        expected_count = 21 if is_queue else 52
        if not isinstance(offsets, dict) or len(offsets) != expected_count:
            raise ValueError(f"Unexpected sprite layout for {object_id}: {offsets_path}")

        if is_queue:
            indices = [index for index in range(1, 17) if (index - 1).bit_count() <= 2]
        else:
            indices = [*range(1, 17), *range(21, 52)]

        os.makedirs(output_dir, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix=".path-", dir=output_dir) as staging:
            filenames = []
            for index in indices:
                sprite = offsets.get(str(index))
                output_name = f"{index:02}.png"
                if not isinstance(sprite, dict):
                    raise ValueError(f"Missing path sprite {index} in {offsets_path}")
                filename = sprite.get("file")
                x, y = sprite.get("x"), sprite.get("y")
                if (not isinstance(filename, str) or not filename or os.path.basename(filename) != filename
                        or type(x) is not int or type(y) is not int):
                    raise ValueError(f"Invalid path sprite {index} in {offsets_path}")
                with Image.open(os.path.join(raw_dir, filename)) as source:
                    image = flat_path_to_square(source, x, y)
                image.save(os.path.join(staging, output_name))
                filenames.append(output_name)

            for filename in filenames:
                os.replace(os.path.join(staging, filename), os.path.join(output_dir, filename))

        print(f"Exported {len(filenames)} square textures for {object_id} to {output_dir}.")
