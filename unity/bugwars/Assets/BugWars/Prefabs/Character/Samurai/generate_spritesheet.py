#!/usr/bin/env python3
"""
Sprite Sheet Generator for Samurai Character
Combines individual PNG sprite strips into a single optimized sprite sheet
Generates JSON atlas with frame data for animation
"""

import os
import json
import math
from PIL import Image
from typing import Dict, List, Tuple

class SpriteSheetGenerator:
    def __init__(self, input_dir: str, output_name: str = "SamuraiAtlas"):
        self.input_dir = input_dir
        self.output_name = output_name
        self.frame_size = 128  # All sprites are 128x128
        self.frames = []
        self.animations = {}

    def extract_frames(self) -> None:
        """Extract individual frames from sprite strip PNGs"""
        print("Extracting frames from sprite strips...")

        png_files = sorted([f for f in os.listdir(self.input_dir) if f.endswith('.png')])

        for png_file in png_files:
            filepath = os.path.join(self.input_dir, png_file)
            img = Image.open(filepath)

            # Calculate number of frames
            width, height = img.size
            num_frames = width // self.frame_size

            # Animation name from filename (remove .png)
            anim_name = os.path.splitext(png_file)[0]
            frame_list = []

            print(f"  {anim_name}: {num_frames} frames")

            # Extract each frame
            for i in range(num_frames):
                x = i * self.frame_size
                frame = img.crop((x, 0, x + self.frame_size, self.frame_size))

                frame_data = {
                    'image': frame,
                    'name': f"{anim_name}_{i}",
                    'animation': anim_name,
                    'index': i
                }

                self.frames.append(frame_data)
                frame_list.append(f"{anim_name}_{i}")

            # Store animation metadata
            self.animations[anim_name] = {
                'frames': frame_list,
                'frameCount': num_frames,
                'fps': self._get_animation_fps(anim_name)
            }

    def _get_animation_fps(self, anim_name: str) -> int:
        """Determine appropriate FPS for each animation"""
        fps_map = {
            'Idle': 8,
            'Walk': 12,
            'Run': 15,
            'Jump': 10,
            'Attack_1': 15,
            'Attack_2': 15,
            'Attack_3': 15,
            'Shield': 6,
            'Hurt': 12,
            'Dead': 8
        }
        return fps_map.get(anim_name, 10)

    def pack_spritesheet(self, max_width: int = 2048) -> Tuple[Image.Image, Dict]:
        """Pack frames into optimized sprite sheet and generate atlas data"""
        print(f"\nPacking {len(self.frames)} frames into sprite sheet...")

        num_frames = len(self.frames)
        frames_per_row = max_width // self.frame_size
        num_rows = math.ceil(num_frames / frames_per_row)

        # Calculate actual dimensions
        actual_cols = min(frames_per_row, num_frames)
        sheet_width = actual_cols * self.frame_size
        sheet_height = num_rows * self.frame_size

        print(f"  Sprite sheet size: {sheet_width}x{sheet_height}")
        print(f"  Grid: {actual_cols} cols x {num_rows} rows")

        # Create sprite sheet
        sprite_sheet = Image.new('RGBA', (sheet_width, sheet_height), (0, 0, 0, 0))

        # Atlas data
        atlas = {
            'meta': {
                'version': '1.0',
                'size': {'w': sheet_width, 'h': sheet_height},
                'frameSize': self.frame_size,
                'frameCount': num_frames
            },
            'frames': {},
            'animations': self.animations
        }

        # Place frames
        for idx, frame_data in enumerate(self.frames):
            row = idx // frames_per_row
            col = idx % frames_per_row

            x = col * self.frame_size
            y = row * self.frame_size

            # Paste frame
            sprite_sheet.paste(frame_data['image'], (x, y))

            # Store frame position in atlas
            atlas['frames'][frame_data['name']] = {
                'x': x,
                'y': y,
                'w': self.frame_size,
                'h': self.frame_size,
                'animation': frame_data['animation'],
                'index': frame_data['index'],
                # Normalized UV coordinates (0-1 range)
                'uv': {
                    'min': {'x': x / sheet_width, 'y': y / sheet_height},
                    'max': {'x': (x + self.frame_size) / sheet_width, 'y': (y + self.frame_size) / sheet_height}
                }
            }

        return sprite_sheet, atlas

    def save(self, sprite_sheet: Image.Image, atlas: Dict) -> None:
        """Save sprite sheet and JSON atlas"""
        # Save PNG
        png_path = os.path.join(self.input_dir, f"{self.output_name}.png")
        sprite_sheet.save(png_path, 'PNG')
        print(f"\n✓ Sprite sheet saved: {png_path}")

        # Save JSON
        json_path = os.path.join(self.input_dir, f"{self.output_name}.json")
        with open(json_path, 'w') as f:
            json.dump(atlas, f, indent=2)
        print(f"✓ Atlas JSON saved: {json_path}")

    def generate(self) -> None:
        """Main generation workflow"""
        print("=" * 60)
        print("Samurai Sprite Sheet Generator")
        print("=" * 60)

        self.extract_frames()
        sprite_sheet, atlas = self.pack_spritesheet()
        self.save(sprite_sheet, atlas)

        print("\n" + "=" * 60)
        print("Generation complete!")
        print("=" * 60)
        print(f"\nTotal frames: {len(self.frames)}")
        print(f"Animations: {len(self.animations)}")
        for anim, data in self.animations.items():
            print(f"  - {anim}: {data['frameCount']} frames @ {data['fps']} fps")

if __name__ == "__main__":
    # Run from the Samurai directory
    current_dir = os.path.dirname(os.path.abspath(__file__))

    generator = SpriteSheetGenerator(current_dir, "SamuraiAtlas")
    generator.generate()
