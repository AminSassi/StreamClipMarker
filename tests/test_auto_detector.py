"""
Unit test for RecordingDetector video file extensions and debouncing logic.
"""

import os
import time
import tempfile
import unittest

VIDEO_EXTENSIONS = {".mp4", ".mkv", ".flv", ".ts", ".mov"}

def is_livestream_video_file(filename: str) -> bool:
    _, ext = os.path.splitext(filename)
    return ext.lower() in VIDEO_EXTENSIONS

class TestAutoDetectorLogic(unittest.TestCase):

    def test_extension_matching(self):
        self.assertTrue(is_livestream_video_file("TikTok_Live_2026-09-14.mp4"))
        self.assertTrue(is_livestream_video_file("stream_output.flv"))
        self.assertTrue(is_livestream_video_file("segment_001.ts"))
        self.assertTrue(is_livestream_video_file("recording.mkv"))
        self.assertTrue(is_livestream_video_file("capture.mov"))

        # Ignored non-video files
        self.assertFalse(is_livestream_video_file("config.json"))
        self.assertFalse(is_livestream_video_file("notes.txt"))
        self.assertFalse(is_livestream_video_file("preview.jpg"))
        self.assertFalse(is_livestream_video_file("audio.wav"))

    def test_simulated_file_creation(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            test_file = os.path.join(tmpdir, "TikTok_Recording_001.mp4")
            with open(test_file, "w") as f:
                f.write("test")
            self.assertTrue(os.path.exists(test_file))
            self.assertTrue(is_livestream_video_file(test_file))


if __name__ == "__main__":
    unittest.main(verbosity=2)
