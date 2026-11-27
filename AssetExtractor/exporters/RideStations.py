import configuration
import json
import os
import sys
import tempfile
import zipfile

from PIL import Image
from exporter import Exporter


class RideStations:
    OBJECT_ID = "rct2.station.plain"
    IMAGE_REFERENCES = ["$LGX:images.dat[0..0]", "$G1[22664..22679]"]

    @staticmethod
    def export_plain():
        try:
            RideStations._export_plain()
        except (OSError, ValueError, KeyError, zipfile.BadZipFile) as error:
            print(f" [!] Plain station sprite export failed: {error}", file=sys.stderr)
            return False
        return True

    @staticmethod
    def _export_plain():
        object_path = os.path.join(
            os.path.dirname(configuration.OPENRCT2_BIN), "data", "object", "rct2", "station",
            f"{RideStations.OBJECT_ID}.parkobj",
        )
        for path in (configuration.OPENRCT2_BIN, object_path):
            if not os.path.isfile(path):
                raise FileNotFoundError(f"Required file not found: {path}")

        with zipfile.ZipFile(object_path) as archive:
            with archive.open("object.json") as source:
                metadata = json.load(source)
        if (
            not isinstance(metadata, dict)
            or metadata.get("id") != RideStations.OBJECT_ID
            or metadata.get("objectType") != "station"
            or metadata.get("images") != RideStations.IMAGE_REFERENCES
        ):
            raise ValueError(f"Unsupported plain station sprite layout: {object_path}")

        output_dir = os.path.join(configuration.OUTPUT_DIR, "station", "plain")
        os.makedirs(output_dir, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix=".plain-station-", dir=output_dir) as staging:
            raw_dir = os.path.join(staging, "raw")
            os.makedirs(raw_dir)
            print(f"Extracting {RideStations.OBJECT_ID} from {object_path}...")
            if not Exporter.export_object(object_path, raw_dir):
                raise ValueError(f"OpenRCT2 could not export {object_path}")

            with open(os.path.join(raw_dir, "sprite_offsets.json"), "r", encoding="utf-8") as source:
                offsets = json.load(source)
            if not isinstance(offsets, dict) or len(offsets) != 17:
                raise ValueError("Unexpected plain station sprite count")

            sprites = []
            for group, first_index in (("entrance", 1), ("exit", 9)):
                for direction in range(4):
                    layers = []
                    for index in (first_index + direction, first_index + 4 + direction):
                        sprite = offsets.get(str(index))
                        if not isinstance(sprite, dict):
                            raise ValueError(f"Missing sprite {index} in exported station object")
                        filename = sprite.get("file")
                        if (
                            not isinstance(filename, str) or not filename
                            or os.path.basename(filename) != filename
                            or type(sprite.get("x")) is not int or type(sprite.get("y")) is not int
                        ):
                            raise ValueError(f"Invalid exported station sprite {index}")
                        with Image.open(os.path.join(raw_dir, filename)) as source:
                            image = source.convert("RGBA")
                        layers.append((index, sprite, image))

                    left = min(sprite["x"] for _, sprite, _ in layers)
                    top = min(sprite["y"] for _, sprite, _ in layers)
                    right = max(sprite["x"] + image.width for _, sprite, image in layers)
                    bottom = max(sprite["y"] + image.height for _, sprite, image in layers)
                    combined = Image.new("RGBA", (right - left, bottom - top))
                    for _, sprite, image in layers:
                        combined.alpha_composite(image, (sprite["x"] - left, sprite["y"] - top))

                    output_name = f"{group}-{direction}.png"
                    combined.save(os.path.join(staging, output_name))
                    sprites.append({
                        "file": output_name,
                        "direction": direction,
                        "x": left,
                        "y": top,
                        "layers": [
                            {"object_index": index, "g1_index": 22664 + index - 1,
                             "x": sprite["x"], "y": sprite["y"]}
                            for index, sprite, _ in layers
                        ],
                    })

            sidecar = "sprite_offsets.json"
            with open(os.path.join(staging, sidecar), "w", encoding="utf-8") as destination:
                json.dump({"object_id": RideStations.OBJECT_ID, "sprites": sprites}, destination, indent=2)
            for filename in [sprite["file"] for sprite in sprites] + [sidecar]:
                os.replace(os.path.join(staging, filename), os.path.join(output_dir, filename))

        print(f"Exported {len(sprites)} plain entrance and exit sprites to {output_dir}.")
