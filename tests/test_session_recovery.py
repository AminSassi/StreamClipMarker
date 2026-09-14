"""
Unit tests for session crash safety, immediate marker persistence, and 3-choice recovery (Resume, Finalize, Delete).
"""

import os
import json
import tempfile
import unittest

class SimulatedStorage:
    def __init__(self, directory):
        self.session_file = os.path.join(directory, "current_session.json")
        self.output_dir = os.path.join(directory, "output")
        os.makedirs(self.output_dir, exist_ok=True)

    def save_marker_immediately(self, session_data):
        tmp = self.session_file + ".tmp"
        with open(tmp, "w", encoding="utf-8") as f:
            json.dump(session_data, f, indent=2)
        if os.path.exists(self.session_file):
            os.remove(self.session_file)
        os.rename(tmp, self.session_file)

    def has_unfinished_session(self):
        return os.path.exists(self.session_file)

    def load_session(self):
        if not self.has_unfinished_session():
            return None
        with open(self.session_file, "r", encoding="utf-8") as f:
            return json.load(f)

    def action_delete(self):
        if os.path.exists(self.session_file):
            os.remove(self.session_file)

    def action_finalize(self):
        data = self.load_session()
        if not data:
            return None
        base = "TikTok_Stream_Finalized"
        txt_path = os.path.join(self.output_dir, base + ".txt")
        json_path = os.path.join(self.output_dir, base + ".json")
        csv_path = os.path.join(self.output_dir, base + ".csv")

        with open(txt_path, "w", encoding="utf-8") as f:
            f.write(f"Duration: {data['duration_seconds']}\nMarkers: {len(data['markers'])}\n")
        with open(json_path, "w", encoding="utf-8") as f:
            json.dump(data, f)
        with open(csv_path, "w", encoding="utf-8") as f:
            f.write("Clip,Marker,Start,End,Note\n")
            for m in data['markers']:
                f.write(f"{m['id']},{m['timestamp']},00:00:00,00:00:20,{m.get('note','')}\n")

        self.action_delete()
        return txt_path, json_path, csv_path

    def action_resume(self):
        data = self.load_session()
        # Resume allows continuing timer with initial offset = data['duration_seconds']
        return data


class TestSessionRecovery(unittest.TestCase):

    def test_immediate_marker_persistence(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            storage = SimulatedStorage(tmpdir)
            session = {
                "session_start": "2026-09-14 20:00:00",
                "duration_seconds": 120.0,
                "markers": [
                    {"id": 1, "timestamp": "00:00:45", "raw_seconds": 45.2, "note": "Headshot"}
                ]
            }
            storage.save_marker_immediately(session)
            self.assertTrue(storage.has_unfinished_session())

            # Simulate adding a 2nd marker via F8
            session["markers"].append({"id": 2, "timestamp": "00:01:30", "raw_seconds": 90.1, "note": "Clutch"})
            session["duration_seconds"] = 90.1
            storage.save_marker_immediately(session)

            # Simulated crash happened here! Re-read from disk
            loaded = storage.load_session()
            self.assertIsNotNone(loaded)
            self.assertEqual(len(loaded["markers"]), 2)
            self.assertEqual(loaded["markers"][1]["note"], "Clutch")

    def test_action_resume(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            storage = SimulatedStorage(tmpdir)
            session = {
                "session_start": "2026-09-14 20:00:00",
                "duration_seconds": 300.0,
                "markers": [{"id": 1, "timestamp": "00:02:15", "raw_seconds": 135.0, "note": ""}]
            }
            storage.save_marker_immediately(session)

            resumed_data = storage.action_resume()
            self.assertEqual(resumed_data["duration_seconds"], 300.0)
            self.assertEqual(len(resumed_data["markers"]), 1)
            # Session file still exists while resumed
            self.assertTrue(storage.has_unfinished_session())

    def test_action_finalize(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            storage = SimulatedStorage(tmpdir)
            session = {
                "session_start": "2026-09-14 20:00:00",
                "duration_seconds": 500.0,
                "markers": [
                    {"id": 1, "timestamp": "00:03:00", "raw_seconds": 180.0, "note": "Funny"}
                ]
            }
            storage.save_marker_immediately(session)
            txt, json_p, csv_p = storage.action_finalize()

            self.assertTrue(os.path.exists(txt))
            self.assertTrue(os.path.exists(json_p))
            self.assertTrue(os.path.exists(csv_p))
            # Current session deleted upon finalization
            self.assertFalse(storage.has_unfinished_session())

    def test_action_delete(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            storage = SimulatedStorage(tmpdir)
            session = {
                "session_start": "2026-09-14 20:00:00",
                "duration_seconds": 10.0,
                "markers": []
            }
            storage.save_marker_immediately(session)
            self.assertTrue(storage.has_unfinished_session())

            storage.action_delete()
            self.assertFalse(storage.has_unfinished_session())


if __name__ == "__main__":
    unittest.main(verbosity=2)
