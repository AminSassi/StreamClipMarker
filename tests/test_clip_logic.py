"""
Automated unit tests for StreamClipMarker timestamp calculation, padding,
recording offset, crash recovery, and export file formatting.
"""

import os
import json
import math
import unittest
from datetime import datetime, timezone

def format_time(total_seconds: float) -> str:
    """Replicates SessionTimer.FormatTime logic."""
    if total_seconds < 0:
        total_seconds = 0
    total_sec = int(math.floor(total_seconds))
    hours = total_sec // 3600
    minutes = (total_sec % 3600) // 60
    seconds = total_sec % 60
    return f"{hours:02d}:{minutes:02d}:{seconds:02d}"

def parse_time(formatted: str) -> float:
    """Replicates SessionTimer.ParseTime logic."""
    parts = formatted.strip().split(":")
    if len(parts) == 3:
        return float(parts[0]) * 3600 + float(parts[1]) * 60 + float(parts[2])
    elif len(parts) == 2:
        return float(parts[0]) * 60 + float(parts[1])
    return 0.0

def calculate_marker(raw_seconds: float, offset_sec: int, pad_before: int, pad_after: int):
    """Replicates ClipMarker calculations."""
    adj_sec = max(0.0, raw_seconds + offset_sec)
    clip_start_sec = max(0.0, adj_sec - pad_before)
    clip_end_sec = adj_sec + pad_after

    return {
        "raw_seconds": raw_seconds,
        "adjusted_seconds": adj_sec,
        "timestamp": format_time(adj_sec),
        "clip_start_seconds": clip_start_sec,
        "clip_start": format_time(clip_start_sec),
        "clip_end_seconds": clip_end_sec,
        "clip_end": format_time(clip_end_sec)
    }

def generate_exports(session_data: dict):
    """Generates sample TXT and JSON exports."""
    offset = session_data.get("recording_offset_seconds", 0)
    pad_before = session_data.get("padding_before_seconds", 10)
    pad_after = session_data.get("padding_after_seconds", 20)
    markers = session_data.get("markers", [])

    # Human-readable TXT
    lines = [
        "TikTok LIVE STREAM CLIP MARKERS",
        "================================",
        "",
        "Session start:",
        session_data["session_start"],
        "",
        "Session duration:",
        session_data["duration"],
        "",
        "Recording offset:",
        f"{'+' if offset >= 0 else ''}{offset}s",
        "",
        "Clip padding:",
        f"Before: {pad_before}s | After: {pad_after}s",
        "",
        "Total markers:",
        str(len(markers)),
        "",
        "Markers:",
        ""
    ]

    calculated_markers = []
    for i, m in enumerate(markers, start=1):
        calc = calculate_marker(m["raw_seconds"], offset, pad_before, pad_after)
        calc["id"] = i
        calc["note"] = m.get("note", "")
        calculated_markers.append(calc)

        note_str = f" - \"{calc['note']}\"" if calc["note"] else ""
        lines.append(f"{i:02d}. {calc['timestamp']}{note_str}")
        lines.append(f"    Clip start: {calc['clip_start']}")
        lines.append(f"    Clip end:   {calc['clip_end']}")
        lines.append("")

    txt_output = "\n".join(lines)

    # Machine-readable JSON
    json_obj = {
        "session_start": session_data["session_start"],
        "duration": session_data["duration"],
        "duration_seconds": session_data.get("duration_seconds", 0.0),
        "recording_offset_seconds": offset,
        "padding_before_seconds": pad_before,
        "padding_after_seconds": pad_after,
        "markers": [
            {
                "id": m["id"],
                "timestamp": m["timestamp"],
                "seconds": round(m["adjusted_seconds"], 1),
                "clip_start": m["clip_start"],
                "clip_start_seconds": round(m["clip_start_seconds"], 1),
                "clip_end": m["clip_end"],
                "clip_end_seconds": round(m["clip_end_seconds"], 1),
                "raw_elapsed_seconds": round(m["raw_seconds"], 3),
                "note": m["note"]
            }
            for m in calculated_markers
        ]
    }

    return txt_output, json_obj


class TestStreamClipMarker(unittest.TestCase):

    def test_format_time_basic(self):
        self.assertEqual(format_time(0), "00:00:00")
        self.assertEqual(format_time(207), "00:03:27")
        self.assertEqual(format_time(763), "00:12:43")
        self.assertEqual(format_time(1638), "00:27:18")
        self.assertEqual(format_time(2466), "00:41:06")
        self.assertEqual(format_time(3832), "01:03:52")
        self.assertEqual(format_time(6207), "01:43:27")

    def test_session_longer_than_24_hours(self):
        # 25 hours, 3 minutes, 12 seconds = 90192 seconds
        self.assertEqual(format_time(90192), "25:03:12")

    def test_parse_time(self):
        self.assertEqual(parse_time("00:12:43"), 763.0)
        self.assertEqual(parse_time("01:43:27"), 6207.0)
        self.assertEqual(parse_time("12:43"), 763.0)

    def test_padding_calculation(self):
        # Marker at 00:12:43 (763s), Before 10s, After 20s
        res = calculate_marker(raw_seconds=763.0, offset_sec=0, pad_before=10, pad_after=20)
        self.assertEqual(res["timestamp"], "00:12:43")
        self.assertEqual(res["clip_start"], "00:12:33")
        self.assertEqual(res["clip_start_seconds"], 753.0)
        self.assertEqual(res["clip_end"], "00:13:03")
        self.assertEqual(res["clip_end_seconds"], 783.0)

    def test_padding_clamping_at_zero(self):
        # Marker at 00:00:05 (5s), Before 10s
        res = calculate_marker(raw_seconds=5.0, offset_sec=0, pad_before=10, pad_after=20)
        self.assertEqual(res["clip_start"], "00:00:00")
        self.assertEqual(res["clip_start_seconds"], 0.0)
        self.assertEqual(res["clip_end"], "00:00:25")
        self.assertEqual(res["clip_end_seconds"], 25.0)

    def test_positive_recording_offset(self):
        # TikTok started 3s before session start -> Offset +3s
        # Marker at 00:12:43 (763s) + 3s = 00:12:46 (766s)
        res = calculate_marker(raw_seconds=763.0, offset_sec=3, pad_before=10, pad_after=20)
        self.assertEqual(res["timestamp"], "00:12:46")
        self.assertEqual(res["clip_start"], "00:12:36")
        self.assertEqual(res["clip_end"], "00:13:06")

    def test_negative_recording_offset(self):
        # TikTok started 5s after session start -> Offset -5s
        res = calculate_marker(raw_seconds=763.0, offset_sec=-5, pad_before=10, pad_after=20)
        self.assertEqual(res["timestamp"], "00:12:38")
        self.assertEqual(res["clip_start"], "00:12:28")
        self.assertEqual(res["clip_end"], "00:12:58")

    def test_json_and_txt_export_structure(self):
        session_data = {
            "session_start": "2026-09-14 19:32:14",
            "duration": "01:43:27",
            "duration_seconds": 6207.0,
            "recording_offset_seconds": 0,
            "padding_before_seconds": 10,
            "padding_after_seconds": 20,
            "markers": [
                {"raw_seconds": 763.0, "note": "Ace clutch"},
                {"raw_seconds": 1638.0, "note": "Funny chat moment"},
                {"raw_seconds": 2466.0, "note": ""}
            ]
        }

        txt, json_obj = generate_exports(session_data)

        # Verify TXT contents
        self.assertIn("TikTok LIVE STREAM CLIP MARKERS", txt)
        self.assertIn("01. 00:12:43 - \"Ace clutch\"", txt)
        self.assertIn("    Clip start: 00:12:33", txt)
        self.assertIn("    Clip end:   00:13:03", txt)
        self.assertIn("02. 00:27:18 - \"Funny chat moment\"", txt)
        self.assertIn("03. 00:41:06", txt)

        # Verify JSON schema
        self.assertEqual(json_obj["session_start"], "2026-09-14 19:32:14")
        self.assertEqual(len(json_obj["markers"]), 3)
        self.assertEqual(json_obj["markers"][0]["timestamp"], "00:12:43")
        self.assertEqual(json_obj["markers"][0]["seconds"], 763.0)
        self.assertEqual(json_obj["markers"][0]["clip_start"], "00:12:33")
        self.assertEqual(json_obj["markers"][0]["clip_end"], "00:13:03")

    def test_crash_safety_json_serializable(self):
        # Ensure json dumps works with no circular references or NaN
        session_data = {
            "session_start": "2026-09-14 19:32:14",
            "duration": "00:05:00",
            "duration_seconds": 300.0,
            "recording_offset_seconds": 0,
            "padding_before_seconds": 10,
            "padding_after_seconds": 20,
            "markers": [{"raw_seconds": 45.234, "note": "Test"}]
        }
        _, json_obj = generate_exports(session_data)
        serialized = json.dumps(json_obj, indent=2)
        parsed = json.loads(serialized)
        self.assertEqual(parsed["markers"][0]["id"], 1)
        self.assertEqual(parsed["markers"][0]["timestamp"], "00:00:45")


if __name__ == "__main__":
    unittest.main(verbosity=2)
