import configuration
import json
import os
import struct
import subprocess
import sys
import tempfile
import zipfile

from PIL import Image
from square import isometric_side_to_square, isometric_to_square

class Terrain:
    RCT1_CSG_SIDES = (
        ("rct1.terrain_edge.brick", 40506),
        ("rct1.terrain_edge.iron", 40590),
        ("rct1aa.terrain_edge.grey", 41134),
        ("rct1aa.terrain_edge.yellow", 41030),
        ("rct1aa.terrain_edge.red", 40926),
        ("rct1ll.terrain_edge.purple", 41238),
        ("rct1ll.terrain_edge.green", 41342),
        ("rct1ll.terrain_edge.stone_brown", 41446),
        ("rct1ll.terrain_edge.skyscraper_a", 41654),
        ("rct1ll.terrain_edge.skyscraper_b", 41758),
    )
    RCT1_GREY_STONE = "rct1ll.terrain_edge.stone_grey"
    G1_SURFACES = (
        (1987, "sand_brown"), (2044, "sand_red"), (2101, "sand"),
        (2158, "ice"), (2215, "grass_clumps"), (2329, "martian"),
        (2386, "chequerboard"), (2500, "dirt"), (2557, "rock"),
        (2716, "grass_0"), (2754, "grass_1a"), (2792, "grass_1b"),
        (2830, "grass_1c"), (2868, "grass_1d"), (2906, "grass_2a"),
        (2944, "grass_2b"), (2982, "grass_2c"), (3020, "grass_2d"),
    )
    RCT1_SURFACES = (
        ("rct1aa.terrain_surface.roof_red", 42090, ("$CSG[42090..42146]",)),
        ("rct1ll.terrain_surface.roof_grey", 42147, ("$CSG[42147..42203]",)),
        ("rct1ll.terrain_surface.rust", 42375, ("$CSG[42375..42431]", "$CSG[42432..42488]")),
        ("rct1ll.terrain_surface.wood", 42261, ("$CSG[42261..42300]", "$CSG[42301..42374]")),
    )
    WILDFLOWERS = "rct1beta.terrain_surface.wildflowers"

    @staticmethod
    def export_all():
        output_dir = os.path.join(configuration.OUTPUT_DIR, "terrain")
        try:
            os.makedirs(output_dir, exist_ok=True)
            with tempfile.TemporaryDirectory(prefix=".terrain-", dir=output_dir) as staging:
                print("Exporting square terrain surface textures from isometric sprites...")
                success = True
                g1 = os.path.join(configuration.GAME_PATH, "Data", "g1.dat")
                for index, name in Terrain.G1_SURFACES:
                    if not Terrain.export_surface(g1, index, name, staging, output_dir):
                        success = False

                try:
                    index_path, data_path = Terrain.rct1_csg_paths()
                    csg = os.path.join(staging, "csg.dat")
                    Terrain.run_sprite_command("combine", index_path, data_path, csg)
                    csg_error = None
                except (OSError, ValueError, RuntimeError) as error:
                    csg_error = str(error)

                for object_id, first_index, references in Terrain.RCT1_SURFACES:
                    try:
                        Terrain.validate_surface_object("rct1", object_id, ["$LGX:images.dat[0..0]", *references], False)
                        if csg_error is not None:
                            raise ValueError(f"RCT1 CSG unavailable: {csg_error}")
                        if not Terrain.export_surface(csg, first_index + 15, object_id.rsplit(".", 1)[-1], staging, output_dir):
                            success = False
                    except (OSError, ValueError, RuntimeError, KeyError, zipfile.BadZipFile) as error:
                        print(f" [!] Terrain surface {object_id} failed: {error}", file=sys.stderr)
                        success = False

                try:
                    object_path = Terrain.validate_surface_object(
                        "official", Terrain.WILDFLOWERS, ["$LGX:images.dat[0..63]"], True)
                    packed_sprites = os.path.join(staging, "wildflowers.dat")
                    with zipfile.ZipFile(object_path) as archive, open(packed_sprites, "wb") as destination:
                        destination.write(archive.read("images.dat"))
                    if not Terrain.export_surface(packed_sprites, 1 + 6 + 15, "wildflowers", staging, output_dir):
                        success = False
                except (OSError, ValueError, RuntimeError, KeyError, zipfile.BadZipFile) as error:
                    print(f" [!] Terrain surface {Terrain.WILDFLOWERS} failed: {error}", file=sys.stderr)
                    success = False

                if success:
                    print("\nComplete.")
                else:
                    print("\n [!] Terrain surface export finished with errors.", file=sys.stderr)
                return success
        except OSError as error:
            print(f" [!] Could not create terrain surface output or staging directory: {error}", file=sys.stderr)
            return False

    @staticmethod
    def validate_surface_object(group, object_id, images, smoothing):
        object_path = os.path.join(os.path.dirname(configuration.OPENRCT2_BIN), "data", "object", group,
                                   "terrain_surface", f"{object_id}.parkobj")
        with zipfile.ZipFile(object_path) as archive, archive.open("object.json") as source:
            metadata = json.load(source)
        if not isinstance(metadata, dict) or metadata.get("id") != object_id or metadata.get("objectType") != "terrain_surface":
            raise ValueError(f"Unexpected terrain surface object metadata: {object_path}")
        properties = metadata.get("properties")
        if (not isinstance(properties, dict) or properties.get("hasPrimaryColour", False)
                or properties.get("colour") is not None
                or bool(properties.get("smoothWithSelf") or properties.get("smoothWithOther")) != smoothing
                or metadata.get("images") != images):
            raise ValueError(f"Unsupported terrain surface image layout or colour settings: {object_path}")
        default = properties.get("default")
        if default is not None and (not isinstance(default, dict) or default.get("normal", 0) != 0):
            raise ValueError(f"Unsupported terrain surface default entry: {object_path}")
        return object_path

    @staticmethod
    def export_surface(sprite_file, index, name, staging, output_dir):
        raw_png = os.path.join(staging, f"{name}-raw.png")
        staged_png = os.path.join(staging, f"{name}.png")
        try:
            Terrain.run_sprite_command("export", sprite_file, index, raw_png)
            with Image.open(raw_png) as source:
                if source.size != (64, 63):
                    raise ValueError(f"Expected a 64x63 terrain sprite, got {source.size}")
                isometric_to_square(source, staged_png)
            os.replace(staged_png, os.path.join(output_dir, f"{name}.png"))
            return True
        except (OSError, ValueError, RuntimeError) as error:
            print(f" [!] Terrain surface {name}, sprite {index} from {sprite_file} failed: {error}", file=sys.stderr)
            return False

    @staticmethod
    def export_all_sides():
        output_dir = os.path.join(configuration.OUTPUT_DIR, "terrain_side")
        try:
            os.makedirs(output_dir, exist_ok=True)
        except OSError as error:
            print(f" [!] Could not create terrain side output directory {output_dir}: {error}", file=sys.stderr)
            return False

        print("Exporting square terrain side textures from isometric sprites...")

        success = True
        for idx, name in ((1594, "rock"), (1668, "wood_black"), (1752, "wood_red"), (1836, "ice")):
            if not Terrain.square_side_tile(idx, name):
                success = False

        if not Terrain.export_rct1_sides(output_dir):
            success = False

        if success:
            print("\nComplete.")
        else:
            print("\n [!] Terrain side export finished with errors.", file=sys.stderr)
        return success

    @staticmethod
    def square_side_tile(idx, name):
        raw_png = os.path.join(configuration.RAW_DIR, "g1", f"sprite_{idx}.png")
        final_png = os.path.join(configuration.OUTPUT_DIR, "terrain_side", f"{name}.png")

        print(f"\nExporting {name}...")

        try:
            with Image.open(raw_png) as iso_img:
                isometric_side_to_square(iso_img, final_png)
        except (OSError, ValueError) as error:
            print(f" [!] Processing failed on asset index #{idx} ({name}): {error}", file=sys.stderr)
            return False
        return True

    @staticmethod
    def export_rct1_sides(output_dir):
        try:
            with tempfile.TemporaryDirectory(prefix=".rct1-sides-", dir=output_dir) as staging:
                csg = os.path.join(staging, "csg.dat")
                try:
                    index_path, data_path = Terrain.rct1_csg_paths()
                    Terrain.run_sprite_command("combine", index_path, data_path, csg)
                    csg_error = None
                except (OSError, ValueError, RuntimeError) as error:
                    csg_error = str(error)

                success = True
                for object_id, first_index in Terrain.RCT1_CSG_SIDES:
                    try:
                        Terrain.validate_edge_object(object_id, f"$CSG[{first_index}..{first_index + 43}]")
                        if csg_error is not None:
                            raise ValueError(f"RCT1 CSG unavailable: {csg_error}")
                        Terrain.export_rct1_side(object_id, csg, first_index + 15, staging, output_dir)
                    except (OSError, ValueError, RuntimeError, KeyError, zipfile.BadZipFile) as error:
                        print(f" [!] Terrain side {object_id} failed: {error}", file=sys.stderr)
                        success = False

                try:
                    object_path = Terrain.validate_edge_object(Terrain.RCT1_GREY_STONE, "$LGX:images.dat[0..104]", grey=True)
                    packed_sprites = os.path.join(staging, "stone_grey.dat")
                    with zipfile.ZipFile(object_path) as archive, open(packed_sprites, "wb") as destination:
                        destination.write(archive.read("images.dat"))
                    Terrain.export_rct1_side(Terrain.RCT1_GREY_STONE, packed_sprites, 16, staging, output_dir, grey=True)
                except (OSError, ValueError, RuntimeError, KeyError, zipfile.BadZipFile) as error:
                    print(f" [!] Terrain side {Terrain.RCT1_GREY_STONE} failed: {error}", file=sys.stderr)
                    success = False
                return success
        except OSError as error:
            print(f" [!] Could not create terrain side staging directory: {error}", file=sys.stderr)
            return False

    @staticmethod
    def rct1_csg_paths():
        if not configuration.RCT1_PATH:
            raise ValueError("Set RCT1_PATH in AssetExtractor configuration.py to a Loopy Landscapes installation")
        root = configuration.RCT1_PATH
        index_paths = (os.path.join(root, "Data", "CSG1I.DAT"),
                       os.path.join(root, "RCTdeluxe_install", "Data", "CSG1I.DAT"))
        data_paths = (os.path.join(root, "Data", "CSG1.DAT"), os.path.join(root, "Data", "CSG1.1"))
        index_path = next((path for path in index_paths if os.path.isfile(path)), None)
        data_path = next((path for path in data_paths if os.path.isfile(path)), None)
        if index_path is None or data_path is None:
            raise FileNotFoundError(f"CSG1I.DAT and CSG1.DAT (or CSG1.1) not found under {root}")
        if os.path.getsize(index_path) != 69917 * 16 or os.path.getsize(data_path) != 41402869:
            raise ValueError(f"Unsupported RCT1 CSG files under {root}; Loopy Landscapes data is required")
        return index_path, data_path

    @staticmethod
    def validate_edge_object(object_id, image_reference, grey=False):
        object_path = os.path.join(os.path.dirname(configuration.OPENRCT2_BIN), "data", "object", "rct1",
                                   "terrain_edge", f"{object_id}.parkobj")
        with zipfile.ZipFile(object_path) as archive:
            with archive.open("object.json") as source:
                metadata = json.load(source)
        if not isinstance(metadata, dict) or metadata.get("id") != object_id or metadata.get("objectType") != "terrain_edge":
            raise ValueError(f"Unexpected terrain edge object metadata: {object_path}")
        images = metadata.get("images")
        expected = [image_reference] if grey else ["$LGX:images.dat[0..0]", image_reference]
        if not isinstance(images, list) or images[:len(expected)] != expected:
            raise ValueError(f"Unsupported terrain edge image layout: {object_path}")
        if grey:
            properties = metadata.get("properties")
            colours = properties.get("colourSettings") if isinstance(properties, dict) else None
            if (not isinstance(properties, dict) or properties.get("hasPrimaryColour") is not True
                    or not isinstance(colours, dict) or colours.get("defaultPrimary") != "grey"):
                raise ValueError(f"Unsupported grey sandstone colour settings: {object_path}")
        return object_path

    @staticmethod
    def run_sprite_command(*args):
        command = [configuration.OPENRCT2_BIN, "sprite", *map(str, args)]
        result = subprocess.run(command, capture_output=True, text=True)
        if result.returncode != 0:
            details = result.stderr.strip() or result.stdout.strip() or "no diagnostic output"
            raise RuntimeError(f"{subprocess.list2cmdline(command)} failed ({result.returncode}): {details}")

    @staticmethod
    def grey_palette_map():
        path = os.path.join(os.path.dirname(configuration.OPENRCT2_BIN), "data", "palettes.dat")
        with open(path, "rb") as source:
            header = source.read(8)
            if len(header) != 8:
                raise ValueError(f"Invalid palette file header: {path}")
            count, data_size = struct.unpack("<II", header)
            if count <= 11:
                raise ValueError(f"Grey palette map missing from {path}")
            source.seek(8 + 11 * 16)
            entry = source.read(16)
            if len(entry) != 16:
                raise ValueError(f"Invalid grey palette entry in {path}")
            offset, width, height, _, _, flags, _ = struct.unpack("<IhhhhHH", entry)
            if width != 256 or height != 1 or flags != 1 or offset + 256 > data_size:
                raise ValueError(f"Unsupported grey palette map in {path}")
            source.seek(8 + count * 16 + offset)
            palette_map = source.read(256)
            if len(palette_map) != 256 or palette_map[0] != 0:
                raise ValueError(f"Invalid grey palette map in {path}")
            return palette_map

    @staticmethod
    def export_rct1_side(object_id, sprite_file, sprite_index, staging, output_dir, grey=False):
        name = object_id.rsplit(".", 1)[-1]
        raw_path = os.path.join(staging, f"{name}-raw.png")
        staged_path = os.path.join(staging, f"{name}.png")
        Terrain.run_sprite_command("export", sprite_file, sprite_index, raw_path)
        with Image.open(raw_path) as source:
            if grey:
                if source.mode != "P":
                    raise ValueError(f"Expected indexed sprite for {object_id}, got {source.mode}")
                palette = source.getpalette()
                if palette is None or len(palette) < 768:
                    raise ValueError(f"Missing sprite palette for {object_id}")
                palette_map = Terrain.grey_palette_map()
                remapped = source.copy()
                remapped.putpalette([component for index in palette_map for component in palette[index * 3:index * 3 + 3]])
                isometric_side_to_square(remapped, staged_path)
            else:
                isometric_side_to_square(source, staged_path)
        final_path = os.path.join(output_dir, f"{name}.png")
        os.replace(staged_path, final_path)
        print(f" [+] Exported {object_id} to {final_path}")
