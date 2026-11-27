import configuration
import json
import os
import re
import subprocess
import sys


class Exporter:
    @staticmethod
    def unpack_all(dat_path: str, raw_png: str):
        return Exporter.__execute("sprite", "unpack", dat_path, raw_png)

    @staticmethod
    def export_index(dat_path: str, idx: int, raw_png: str):
        return Exporter.__execute("sprite", "export", dat_path, str(idx), raw_png)

    @staticmethod
    def export_object(object_path: str, output_dir: str):
        output = Exporter.__execute("sprite", "exportobject", object_path, output_dir, capture_stdout=True)
        if not isinstance(output, str):
            return False

        try:
            sprites = Exporter.__parse_object_sprites(output)
            offsets = {
                str(index): {
                    "file": os.path.basename(sprite["path"]),
                    "x": sprite["x"],
                    "y": sprite["y"],
                }
                if isinstance(sprite, dict)
                else None
                for index, sprite in enumerate(sprites)
            }
            with open(os.path.join(output_dir, "sprite_offsets.json"), "w", encoding="utf-8") as metadata_file:
                json.dump(offsets, metadata_file, indent=2)
        except (ValueError, KeyError, OSError, TypeError) as error:
            print(f" [!] Could not save sprite offsets for {object_path}: {error}", file=sys.stderr)
            return False

        return True

    @staticmethod
    def __parse_object_sprites(output: str):
        try:
            sprites = json.loads(output)
            if isinstance(sprites, dict) or sprites == "":
                sprites = [sprites]
        except json.JSONDecodeError:
            # exportobject emits comma-separated records after context startup diagnostics.
            start = re.search(r'(?m)^[ \t]*(?=\{\s*"path"\s*:|""[ \t]*(?:,|$))', output)
            if start is None:
                raise ValueError(f"No sprite records in OpenRCT2 output: {output[:200]!r}")
            sprites = json.loads(f"[{output[start.start():]}]")

        if not isinstance(sprites, list) or not sprites:
            raise ValueError("OpenRCT2 did not return a nonempty sprite list")
        for index, sprite in enumerate(sprites):
            if sprite == "":
                continue
            if (
                not isinstance(sprite, dict)
                or not isinstance(sprite.get("path"), str) or not sprite["path"]
                or type(sprite.get("x")) is not int or type(sprite.get("y")) is not int
            ):
                raise ValueError(f"Invalid OpenRCT2 sprite record at index {index}: {sprite!r}")
        return sprites

    @staticmethod
    def __execute(*args: str, capture_stdout=False):
        cmd = [configuration.OPENRCT2_BIN, *args]
        
        try:
            stdout = subprocess.PIPE if capture_stdout else subprocess.DEVNULL
            result = subprocess.run(cmd, stdout=stdout, stderr=subprocess.PIPE, text=True)

            if result.returncode == 0:
                return result.stdout if capture_stdout else True
            
            err_msg = result.stderr.strip()
            
            # Check if it failed safely because the index is just empty/unused in g1.dat
            # (OpenRCT2 usually prints "Failed to export sprite" or similar for invalid indices)
            # if "failed" in err_msg.lower() or "invalid" in err_msg.lower() or not os.path.exists(raw_png):
            if "failed" in err_msg.lower() or "invalid" in err_msg.lower():
                return False

            if re.search(r'Sprite \#\d+ does not exist in sprite file\.', err_msg):
                return False

            if "could not load the object" in err_msg.lower() or "could not export" in err_msg.lower():
                return False
            
            print(f"\n[!] OpenRCT2 process crashed with code {result.returncode}", file=sys.stderr)
            print(f"Error details:\n{err_msg}", file=sys.stderr)
            sys.exit(result.returncode)
        except FileNotFoundError:
            print(f"\n[!] Critical Error: Could not find or execute the binary at '{configuration.OPENRCT2_BIN}'.", file=sys.stderr)
            print("Please double check your OPENRCT2_BIN path setting configuration.", file=sys.stderr)
            sys.exit(1)
