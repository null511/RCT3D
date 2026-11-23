import json
import os
import shutil
import sys
import tempfile

import configuration
from exporter import Exporter


class RideVehicles:
    OBJECT_ID = "rct2.ride.wmmine"
    DAT_NAME = "WMMINE.DAT"
    ROTATIONS = (0, 8, 16, 24)
    # Three object previews precede car 0; each rotation has three swing frames.
    CAR_IMAGE_START = 3
    FRAMES_PER_ROTATION = 3

    @staticmethod
    def export_mine_cars():
        try:
            RideVehicles._export_mine_cars()
        except (OSError, ValueError) as error:
            print(f" [!] Mine Cars export failed: {error}", file=sys.stderr)
            return False
        return True

    @staticmethod
    def _export_mine_cars():
        metadata_path = os.path.join(
            os.path.dirname(configuration.OPENRCT2_BIN), "data", "object", "rct2", "ride",
            f"{RideVehicles.OBJECT_ID}.json",
        )
        dat_path = os.path.join(configuration.GAME_PATH, "ObjData", RideVehicles.DAT_NAME)
        for path in (configuration.OPENRCT2_BIN, metadata_path, dat_path):
            if not os.path.isfile(path):
                raise FileNotFoundError(f"Required file not found: {path}")

        with open(metadata_path, "r", encoding="utf-8") as source:
            metadata = json.load(source)
        RideVehicles._validate_layout(metadata, metadata_path)

        raw_dir = os.path.join(configuration.RAW_DIR, "Vehicles", RideVehicles.OBJECT_ID, "WMMINE")
        output_dir = os.path.join(configuration.OUTPUT_DIR, "vehicles", "mine-cars")
        os.makedirs(raw_dir, exist_ok=True)
        print(f"Extracting {RideVehicles.OBJECT_ID} from {dat_path}...")
        if not Exporter.export_object(dat_path, raw_dir):
            raise ValueError(f"OpenRCT2 could not export {dat_path} into {raw_dir}")

        offsets_path = os.path.join(raw_dir, "sprite_offsets.json")
        with open(offsets_path, "r", encoding="utf-8") as source:
            offsets = json.load(source)
        if not isinstance(offsets, dict):
            raise ValueError(f"Invalid sprite offsets: {offsets_path}")

        views = []
        for rotation in RideVehicles.ROTATIONS:
            index = RideVehicles.CAR_IMAGE_START + rotation * RideVehicles.FRAMES_PER_ROTATION
            sprite = offsets.get(str(index))
            if not isinstance(sprite, dict):
                raise ValueError(f"Missing sprite {index} in {offsets_path}")
            filename = sprite.get("file")
            if (
                not isinstance(filename, str) or not filename
                or os.path.basename(filename) != filename
                or type(sprite.get("x")) is not int or type(sprite.get("y")) is not int
            ):
                raise ValueError(f"Invalid file or drawing offsets for sprite {index} in {offsets_path}")
            if not os.path.isfile(os.path.join(raw_dir, filename)):
                raise FileNotFoundError(f"Missing exported sprite {index}: {os.path.join(raw_dir, filename)}")
            views.append((filename, {
                "rotation": rotation,
                "source_index": index,
                "file": f"view-{rotation:02d}.png",
                "x": sprite["x"],
                "y": sprite["y"],
            }))

        references = {
            "object_id": RideVehicles.OBJECT_ID,
            "source_dat": RideVehicles.DAT_NAME,
            "car_type": 0,
            "sprite_group": "slopeFlat",
            "swing_frame": 0,
            "riders": False,
            "views": [view for _, view in views],
        }
        os.makedirs(output_dir, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix=".mine-cars-", dir=output_dir) as staging:
            for source_file, view in views:
                shutil.copyfile(os.path.join(raw_dir, source_file), os.path.join(staging, view["file"]))
            sidecar = "reference_views.json"
            with open(os.path.join(staging, sidecar), "w", encoding="utf-8") as destination:
                json.dump(references, destination, indent=2)
            for filename in [view["file"] for _, view in views] + [sidecar]:
                os.replace(os.path.join(staging, filename), os.path.join(output_dir, filename))

        print(f"Exported {len(views)} empty Mine Cars reference views to {output_dir}.")

    @staticmethod
    def _validate_layout(metadata, metadata_path):
        if not isinstance(metadata, dict):
            raise ValueError(f"Invalid ride metadata: {metadata_path}")
        properties = metadata.get("properties")
        if not isinstance(properties, dict):
            raise ValueError(f"Missing ride properties: {metadata_path}")
        cars = properties.get("cars")
        car = cars[0] if isinstance(cars, list) and cars else None
        if not isinstance(car, dict):
            raise ValueError(f"Missing car type 0: {metadata_path}")
        frames = car.get("frames")
        if (
            metadata.get("id") != RideVehicles.OBJECT_ID
            or metadata.get("objectType") != "ride"
            or properties.get("type") != "wooden_wild_mouse"
            or metadata.get("images") != ["$RCT2:OBJDATA/WMMINE.DAT[0..1106]", ""]
            or not isinstance(frames, dict) or frames.get("flat") is not True
            or car.get("rotationFrameMask") != 31
            or car.get("hasSwinging") is not True
            or car.get("useWoodenWildMouseSwing") is not True
            or "spriteGroups" in car
            or any(car.get(flag) for flag in (
                "use16RotationFrames", "overrideNumberOfVerticalFrames",
                "hasSpinningCombinedWithNonSpinning", "hasVehicleAnimation",
                "useSuspendedSwing", "useSlideSwing",
            ))
        ):
            raise ValueError(f"Unsupported Mine Cars sprite layout: {metadata_path}")