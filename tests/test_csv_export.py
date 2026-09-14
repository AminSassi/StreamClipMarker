"""
Unit tests for CSV export format (Marker,Timestamp,Seconds) and RFC 4180 escaping.
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

def generate_csv(markers, offset_sec=0):
    any_notes = any(bool(m.get("note")) for m in markers)
    if any_notes:
        lines = ["Marker,Timestamp,Seconds,Note"]
    else:
        lines = ["Marker,Timestamp,Seconds"]

    for i, m in enumerate(markers, 1):
        raw = m["raw"]
        adj = max(0, raw + offset_sec)
        sec = int(round(adj))

        def fmt(s_val):
            s = int(s_val)
            return f"{s//3600:02d}:{(s%3600)//60:02d}:{s%60:02d}"

        if any_notes:
            note = escape_csv_val(m.get("note", ""))
            lines.append(f"{i},{fmt(adj)},{sec},{note}")
        else:
            lines.append(f"{i},{fmt(adj)},{sec}")

    return "\n".join(lines)


class TestCsvExport(unittest.TestCase):

    def test_standard_csv_columns_without_notes(self):
        markers = [
            {"raw": 763},
            {"raw": 1638},
            {"raw": 3891}
        ]
        csv_text = generate_csv(markers, offset_sec=0)
        lines = csv_text.splitlines()

        self.assertEqual(lines[0], "Marker,Timestamp,Seconds")
        self.assertEqual(lines[1], "1,00:12:43,763")
        self.assertEqual(lines[2], "2,00:27:18,1638")
        self.assertEqual(lines[3], "3,01:04:51,3891")

    def test_csv_with_notes_and_escaping(self):
        markers = [
            {"raw": 763, "note": "Normal Note"},
            {"raw": 1638, "note": "Note with, comma"},
            {"raw": 2400, "note": "Note with \"quotes\" inside"}
        ]
        csv_text = generate_csv(markers)
        reader = csv.reader(io.StringIO(csv_text))
        rows = list(reader)

        self.assertEqual(rows[0], ["Marker", "Timestamp", "Seconds", "Note"])
        self.assertEqual(rows[1], ["1", "00:12:43", "763", "Normal Note"])
        self.assertEqual(rows[2], ["2", "00:27:18", "1638", "Note with, comma"])
        self.assertEqual(rows[3], ["3", "00:40:00", "2400", "Note with \"quotes\" inside"])

    def test_empty_markers_csv(self):
        csv_text = generate_csv([])
        self.assertEqual(csv_text.strip(), "Marker,Timestamp,Seconds")


if __name__ == "__main__":
    unittest.main(verbosity=2)
