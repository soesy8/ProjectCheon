"""정면 레퍼런스의 기준점에 맞춰 측면 이미지를 픽셀 단위로 보정한다."""

import json
import struct
import zlib
from pathlib import Path

import numpy as np


class PixelReferenceAlignment:
    SOURCE_FOLDER = Path(r"E:\C.J.I\ReferenceImage\PlayerCharacter")
    WORK_FOLDER = Path(r"D:\project\ProjectCheon\Assets\Temp\BlenderReferenceAlignment")
    LANDMARKS = [
        ("chin", "skin", (500, 270, 520, 310), (555, 270, 576, 310), "last"),
        ("shoulder", "ivory", (380, 325, 412, 400), (455, 325, 500, 400), "first"),
        ("belt_top", "gold", (440, 465, 470, 525), (535, 465, 560, 530), "first"),
        ("belt_bottom", "gold", (440, 465, 470, 525), (535, 465, 560, 530), "last"),
        ("fingers", "skin", (255, 570, 320, 690), (435, 565, 510, 690), "last"),
        ("bag", "brown", (585, 530, 646, 645), (600, 530, 638, 645), "last"),
        ("short_sash", "gold", (535, 640, 542, 685), (584, 640, 592, 695), "last"),
        ("long_sash", "gold", (554, 685, 592, 745), (604, 690, 625, 750), "last"),
        ("hem", "teal", (310, 750, 705, 855), (370, 750, 650, 860), "last"),
        ("ankle_top", "ivory", (396, 906, 445, 935), (465, 906, 523, 935), "first"),
        ("sole", "ivory", (350, 950, 458, 1000), (452, 950, 583, 1000), "last"),
    ]

    @staticmethod
    def read_png(path):
        """별도 패키지 설치 없이 8비트 RGB/RGBA PNG의 원래 색 값을 읽는다."""
        content = Path(path).read_bytes()
        assert content[:8] == b"\x89PNG\r\n\x1a\n"
        position = 8
        compressed = bytearray()
        while position < len(content):
            length = struct.unpack(">I", content[position:position + 4])[0]
            kind = content[position + 4:position + 8]
            payload = content[position + 8:position + 8 + length]
            if kind == b"IHDR":
                width, height, depth, color, _, _, interlace = struct.unpack(">IIBBBBB", payload)
                assert depth == 8 and color in (2, 6) and interlace == 0
                channels = 3 if color == 2 else 4
            elif kind == b"IDAT":
                compressed.extend(payload)
            position += length + 12
        decoded = zlib.decompress(compressed)
        row_size = width * channels
        pixels = np.zeros((height, row_size), dtype=np.uint8)
        for y in range(height):
            start = y * (row_size + 1)
            filter_type = decoded[start]
            row = np.frombuffer(decoded, dtype=np.uint8, count=row_size, offset=start + 1).astype(np.int32)
            above = pixels[y - 1].astype(np.int32) if y else np.zeros(row_size, dtype=np.int32)
            if filter_type == 1:
                row = np.cumsum(row.reshape(width, channels), axis=0).reshape(-1) % 256
            elif filter_type == 2:
                row = (row + above) % 256
            elif filter_type in (3, 4):
                for x in range(row_size):
                    left = int(row[x - channels]) if x >= channels else 0
                    up = int(above[x])
                    upper_left = int(above[x - channels]) if x >= channels else 0
                    if filter_type == 3:
                        predictor = (left + up) // 2
                    else:
                        estimate = left + up - upper_left
                        distances = (abs(estimate - left), abs(estimate - up), abs(estimate - upper_left))
                        predictor = (left, up, upper_left)[distances.index(min(distances))]
                    row[x] = (int(row[x]) + predictor) % 256
            else:
                assert filter_type == 0
            pixels[y] = row.astype(np.uint8)
        return pixels.reshape(height, width, channels)

    @staticmethod
    def write_png(path, pixels):
        pixels = np.asarray(pixels, dtype=np.uint8)
        height, width, channels = pixels.shape
        assert channels in (3, 4)

        def chunk(kind, payload):
            return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF)

        header = struct.pack(">IIBBBBB", width, height, 8, 2 if channels == 3 else 6, 0, 0, 0)
        rows = b"".join(b"\0" + row.tobytes() for row in pixels)
        content = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header)
        content += chunk(b"sRGB", b"\0") + chunk(b"IDAT", zlib.compress(rows, 6)) + chunk(b"IEND", b"")
        Path(path).write_bytes(content)

    @staticmethod
    def color_mask(pixels, kind):
        red, green, blue = pixels[..., :3].astype(np.float32).transpose(2, 0, 1)
        if kind == "skin":
            return (red > 180) & (green > 100) & (green < 225) & (blue < green) & (red - green > 35)
        if kind == "teal":
            return (green > 90) & (red < green * 0.65) & (blue > green * 0.8)
        if kind == "gold":
            return (red > 170) & (green > 100) & (green < 215) & (blue < 105) & (red > green * 1.10)
        if kind == "brown":
            return (red > 70) & (red < 200) & (green > 40) & (green < 135) & (red > green * 1.22) & (blue > green * 0.53) & (blue < green * 0.86)
        if kind == "ivory":
            return (red > 180) & (green > 170) & (blue > 130) & (red > blue * 1.06)
        if kind == "hat":
            return (red < 100) & (green < 100) & (blue < 100)
        raise ValueError(kind)

    @classmethod
    def measure_region(cls, pixels, kind, bounds):
        left, top, right, bottom = bounds
        mask = cls.color_mask(pixels, kind)[top:bottom, left:right]
        y_positions, x_positions = np.nonzero(mask)
        return {
            "first_y": int(y_positions.min()) + top,
            "last_y": int(y_positions.max()) + top,
            "first_x": int(x_positions.min()) + left,
            "last_x": int(x_positions.max()) + left,
            "row_counts": {str(y + top): int(mask[y].sum()) for y in range(mask.shape[0]) if mask[y].any()},
        }

    @classmethod
    def align_pixels(cls):
        front = cls.read_png(cls.SOURCE_FOLDER / "T_PlayerCharacter_Front.png")
        side = cls.read_png(cls.SOURCE_FOLDER / "T_PlayerCharacter_Side.png")
        height, width = side.shape[:2]

        # 전체 확대 대신 구간 사이를 보간하여, 조절한 기준점 외의 형태 변화는 작게 유지한다.
        source_heights = np.array([0, 7, 96, 181, 296, 345, 482, 512, 573, 669, 835, 880, 917, 925, 988, 1023], dtype=float)
        target_heights = np.array([0, 7, 96, 190, 303, 353, 480, 509, 574, 668, 823, 880, 915, 924, 988, 1023], dtype=float)
        grid_y, grid_x = np.mgrid[:height, :width].astype(np.float64)
        source_y = np.interp(grid_y, target_heights, source_heights)

        # 가방과 띠 끝은 손과 같은 높이에 있어 전체 행을 이동할 수 없다.
        # 이 부위만 작은 범위의 부드러운 변형으로 맞추고, 손/허리/옷단은 고정한다.
        local_points = np.array([
            [601, 637, 627], [610, 637, 627], [622, 633, 623],
            [588, 665, 672], [598, 666, 674],
            [610, 724, 730], [615, 724, 730],
            [490, 668, 669], [545, 509, 512], [595, 509, 512],
            [600, 574, float(np.interp(574, target_heights, source_heights))],
            [500, 823, 835], [635, 823, 835],
        ], dtype=float)
        centers = local_points[:, :2]
        sigma_x, sigma_y = 28.0, 18.0
        distances = (centers[:, None, :] - centers[None, :, :]) / np.array([sigma_x, sigma_y])
        matrix = np.exp(-0.5 * np.sum(distances * distances, axis=2))
        deltas = local_points[:, 2] - np.interp(local_points[:, 1], target_heights, source_heights)
        amplitudes = np.linalg.solve(matrix, deltas)
        for (center_x, center_y), amplitude in zip(centers, amplitudes):
            source_y += amplitude * np.exp(-0.5 * (((grid_x - center_x) / sigma_x) ** 2 + ((grid_y - center_y) / sigma_y) ** 2))

        # 원형 갓의 지름도 정면 445px와 같게 맞춘다. 얼굴 아래로 갈수록 가로 보정은 없어진다.
        hat_weight = np.clip((315.0 - grid_y) / (315.0 - 190.0), 0, 1)
        hat_source_x = 510.5 + (grid_x - 509.0) * (431.0 / 444.0)
        source_x = grid_x + hat_weight * (hat_source_x - grid_x)
        source_x = np.clip(source_x, 0, width - 1)
        source_y = np.clip(source_y, 0, height - 1)

        # 역방향 샘플링은 빈 픽셀이 생기지 않으며, 원래의 RGB/알파 값을 직접 보간한다.
        left = np.floor(source_x).astype(np.int32)
        top = np.floor(source_y).astype(np.int32)
        right = np.minimum(left + 1, width - 1)
        bottom = np.minimum(top + 1, height - 1)
        horizontal = (source_x - left)[..., None]
        vertical = (source_y - top)[..., None]
        upper = side[top, left] * (1 - horizontal) + side[top, right] * horizontal
        lower = side[bottom, left] * (1 - horizontal) + side[bottom, right] * horizontal
        result = np.clip(np.rint(upper * (1 - vertical) + lower * vertical), 0, 255).astype(np.uint8)
        cls.write_png(cls.WORK_FOLDER / "T_PlayerCharacter_Side.png", result)

        # 모든 열에서 매핑이 단조 증가하는지 확인해 이미지가 접히거나 뒤집히는 것을 막는다.
        minimum_step = float(np.diff(source_y, axis=0).min())
        assert minimum_step > 0, f"Pixel mapping folds: {minimum_step}"
        report = {
            "method": "piecewise vertical registration plus localized deterministic pixel warp",
            "user_choice": "pixel-based exact correction",
            "source_heights": source_heights.tolist(),
            "target_heights": target_heights.tolist(),
            "local_registration_points": local_points.tolist(),
            "hat_front_diameter_pixels": 445,
            "hat_source_diameter_pixels": 432,
            "minimum_vertical_mapping_step": minimum_step,
            "output_size": [width, height],
            "front_unchanged": True,
        }
        (cls.WORK_FOLDER / "PixelAlignmentReport.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
        cls.verify(front, result)
        return front, result

    @classmethod
    def verify(cls, front, side):
        results = []
        for name, kind, front_bounds, side_bounds, edge in cls.LANDMARKS:
            heights = []
            for pixels, bounds in ((front, front_bounds), (side, side_bounds)):
                left, top, right, bottom = bounds
                region = cls.color_mask(pixels, kind)[top:bottom, left:right]
                # 한두 개의 압축/안티앨리어싱 픽셀 대신 최소 세 픽셀이 있는 실제 경계를 측정한다.
                rows = np.flatnonzero(region.sum(axis=1) >= 3)
                heights.append(int(rows[0 if edge == "first" else -1] + top))
            results.append({"name": name, "front_y": heights[0], "side_y": heights[1], "error_pixels": heights[1] - heights[0]})
        hat_bounds = (270, 0, 750, 195)
        front_hat = cls.measure_region(front, "hat", hat_bounds)
        side_hat = cls.measure_region(side, "hat", hat_bounds)
        results.append({"name": "hat_top", "front_y": front_hat["first_y"], "side_y": side_hat["first_y"], "error_pixels": side_hat["first_y"] - front_hat["first_y"]})
        hat_widths = [entry["last_x"] - entry["first_x"] + 1 for entry in (front_hat, side_hat)]
        results.append({"name": "hat_diameter", "front_pixels": hat_widths[0], "side_pixels": hat_widths[1], "error_pixels": hat_widths[1] - hat_widths[0]})
        (cls.WORK_FOLDER / "PixelVerification.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
        print(json.dumps(results, indent=2))
        maximum_error = max(abs(entry["error_pixels"]) for entry in results)
        assert maximum_error == 0, f"Registration requires refinement: {maximum_error}px"
        return results

    @classmethod
    def inspect(cls):
        front = cls.read_png(cls.SOURCE_FOLDER / "T_PlayerCharacter_Front.png")
        side = cls.read_png(cls.SOURCE_FOLDER / "T_PlayerCharacter_Side.png")
        regions = {
            "chin": ("skin", (490, 270, 530, 310), (545, 270, 580, 310)),
            "shoulder": ("ivory", (380, 325, 412, 400), (455, 325, 500, 400)),
            "belt": ("gold", (440, 465, 470, 525), (535, 465, 560, 530)),
            "hand": ("skin", (255, 570, 320, 690), (435, 565, 510, 690)),
            "bag": ("brown", (574, 525, 649, 652), (600, 515, 638, 652)),
            "short_sash": ("gold", (530, 640, 552, 685), (580, 640, 596, 695)),
            "long_sash": ("gold", (554, 685, 592, 745), (604, 690, 625, 750)),
            "hem": ("teal", (310, 750, 705, 855), (370, 750, 650, 860)),
            "ankle": ("ivory", (396, 906, 445, 935), (465, 906, 523, 935)),
            "sole": ("ivory", (350, 950, 458, 1000), (452, 950, 583, 1000)),
            "hat": ("hat", (270, 0, 750, 195), (270, 0, 750, 195)),
        }
        results = {}
        for name, (kind, front_bounds, side_bounds) in regions.items():
            results[name] = {
                "front": cls.measure_region(front, kind, front_bounds),
                "side": cls.measure_region(side, kind, side_bounds),
            }
        (cls.WORK_FOLDER / "PixelLandmarkMeasurements.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
        print(json.dumps({name: {view: {key: value for key, value in data.items() if key != "row_counts"} for view, data in entry.items()} for name, entry in results.items()}, indent=2))


if __name__ == "__main__":
    import sys
    if "--align" in sys.argv:
        PixelReferenceAlignment.align_pixels()
        print("Pixel alignment saved")
    else:
        PixelReferenceAlignment.inspect()
