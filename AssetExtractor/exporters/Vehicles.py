import json
import os
import shutil
import sys
import tempfile
import zipfile

import configuration
from exporter import Exporter


class Vehicles:
    RIDE_TYPE_IMAGE_COUNT = 3
    DIRECTIONS = (0, 1, 2, 3)
    SPRITE_GROUPS = (
        ("slopeFlat", 1), ("slopes12", 2), ("slopes25", 2), ("slopes42", 2),
        ("slopes60", 2), ("slopes75", 2), ("slopes90", 2), ("slopesLoop", 10),
        ("slopeInverted", 1), ("slopes8", 2), ("slopes16", 2), ("slopes50", 2),
        ("flatBanked22", 2), ("flatBanked45", 2), ("flatBanked67", 2), ("flatBanked90", 2),
        ("inlineTwists", 6), ("slopes12Banked22", 4), ("slopes8Banked22", 4),
        ("slopes25Banked22", 4), ("slopes8Banked45", 4), ("slopes16Banked22", 4),
        ("slopes16Banked45", 4), ("slopes25Banked45", 4), ("slopes12Banked45", 4),
        ("slopes25Banked67", 4), ("slopes25Banked90", 4), ("slopes25InlineTwists", 12),
        ("slopes42Banked22", 4), ("slopes42Banked45", 4), ("slopes42Banked67", 4),
        ("slopes42Banked90", 4), ("slopes60Banked22", 4), ("slopes50Banked45", 4),
        ("slopes50Banked67", 4), ("slopes50Banked90", 4), ("corkscrews", 20),
        ("restraintAnimation", 3), ("curvedLiftHillUp", 1), ("curvedLiftHillDown", 1),
    )

    @staticmethod
    def export_vehicle(object_path, metadata_path=None):
        try:
            Vehicles._export_vehicle(object_path, metadata_path)
        except (OSError, ValueError, KeyError, TypeError, zipfile.BadZipFile) as error:
            print(f" [!] Vehicle sprite export failed: {error}", file=sys.stderr)
            return False
        return True

    @staticmethod
    def _export_vehicle(object_path, metadata_path):
        if not os.path.isfile(configuration.OPENRCT2_BIN):
            raise FileNotFoundError(f"Required file not found: {configuration.OPENRCT2_BIN}")
        if not os.path.isfile(object_path):
            raise FileNotFoundError(f"Required file not found: {object_path}")

        if metadata_path is None:
            if not zipfile.is_zipfile(object_path):
                raise ValueError("A metadata path is required when the vehicle source is not a .parkobj file")
            with zipfile.ZipFile(object_path) as archive:
                with archive.open("object.json") as source:
                    metadata = json.load(source)
        else:
            if not os.path.isfile(metadata_path):
                raise FileNotFoundError(f"Required file not found: {metadata_path}")
            with open(metadata_path, "r", encoding="utf-8") as source:
                metadata = json.load(source)

        object_id, views = Vehicles._get_primary_views(metadata)
        raw_dir = os.path.join(
            configuration.RAW_DIR, "Vehicles", object_id, os.path.splitext(os.path.basename(object_path))[0],
        )
        output_dir = os.path.join(configuration.OUTPUT_DIR, "vehicles", object_id)
        os.makedirs(raw_dir, exist_ok=True)
        print(f"Extracting {object_id} from {object_path}...")
        if not Exporter.export_object(object_path, raw_dir):
            raise ValueError(f"OpenRCT2 could not export {object_path} into {raw_dir}")

        offsets_path = os.path.join(raw_dir, "sprite_offsets.json")
        with open(offsets_path, "r", encoding="utf-8") as source:
            offsets = json.load(source)
        if not isinstance(offsets, dict):
            raise ValueError(f"Invalid sprite offsets: {offsets_path}")

        for view in views:
            sprite = offsets.get(str(view["source_index"]))
            if not isinstance(sprite, dict):
                raise ValueError(f"Missing sprite {view['source_index']} in {offsets_path}")
            filename = sprite.get("file")
            if (
                not isinstance(filename, str) or not filename
                or os.path.basename(filename) != filename
                or type(sprite.get("x")) is not int or type(sprite.get("y")) is not int
            ):
                raise ValueError(f"Invalid sprite {view['source_index']} in {offsets_path}")
            source_file = os.path.join(raw_dir, filename)
            if not os.path.isfile(source_file):
                raise FileNotFoundError(f"Missing exported sprite {view['source_index']}: {source_file}")
            view["source_file"] = filename
            view["x"] = sprite["x"]
            view["y"] = sprite["y"]

        references = {
            "object_id": object_id,
            "source_object": os.path.basename(object_path),
            "sprite_group": "slopeFlat",
            "views": views,
        }
        os.makedirs(output_dir, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix=".vehicle-sprites-", dir=output_dir) as staging:
            for view in views:
                shutil.copyfile(
                    os.path.join(raw_dir, view["source_file"]),
                    os.path.join(staging, view["file"]),
                )
            sidecar = "reference_views.json"
            with open(os.path.join(staging, sidecar), "w", encoding="utf-8") as destination:
                json.dump(references, destination, indent=2)
            for filename in [view["file"] for view in views] + [sidecar]:
                os.replace(os.path.join(staging, filename), os.path.join(output_dir, filename))

        print(f"Exported {len(views)} primary vehicle views to {output_dir}.")

    @staticmethod
    def _get_primary_views(metadata):
        if not isinstance(metadata, dict) or metadata.get("objectType") != "ride":
            raise ValueError("Vehicle metadata must describe a ride object")
        object_id = metadata.get("id")
        if (
            not isinstance(object_id, str) or not object_id
            or os.path.basename(object_id) != object_id
            or any(character in object_id for character in '<>:"/\\|?*')
        ):
            raise ValueError("Vehicle metadata has an invalid object id")

        properties = metadata.get("properties")
        cars = properties.get("cars") if isinstance(properties, dict) else None
        if isinstance(cars, dict):
            cars = [cars]
        if not isinstance(cars, list) or not cars:
            raise ValueError("Vehicle metadata must contain at least one car type")

        views = []
        car_image_index = Vehicles.RIDE_TYPE_IMAGE_COUNT
        for car_index, car in enumerate(cars):
            if not isinstance(car, dict):
                raise ValueError(f"Invalid car type {car_index} in vehicle metadata")
            groups = Vehicles._get_sprite_groups(car)
            rotations = groups.get("slopeFlat", 0)
            if rotations == 0:
                continue

            base_frames = Vehicles._get_base_frame_count(car)
            for direction in Vehicles.DIRECTIONS:
                source_index = car_image_index + (direction * rotations // 4) * base_frames
                views.append({
                    "car_type": car_index,
                    "direction": direction,
                    "source_index": source_index,
                    "file": f"car-{car_index:02d}-view-{direction:02d}.png",
                })

            car_images = base_frames * sum(
                groups.get(group_name, 0) * multiplier
                for group_name, multiplier in Vehicles.SPRITE_GROUPS
            )
            car_image_index += car_images * (1 + Vehicles._get_seat_row_count(car))

        if not views:
            raise ValueError("Vehicle metadata does not define any flat car sprite directions")
        return object_id, views

    @staticmethod
    def _get_sprite_groups(car):
        groups = car.get("spriteGroups")
        if isinstance(groups, dict):
            result = {}
            known_groups = {name for name, _ in Vehicles.SPRITE_GROUPS}
            for group_name, rotation_count in groups.items():
                if group_name not in known_groups:
                    raise ValueError(f"Unsupported sprite group {group_name}")
                if type(rotation_count) is not int or rotation_count < 0:
                    raise ValueError(f"Invalid rotation count for sprite group {group_name}")
                if rotation_count and rotation_count & (rotation_count - 1):
                    raise ValueError(f"Sprite group {group_name} rotation count must be a power of two")
                result[group_name] = rotation_count
            return result

        frames = car.get("frames")
        if not isinstance(frames, dict):
            raise ValueError("Car metadata must define spriteGroups or legacy frames")

        flags = car
        precision = 4 if frames.get("VEHICLE_SPRITE_FLAG_15") else 16 if flags.get("use16RotationFrames") else 32
        result = {}
        if frames.get("flat"):
            result["slopeFlat"] = precision
        if frames.get("gentleSlopes"):
            result["slopes12"] = 4
            result["slopes25"] = 4 if flags.get("hasAdditionalSpinningFrames") else precision
        if frames.get("steepSlopes"):
            result["slopes42"] = 8
            result["slopes60"] = precision
        if frames.get("verticalSlopes"):
            result.update({"slopes75": 4, "slopes90": precision, "slopesLoop": 4, "slopeInverted": 4})
        if frames.get("diagonalSlopes"):
            result.update({"slopes8": 4, "slopes16": 4, "slopes50": 4})
        if frames.get("flatBanked"):
            result.update({"flatBanked22": 8, "flatBanked45": precision})
        if frames.get("inlineTwists"):
            result.update({"flatBanked67": 4, "flatBanked90": 4, "inlineTwists": 4})
        if frames.get("flatToGentleSlopeBankedTransitions"):
            result["slopes12Banked22"] = precision
        if frames.get("diagonalGentleSlopeBankedTransitions"):
            result["slopes8Banked22"] = 4
        if frames.get("gentleSlopeBankedTransitions"):
            result["slopes25Banked22"] = 4
        if frames.get("gentleSlopeBankedTurns"):
            result["slopes25Banked45"] = precision
        if frames.get("flatToGentleSlopeWhileBankedTransitions"):
            result["slopes12Banked45"] = 4
        if frames.get("corkscrews"):
            result["corkscrews"] = 4
        if frames.get("restraintAnimation"):
            result["restraintAnimation"] = 4
        if frames.get("curvedLiftHill"):
            result["curvedLiftHillUp"] = precision
        return result

    @staticmethod
    def _get_base_frame_count(car):
        if car.get("overrideNumberOfVerticalFrames"):
            vertical_frames = car.get("numVerticalFramesOverride")
            if type(vertical_frames) is not int or vertical_frames < 1:
                raise ValueError("Invalid numVerticalFramesOverride in car metadata")
        elif car.get("hasAdditionalSpinningFrames"):
            vertical_frames = car.get("spinningNumFrames") or 32
        elif car.get("hasVehicleAnimation"):
            animation = car.get("animation")
            animation_type = animation.get("animationType") if isinstance(animation, dict) else animation
            vertical_frames = 1 if animation_type in ("observationTower", 6) else 2 if car.get("hasDodgemInUseLights") else 4
        else:
            vertical_frames = 1

        if not car.get("hasSwinging"):
            horizontal_frames = 1
        elif car.get("useSuspendedSwing") and car.get("useSlideSwing"):
            horizontal_frames = 13
        elif car.get("useSuspendedSwing") or car.get("useSlideSwing"):
            horizontal_frames = 7
        elif car.get("useWoodenWildMouseSwing"):
            horizontal_frames = 3
        else:
            horizontal_frames = 5
        return vertical_frames * horizontal_frames

    @staticmethod
    def _get_seat_row_count(car):
        rows = car.get("numSeatRows", 0)
        if type(rows) is not int or rows < 0:
            raise ValueError("Invalid numSeatRows in car metadata")
        return rows
