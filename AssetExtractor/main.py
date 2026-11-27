import argparse
import configuration
import os
import sys

from exporters.Paths import Paths
from exporters.RideStations import RideStations
from exporters.Terrain import Terrain
from exporters.Vegetation import Vegetation
from exporters.Vehicles import Vehicles
from exporter import Exporter


def extract_all():
    # Extract game sprites
    print("Extracting sprites...")

    if not os.path.exists(configuration.RAW_DIR):
        os.makedirs(configuration.RAW_DIR)

    extract_g1()

    print("\nExtraction complete.")

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
        
def main():
    parser = argparse.ArgumentParser(description="Extract and process game sprites.")
    exports = parser.add_mutually_exclusive_group()

    exports.add_argument(
        "--extract-all",
        help="Extract all assets",
    )

    exports.add_argument(
        "--export-terrain",
        action="store_true",
        help="Export terrain surfaces",
    )

    exports.add_argument(
        "--export-terrain-sides",
        action="store_true",
        help="Export terrain sides",
    )

    exports.add_argument(
        "--export-paths",
        action="store_true",
        help="Export raw and square footpath surface sprites.",
    )

    exports.add_argument(
        "--export-vegetation",
        help="Export vegetation",
    )

    exports.add_argument(
        "--export-vehicle", metavar="OBJECT_PATH",
        help="Export the four primary flat-track views for every car in a ride object.",
    )

    exports.add_argument(
        "--export-station", choices=("plain",),
        help="Export plain ride entrance and exit sprite layers.",
    )

    parser.add_argument(
        "--vehicle-metadata", metavar="METADATA_PATH",
        help="Ride object JSON metadata when the vehicle source is not a .parkobj file.",
    )

    args = parser.parse_args()

    if args.vehicle_metadata and not args.export_vehicle:
        parser.error("--vehicle-metadata requires --export-vehicle")

    if args.extract_all:
        return 0 if extract_all() else 1

    if args.export_terrain_sides:
        return 0 if Terrain.export_all_sides() else 1

    if args.export_terrain:
        return 0 if Terrain.export_all() else 1

    if args.export_paths:
        return 0 if Paths.export_all() else 1

    if args.export_vegetation:
        return 0 if Vegetation.run() else 1

    if args.export_vehicle:
        return 0 if Vehicles.export_vehicle(args.export_vehicle, args.vehicle_metadata) else 1

    if args.export_station == "plain":
        return 0 if RideStations.export_plain() else 1

    print("\nHello World!")
    return 0


if __name__ == "__main__":
    sys.exit(main())
