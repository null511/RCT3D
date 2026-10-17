from typing import cast

from PIL.ImageFile import ImageFile
from PIL import Image

def isometric_to_square(iso_img: ImageFile, output_path, target_dim = 16):
    """
    Extracts a pixel-perfect, centered, and correctly oriented 
    16x16 native square texture out of the 32x31 RCT isometric diamond sprite.
    """
    width, height = iso_img.size

    # CRITICAL STRUCTURAL FILTER: 
    if width != 32 or height != 31:
        # os.remove(raw_png)
        return

    iso_img2 = iso_img.convert("RGBA")
    pixels = iso_img2.load()

    output_img = Image.new("RGBA", (target_dim, target_dim), (0, 0, 0, 0))
    # out_pixels = output_img.load()

    # Center anchors of the source 32x31 sprite
    center_x = 15
    center_y = 0

    # Direct structural coordinate mapping loop
    for u in range(target_dim):
        for v in range(target_dim):
            # Calculate the exact pixel origin inside the diamond space
            src_x = center_x + (v - u)
            src_y = center_y + (u + v)
            
            # Double-check array boundaries
            if 0 <= src_x < 32 and 0 <= src_y < 31:
                raw_color = iso_img2.getpixel((src_x, src_y))
                
                if isinstance(raw_color, tuple) and len(raw_color) >= 3:
                    output_img.putpixel((u, v), raw_color)

    # Save finalized square asset
    output_img.save(output_path)
    print(f" [+] Generated square texture asset: {output_path}")
