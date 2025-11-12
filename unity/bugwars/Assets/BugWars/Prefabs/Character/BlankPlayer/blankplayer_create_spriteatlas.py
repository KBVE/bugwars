#!/usr/bin/env python3
"""
Sprite Sheet Generator for BlankPlayer Character
Combines 4-directional PNG sprite strips into a single optimized sprite sheet
Generates JSON atlas with frame data for 4-directional animation

This character supports 4 directions: Down, Left, Right, Up
Each sprite strip follows pattern: Sword_{Action}_{Direction}_full.png
"""

import os
import json
import math
from PIL import Image
from typing import Dict, List, Tuple

class BlankPlayerSpriteSheetGenerator:
    def __init__(self, input_dir: str, output_name: str = "BlankPlayerAtlas"):
        self.input_dir = input_dir
        self.output_name = output_name
        self.frame_size = 64  # BlankPlayer uses 64x64 frames
        self.frames = []
        self.animations = {}

        # 4 directional animations - each sprite sheet has 4 rows
        # Row 0 (top): Down, Row 1: Left, Row 2: Right, Row 3 (bottom): Up
        self.directions = ['Down', 'Left', 'Right', 'Up']

    def extract_frames(self) -> None:
        """Extract individual frames from 4-directional sprite grid PNGs
        Each sprite sheet has 4 rows (Down, Left, Right, Up) and N columns (frames)
        """
        print("Extracting frames from 4-directional sprite grids...")

        # Get all PNG files that match the pattern: Sword_{Action}_full.png
        png_files = sorted([f for f in os.listdir(self.input_dir)
                          if f.endswith('_full.png') and not f.startswith(self.output_name)])

        for png_file in png_files:
            filepath = os.path.join(self.input_dir, png_file)
            img = Image.open(filepath)

            # Get dimensions
            width, height = img.size
            num_frames_per_row = width // self.frame_size
            num_rows = height // self.frame_size

            # Parse filename: Sword_{Action}_full.png
            # Example: Sword_Attack_full.png -> Action=Attack
            filename_parts = os.path.splitext(png_file)[0].split('_')

            # Extract action (remove 'Sword' prefix and 'full' suffix)
            # Handle compound actions like "Run_Attack"
            if filename_parts[0] == 'Sword':
                # Join all parts between 'Sword' and 'full'
                action_parts = filename_parts[1:-1]
                action = '_'.join(action_parts)
            else:
                continue  # Skip files that don't match pattern

            print(f"\n  {action}: {num_frames_per_row} frames x {num_rows} directions")

            # Extract frames from each row (direction)
            for row_idx, direction in enumerate(self.directions[:num_rows]):
                anim_name = f"{action}_{direction}"
                frame_list = []

                print(f"    - {anim_name}: {num_frames_per_row} frames")

                for col_idx in range(num_frames_per_row):
                    x = col_idx * self.frame_size
                    y = row_idx * self.frame_size

                    # Crop individual frame from grid
                    frame = img.crop((x, y, x + self.frame_size, y + self.frame_size))

                    frame_data = {
                        'image': frame,
                        'name': f"{anim_name}_{col_idx}",
                        'animation': anim_name,
                        'action': action,
                        'direction': direction,
                        'index': col_idx
                    }

                    self.frames.append(frame_data)
                    frame_list.append(f"{anim_name}_{col_idx}")

                # Store animation metadata for this direction
                self.animations[anim_name] = {
                    'frames': frame_list,
                    'frameCount': num_frames_per_row,
                    'fps': self._get_animation_fps(action),
                    'action': action,
                    'direction': direction
                }

    def _get_animation_fps(self, action: str) -> int:
        """Determine appropriate FPS for each animation action"""
        fps_map = {
            'Idle': 8,
            'Walk': 12,
            'Run': 15,
            'Attack': 15,
            'Run_Attack': 15,
            'Walk_Attack': 15,
            'Hurt': 12,
            'Death': 8
        }
        return fps_map.get(action, 10)

    def pack_spritesheet(self, max_width: int = 2048) -> Tuple[Image.Image, Dict]:
        """Pack frames into optimized sprite sheet and generate atlas data"""
        print(f"\nPacking {len(self.frames)} frames into sprite sheet...")

        num_frames = len(self.frames)
        if num_frames == 0:
            raise ValueError("No frames found to pack!")

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
                'frameCount': num_frames,
                'characterType': '4-directional',
                'directions': self.directions
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
            # Unity uses bottom-left origin for UVs, so flip Y coordinate
            uv_y_min = 1.0 - ((y + self.frame_size) / sheet_height)
            uv_y_max = 1.0 - (y / sheet_height)

            atlas['frames'][frame_data['name']] = {
                'x': x,
                'y': y,
                'w': self.frame_size,
                'h': self.frame_size,
                'animation': frame_data['animation'],
                'action': frame_data['action'],
                'direction': frame_data['direction'],
                'index': frame_data['index'],
                # Normalized UV coordinates (0-1 range) - Unity bottom-left origin
                'uv': {
                    'min': {'x': x / sheet_width, 'y': uv_y_min},
                    'max': {'x': (x + self.frame_size) / sheet_width, 'y': uv_y_max}
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
        print("BlankPlayer 4-Directional Sprite Sheet Generator")
        print("=" * 60)

        self.extract_frames()

        if len(self.frames) == 0:
            print("\nERROR: No frames found!")
            print("Please ensure PNG files follow the pattern:")
            print("  Sword_{Action}_full.png")
            print("  Examples: Sword_Attack_full.png, Sword_Walk_full.png")
            print("\nEach sprite sheet should be a grid:")
            print("  - 4 rows (Down, Left, Right, Up)")
            print("  - N columns (animation frames)")
            print("  - Frame size: 64x64 pixels")
            return

        sprite_sheet, atlas = self.pack_spritesheet()
        self.save(sprite_sheet, atlas)

        print("\n" + "=" * 60)
        print("Generation complete!")
        print("=" * 60)
        print(f"\nTotal frames: {len(self.frames)}")
        print(f"Animations: {len(self.animations)}")

        # Group animations by action for better readability
        actions = {}
        for anim_name, data in self.animations.items():
            action = data['action']
            if action not in actions:
                actions[action] = []
            actions[action].append({
                'name': anim_name,
                'frameCount': data['frameCount'],
                'fps': data['fps'],
                'direction': data['direction']
            })

        for action, anims in sorted(actions.items()):
            print(f"\n  {action}:")
            for anim in sorted(anims, key=lambda x: x['direction']):
                print(f"    - {anim['name']}: {anim['frameCount']} frames @ {anim['fps']} fps")

if __name__ == "__main__":
    # Run from the BlankPlayer directory
    current_dir = os.path.dirname(os.path.abspath(__file__))

    generator = BlankPlayerSpriteSheetGenerator(current_dir, "BlankPlayerAtlas")
    generator.generate()
