"""
Unit tests for RecordingStateMachine and event debouncing logic:
- Duplicate Created events
- Multiple Changed events
- Recording start
- Recording stop
- Immediate restart of recording
- Multiple sessions
"""

import time
import unittest

class RecordingState:
    Idle = "Idle"
    WaitingForRecording = "WaitingForRecording"
    Recording = "Recording"
    Finalizing = "Finalizing"
    Completed = "Completed"


class SimulatedStateMachine:
    def __init__(self, auto_detect=True):
        self.state = RecordingState.WaitingForRecording if auto_detect else RecordingState.Idle
        self.sessions_created = 0
        self.sessions_finalized = 0
        self.active_file = None
        self.last_detection_time = 0
        self.last_detected_file = None
        self.unlocked_streak = 0
        self.diagnostic_logs = []

    def log(self, msg):
        self.diagnostic_logs.append(msg)

    def handle_file_event(self, full_path, event_type, now_timestamp):
        # Debounce duplicate events for same file within 3 seconds
        if self.last_detected_file == full_path and (now_timestamp - self.last_detection_time) < 3.0:
            self.log(f"Duplicate {event_type} ignored: {full_path}")
            return False

        self.last_detection_time = now_timestamp
        self.last_detected_file = full_path

        # If already recording this exact file, ignore
        if self.state == RecordingState.Recording and self.active_file == full_path:
            return False

        if self.state == RecordingState.Finalizing:
            return False

        # Transition to Recording
        self.active_file = full_path
        self.unlocked_streak = 0
        self.state = RecordingState.Recording
        self.sessions_created += 1
        self.log(f"Started session #{self.sessions_created} for {full_path}")
        return True

    def monitor_tick(self, is_locked, file_exists=True):
        if self.state != RecordingState.Recording or not self.active_file:
            return False

        if not file_exists:
            # File moved or deleted
            self.finalize_session("File Moved/Deleted")
            return True

        if is_locked:
            self.unlocked_streak = 0
        else:
            self.unlocked_streak += 1
            if self.unlocked_streak >= 2:
                self.finalize_session("File Released Lock")
                return True
        return False

    def finalize_session(self, reason):
        self.state = RecordingState.Finalizing
        self.sessions_finalized += 1
        self.log(f"Finalized session #{self.sessions_finalized}: {reason}")
        self.active_file = None
        self.unlocked_streak = 0
        self.state = RecordingState.Completed


class TestStateMachine(unittest.TestCase):

    def test_single_session_on_created(self):
        sm = SimulatedStateMachine(auto_detect=True)
        self.assertEqual(sm.state, RecordingState.WaitingForRecording)

        started = sm.handle_file_event("C:\\Videos\\TikTok_01.mp4", "Created", now_timestamp=100.0)
        self.assertTrue(started)
        self.assertEqual(sm.state, RecordingState.Recording)
        self.assertEqual(sm.sessions_created, 1)

    def test_duplicate_created_events_debounced(self):
        sm = SimulatedStateMachine(auto_detect=True)
        # Event 1: File created
        sm.handle_file_event("C:\\Videos\\TikTok_01.mp4", "Created", now_timestamp=100.0)
        self.assertEqual(sm.sessions_created, 1)

        # Event 2: Duplicate Created event 0.5s later
        started2 = sm.handle_file_event("C:\\Videos\\TikTok_01.mp4", "Created", now_timestamp=100.5)
        self.assertFalse(started2)
        self.assertEqual(sm.sessions_created, 1)

        # Event 3: Another duplicate Created event 1.2s later
        started3 = sm.handle_file_event("C:\\Videos\\TikTok_01.mp4", "Created", now_timestamp=101.2)
        self.assertFalse(started3)
        self.assertEqual(sm.sessions_created, 1)

    def test_multiple_changed_events_do_not_reset_session(self):
        sm = SimulatedStateMachine(auto_detect=True)
        sm.handle_file_event("C:\\Videos\\TikTok_01.mp4", "Created", now_timestamp=100.0)
        self.assertEqual(sm.sessions_created, 1)

        # Video file writes multiple chunks firing Changed events
        for t in [100.8, 101.5, 102.3, 104.0, 106.5]:
            started = sm.handle_file_event("C:\\Videos\\TikTok_01.mp4", "Changed", now_timestamp=t)
            # Must NOT create a new session
            self.assertEqual(sm.sessions_created, 1)
            self.assertEqual(sm.state, RecordingState.Recording)

    def test_recording_stop_requires_confirmed_unlock(self):
        sm = SimulatedStateMachine(auto_detect=True)
        sm.handle_file_event("C:\\Videos\\TikTok_01.mp4", "Created", now_timestamp=100.0)

        # Recorder writing frames (locked)
        sm.monitor_tick(is_locked=True)
        self.assertEqual(sm.state, RecordingState.Recording)

        # Recorder still writing frames
        sm.monitor_tick(is_locked=True)
        self.assertEqual(sm.state, RecordingState.Recording)

        # Recorder stops! First unlocked tick
        stopped1 = sm.monitor_tick(is_locked=False)
        self.assertFalse(stopped1) # Requires 2 ticks
        self.assertEqual(sm.state, RecordingState.Recording)

        # Second consecutive unlocked tick -> CONFIRMED STOP!
        stopped2 = sm.monitor_tick(is_locked=False)
        self.assertTrue(stopped2)
        self.assertEqual(sm.state, RecordingState.Completed)
        self.assertEqual(sm.sessions_finalized, 1)

    def test_immediate_restart_of_recording(self):
        sm = SimulatedStateMachine(auto_detect=True)
        # Session 1:
        sm.handle_file_event("C:\\Videos\\TikTok_01.mp4", "Created", now_timestamp=100.0)
        sm.monitor_tick(is_locked=True)
        sm.monitor_tick(is_locked=False)
        sm.monitor_tick(is_locked=False)
        self.assertEqual(sm.sessions_finalized, 1)

        # Streamer immediately hits "Record" again for another match (new file)
        sm.state = RecordingState.WaitingForRecording
        started_again = sm.handle_file_event("C:\\Videos\\TikTok_02.mp4", "Created", now_timestamp=115.0)
        self.assertTrue(started_again)
        self.assertEqual(sm.state, RecordingState.Recording)
        self.assertEqual(sm.sessions_created, 2)

        # Session 2 stops
        sm.monitor_tick(is_locked=False)
        sm.monitor_tick(is_locked=False)
        self.assertEqual(sm.sessions_finalized, 2)

    def test_multiple_sequential_sessions(self):
        sm = SimulatedStateMachine(auto_detect=True)
        for i in range(1, 4):
            sm.state = RecordingState.WaitingForRecording
            sm.handle_file_event(f"C:\\Videos\\Stream_{i:02d}.mp4", "Created", now_timestamp=i * 100.0)
            self.assertEqual(sm.sessions_created, i)
            sm.monitor_tick(is_locked=False)
            sm.monitor_tick(is_locked=False)
            self.assertEqual(sm.sessions_finalized, i)


if __name__ == "__main__":
    unittest.main(verbosity=2)
