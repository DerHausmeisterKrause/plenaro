# Geprüfte Anhang-Vorschau (Znuny)

## Verhalten

- PNG, JPEG, GIF, WebP und PDF: Klick auf den Anhang öffnet einen Dialog. Kein automatischer Dateidownload.
- Plenaro prüft die Ticketberechtigung, ruft die ausgewählte Datei aus Znuny ab und prüft Typ, Dateisignatur und Größe.
- Der lokale ClamAV-Daemon erhält ausschließlich die Bytes des ausgewählten Anhangs per INSTREAM. Der bestehende Znuny-TicketGet kann technisch weitere Anhänge im Upstream-JSON enthalten; diese werden nicht gescannt, gespeichert oder ausgeliefert.
- Nur ein vollständiges `stream: OK` führt zur Vorschau. Fund, Limitüberschreitung, fehlende/fehlerhafte Scanner-Antwort und Timeout sperren die Vorschau. Keine automatische Rückfallebene auf ungeprüfte Dateien.
- Rasterbilder werden direkt per createImageBitmap aus geprüften Bytes in Canvas gerendert (GIF als Standbild), unabhängig von blob:-Bildadressen; PDF-Seiten mit lokal gebündeltem PDF.js als Canvas gerendert. Keine aktiven PDF-Formulare, Skripte oder anklickbaren PDF-Links. Optionaler bewusster Download der bereits geprüften Bytes.
- Der Browser muss Bytes zur Darstellung empfangen; weder die Anwendung noch Cache-Control können Bildschirmfotos, Browser-internes Spooling oder Speicherung durch den Benutzer grundsätzlich verhindern.

## Scanner starten

```bash
cd /opt/plenaro
git pull --ff-only
docker compose --profile attachment-scan up -d clamav
docker compose up -d --build --no-deps web && docker compose restart nginx
docker compose ps
docker compose logs --tail=80 clamav
```

Das optionale Profil startet genau einen zusätzlichen lokalen Container aus dem offiziellen Debian-ClamAV-Image. Ohne Scanner bleibt die Vorschau geschlossen, die sonstige Anwendung läuft. Kein Host-Port, keine neuen öffentlichen Hostnamen, keine Datenbankmigration. Der Scanner benötigt Internetzugriff für FreshClam-Signaturupdates; Anhänge werden nicht an externe Dienste gesendet. Die Virensignaturen bleiben in einem eigenen Volume; temporäre Scanner-Arbeitsdateien liegen auf tmpfs. ClamAV benötigt typischerweise mehrere GB RAM; die Datenbank-Neuladung ohne parallele Engine reduziert Spitzenverbrauch. Der Scanner wird mit den Compose-Umgebungsvariablen für verschlüsselte Dateien, Größenlimits und alte Signaturdatenbanken restriktiv konfiguriert.

## Begrenzungen und Schutz

- Bestehendes Znuny-Limit: 5 MiB je Datei, 8 MiB für die gesamte TicketGet-Antwort. Vorschau unterstützt bis zu 500 PDF-Seiten, mit begrenzter Canvas- und Bildgröße.
- Benutzer-/Agent-/Server-/Ticket-/Artikel-/Datei-ID bilden den Cache-Schlüssel. Kein globaler Dateicache, keine Freigabe über erratbare URL. Berechtigung bei jedem Zugriff erneut geprüft.
- Max. 64 MiB **Anhang-Nutzdaten** bzw. 32 Einträge im Web-RAM-Cache; zwei parallele Anhangabrufe/Scans, ältere Cacheeinträge werden bei Bedarf verdrängt.
- Drei Minuten Inaktivität, Keepalive nur bei geöffnetem Dialog (30 s); Hintergrundbereinigung alle 15 s. Bei kontinuierlichem Zugriff spätestens nach 30 Minuten ab Scan aus dem Servercache entfernt. Danach wird bei erneutem Öffnen wieder abgerufen und geprüft. Schließen verwirft den lokalen Blob-Link und beendet den Viewer/Worker. GC gibt den Server-RAM frei; keine Garantie einer forensischen Speicherlöschung.
- HTTP-Antworten sind no-store, private, nosniff, mit Attachment-Disposition und isolierender CSP. Rendering nur in Plenaros eigenem Viewer nach Prüfung.
- Bestehende Downloads nicht unterstützter Dateitypen und automatische kleine CID-E-Mail-Bilder sind nicht Teil dieses Vorschau-Scans. Die bestehende Download-API und das native Znuny-Webinterface sind unverändert. ClamAV kann unbekannte Schadsoftware übersehen; ein erfolgreicher Scan ist keine Garantie für Ungefährlichkeit.

## Abnahme

- Scanner gesund, Signaturaktualisierung ohne Fehler. Ein bekannt unauffälliges PNG und PDF bis 5 MiB aus Testticket öffnen; Bild/PDF-Seiten und Schließen testen. Kein automatischer Download.
- Erneut öffnen innerhalb von drei Minuten; nach Ablauf erneut abrufen. Zwei Benutzer getrennt testen.
- Scanner stoppen und nach Cacheablauf Datei erneut öffnen: Fehler statt Darstellung; Scanner danach wieder starten.
- ClamAV separat mit einer standardisierten, harmlosen EICAR-Testdatei prüfen (keine reale Schadsoftware).
- Tests im Repository decken MIME-Prüfung, Cache-Lebensdauer, ClamD-Protokoll sowie blockierte Vorschau und Blob-Aufräumen ab. Produktive Scanner-/PDF-End-to-End-Abnahme bleibt separat erforderlich.

Referenzen: https://docs.clamav.net/manual/Usage/ClamdProtocol.html und https://mozilla.github.io/pdf.js/getting_started/ .
