import json
from pathlib import Path
import sqlite3
import subprocess
import sys
import tempfile
import unittest


SCRIPT = Path(__file__).with_name("export_classic.py")


class ClassicExportTest(unittest.TestCase):
    def test_readonly_export_maps_legacy_data_without_secrets(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            database = root / "TaskTool.db"
            settings = root / "settings.json"
            output = root / "clean.json"
            con = sqlite3.connect(database)
            con.executescript("""
                CREATE TABLE schema_version(version INTEGER);
                INSERT INTO schema_version VALUES(32);
                CREATE TABLE tasks(id TEXT,title TEXT,description TEXT,start_local TEXT,status TEXT,
                    priority INTEGER,tags TEXT,created_utc TEXT);
                CREATE TABLE task_segments(id INTEGER,task_id TEXT,start_local TEXT,end_local TEXT,note TEXT);
                CREATE TABLE time_logs(id INTEGER,task_id TEXT,start_utc TEXT,end_utc TEXT,note TEXT);
                CREATE TABLE work_days(day TEXT,day_type TEXT,is_br INTEGER,is_ho INTEGER,come_local TEXT);
                INSERT INTO tasks VALUES(
                    'c915d72c-dc1e-4e57-97c9-98d52c127566','Altaufgabe','Beschreibung',
                    '2026-10-30 10:00:00','Running',2,'wichtig,intern','2026-10-01 09:00:00');
                INSERT INTO task_segments VALUES(17,'c915d72c-dc1e-4e57-97c9-98d52c127566',
                    '2026-10-30 10:00:00','2026-10-30 11:00:00','Planung');
                INSERT INTO time_logs VALUES(9,'c915d72c-dc1e-4e57-97c9-98d52c127566',
                    '2026-10-30 08:00:00','2026-10-30 08:30:00','Aufwand');
                INSERT INTO work_days VALUES('2026-10-31','UL',1,0,'07:55:00');
            """)
            con.commit()
            con.close()
            settings.write_text(json.dumps({
                "ClientInstanceId": "ci-classic-export",
                "ReminderLeadMinutes": 7,
                "TicketSystemPassword": "VERY_PRIVATE_PASSWORD",
                "TicketSystemPasswordEncrypted": "SECRETDATA",
                "AiApiKeyEncrypted": "SECRET_KEY",
                "ShowWeekendInWeekView": True,
            }), encoding="utf-8")
            run = subprocess.run([sys.executable, str(SCRIPT), str(database),
                                  "-s", str(settings), "-o", str(output)],
                                 capture_output=True, text=True)
            self.assertEqual(run.returncode, 0, run.stderr)
            content = output.read_text(encoding="utf-8")
            payload = json.loads(content)
            self.assertEqual(payload["sourceId"], "ci-classic-export")
            self.assertEqual(payload["schemaVersion"], 32)
            self.assertEqual(payload["tasks"][0]["status"], "active")
            self.assertEqual(len(payload["segments"]), 1)
            self.assertEqual(len(payload["timeEntries"]), 1)
            self.assertEqual(len(payload["dayMarkers"]), 1)
            self.assertEqual(payload["settings"]["reminderLeadMinutes"], 7)
            self.assertNotIn("VERY_PRIVATE_PASSWORD", content)
            self.assertNotIn("SECRET_KEY", content)
            self.assertNotIn("07:55", content)
            # Exporter never modifies the source file.
            con = sqlite3.connect(database)
            self.assertEqual(con.execute("SELECT COUNT(*) FROM tasks").fetchone()[0], 1)
            con.close()

    def test_unsupported_database_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            database = Path(directory) / "empty.db"
            sqlite3.connect(database).close()
            run = subprocess.run([sys.executable, str(SCRIPT), str(database)],
                                 capture_output=True, text=True)
            self.assertNotEqual(run.returncode, 0)
            self.assertIn("missing core tables", run.stderr)


if __name__ == "__main__":
    unittest.main()
