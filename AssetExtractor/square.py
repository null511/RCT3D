from PIL.ImageFile import ImageFile
from PIL import Image

def isometric_to_square(iso_img: ImageFile, output_path, target_dim = 32):
    """
    Extracts a pixel-perfect, centered, and correctly oriented 32x32 native square texture out of the 64x63 RCT isometric diamond sprite.
    """

    iso_img2 = iso_img.convert("RGBA")

    output_img = Image.new("RGBA", (target_dim, target_dim), (0, 0, 0, 0))

    # Center anchors of the source 32x31 sprite
    center_x = 31
    center_y = 0

    # Direct structural coordinate mapping loop
    for u in range(target_dim):
        for v in range(target_dim):
            # Calculate the exact pixel origin inside the diamond space
            src_x = center_x + (v - u)
            src_y = center_y + (u + v)
            
            # Double-check array boundaries
            if 0 <= src_x < 64 and 0 <= src_y < 63:
                raw_color = iso_img2.getpixel((src_x, src_y))
                
                if isinstance(raw_color, tuple) and len(raw_color) >= 3:
                    output_img.putpixel((u, v), raw_color)

    # Save finalized square asset
    output_img.save(output_path)
    print(f" [+] Generated square texture asset: {output_path}")


def flat_path_to_square(iso_img: Image.Image, x_offset: int, y_offset: int):
    source = iso_img.convert("RGBA")
    output = Image.new("RGBA", (32, 32))

    for u in range(32):
        for v in range(32):
            # Match OpenRCT2's 1:2 projection and align alternating output columns.
            sample_v = max(0, v - (u % 2))
            x = sample_v - u - x_offset
            y = (u + sample_v) // 2 - y_offset

            if 0 <= x < source.width and 0 <= y < source.height:
                output.putpixel((u, v), source.getpixel((x, y)))

    return output


def isometric_side_to_square(iso_img: Image.Image, output_path):
    if iso_img.size != (32, 31):
        raise ValueError(f"Expected a 32x31 terrain side sprite, got {iso_img.size}")

    source = iso_img.convert("RGBA")
    staging = Image.new("RGBA", (32, 16))
    for x in range(32):
        top = 15 - x // 2
        staging.paste(source.crop((x, top, x + 1, top + 16)), (x, 0))

    output_img = Image.new("RGBA", (32, 32))
    output_img.paste(staging, (0, 0))
    output_img.paste(staging, (0, 16))
    output_img.save(output_path)
    print(f" [+] Generated square texture asset: {output_path}")
