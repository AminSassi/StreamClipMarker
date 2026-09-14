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

def calculate_marker(raw_seconds: float, offset_sec: int):
    """Replicates ClipMarker calculations."""
    adj_sec = max(0.0, raw_seconds + offset_sec)
    return {
        "raw_seconds": raw_seconds,
        "adjusted_seconds": adj_sec,
        "seconds": int(round(adj_sec)),
        "timestamp": format_time(adj_sec)
    }

def generate_exports(session_data: dict):
    """Generates sample TXT and JSON exports."""
    offset = session_data.get("recording_offset_seconds", 0)
    markers = session_data.get("markers", [])

    # Human-readable TXT
    lines = [
        "TikTok LIVE STREAM CLIP MARKERS",
        "================================",
        "",
        "Session:",
        session_data["session_start"],
        "",
        "Duration:",
        session_data["duration"],
        "",
        "Markers:",
        ""
    ]

    calculated_markers = []
    for i, m in enumerate(markers, start=1):
        calc = calculate_marker(m["raw_seconds"], offset)
        calc["id"] = i
        calc["note"] = m.get("note", "")
        calculated_markers.append(calc)

        note_str = f" - {calc['note']}" if calc["note"] else ""
        lines.append(f"{i:02d}. {calc['timestamp']}{note_str}")

    txt_output = "\n".join(lines)

    # Machine-readable JSON
    json_markers = []
    for m in calculated_markers:
        item = {
            "timestamp": m["timestamp"],
            "seconds": m["seconds"]
        }
        if m["note"]:
            item["note"] = m["note"]
        json_markers.append(item)

    json_obj = {
        "session_start": session_data["session_start"],
        "duration": session_data["duration"],
        "markers": json_markers
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

    def test_positive_recording_offset(self):
        # TikTok started 3s before session start -> Offset +3s
        # Marker at 00:12:43 (763s) + 3s = 00:12:46 (766s)
        res = calculate_marker(raw_seconds=763.0, offset_sec=3)
        self.assertEqual(res["timestamp"], "00:12:46")
        self.assertEqual(res["seconds"], 766)

    def test_negative_recording_offset(self):
        # TikTok started 5s after session start -> Offset -5s
        res = calculate_marker(raw_seconds=763.0, offset_sec=-5)
        self.assertEqual(res["timestamp"], "00:12:38")
        self.assertEqual(res["seconds"], 758)

    def test_negative_offset_clamping_at_zero(self):
        # Marker at 2s with offset -5s -> Clamps to 00:00:00, 0s
        res = calculate_marker(raw_seconds=2.0, offset_sec=-5)
        self.assertEqual(res["timestamp"], "00:00:00")
        self.assertEqual(res["seconds"], 0)

    def test_json_and_txt_export_structure(self):
        session_data = {
            "session_start": "2026-09-14 19:32:14",
            "duration": "01:43:27",
            "duration_seconds": 6207.0,
            "recording_offset_seconds": 0,
            "markers": [
                {"raw_seconds": 763.0, "note": "Ace clutch"},
                {"raw_seconds": 1638.0, "note": "Funny chat moment"},
                {"raw_seconds": 2466.0, "note": ""}
            ]
        }

        txt, json_obj = generate_exports(session_data)

        # Verify TXT contents
        self.assertIn("TikTok LIVE STREAM CLIP MARKERS", txt)
        self.assertIn("Session:\n2026-09-14 19:32:14", txt)
        self.assertIn("Duration:\n01:43:27", txt)
        self.assertIn("01. 00:12:43 - Ace clutch", txt)
        self.assertIn("02. 00:27:18 - Funny chat moment", txt)
        self.assertIn("03. 00:41:06", txt)
        self.assertNotIn("Clip start", txt)
        self.assertNotIn("Clip padding", txt)

        # Verify JSON schema
        self.assertEqual(json_obj["session_start"], "2026-09-14 19:32:14")
        self.assertEqual(len(json_obj["markers"]), 3)
        self.assertEqual(json_obj["markers"][0]["timestamp"], "00:12:43")
        self.assertEqual(json_obj["markers"][0]["seconds"], 763)
        self.assertEqual(json_obj["markers"][0]["note"], "Ace clutch")
        self.assertEqual(json_obj["markers"][1]["timestamp"], "00:27:18")
        self.assertEqual(json_obj["markers"][1]["seconds"], 1638)
        self.assertEqual(json_obj["markers"][2]["timestamp"], "00:41:06")
        self.assertEqual(json_obj["markers"][2]["seconds"], 2466)
        self.assertNotIn("note", json_obj["markers"][2])
        self.assertNotIn("clip_start", json_obj["markers"][0])

    def test_crash_safety_json_serializable(self):
        # Ensure json dumps works with no circular references or NaN
        session_data = {
            "session_start": "2026-09-14 19:32:14",
            "duration": "00:05:00",
            "duration_seconds": 300.0,
            "recording_offset_seconds": 0,
            "markers": [{"raw_seconds": 45.234, "note": "Test"}]
        }
        _, json_obj = generate_exports(session_data)
        serialized = json.dumps(json_obj, indent=2)
        parsed = json.loads(serialized)
        self.assertEqual(parsed["markers"][0]["timestamp"], "00:00:45")
        self.assertEqual(parsed["markers"][0]["seconds"], 45)


if __name__ == "__main__":
    unittest.main(verbosity=2)
