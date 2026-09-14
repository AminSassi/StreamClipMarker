"""
Unit tests for CSV export format (Clip,Marker,Start,End,Note) and RFC 4180 escaping.
"""

import io
import csv
import unittest

def escape_csv_val(val: str) -> str:
    if not val:
        return ""
    if any(c in val for c in [",", "\"", "\n", "\r"]):
        return "\"" + val.replace("\"", "\"\"") + "\""
    return val

def generate_csv(markers, offset_sec=0, pad_before=10, pad_after=20):
    lines = ["Clip,Marker,Start,End,Note"]
    for i, m in enumerate(markers, 1):
        raw = m["raw"]
        adj = max(0, raw + offset_sec)
        start = max(0, adj - pad_before)
        end = adj + pad_after

        def fmt(sec):
            s = int(sec)
            return f"{s//3600:02d}:{(s%3600)//60:02d}:{s%60:02d}"

        note = escape_csv_val(m.get("note", ""))
        lines.append(f"{i},{fmt(adj)},{fmt(start)},{fmt(end)},{note}")
    return "\n".join(lines)


class TestCsvExport(unittest.TestCase):

    def test_standard_csv_columns(self):
        markers = [
            {"raw": 763, "note": "INSANE KILL"},
            {"raw": 1638, "note": "FUNNY"},
            {"raw": 3891, "note": "RAGE"}
        ]
        csv_text = generate_csv(markers, offset_sec=0, pad_before=10, pad_after=20)
        lines = csv_text.splitlines()

        self.assertEqual(lines[0], "Clip,Marker,Start,End,Note")
        self.assertEqual(lines[1], "1,00:12:43,00:12:33,00:13:03,INSANE KILL")
        self.assertEqual(lines[2], "2,00:27:18,00:27:08,00:27:38,FUNNY")
        self.assertEqual(lines[3], "3,01:04:51,01:04:41,01:05:11,RAGE")

    def test_csv_parser_compatibility(self):
        markers = [
            {"raw": 763, "note": "Normal Note"},
            {"raw": 1638, "note": "Note with, comma"},
            {"raw": 2400, "note": "Note with \"quotes\" inside"}
        ]
        csv_text = generate_csv(markers)
        reader = csv.reader(io.StringIO(csv_text))
        rows = list(reader)

        self.assertEqual(rows[0], ["Clip", "Marker", "Start", "End", "Note"])
        self.assertEqual(rows[1], ["1", "00:12:43", "00:12:33", "00:13:03", "Normal Note"])
        self.assertEqual(rows[2], ["2", "00:27:18", "00:27:08", "00:27:38", "Note with, comma"])
        self.assertEqual(rows[3], ["3", "00:40:00", "00:39:50", "00:40:20", "Note with \"quotes\" inside"])

    def test_empty_markers_csv(self):
        csv_text = generate_csv([])
        self.assertEqual(csv_text.strip(), "Clip,Marker,Start,End,Note")


if __name__ == "__main__":
    unittest.main(verbosity=2)
