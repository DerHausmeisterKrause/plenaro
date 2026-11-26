# Plenaro aktualisieren

Für eine bestehende, laufende Installation auf Linux (Bash, Git, Docker Compose, flock, gzip und GNU coreutils):

```bash
cd /opt/plenaro
# Einmalig, um den neuen Updater zu beziehen:
git pull --ff-only && bash update.sh
# Danach bei jedem Update nur noch:
bash update.sh
```

Der Befehl führt einen Fast-forward-Pull von origin/main aus und startet anschließend die frisch bezogene Update-Implementierung. Lokale Änderungen an versionierten Dateien, andere Branches, lokale eigene Commits und parallele Update-Aufrufe werden abgelehnt. Lokale .env, Compose-Overrides, Secrets und Zertifikate bleiben unberührt.

## Reihenfolge

1. Laufende und konfigurierte optionale Dienste erkennen (ClamAV / Browser-Login). Nicht aktivierte Profile bleiben aus.
2. Compose-Konfiguration prüfen. PostgreSQL muss bereits laufen.
3. Aktuelle Registry-Images der aktiven Dienste holen, z. B. nginx:stable-alpine, postgres:17-alpine und clamav/clamav:stable-debian. Das aktuelle und das neue PostgreSQL-Image müssen auf Major 17 liegen. Kein automatisches Major-Upgrade.
4. Aktive Build-Dienste mit `--pull --no-cache` neu bauen, damit auch Node/.NET-Basisimages sowie APT- und Installationsschichten aktualisiert werden. Bis hier werden die laufenden Dienstcontainer nicht ersetzt.
5. Vollständigen logischen PostgreSQL-Dump erstellen, komprimieren und auf Lesbarkeit sowie nichtleeren Inhalt prüfen. Standard: /var/backups/plenaro, Verzeichnis 0700, Dateien 0600. Optional absoluten Pfad mit PLENARO_BACKUP_DIR setzen. Kein Backup im Git-Checkout. Fehlgeschlagene Teildateien werden entfernt.
6. Compose übernimmt die aktualisierten Images und Konfigurationen, wartet auf die definierten Healthchecks und lädt NGINX neu. Persistente Volumes bleiben erhalten. Ausgabe der installierten Versionen und des Backup-Pfads.

## Grenzen und Betrieb

- Manuell gestartetes Wartungsupdate, kein Watchtower, Cronjob oder Git-Hook. Ein gewöhnliches git pull allein aktualisiert keine Container.
- Kurze Dienstunterbrechungen sind möglich. Pull-/Build-/Backupfehler verhindern das Deployment. Fehler während der Containerübernahme können einen teilweise aktualisierten Stack hinterlassen. Kein automatischer Rollback oder DB-Downgrade.
- Das Backup enthält sensible Daten (auch Rollen und Passwort-Hashes). Es wird nicht ins Repository oder in das Docker-Image kopiert. Backups werden bewusst nicht automatisch gelöscht. Speicherplatz, sichere externe Sicherung, Sicherung des separat benötigten Vault-Schlüssels und Restore-Tests liegen beim Betreiber.
- Updates beziehen die neuesten Images innerhalb der in Compose/Dockerfiles hinterlegten Tags. Kein garantierter CVE-freier Stand: festgelegte npm-/NuGet-/Playwright-Abhängigkeiten und die Docker Engine des Hosts erfordern weiterhin eigene Versionsupdates und Audits. Der optionale Scanner wird auf seinen Image-Stand aktualisiert; Signaturen bleiben im Volume.
- Healthchecks prüfen die bestehende Readiness. Das ist kein vollständiger Live-Funktionstest (insbesondere ClamAV und externe Integrationen).
- Keine Entfernung von Volumes, Images oder anderen Docker-Projekten. Keine Änderung an Host-APT, Netzwerkfreigaben oder Datenbankschema durch das Skript selbst; Anwendungsmigrationen laufen wie bisher beim Webstart.

Bei einem Fehler: Ausgabe beachten und `docker compose --profile '*' ps` prüfen; nicht blind auf einen alten Stand zurücksetzen. Backup ggf. in einer getrennten Wiederherstellungsumgebung testen.
