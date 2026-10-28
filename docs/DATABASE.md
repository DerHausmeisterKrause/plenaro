# Datenbank und versionierte Migrationen

Stand: 28.10.2026. PostgreSQL 17, automatische Schema-Migrationen beim Webserver-Start.

## Schema

| Tabelle | Zweck |
| --- | --- |
| `schema_migrations` | Reihenfolge und SHA-256-Prüfsummen angewendeter Migrationen |
| `tasks` | Aufgaben, Status, Priorität, Tags, UUID-Besitzer |
| `task_segments` | Geplante Zeitsegmente pro Aufgabe |
| `time_entries` | Arbeits-/Pausenzeiten, optional Aufgabenbezug |
| `system_settings` | Globale JSONB-Einstellungen (noch keine API) |

Keine Benutzer- oder Beispieldaten werden erstellt. `owner_user_id` hat noch **keine** referenzierte Benutzertabelle: Die User-Tabellen, Identity-Authentifizierung, Besitzer-FKs und Autorisierung folgen im nächsten Schritt. Deshalb werden jetzt **keine** CRUD-Endpunkte freigeschaltet.

Die beiden initialen SQL-Migrationen sind `0001_core_schema.sql` und `0002_indexes.sql`. Weitere Schemaänderungen erhalten neue, monoton sortierte SQL-Dateien. Bereits veröffentlichte Migrationen **niemals ändern**.

## Migrationsablauf und Sicherheitsverhalten

1. Der Webprozess verbindet sich vor dem Öffnen des HTTP-Ports mit PostgreSQL.
2. Er startet eine Datenbanktransaktion und erwirbt einen transaktionsgebundenen PostgreSQL Advisory Lock. So kann nur eine Instanz migrieren.
3. Er vergleicht `schema_migrations` einschließlich SHA-256 der ausgelieferten SQL-Dateien.
4. Neue Dateien werden in Reihenfolge ausgeführt und protokolliert; ein Fehler löst Rollback aus.
5. Ungültige Prüfsummen, unbekannte Versionen (Downgrade) oder lückenhafte Historie verhindern den App-Start.
6. `/health/ready` bestätigt nur eine erreichbare Datenbank mit vollständiger gültiger Migration-Historie und vorhandenen Kerntabellen; andernfalls 503.

Der Container startet bei fehlerhafter Migration nicht funktionsfähig. Kein automatischer Datenverlust durch `docker compose up -d --build`, aber Schemaänderungen können bei späteren Versionen nicht ohne Backup zurückgenommen werden.

## Prüfen

```bash
docker compose ps
curl -f http://127.0.0.1:8080/health/ready
docker compose exec -T database psql -U plenaro -d plenaro -c "SELECT migration_id, applied_at FROM schema_migrations ORDER BY migration_id;"
docker compose exec -T database psql -U plenaro -d plenaro -c '\\dt'
```

Falls `POSTGRES_USER`/`POSTGRES_DB` in `.env` geändert wurden: Werte im psql-Aufruf anpassen.

## Backup vor Updates

**Kein `docker compose down -v`**: dieser Befehl löscht das Datenvolume. Vor späteren Schema-Upgrades ein konsistentes `pg_dump`-Backup erstellen, sicher außerhalb des Servers speichern und Wiederherstellung separat testen. Achtung: `pg_dump` umfasst unverschlüsselt gespeicherte Daten, daher sicher ablegen.

```bash
# Nur Beispiel: Benutzer/DB aus .env im Bedarfsfall anpassen!
docker compose exec -T database pg_dump -U plenaro -d plenaro -Fc > plenaro-backup.dump
```

Diese Erstimplementierung enthält noch keinen automatisierten Backup-/Restore-Dienst.
