# Plenaro 2.0 – 7-Phasen-Roadmap

Stand: 28.10.2026. Die ersten vier Meilensteine folgen dem bisherigen Gespräch (Docker → Weboberfläche → lokale Benutzer → Entra). Die späteren Phasen sind anhand des tatsächlich vorhandenen Desktop-Codes für eine vollständige Webmigration **konkretisiert**, nicht wörtlich aus einer früheren Liste übernommen.

Legende: `[x]` = implementierter Code / vorhandene Datei; `[ ]` = offen. Ein grüner CI-Lauf und ein echter Starttest sind zusätzliche Abnahmekriterien.

## Phase 1 – Repository & Docker-Basis (CI bestanden, Zielhost teilweise geprüft)
- [x] Neues getrenntes Plenaro-2.0-Repository, README und Architektur dokumentieren
- [x] Source-Available-Community-Lizenz und gewerbliche Zweitlizenzierung dokumentieren (Vertragstext vor Vermarktung juristisch prüfen)
- [x] .gitignore, .dockerignore, .env.example und Secret-Grundregeln
- [x] .NET-10-Webdienst mit Live-/Datenbank-Readiness-Endpoints
- [x] PostgreSQL 17, Docker-Volume, internes Netzwerk und Healthcheck
- [x] Versioniertes PostgreSQL-Basisschema (Tasks, Segmente, Zeitbuchungen, globale Einstellungen), Transaktionsmigrationen und Schema-Readiness hinzugefügt
- [x] Migrationen gegen nachträgliche Änderungen, Downgrades und parallelen Containerstart abgesichert
- [x] Docker Compose mit PostgreSQL, Web und NGINX-Reverse-Proxy; Port 80/443 und LAN-Bindung konfigurierbar
- [x] Minimale Browser-Statusseite
- [x] GitHub Actions CI: Build, Docker-Compose-Syntax, Build & HTTP-Smoke-Test (Workflow hinzugefügt, Lauf noch zu prüfen)
- [x] GitHub Actions: .NET-Build, Compose-Syntax und Docker-HTTP-Smoke-Test erfolgreich ([CI #1](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/40755261974))
- [x] Nutzer bestätigt: Docker läuft, Webseite per curl auf dem Host erreichbar (28.10.2026)
- [ ] LAN-Zugriff via Host-IP nach Update und Datenpersistenz/Neustart am Zielhost prüfen (CI prüft Neustart und Persistenz automatisch)
- [ ] Lizenz juristisch final prüfen (vor kommerziellem Release)

**Abnahme:** `http://127.0.0.1` zeigt den Webstatus, `/health/ready` prüft PostgreSQL und Persistenz über Neustarts.

## Phase 2 – Browseroberfläche (Web-Shell implementiert)
- [x] React 19, TypeScript, Vite, Routing und Docker-Multi-Stage-Build
- [x] Responsive Navigation: Heute, Woche, Aufgaben, Tickets, Wiki, KI, Berichte, Einstellungen
- [x] Login-, Logout- und Fehlerseiten als noch funktionslose Platzhalter
- [x] Öffentliche Startseite mit Live-Status und Lade-/Fehlerzuständen
- [x] Responsive Mobile/Desktop-Darstellung und Hell-/Dunkelmodus
- [x] UI-Komponententests und HTTP-Smoke-Tests
- [x] Geschützte Placeholder-Ansichten mit Sessionprüfung und lokale Login-/Logout-Seiten
- [x] Login ohne App-Navigation, Setup-Assistent automatisch bei leerer Datenbank
- [x] Classic-Layout für Aufgaben und Ticketansicht (Liste links, Detailbereich rechts) mit responsivem Theme, horizontal kategorisierte Einstellungen
- [ ] E2E-Browserprüfung und Accessibility-Audit

**Abnahme:** Navigierbare Weboberfläche, geschützte Bereiche ohne Session nicht erreichbar.

## Phase 3 – Benutzer, Admin & Rechte
- [x] Versioniertes PostgreSQL-Basisschema für Aufgaben, Segmente und Zeiten (Vorarbeit aus Phase 1)
- [x] ASP.NET-Identity-Schema/Versionierung für Nutzer, Rollen und Sessions (inkl. Eigentümer-FKs)
- [x] Einmalige Ersteinrichtung: automatischer Browser-Assistent mit hostseitig erzeugtem Einmalcode (15 Minuten), gesicherter Admin-Erstellung und serverseitiger Sperre nach Installation
- [x] Adminoberfläche: lokale Benutzer anlegen, Anzeigename ändern, sperren/entsperren, Kennwort ändern/resetten
- [x] Identity-Passworthashes, Cookie-Sessions, CSRF, Login-Rate-Limit/Lockout und grundlegende API-Autorisierung
- [x] Admin/User-Rollenverwaltung mit serverseitiger RBAC-Prüfung und Schutz des letzten aktiven Admins
- [x] Persistente persönliche Einstellungen je Benutzer und globale Instanzeinstellungen mit getrennten API-Endpunkten (Phase 3)
- [x] Serverseitige Admin-Prüfung für Änderungen globaler Einstellungen; persönliche Daten über Session-ID isoliert
- [ ] Separater verschlüsselter Secret-Speicher für Integrationen, fachliche Datentrennung und Rechteprüfung künftiger Datenendpunkte
- [x] CI-Integrationstests: Admin-Bootstrap, Login/Logout, CSRF, Zugriffscheck und Session-Neustart

**Abnahme:** Zwei Nutzer sehen ohne ausdrückliche Freigabe nicht gegenseitig Daten.

## Phase 4 – Microsoft Entra & Outlook
- [x] Entra App Registration: Tenant, Web Redirect URI, OIDC Authorization Code + PKCE, Minimal-Scopes dokumentieren (docs/ENTRA_SSO.md)
- [x] Optionaler Single-Tenant-Entra-SSO-Login neben lokalen Benutzern (HTTPS, tenantgebundene ID-Token-Validierung, keine automatische Registrierung)
- [x] Entra-Identität nur nach ausdrücklicher Kontoverknüpfung im lokalen Login via tid+oid und bestätigendem CSRF-POST zulassen; E-Mail-Matching ausgeschlossen
- [x] Serverseitige delegierte Graph-Tokenverwaltung: OAuth/PKCE und pro User AES-256-GCM-verschlüsselte Refresh-Tokens (Token-Rotation beim Abruf)
- [x] Entra Tenant-/App-IDs und Client-Secret ausschließlich im verschlüsselten Vault, Konfiguration über Admin-Weboberfläche (ohne Entra-.env)
- [ ] Realer Entra-Tenant-/Graph-End-to-End-Test offen
- [x] Graph-Kalender-Lese-API und lokale 5-Tage-Wochenansicht mit optionalen Outlook-Terminen implementiert (maximal 100 Graph-Einträge; ohne echte Entra-Abnahme)
- [ ] Microsoft Graph: Termine erstellen/ändern/löschen, Pagination und Mehrkalender-Auswahl
- [ ] Busy-Anzeige, Teams-Einladungen und Fokusblock-Synchronisation
- [ ] Echter Entra-Tenant-SSO-/Verknüpfungs-/Konflikttest auf dem Zielhost (ohne Microsoft-Credentials in CI)

**Abnahme:** Lokal-/SSO-Login parallel; Outlook funktioniert ohne lokal installiertes Outlook.

## Phase 5 – Aufgaben, Zeit, Berichte
- [x] Tasks-CRUD mit Status, Priorität, Tags, Suche und Fällig-/Planungsdatum; Eigentümerabfragen serverseitig abgesichert
- [x] Lokale Heute-Ansicht: Aktuell/Geplant/Erledigt aus eigenen PostgreSQL-Aufgaben (ohne Demo-Daten)
- [ ] Vollständige Funktionsparität/Migrationsmapping mit dem Desktop-Programm und weitere Aufgabenfilter
- [x] Lokales 7-Tage-Kalenderraster (06–18 Uhr) mit geplanten Aufgaben und UL/AM/BR, **auch ohne Entra/Outlook**
- [x] Bestehende Zeitsegmente im lokalen Mo–Fr-Kalender mit Drag-and-drop und Halb-Stunden-Raster planen (PR #26; Host-Abnahme offen)
- [x] Aufgaben-Timer und manuelle Korrekturminuten (separat von gestrichener Kommen/Gehen-/Pausenerfassung)
- [x] Monatsberichte für aufgabenbezogene Timer und Korrekturen, monatsgenaue Europe/Berlin-Grenzen, CSV-Export ohne fremde Nutzer (PR #25; CI-/Host-Abnahme offen)
- [ ] Homeoffice-Workflows
- [ ] Aufgaben teilen, Einladungen und Besitzerrechte
- [x] Erster SQLite-v1–v32-Importpfad: lokaler bereinigender Export, Web-Vorschau, transaktionaler Import, ID-Deduplizierung und snapshotgeschützte Rücknahme ([PR #29](https://github.com/DerHausmeisterKrause/plenaro/pull/29), [CI erfolgreich](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41136074441); Host-Abnahme offen)
- [ ] **Vollständige** Datenmigration/Classic-Feldparität (Ticket-/Outlook-Verknüpfungen, Buchungen, weitere Einstellungen und Legacy-Datenmengen)

**Abnahme:** Kernabläufe der Desktop-Aufgaben-/Zeitverwaltung funktionieren im Browser.

## Phase 6 – Ticketsysteme / ITSM
- [x] Ticket-Zwei-Spalten-Weboberfläche als leeres Grundlayout (noch **ohne** Ticketdaten/Adapter)
- [ ] Adapter-Interface und konfigurierbare Verbindung je Ticketsystem
- [ ] Znuny/OTRS als Standard: sichere Anmeldung, Session, TicketSearch/Get
- [ ] Tickets zuweisen, aktualisieren, beantworten, schließen und Zeiten buchen
- [ ] Neue Aufgaben/Kandidaten, Filter/Ausschlusswörter und Delta-Synchronisation
- [ ] Zusätzliche Ticketsysteme als Erweiterungen ohne Kernänderung
- [ ] ITSM-Workflows: Incident, Problem, Change
- [ ] Kundenportal und KI-gestützter Intake als ausbaubare Funktionen
- [ ] API-, Rechte-, Retry- und Rate-Limit-Tests

**Abnahme:** Bestehende Znuny-Workflows sind funktionsgleich; weiterer Adapter technisch möglich.

## Phase 7 – Wissen, KI, Erweiterungen & Release
- [ ] Wiki/Knowledge-Quellen, Suche, Dokumentenverarbeitung
- [ ] KI-Chat, Knowledge-Retrieval und optionale lokale/OpenAI-kompatible Anbieter
- [x] Lokale Zeitsegment-Reminder mit konfigurierbarem Vorlauf, Startbenachrichtigung, Glocke im Web, gelesen-Status und optionalen Browserhinweisen ([PR #28](https://github.com/DerHausmeisterKrause/plenaro/pull/28), [CI erfolgreich](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41134694355); Host-Abnahme offen)
- [ ] Ticketbenachrichtigungen und Favoriten erst nach Znuny-/OTRS-Anbindung; Web Push bei geschlossenem Browser
- [ ] Erweiterungs-/Add-in-Schnittstelle mit Sicherheitsmodell
- [ ] DSGVO-Konzept, Audit, Backups/Restore-Tests und Betriebsdokumentation
- [ ] End-to-End- und Migrationstests auf Funktionsparität mit Desktop
- [ ] Versionierung, Releases, Docker-Image-Publishing und Upgradepfade
- [ ] Kommerzielle Lizenz- und Betriebsbedingungen finalisieren

**Abnahme:** Nachvollziehbar getestete produktionsreife Webversion mit Dokumentation und Updatepfad.

## CI-Strategie
- [x] Ein GitHub-Actions-Workflow nur für PRs und manuell, mit Build und einem Docker-Smoke-Test (statt redundanter Testschritte/Läufe)

## Sicherheitsgrundlage für Phasen 3/4 – Stand dieses Schritts

- [x] NGINX-Reverse-Proxy direkt in der Standard-Compose; HTTP auf Port 80 oder optional HTTPS auf 443 mit automatischer HTTP-Weiterleitung und Zertifikat-/Schlüsselpfaden aus .env
- [x] `Secure`-Cookies und HTTPS-Pflicht im sicheren Modus, interne HTTP-Health-Ausnahme
- [x] Internes ASP.NET 8080 nicht mehr am Host veröffentlicht; Forwarded Headers ausschließlich vom NGINX-IP-Endpunkt
- [x] Verschlüsselter Secret-Vault mit PostgreSQL-Migration 0007, externem Masterkey, User-/Admin-Scopes und CSRF
- [x] Verwaltung der Secret-Namen, Erstellung/Aktualisierung/Löschung im Web (ohne Klartextabruf)
- [ ] NGINX-LAN-HTTPS, Zertifikatvertrauen und FQDN/IP-Zugriff auf dem Zielhost manuell abnehmen
- [ ] Master-Key-Rotation, verschlüsselter DataProtection-Keyring und Secret-Recovery-Konzept
- [ ] Vollständige Phase-3-Sicherheitsprüfung inklusive Backup-Restore und Bedrohungsmodell

## Anpassung nach Nutzer-Checkliste – UL / AM / BR statt Arbeitszeiterfassung

- [x] UL und AM als exklusive Tagesarten sowie BR als unabhängigen Tagesmarker aus der Desktop-Version übernommen (ohne Kommen/Gehen/Pausen)
- [x] Monatsmarker, Tagesauswahl und Markeränderungen in Heute und Wochenplanung
- [x] Persönlich konfigurierbare jährliche UL-Tage und monatliche BR-Tage; Warnungen bei ausgeschöpftem oder überschrittenem Kontingent
- [x] Serverseitige Benutzerisolation und CSRF, additive Versionierung der Marker-/Budgettabellen
- [ ] Browser-Abnahme auf dem Zielhost und Übernahme früherer SQLite-Tagesmarker
- [ ] Feiertage/Halbtage/Übertrag von Resturlaub (nur falls später ausdrücklich gewünscht)

**Entfallen nach Nutzerentscheidung:** Kommen, Gehen, Pausen, Sollzeit und Arbeitszeiterfassung in diesem Aufgabenbereich. Die vorhandenen neutralen Datenbanktabellen für Zeitbuchungen werden nicht gelöscht, um bestehende Daten und spätere optional gewünschte Funktionen nicht zu beschädigen.

## UI-Referenz – Desktop Plenaro Classic

- [x] Classic-Bedienmuster für Aufgaben: kompakte Datensatzliste links, Detail/Editor rechts, Schnellhinzufügen, Statusfilter und Suchfeld
- [x] Gleiches Layout als datenehrliche Vorlage für Ticketansicht (ohne angebundenen Znuny-Adapter)
- [x] Lokaler Kalender als Standard statt blockierender Entra-Einrichtungsseite; Microsoft Graph nur als optionale Quelle
- [x] Einstellungsbereiche über horizontale Kategorien sortiert; bestehende Light-/Dark-Variablen genutzt
- [ ] Echte Ticketliste/Details aus Znuny/OTRS und Desktop-Feldparität
- [ ] Heute/Task-Detailbereiche vollständig an Classic-Referenz angleichen, Zeitsegmente und Reports ergänzen

Siehe [Classic UI](CLASSIC_UI.md).

## Classic-Heute und Entra-Admin-Konfiguration (PR #22)

- [x] Screenshottreue Heute-Aufteilung: Schnellerfassung, Arbeitstag und vier Aufgabenreiter links; Aufgabendetails, Timer/Segment-Platz für Funktionen rechts
- [x] HO als zusätzliches unabhängiges Tagesmerkmal in der PostgreSQL-Migration 0010 und in der Tagesmarker-API
- [x] Wochenkalender standardmäßig als klassische 5-Tage-Arbeitswoche (Mo–Fr), lokal ohne Microsoft nutzbar
- [x] Entra-Tenant-ID, Client-ID, Client-Secret ausschließlich über Administrator-UI, verschlüsselt im Vault, Laufzeitänderung ohne Dockerneustart
- [x] Kalender-Scopes bei bereits verknüpften Benutzern im SSO-Flow; Refresh-Token nur im verschlüsselten User-Vault
- [ ] Live-Entra-End-to-End-Test für Konto-/Token-Verknüpfung auf echtem Microsoft-Tenant
- [ ] Detailfelder Ticket-URL, Timer-/Buchungslogik, Zeitsegmente, Znuny-Kandidaten und echte Terminplanung vervollständigen
- [ ] Datenbankpasswort auf Docker-Secrets umstellen (derzeit noch Klartext als Environment-Wert im Compose)

## Aufgabenzeit und Segmentplanung – nächste Phase-2-Umsetzung

- [x] Aufgabentimer: Start/Stop in „Heute“, serverseitig persistente Sessions, maximal ein aktiver Timer pro Benutzer
- [x] Manuelle Zeitkorrekturen (+/−15/30/60 Minuten und freier Betrag) mit optionalem Notiz-/Korrekturjournal
- [x] Segment-CRUD mit Datum, Start-/Enduhrzeit, Kurzbeschreibung und Teilnehmer-E-Mail-Adressen im rechten Classic-Panel
- [x] Eigene Segmente im lokalen Wochenkalender (Mo–Fr, Zeitraster) und in Tagesdetails darstellen, auch ohne Microsoft
- [x] Auth-, CSRF-, Cross-User-Isolation und Integrationschecks für Timer, Anpassungen, Segment-CRUD im bestehenden CI-Job
- [x] Lokale Konflikt- und Verfügbarkeitsprüfung für **eigene** Zeitsegmente: Vorschau, Kollisionen beim Erstellen, Bearbeiten und Verschieben verhindern (CI-/Host-Abnahme offen)
- [ ] Vollständige Kalender-/Teilnehmer-Verfügbarkeitsprüfung über Microsoft Graph, Kalendereinladungen und Outlook-/Teams-Synchronisation
- [x] Abgeschlossene Desktop-Aufgabenzeitbuchungen und geplante Segmente über SQLite-Exportpfad übertragbar (CI/Host-Abnahme getrennt)
- [ ] Vollständige Altimport-Feldparität und Browser-Abnahme
- [x] Drag-and-drop der lokalen Zeitsegmente mit Konfliktprüfung (PR #26)
- [ ] Weitere Planungsoptimierungen und erweiterte Tages-/Segmentberichte
- [x] Kommen/Gehen/Pausen/Sollzeit weiterhin bewusst ausgeschlossen


## Monatsreport – Aufgabenzeit und CSV

- [x] `GET /api/v1/reports/month?month=JJJJ-MM` liefert für den authentifizierten Nutzer aufgabenbezogene Timersitzungen und Korrekturen; Berichte zeigen nur Aufgaben mit entsprechender Monatsaktivität.
- [x] `GET /api/v1/reports/month.csv?month=JJJJ-MM` lädt Excel-kompatibles UTF-8/CSV (Semikolon) mit entschärften Formelnamen herunter.
- [x] Monatsgrenzen in Europe/Berlin einschließlich Sommerzeit; Monatsübertritte werden auf den jeweiligen Monatsabschnitt begrenzt, laufende Timer bis zum Abrufzeitpunkt gezählt, Korrekturen im Erfassungsmonat. Keine Anwesenheits-, Pausen- oder Sollzeitberichte.
- [ ] Vollständige CI-Abnahme, Browser-Abnahme am Zielhost und Reports nach Desktop-Altimport.\n
## Wochenplanung – lokale Zeitsegmente verschieben

- [x] Zeitsegmente im Mo–Fr-Kalender 06:00–18:00 in 30-Minuten-Schritten per Drag-and-drop verschieben.
- [x] Formular für Touch und Tastatur im Tagesdetail mit Auswahl von Wochentag und Startzeit.
- [x] Segmentdauer beibehalten; Notizen, Teilnehmer und Aufgabenbezug unverändert lassen.
- [x] Geschützte schedule-PATCH-API mit User-Filter, CSRF und optimistischer Konflikterkennung (409).
- [ ] Manuelle Browser- und Touch-Geräte-Abnahme auf dem Zielhost.

## Nächster Schritt – lokale Verfügbarkeit (PR nach CI)
- [x] Authentifizierte API `GET /api/v1/segments/availability?start=...&end=...&excludeId=...` für eigenen Segmentkalender.
- [x] Serverseitig keine lokalen Doppelbuchungen bei CREATE/PUT/PATCH: halb-offene Zeitintervalle, exklusive Ausnahme für das bearbeitete Segment, Besitzerprüfung, CSRF und per-User-transaktionale PostgreSQL-Advisory-Locks.
- [x] Eintragungsformular meldet freie/belegte lokale Zeit; Wochenansicht warnt bei lokal sichtbaren Überschneidungen und zeigt bei angebundenem Outlook möglicherweise belegte eigene Termine als Hinweis (kein Graph-Schreibzugriff).
- [ ] Graph-/Teams-Teilnehmer-Free/Busy und Einladungen, externe Kalender-Änderungen; noch nicht umgesetzt.
- [ ] Host-/Browser-Abnahme, unabhängig vom CI-Status.

## Web-Reminder und Benachrichtigungen (Phase 5)

- [x] Geplante/aktive Aufgabensegmente erzeugen zwei Erinnerungstypen wie das Desktop-Plenaro: Vorlauf (Standard 2 Min.; 0 = aus) und Start.
- [x] Userbezogene, persistente Einstellungen in PostgreSQL; individuelle Aktivierung/Deaktivierung und Vorlauf 0–1440 Minuten.
- [x] 20-Sekunden-Abfrage bei geöffneter Webseite; Benachrichtigungsglocke mit ungelesenen Hinweisen, Browser-Desktop-Hinweise nach expliziter Freigabe (HTTPS vorausgesetzt).
- [x] Quittierung pro Benutzer, Segment, geplantem Startzeitpunkt und Erinnerungstyp dauerhaft gespeichert. Alter Startzeitpunkt nach Verschieben löst keine veraltete Quittierung aus.
- [x] Benutzerisolation, Auth, CSRF, Deaktivierung und Zeit-/Start-Erinnerungen im bestehenden CI-Docker-Test.
- [ ] Browser-Schließen / PWA-Web-Push / Service Worker und dauerhafte Background-Push-Zustellung.
- [ ] Ticket-/Znuny-Benachrichtigungen, bevor der Ticketsystem-Adapter implementiert ist.
- [ ] Manuelle Desktop-/Mobilbrowser-Abnahme auf dem Zielhost.

## Classic-Import in Phase 5 – Teilmigration

- [x] `tools/export_classic.py`: SQLite 1–32 schreibgeschützt öffnen und als bereinigtes JSON exportieren, optional `settings.json` mit strikt erlaubtem `ReminderLeadMinutes`-Wert. Keine Zugangsdaten exportieren.
- [x] Eigene Aufgaben, geplante Zeitsegmente mit Teilnehmern, abgeschlossene `time_logs` und UL/AM/BR/HO aus `work_days`; keine Anwesenheit, Pausen oder offene Timer.
- [x] Web-Einstellungen → Datenimport mit Vorschau, Hinweisen, Zählung, expliziter Bestätigung und Importhistorie.
- [x] In einer PostgreSQL-Transaktion importieren, UUIDs/Benutzerbesitz absichern, Wiederholungsimporte ohne Duplikate, bestehende eigene Marker nicht überschreiben.
- [x] Manuelle batchbezogene Rücknahme mit Daten-Snapshots; bei bearbeiteten Datensätzen/abhängigen Einträgen verweigern.
- [ ] Volle Classic-Übernahme von Ticketbuchungen, Zeitanpassungen, Outlook-/Znuny-/Wiki-Einstellungen und Zuordnungen.
- [ ] Host-Abnahme mit einer echten Kopie der bisherigen `TaskTool.db` und `settings.json` nach PostgreSQL-Backup.

## Classic-Import Phase 5 – historisches Archiv (PR #30)

- [x] Exportformat 2 ergänzt Desktop-Aufgabenfelder (Pin, Znuny-Zuweisung, Status, Ticketadresse, gebuchte Classic-Zeit und alte Outlook-ID), historische Ticketbuchungen und monatsbezogene Nachkorrekturen.
- [x] Historische Daten liegen in separaten PostgreSQL-Archivtabellen mit Benutzertrennung. **Keine** automatische Rückbuchung nach Znuny, keine aktive Outlook-Bindung und **keine** Verdopplung der normalen Aufgabenzeiterfassung.
- [x] Aus `settings.json` werden nur nichtgeheime Planungs-/Hinweis-Einstellungen als inaktive Referenzdaten archiviert (z. B. Wochenenden, Standardsegmentdauer, Kalender-Zeitzone). Einzig der zuvor implementierte `ReminderLeadMinutes` wird als aktive Einstellung übernommen.
- [x] Anzeige alter Aufgabenfelder/Ticketbuchungen im Aufgaben-Detail, Archivübersicht und importweise Einsicht gespeicherter Settings; Batch-Rücknahme und Quell-Deduplizierung erweitert.
- [x] Exporter entfernt in Ticket-URLs Credentials und fremde Query-Parameter; Zugangsdaten/API-Schlüssel bleiben ausgeschlossen.
- [ ] Historische Ticketbuchungen erst nach fachlicher Zuordnung zu Znuny/OTRS in eine echte Buchungsintegration übernehmen; keine automatische Wiederholung alter Buchungen.
- [ ] Monatskorrekturen als eigene historische Berichtsdaten anzeigen bzw. mit verifizierter, nicht doppelter Zählung in Berichte integrieren.
- [ ] Migration mit echter Classic-Datenbankkopie/Host prüfen und weitere individuelle/SSO/Outlook-Konfiguration manuell einrichten.

## Znuny-/OTRS-Konfiguration aus Classic (Phase 6)

- [x] Die serverweiten GenericTicketConnectorREST-Standardwerte aus `project_tool/Models/AppSettings.cs` und `ZnunySyncPolicy.cs` übernommen: `https://SERVER/index.pl` und `https://SERVER/nph-genericinterface.pl/Webservice/GenericTicketConnectorREST` als Platzhalter; SessionCreate `POST /Session`, TicketSearch `GET /Ticket`, TicketGet `GET /Ticket/{TicketID}`; zusätzliche TicketUpdate/Create-/Agentenlisten-/Dynamikfeld-Routenvorlagen dokumentiert und konfigurierbar.
- [x] Default: Session-Authentifizierung; Intervall 15 Min., Kandidatenintervall 5 Min., SearchLimit 100 und ArticleLimit 20; nur offene Tickets, Owner/Responsible aktiv. Erstellen, Update und Zeitbuchung werden **nicht** durch Hinterlegen der Einstellungen aktiviert.
- [x] Systemweite Serverkonfiguration ausschließlich durch Administrator; persönliche Agenten-ID, Kandidaten-ID und Suchfilter individuell. Znuny-Passwort AES-GCM-verschlüsselt im bestehenden Secret-Vault, nie auslesbar über Browser.
- [x] Auf Benutzeraktion beschränkter SessionCreate-Verbindungstest mit TLS, ohne Weiterleitungen, ohne Rückgabe/Logging von SessionID oder Passwort. Sicherheit: kein HTTP-/Loopback-/Metadata-Ziel, begrenzte Antwortgröße und Zeitlimit.
- [ ] Echter TicketSearch/TicketGet-Datenfluss und Anzeige in Neue Aufgaben; danach Delta-Synchronisation/Benachrichtigungen und Ticketaktionen.
- [ ] Verbindungstest gegen eine echte Znuny-/OTRS-Instanz des Benutzers (Platzhalteradresse wird nicht verbunden).

## Zentraler Znuny-Sync (Phase 6, PR #32)

- [x] **Eine** technische Znuny-Verbindung pro Instanz, zentraler verschlüsselter Serviceaccount und PostgreSQL-Ticketcache. Benutzer führen **keinen eigenen** Remote-TicketSync aus.
- [x] Wiederaufnehmbarer Erstimport der letzten 365 Tage (administrativ 7–3650 Tage einstellbar) in maximal acht Tagesschritten pro Hintergrundlauf. Der Cursor wird erst nach erfolgreich gelesenen Details aktualisiert.
- [x] Danach Delta-Search mit `TicketLastChangeTimeNewerDate`, 24 Stunden Überschneidung, Upsert nach TicketID; ältere offene Tickets werden in einem gesonderten fortsetzbaren 90-Tage-Fenster-Sweep ab Jahr 2000 aufgenommen und regelmäßig neu geprüft.
- [x] Zeitfenster bei vollständig erreichtem SearchLimit rekursiv unterteilen, niemals abgeschnittenen Suchlauf als vollständig markieren; API-Request-Budget, Status und Fehlercodes in Admin-Ansicht sichtbar.
- [x] Admin aktiviert/deaktiviert Sync; konfiguriert 365-Tage-Import, Delta-/Open-Intervalle, Budget und Service-Credentials. Expliziter Reset bei Serverwechsel; zentraler DB-Advisory-Lock verhindert parallele Instanzläufe.
- [x] Persönlicher Ticketcache-Zugriff ausschließlich über **administrativ genehmigte** Zuordnung Benutzer→Znuny-Agent, nicht über ein manipulierbares persönliches Agenten-ID-Feld. Owner/Responsible-Sicht und neuer-Ticket-Reiter im Ticketarbeitsplatz.
- [ ] Integration mit realem Znuny-Server und breit abweichenden REST-Mappings; API-Endpunkte können je Webservice-Konfiguration variieren.
- [ ] Kandidatenpool über Queue-/Rollenfreigaben und Ticketaktionen, Benachrichtigungen, Ticketartikeleinsicht.
- [ ] Große Alt-Datenbestände und Laufzeiten auf echtem Server messen, Suchlimit/Sessionfehler verifizieren.

## Znuny-Agenten-ID bei Benutzeranlage (Phase 6, PR #34)

- [x] In der **Administration → Benutzerverwaltung** die Znuny-Agenten-ID beim
  Anlegen und beim Bearbeiten direkt hinterlegen (optional; null = kein Zugriff).
- [x] Die Zuordnung atomar mit der Benutzeranlage/-bearbeitung in
  `znuny_approved_agent_bindings` speichern, Eindeutigkeit erzwingen
  und Doppelbelegungen als HTTP 409 zurückweisen, ohne halbfertige Benutzer.
- [x] Benutzer können ihre Agenten-ID weder über das persönliche Formular
  noch über die persönliche API ändern; GET zeigt nur die Admin-Freigabe.
- [x] Obsoletes Benutzerfeld aus PostgreSQL entfernen, vorhandene
  Adminfreigaben erhalten und separaten Bindings-Editor/API abschaffen.
- [x] Ticketcache für frisch angelegte Benutzer ohne persönliche Znuny-Settings
  mit sicheren Standardfiltern für Owner/Responsible lesbar machen.
- [ ] Live-Abnahme von SSO-Provisionierung und tatsächlichem Znuny-System
  unabhängig von den geprüften lokalen Admin-/API-Flows.

## Classic-Heute-Reiter nach Znuny/OTRS-Workflow (PR #35)

- [x] **Heute**: ausschließlich persönliche Outlook-Termine des lokalen Tages sowie lokal geplante Znuny-Ticketsegmente, chronologisch sortiert; keine unsegmentierten lokalen Aufgaben
- [x] **Aktuelle Aufgaben**: offene Tickets, bei denen die administrativ freigegebene Agenten-ID Owner **oder** Responsible ist, unabhängig von heute
- [x] **Neue Aufgaben**: offene Tickets ohne echten Owner/Responsible oder mit dem administrativ konfigurierten Standard-OTRS-Agenten (standardmäßig ID 1); keine irreführende Suche nur nach Znuny-State `new`
- [x] **Erledigte Aufgaben**: geschlossene Tickets (Znuny `state_type=closed`) mit eigener Owner-/Responsible-Zuordnung
- [x] Persönliche Znuny-Ticket-Zeitsegmente lokal anlegen, anzeigen und entfernen; Datenbank mit Besitzerrechten; Tagesagenda per Europe/Berlin-Zeitfenster
- [x] Identische offene/neu/erledigte Filter im separaten Ticketsystem; keine externen Ticketänderungen ohne implementierte Znuny-Schreibaktionen
- [x] Serverprüfungen für CSRF, Agentenbindung, fremde Tickets, geschlossene Tickets, lokale Segmente und Ticket-Pool-Trennung
- [ ] Gegen echten Znuny-Server prüfen: Default-/Root-Agenten-ID, Status-Typen, tatsächliche Ticketdaten und neue Kandidaten
- [ ] Bereits im WPF gespeicherte Znuny-Ticket-Segmente ggf. gesondert importieren/migrieren
- [ ] Remote-Ticketaktionen, Nachrichten/Artikel, Dynamic Fields und Zeitbuchungen umsetzen

## WPF-Taskdetail in Heute / Aktuelle Aufgaben / Ticketsystem (PR #36)

- [x] Gemeinsam genutzter, responsiver Ticketdetailbereich nach
  `project_tool/Views/TaskDetailPanel.xaml`: Titel, Beschreibung,
  Ticket-URL, Status, Owner/Responsible, Timer/Buchung, Planung (Segmente)
- [x] Persönliche lokale Ticketbeschreibung und eigene Ticket-URL
  in PostgreSQL gespeichert; je Benutzer und Ticket autorisiert, CSRF und URL-Prüfung
- [x] Serverseitig persistenter Ticket-Timer (Start/Stop auch beim
  Schließen des Browsers) und manuelle Minutenkorrekturen (+15/+30/+1h und −)
  mit eindeutigem aktivem Ticket-Timer pro Benutzer
- [x] Segment-Editor nach Desktop-Schema: Datum, Beginn, Ende,
  Kurzbeschreibung, Teilnehmer, visuelle lokale Zeitübersicht, bestehende
  Segmente bearbeiten und löschen (lokale Daten, ohne Outlook-Einladungen)
- [x] Identische Ansicht in Heute, Aktuelle Aufgaben, Neue Aufgaben und
  im zentralen Ticketsystem; geschlossene Tickets behalten bearbeitbare
  lokale Notizen und Zeitdaten
- [x] WPF-Bereiche für Nachrichten, Zuweisungen, Wiki-Treffer und
  Znuny-Zeitbuchungen im Layout gekennzeichnet, aber ohne falsche
  Remote-Schreibaktionen: externe Funktionen bis zur sicheren Integration deaktiviert
- [ ] Artikelabruf, Ticketantwort, Übernahme/Zuweisung, Remote-Schließen,
  Dynamic Fields/Kostenstelle/Auftrag und Remote-Zeitbuchung
- [ ] Wiki-Verbindungen und echte automatische Wiki-Treffer pro Ticket
- [ ] Echte systemweite Graph-Kalender-/Teilnehmerverfügbarkeit und
  Termineinladungen für Segmente (Anzeige derzeit nur für dieses Ticket)
- [ ] Real-Znuny-Live-Abnahme der Detailfelder, WPF-Feldparität und
  Legacy-Import bisheriger Ticketzeitdaten

## Znuny live: Artikel, Antworten, Zuweisungen (Phase 4 / PR #37)

- [x] Gezielt einzelne Ticketartikel über Znuny GenericInterface lesen, nur mit administrativ freigegebener Agentenbindung; reine Textanzeige, kein fremdes HTML
- [x] Ticketantwort per Mail: Empfänger ausschließlich aus Kundenartikeln auf dem Server ermitteln, Nutzereingabe HTML-escaped, eigene Znuny-Zugangsdaten und Benutzerbestätigung
- [x] Besitzer und Verantwortlichen aus Znuny-Agentenliste auswählen, remote bestätigen und anschließend Cache aktualisieren; „Mir zuweisen“ separat
- [x] CSRF, Einzel-Ticket-Zugriff, geschlossene Tickets, begrenzter Requestumfang, getrennte Read-/Write-Rate-Limits, keine automatischen Wiederholungen unbestätigter Schreibaktionen
- [ ] Live-Znuny-Integration mit konkretem GenericTicketConnectorREST testen
- [x] Ticket-Schließen und Wiederöffnen mit persönlicher Znuny-Session und bestätigter Remote-Statusänderung; Cache-Update nach Bestätigung
- [ ] Wiki-Provider, Dynamic Fields und echte Znuny-Zeitbuchung ergänzen


## Anpassung 02.11.2026 – automatischer Entra-Zugang und gemeinsamer Arbeitsplatz (PR #38)

- [x] Entra-Single-Tenant-Identität anhand validiertem `tid:oid` beim ersten Login als neuen lokalen `User` ohne Passwort automatisch provisionieren; keine E-Mail-Zuordnung und keine Znuny-Freigaben
- [x] Manuelle Kontoverknüpfungs-UI/-Endpunkte für neue Anmeldungen entfernen; bestehende explizite Entra-Logins beibehalten
- [x] Kandidaten-Benutzer-ID aus persönlichen Znuny-PUT-Anfragen entfernen und serverseitig verbieten; nur Administrator darf die ID bei Benutzeranlage/-bearbeitung setzen oder entfernen
- [x] Individuelle Kandidaten-ID in Ticketliste, Detail, Segmentplanung und Remote-Berechtigungsprüfung verwenden; fehlende ID erteilt keine Kandidatenrechte
- [x] Arbeitsplatz als zentrale Ansicht statt separater Aufgaben-/Ticketsystem-Navigation; lokale Aufgaben als Unterreiter, Legacy-Pfade als Weiterleitungen
- [ ] Vollständigen GitHub-CI-/Docker-Test für PR #38 abschließen
- [ ] Microsoft-SSO-Erstlogin mit echtem Tenant samt Admin-Sichtbarkeit und Outlook-Token live abnehmen
- [ ] Echten Znuny-Ticketbestand gegen Owner/Responsible/Standard-Agenten-IDs, offene und geschlossene Ticketstatus testen
- [ ] Browser-/Hostabnahme inklusive bestehenden lokalen Konten und Altlinks durchführen


## 02.11.2026 – Benutzer löschen (Phase 3, nach PR #38)
- [x] Admin-only DELETE-API mit CSRF, Benutzer-ID und exakt eingegebener Kontomail als Bestätigung
- [x] Eigenes Administratorkonto und letzten aktiven Administrator vor Löschung schützen
- [x] Benutzer ohne geschäftliche Aufgaben löschen; Entra-Logins, Benutzerrollen, persönliche Einstellungen, Znuny-Zuordnungen, Kalendertokens und Zeitplanungen über FK-Cascades entfernen
- [x] Aktive Aufgaben und Zeitbuchungen vor unbeabsichtigter Löschung schützen (HTTP 409); vorhandene DB-RESTRICT-FKs beibehalten
- [x] **Korrigiert:** Gelöschte Entra-Konten dürfen sich erneut anmelden und werden als neue Standardbenutzer ohne die alten Znuny-Rechte erstellt; bisherige Sperrliste mit Migration 0022 aufheben (PR #40)
- [x] Benutzerverwaltung mit zweistufiger Löschbestätigung und klaren Hinweisen
- [x] Ursprünglicher CI/Docker-Smoke-Test zu PR #39 erfolgreich (einschließlich mittlerweile zurückgenommener Entra-Sperre); Regressionstest für erlaubte Neuanmeldung ergänzt in PR #40
- [ ] Live-Abnahme an lokaler Plenaro-Instanz einschließlich echtem Entra- und Znuny-Zugriff

### Korrektur 02.11.2026 – Entra-Neuanmeldung nach administrativer Löschung (PR #40)
- [x] Entra-Sperrprüfung aus der OIDC-Provisionierung entfernen
- [x] Permanente Sperreinträge beim Löschen von Entra-Konten vermeiden
- [x] Bestehende Einträge aus PR #39 per Migration 0022 vollständig entfernen
- [x] Beim erneuten SSO-Login bleibt die `tid:oid`-Prüfung erhalten; ein neues Benutzerkonto erhält neue Plenaro-ID und nur Rolle `User`, keine vorherigen Ticketfreigaben
- [x] UI-Hinweis im Löschdialog korrigieren
- [x] CI inkl. Regressionstest bestanden ([Run #41443551269](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41443551269))
- [ ] Reale Entra-Anmeldung mit zuvor gelöschtem Benutzer auf dem Host bestätigen

### 02.11.2026 – Historischen Znuny-Erstimport neu starten (Phase 6)
- [x] Admin-Button „Erstimport neu starten“ in der zentralen Synchronisationskonfiguration
- [x] Geänderten Rückblickzeitraum (z. B. 7 → 365 Tage) beim Buttonklick zunächst speichern
- [x] CSRF-geschütztes Admin-API für erneuten Bootstrap-Scan mit konfliktfreier PostgreSQL-Advisory-Sperre
- [x] Bootstrap-Datum, Cursor und Abschluss zurücksetzen; bestehende zentral zwischengespeicherte Tickets, persönliche Segmente, Notizen und Zeiterfassungen bleiben erhalten
- [x] Vorherigen Delta-Watermark bei einem erneuten Bootstrap bewahren, um während des Nachladens geänderte Tickets nachträglich abzugleichen
- [x] Fehlertolerantes Nachladen in bestehenden Tagesabschnitten; nach einem unmittelbaren Lauf fährt der Hintergrunddienst weiter
- [x] Frontend-Test für Ein-Klick-Änderung des Rückblickzeitraums und nicht-destruktiven Neustart ergänzt
- [x] Automatisierte Docker-/API-Prüfung und Gesamt-CI erfolgreich ([CI #41446503580](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41446503580), [PR #41](https://github.com/DerHausmeisterKrause/plenaro/pull/41))
- [ ] Live-Abnahme am echten Znuny-Server (historischer Ticketimport, Dauer, Rate-Limits)


### 02.11.2026 – E-Mail-Darstellung im Znuny-Ticketdetail (Phase 6)
- [x] MIME-`ContentType` der Znuny-Artikel zum Frontend durchreichen; HTML nicht mehr per Regex in unformatierten Text umwandeln
- [x] Originale HTML-Tabellen, Absätze und Zitate in einer isolierten `iframe sandbox`-Vorschau darstellen
- [x] Restriktive Content-Security-Policy für E-Mails (keine Skripte, Remote-Bilder, Formulare, Webanfragen oder externe Schriften); kein `dangerouslySetInnerHTML` im App-DOM
- [x] Plain-Text-E-Mails mit erhaltenen Zeilenumbrüchen, korrekt umgebrochenen langen Zeilen, lesbaren Absender-/Empfängerdaten anzeigen
- [x] HTML-Erkennung auch ohne von Znuny gelieferten MIME-Typ und Vergrößern der Vorschau für lange E-Mails
- [x] Automatisierte Tests für HTML-Mails, Plaintext, Sicherheit und MIME-Erkennung ergänzt
- [x] CI-Build + Docker-Smoke-Test erfolgreich ([CI #41449089718](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41449089718), [PR #42](https://github.com/DerHausmeisterKrause/plenaro/pull/42))
- [ ] Echtes Znuny-Livebeispiel mit HTML, Inline-Bildern, Tabellen und Sonderzeichen prüfen


### 02.11.2026 – Znuny-E-Mail-Anhänge und eingebettete CID-Bilder (Phase 6)
- [x] Znuny-TicketGet mit `Attachments=1` und `GetAttachmentContents=0` für reine Datei-Metadaten ohne automatische Base64-Downloads
- [x] `FileID`, `ContentID`, Dateiname, Dateigröße, MIME-Typ und Disposition je Artikel verarbeiten
- [x] Anhänge pro E-Mail anzeigen und über Plenaro mit erneuter Benutzer-/Ticketberechtigungsprüfung herunterladen (Dateiname serverseitig entschärft; Content-Disposition attachment, nosniff, no-store)
- [x] Dateiinhalte nur auf Anforderung abrufen, Größenlimit 5 MiB je Datei und 8 MiB gesamte Znuny-Antwort; übergroße Anhänge mit HTTP 413 ablehnen
- [x] Eingebettete referenzierte PNG/JPEG/GIF/WebP-Bilder bis 512 KiB über begrenzte authentifizierte CID-Endpunkte als Daten-URIs in die bereits isolierte HTML-E-Mail-Vorschau einbetten
- [x] Keine externe Bildernachladung, kein SVG oder direktes Ausführen von Anhängen; Zuordnungen bei Nachrichtenwechsel neu prüfen
- [x] Frontend-Regressionstests und Docker-Zugriffskontrollen für die neuen Endpunkte
- [x] Vollständige CI inklusive Docker-Smoke-Tests erfolgreich ([CI #41478462307](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41478462307), [PR #43](https://github.com/DerHausmeisterKrause/plenaro/pull/43))
- [ ] Live-Test mit echten Znuny-Artikeln (`ContentID`, Inline-Bilder, Dateien, Größenlimits, Berechtigungen) ausstehend

### 02.11.2026 – Automatische Znuny-Agentennamen und Segmentverfügbarkeit (Phase 6)
- [x] Besitzer und Verantwortlicher mit Namen direkt im Ticketdetail, auch vor dem Editieren; Fallback Agent-ID bei nicht erreichbarer Znuny-Agentenliste
- [x] Znuny-Agentennamen automatisch beim Ticketöffnen und alle fünf Minuten bei sichtbarem Tab abrufen (manueller Button optional)
- [x] Ticket-Nachrichten automatisch im Hintergrund alle 60 Sekunden aktualisieren, während ein Entwurf nicht unterbrochen wird
- [x] 48 farbige 15-Minuten-Slots von 06:00 bis 18:00 wie im WPF-Vorgänger: grün frei in bekannten Quellen, rot belegt, grau unbekannt; ausgewähltes Zeitfenster markiert
- [x] Zentrale Plenaro-Verfügbarkeit mit eigenen lokalen Aufgaben- und Znuny-Segmenten, zusätzlich eigener Outlook-Kalender bei bestehender Graph-Verbindung; keine unbelegte Aussage über fremde Teilnehmer-Kalender
- [x] Ticket- und nutzerberechtigter API-Endpunkt für eigene Tagesbelegungen, ohne Fremdtermine offenzulegen
- [x] Bestehenden Segmenteditor mit Echtzeit-Konfliktwarnungen, sicheren Zeitfeldern und Konfliktsperre erweitern; serverseitige atomare Überschneidungsprüfung für Znuny-Segmente gegen eigene lokale Aufgaben und andere Znuny-Tickets
- [x] Frontend-Tests für Farbe, Zeitraster, Outlook-Frei/Belegt, Bearbeitung und Backend-Docker-RBAC-/Konflikttests ergänzt
- [x] GitHub-CI vollständig erfolgreich ([Run #41483017559](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41483017559), [PR #44](https://github.com/DerHausmeisterKrause/plenaro/pull/44))
- [ ] Live-Abnahme mit echtem Znuny-Agentenverzeichnis und verbundenem Outlook
- [ ] Outlook-Blocker-Erstellung/Löschung für Segmente und Teilnehmer-Free/Busy: erfordert getrennt zu prüfende Microsoft-Graph-Schreibrechte (`Calendars.ReadWrite` bzw. delegierte Berechtigungen). Noch nicht implementiert, keine vermeintliche Teilnehmerverfügbarkeit.


### 02.11.2026 – Znuny-Kostenstellen, Aufträge und Desktop-Zeitbuchung (Phase 6)
- [x] Backend-Laden der tatsächlichen Znuny-DynamicField-Optionen für Kostenstelle und Auftrag sowie deren aktuellem Ticketwert; WPF-kompatible administrativ konfigurierte Fallback-Werte (`key=label;...`)
- [x] Kostenstellen-/Auftragsauswahl mit Suchfeld im Znuny-Ticketdetail und expliziter Auswahlschlüssel-Validierung vor dem Schreiben
- [x] Alte separate Minuten-„Zeitkorrektur“-UI entfernen: direkte Bearbeitung der angezeigten **ungebuchten** Zeit per Klick im `HH:MM:SS`-Format; sekundengenaue persistente Korrektur
- [x] Znuny-Zeitbuchung als interner Artikel mit `TimeUnit`, dynamischen Feldern, Buchungsbeschreibung, Notiz und eindeutiger `TaskTool-Booking-ID`
- [x] Immer auf volle 15 Minuten **aufrunden** (`ceil(source_seconds/900)*15`), während exakte Quellsekunden lokal erhalten bleiben
- [x] Vor dem Buchen laufenden Timer stoppen, bereits gebuchte Quellsekunden bei der ungebuchten Timerzeit abziehen
- [x] Zeitbuchungsjournal mit Pending-Sperre gegen doppelte Übertragungen, manueller Abgleich anhand der Buchungs-ID und Buchungshistorie
- [x] Backend-CSRF/Benutzer-/Ticketberechtigung und nur persönliche Znuny-Zugangsdaten für Schreiben; kein automatisches Retry bei unklarer Remoteantwort
- [x] React-Rundungstests anhand der WPF-Vorgängertests und Docker-API-Checks für Timer/Kontrollen ergänzt
- [x] Gesamte GitHub-CI grün (Frontend-/Backend-Build, Tests, Docker) ([CI #41486074954](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41486074954), [PR #45](https://github.com/DerHausmeisterKrause/plenaro/pull/45))
- [ ] Produktiven Znuny-Connector auf Optionen-Lieferung und echte `TimeUnit`-Buchungen (Status, ArticleID, korrekte Feldwerte) prüfen
- [ ] Spätere Admin-Funktion zur bewussten Auflösung eines dauerhaft unklaren Pending-Status ergänzen, falls die Znuny-Artikelhistorie den Eintrag nicht mehr liefert


### 02.11.2026 – Classic-Kandidatenfilter, Volltextsuche und Antwortvorlage (Phase 6)
- [x] „Alle neuen Aufgaben“ als direkt in der Ticketansicht schaltbarer, benutzerbezogen gespeicherter Pool-Filter (aktiv: alle freien Tickets; inaktiv: nur Suchkriterien **ohne** Ausschlusstreffer)
- [x] Classic-Suchbegriffe mit Komma, Semikolon und Zeilenumbrüchen trennen; Titel und indizierte Artikelinhalte auf Teilstring prüfen, Großschreibung ignorieren
- [x] Zentrale schrittweise Artikel-Volltextindizierung durch technischen Znuny-Dienst, getrennt vom Metadaten-Sync und mit kleinem Request-Budget; Suche bleibt immer auf freigegebene Tickets beschränkt
- [x] Server-seitige Volltextsuche über Nummer, Titel, Queue, Status, indizierte Artikel und persönliche Ticketnotiz; Wörter kombinieren (UND)
- [x] Filter für Queue und Status sowie Sortierfeld/Richtung in „Aktuelle“ und „Neue Aufgaben“ samt Rücksetzen und serverseitiger Pagination
- [x] Vorhandene konfigurierbare Antwortvorlage vor dem Antworten automatisch ausfüllen, WPF-Variablen `$Customer`, `$CustomerFirstName`, `$CustomerLastName`, `$TicketNumber`, `$TicketTitle`, `$Date` ersetzen, ungelöste markieren
- [x] „Erledigt“ öffnet wie Classic zuerst eine bearbeitbare Antwort samt „Ist erfolgt. Ticket wird geschlossen.“; Antwort und Ticketstatus in einem einzigen Znuny-TicketUpdate senden
- [x] Bestehende Fremdticket-/Adminberechtigungen, CSRF und keine automatische Wiederholung unklarer Znuny-Schreibaufrufe
- [x] Browser- und Docker-Integrationstests für Kandidatenmodus, Such-/Ausschlusslogik und Vorlagenvariablen ergänzt
- [x] Vollständige CI erfolgreich ([CI #41493282973](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41493282973), [PR #46](https://github.com/DerHausmeisterKrause/plenaro/pull/46))
- [ ] Live-Abnahme mit echten Kunden-Mails und vollständiger Artikelindizierung; zunächst noch nicht eingelesene historische Nachrichtentexte erscheinen erst nach der schrittweisen Indexierung


### 02.11.2026 – Znuny-Persönlicheinstellungen und eingebettete Weboberfläche (Phase 6)
- [x] Falsche Benutzer-Speicheranforderung korrigieren: schreibgeschützte `candidateUserId` aus dem PUT-Body entfernen, Agenten-ID wird weiterhin nur administrativ vergeben; keine Absenkung der API-Berechtigungen
- [x] Queue- und Statusfilter in den Ticketlisten „Aktuelle Aufgaben“ und „Neue Aufgaben“ entfernen; Volltextsuche und Sortierung bleiben
- [x] Sidebar unter „Wochenplanung“ um „Ticketsystem“ und eigenen Browser-Reiter `/ticket-system` ergänzen
- [x] „Ticket öffnen“ von Classic-Ticketdetails auf den Plenaro-Ticketsystem-Reiter mit Ticket-ID verlinken, statt eine neue Znuny-Browserseite zu öffnen
- [x] Znuny-spezifischen, an die administrativ konfigurierte HTTPS-Webadresse gebundenen Server-Bridge-Endpunkt mit DNS-/Peer-Schutz statt beliebigem Open Proxy erstellen; externe Domain-Redirects sperren
- [x] Native Znuny-Webanmeldung serverseitig mit eigenem verschlüsseltem Benutzerpasswort versuchen; getrennte serverseitige Cookie-Jars je Plenaro-Benutzer; niemals Znuny-Passwort oder Native-Cookies an den Browser weiterreichen
- [x] Native HTML in opak-origin sandboxed iframe rendern (kein `allow-same-origin`); fremdes JavaScript kann nicht auf Plenaro-Sitzung, API-Daten oder Eltern-DOM zugreifen
- [x] Grundlegende relative URL-/Asset-/Formular-Linkanpassung, um die Webansicht innerhalb des Proxy-Pfads zu halten
- [x] CI-Build und Docker-Smoke-Test erfolgreich ([Run #41499453099](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41499453099), [PR #47](https://github.com/DerHausmeisterKrause/plenaro/pull/47))
- [ ] Live-Abnahme gegen tatsächliche Znuny-Weboberfläche, inklusive Login-CSRF, Assetpfaden, Sessions, Formularen und Ticket-Deep-Links; benutzerdefinierte Plugins/Skripte und Entra-SSO/2FA können unter iframe-Sandbox nicht garantiert funktionieren
- [ ] Für frei konfigurierbare Favoriten/Wiki eine **separate isolierte Origin** mit expliziter Website-Freigabe und Authentifizierungskonfiguration entwerfen. Bestehende Znuny-Bridge ist ausdrücklich kein offener Universal-Proxy.


### 02.11.2026 – Regression: Antworten bei fehlenden Znuny-Kundenartikel-Metadaten (Phase 6)
- [x] „Antworten“ und „Erledigt“ dürfen die Eingabemaske nicht mehr allein wegen fehlender `SenderType`-/`CommunicationChannel`-Angaben oder rein interner Artikel sperren
- [x] Wie in Classic Empfänger ausschließlich serverseitig bestimmen: letzte passende Kunden-E-Mail, alternativ gültige E-Mail aus `Ticket.CustomerUserID` (`CustomerUser` als API-Alias); kein clientseitig angegebener `To`-Wert
- [x] Znuny-Article-Parsing um `SenderTypeID=3`, `ArticleType` und alternative Kanalfelder ergänzen
- [x] Antwortvorlage wird auch ohne Kundenartikel angezeigt; nicht auflösbare Personennamen bleiben markiert, kein geratener Empfänger
- [x] Frontend-Regressionstests für fehlende Kundenmetadaten, eingehende `email-external`-Artikel und ausschließliche Agentenartikel
- [x] CI (Frontend, Backend, Docker) erfolgreich ([Run #41503261769](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41503261769), [PR #48](https://github.com/DerHausmeisterKrause/plenaro/pull/48))
- [ ] Liveprüfung der Empfängerauflösung und Antwortversand am echten Znuny-Ticket (keine produktive Nachricht durch CI)


### 02.11.2026 – Persönliche Znuny-Antwortvorlage und Browser-Logo (Phase 6)
- [x] Persönliche Antwortvorlage in den eigenen Znuny-Einstellungen bearbeitbar machen (auch ohne Administratorrolle), mit Benutzerprofil in PostgreSQL speichern (Migration 0025)
- [x] Globale Admin-Antwortvorlage als Fallback behalten, falls der Agent keine eigene Vorlage festlegt; keine Änderung an Agenten-ID-/Kandidaten-ID-Berechtigungen
- [x] Beide Antwortvorlagen-Felder über volle Formularbreite als mehrzeilige Textfelder mit Dark/Light-Theme darstellen; Beispiel mit echten Zeilenumbrüchen statt sichtbaren `\\n`-Zeichen
- [x] Persönliche Vorlage bei „Antworten“ und „Erledigt“ priorisieren; Variablen aus Ticket und Kundenartikel wie Classic ersetzen
- [x] Bereits gespeicherte `\\n`-Zeichenfolgen beim Rendern in normale Absatzumbrüche umwandeln
- [x] Vorhandenes Desktop-Plenaro-Logo `project_tool/Assets/Plenaro.png` als Browser-Tab-Favicon verlinken (aktuell aus öffentlichem GitHub-Repository geladen)
- [x] Frontend- und Docker-Regressionstests für Vorlagenspeicherung, -Priorität und Zeilenumbrüche
- [x] Vollständige GitHub-CI erfolgreich ([Run #41509498760](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41509498760), [PR #49](https://github.com/DerHausmeisterKrause/plenaro/pull/49))
- [ ] Live-Verifikation der Vorlage beim Antworten und Schließen am Znuny-Server
- [ ] Optional Favicon-Binärdatei in das Webrepo lokal übernehmen, um keine externe GitHub-Bildanforderung zu benötigen


### 02.11.2026 – Znuny-Schreibaktionen: 502-ACK-Fix und durchgängige Tests (Phase 6)
- [x] TicketUpdate akzeptiert jetzt bestätigtes Znuny-`{"Success":1}`/`true` sowie klassische `TicketID`-/`ArticleID`-ACKs, auch in `Data`; ein HTTP-200 mit leeren/ungültigen Daten wird weiterhin **nicht** als Bestätigung gewertet
- [x] Eindeutige Znuny-API-Ablehnungen (`Success=0`, `ErrorCode`, HTTP 4xx) von unklaren Zuständen (Timeout, HTTP 5xx, kaputte Antwort) trennen; nur kurze sichere Fehlercodes ohne E-Mail-/Ticketinhalt zurückgeben
- [x] Fehler bei Session-Aufbau / TicketGet vor dem Schreiben von unklaren Übertragungsfehlern unterscheiden; **nie** einen Remote-Write automatisch wiederholen
- [x] Das gilt für Antwort, Antwort und gleichzeitiges Schließen, Besitz-/Verantwortlich-Zuweisung, direkte Statusänderung und Znuny-Zeitbuchung
- [x] SQL-Migration 0026: explizit abgelehnte Buchungen als `rejected` auditieren und für korrigierte erneute Buchung entsperren. Timeouts/5xx bleiben `pending` gegen Doppelbuchung
- [x] Zeitbuchungs-UI stoppt jetzt bei einem laufenden Timer tatsächlich vor der Übertragung; abgelehnte Buchungen sichtbar von ungeklärten getrennt
- [x] POST/PATCH als Administrator-Einstellung für TicketUpdate hinzugefügt (Default Classic POST /Ticket/{TicketID}/Update; Standard Znuny kann PATCH /Ticket/{TicketID} nutzen), kein automatischer Wechsel oder zweiter Schreibversuch
- [x] Zusätzlicher mockbasierter .NET-HTTP-Kontrakttest für Reply, Reply+Close, Owner/Responsible, Internal-Zeitbuchung mit `TimeUnit`, DynamicFields und unterschiedlichen Znuny-ACKs/Fehlercodes; keine echte API-Nachricht oder Buchung
- [x] CI grün: Frontend-/Backend-Build, Znuny-Mock-Integration und Docker ([Run #41513985836](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41513985836), [PR #50](https://github.com/DerHausmeisterKrause/plenaro/pull/50))
- [ ] Echte Znuny-Installation: TicketUpdate-Mapping, API-Version, Artikel-Versand-Erweiterung `ArticleSend`, tatsächliche E-Mail-Zustellung, Schließen und 15-Minuten-Buchung produktionsnah verifizieren


### 02.11.2026 – Browser-Egress für Znuny auf Plenaro beschränken (Security-Hardening)
- [x] Gespeicherte Ticket-URLs bleiben als persönliche Referenz erhalten, werden aus dem Ticketdetail aber nicht mehr als externe Browser-Links geöffnet. „Ticket öffnen“ verwendet ausschließlich `/ticket-system?ticket=ID`.
- [x] Die native Znuny-Webbridge ersetzt fremde oder nicht genehmigte Link-/Asset-URLs durch `about:blank` statt sie im Browser direkt anzufordern.
- [x] Auf eingebettetes Znuny-HTML kommt eine restriktive CSP für Skripte, Styles, Bilder, Formulare und Netzwerkzugriffe: als Netzwerkziel ist ausschließlich die aktuelle Plenaro-Proxy-Origin freigegeben; Sitzungen und Zugangsdaten bleiben auf dem Backend. Im Fallback bleibt die opaque iframe-Sandbox ohne `allow-same-origin`; am dedizierten HTTPS-Port ist `allow-same-origin` erlaubt, weil dort keine normalen Plenaro-APIs erreichbar sind.
- [x] Frontend-Regressionstest für internen Ticket-Deep-Link und fehlenden externen Direktlink ergänzt.
- [ ] GitHub-CI nach PR-Erstellung prüfen; den neuen Egress-Schutz zusätzlich im Browser-Netzwerkmonitor bestätigen.
- [x] Getrennte, konfigurierte HTTPS-Origin auf eigenem NGINX-Port 8444 mit unveränderten Znuny-AJAX-/Root-URL-Anfragen über die serverseitige Bridge und exakt beschränkter Origin-Freigabe implementieren; bei fehlender Portal-Origin bleibt der sichere Fallback.
- [ ] Vollständige interaktive Znuny-Web-GUI (AJAX/Formulare, ggf. SSO) sowie die Freischaltung des separaten Ports über Teleport auf dem Zielhost testen.
- [ ] Antwort/Schließen/Zeiten mit der tatsächlich in Znuny konfigurierten TicketUpdate-Methode und -Route gegen ein Testticket live prüfen. Kein zweiter automatischer Schreibversuch bei HTTP 502 oder Timeout.

- [x] Sicheren persönlichen Znuny-API-Test pro Ticket implementieren: SessionCreate + TicketGet ohne TicketUpdate, mit Anzeige der tatsächlichen Schreibroute/-methode und eindeutiger Stufendiagnose
- [x] SessionCreate-Fehler trotz HTTP 200 (keine SessionID oder Znuny ErrorCode) von TicketGet und echten 502-Schreib-Timeouts unterscheidbar machen; ohne sensibles Response-Logging
- [x] Optional fehlende DynamicFieldOptions-Route (404 / nicht freigeschaltet) blockiert die Zeitbuchungsvorbereitung nicht mehr; nur Admin-Fallback-Optionswerte verwenden
- [x] Mockkontrakttests für die fehlgeschlagene persönliche Session und die Zeitbuchungs-Fallbackwerte ergänzen
- [ ] Die bislang fehlschlagenden produktiven Antworten, Schließen, Zuweisungen und Zeitbuchungen am echten Znuny-Testticket mit persönlichem Konto verifizieren; der Browser-/Docker-Test simuliert diese Aktionen ausdrücklich nur

### 03.11.2026 – Echte Znuny-WebView aus Docker ohne zusätzlichen öffentlichen Port (Phase 6)

Die in PR #51 eingesetzte HTML-iframe-Bridge mit Port 8444 ist **abgelöst**:
Sie konnte native Znuny-Skripte und AJAX nicht zuverlässig in einer
fremden/opaque Browser-Origin darstellen; der Reiter blieb auf dem
Zielhost weiß. Die historische Checkliste dokumentiert den Zwischenstand.

- [x] Eigenen öffentlichen Port 8444, zweites HTTPS-NGINX-VHost und separate Browser-Origin entfernen
- [x] Defekte HTML/JS-Umschreib-Bridge sowie den direkten iframe aus dem Ticketsystem-Reiter entfernen
- [x] Echten Chromium-Browser im zusätzlichen internen Docker-Container `znuny-browser` einrichten, ohne Host-Portfreigabe
- [x] Private Chromium-Sitzungen pro authentifiziertem Plenaro-Benutzer mit Agentenprüfung, Sessionlimit, Inaktivitätslimit und verschlüsselten persönlichen Zugangsdaten
- [x] Direkte Ticket-Deep-Links nur bei serverseitig bestätigtem Ticketzugriff an interne Chromium-Sitzung übergeben
- [x] Interaktive browserseitige Bildansicht mit Maus-, Scroll-, Tastatur-, Navigations- und Paste-Eingaben über die geschützte Plenaro-API
- [x] Kein Znuny-Zugriff, keine Znuny-Cookies, keine Znuny-JavaScript-Ausführung und kein Selenium-WebDriver-Zugriff am Benutzer-PC
- [x] Start-/Fehlerstatus statt rein weißer Oberfläche und Frontend-Regressionstest für in Plenaro gerendertes Browserbild
- [x] GitHub-CI Frontend-, .NET- und Docker-Tests inklusive echtem Chromium-Screenshot erfolgreich ([PR #52](https://github.com/DerHausmeisterKrause/plenaro/pull/52), [CI #41585802921](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41585802921))
- [ ] Echter Zielhost: Netzverbindung und Zertifikatsvertrauen des Chromium-Containers zu `ticketsystem.intern.example` bestätigen
- [ ] Echtes Znuny-Webportal: Login, CSS, JavaScript/AJAX, Ticket-Deep-Links, Formularbedienung, Anhänge und Bedienbarkeit prüfen
- [ ] Reale Ticketantworten, Schließen, Zuweisungen und Zeitbuchungen weiterhin separat gegen ein sicheres Testticket bestätigen; die WebView ersetzt **nicht** die API-Write-Verifikation

**Phase:** 6 / Issue #7; Desktop-Ansicht funktional statt HTML eingebettet.

### 03.11.2026 – Gemeinsames internes CA-Trust und JPEG-Streaming (Phase 6)

- [x] `.env`-Variable `PLENARO_TRUSTED_CA_FILE` für eine vertrauenswürdige interne Root-CA auf dem Host bereitstellen
- [x] Persönlichen Backend-Znuny-HTTPS-Client um interne CA ergänzen und die öffentlichen CA-Roots erhalten (OpenSSL/`SSL_CERT_FILE`)
- [x] Interne CA beim Chromium-Start in den NSS-Store importieren; keine unsicheren TLS-Flags
- [x] Backend und Selenium-Container bleiben unprivilegiert; keine weiteren öffentlichen Ports
- [x] Serverseitige Screenshot-Übertragung von großen PNG-Bildern auf komprimiertes JPEG mit PNG-Fallback umstellen
- [x] CI: CA-Mounts, NSS-Trust, .NET-Bundle und Chromium-JPEG-Screenshot erfolgreich ([PR #53](https://github.com/DerHausmeisterKrause/plenaro/pull/53), [CI #41588658814](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41588658814))
- [ ] Auf dem Zielhost echtes Znuny-HTTPS gegen die interne interne Root-CA verifizieren und Zertifikatswarnung in Chromium prüfen
- [ ] Die Responsivität über die tatsächliche Teleport-Verbindung im Alltag bestätigen

### 03.11.2026 – Interaktive Browseransicht beschleunigen (Phase 6)

- [x] Echte Chromium-CDP-Bildereignisse über serverseitige WebSocket-Verbindung abonnieren und als geschützten, same-origin SSE-Stream an die Weboberfläche liefern
- [x] Vollständige Screenshot-Polling-Rundreisen durch kontinuierliche komprimierte JPEG-Ereignisse ersetzen; bei fehlender CDP-Unterstützung kompatiblen Bildmodus beibehalten
- [x] Chromium-Viewport bei Größenänderungen an die verfügbare Fläche der Plenaro-Seite anpassen und nicht mehr auf 1280 Pixel Breite begrenzen
- [x] Unnötiges zusätzliches Text-Einfügeformular unter der Browseransicht entfernen; Einfügen über die reguläre Zwischenablage bleibt möglich
- [x] Keine internen WebDriver-/CDP-Ports veröffentlichen; nur der authentifizierte Benutzer erhält Bilddaten, persönliche Zugangsdaten bleiben serverseitig
- [x] End-to-End-CI: Frontend, Backend, Chromium-WebSocket-Screencast und Docker-Smoke erfolgreich ([PR #1](https://github.com/DerHausmeisterKrause/plenaro/pull/1), [CI #41596808475](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41596808475))
- [ ] Echtes Zielsystem: Geschwindigkeit, Darstellung, Tastatur, Navigation und Scrollen im Browser überprüfen

### Phase 6 – Native HTML-Einbettung über isolierte Ticketsystem-Origin

- [x] Separate HTTPS-Hostnamen für Plenaro-Frontend und Ticketsystem-Proxy konfigurieren – gleicher Server, gleicher Port 443
- [x] Interne HTTP-Requests, Redirects, HTML/CSS/JS/JSON und binäre Antworten serverseitig weiterleiten
- [x] Automatisches Agenten-Login mit individuellen Vault-Zugangsdaten, Loginformular und getrennten Upstream-Cookie-Jars
- [x] Einmaliges Bootstrap-Token, host-exklusives HttpOnly-Cookie, CSP und iframe-Sandbox für getrennte Browser-Origin
- [x] Je Nutzer eigene parallele Proxy-Sitzung mit Ablaufzeiten, Autorisierungsprüfung und Widerruf beim Plenaro-Logout
- [x] Native HTML-Weboberfläche füllt die Ticketsystem-Fläche, ohne Server-Screenshot-Streaming
- [x] Vorherigen Chromium-Modus als manuell auswählbaren Fallback behalten
- [x] CI: .NET-Build, React-UI-Tests, isolierte Loginformular-/Origin-Prüfungen und Docker-Start erfolgreich ([PR #2](https://github.com/DerHausmeisterKrause/plenaro/pull/2), [CI #41601862700](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41601862700))
- [ ] Zwei HTTPS-DNS-Namen und gemeinsames Zertifikat am echten Plenaro-Host eingerichtet
- [ ] Native Anmeldung, Navigation, AJAX, Formulare, Download und Mehrbenutzertrennung am echten Ticketsystem geprüft
- [ ] Nach bestätigter Funktionalität optional den bisherigen Chromium-Container aus Compose entfernen


### 03.11.2026 – Chromiums Remote-Browser vollständig entfernt (Phase 6)

- [x] Serverseitigen Selenium-/Chromium-Docker-Service inklusive CA-Importskript und Image-Build entfernen
- [x] WebDriver-Backend, Screenshot-API und CDP-Screencast vollständig entfernen
- [x] React-Ticketsystem-Reiter ausschließlich mit nativer HTML-Einbettung über die isolierte HTTPS-Proxy-Origin betreiben
- [x] Screenshot-Bildmodus, unnötiges Paste-Feld und zugehörige CSS-Klassen entfernen
- [x] Ohne separate HTTPS-Origin keine unsichere Einbettung oder stillschweigenden Rückfall: klare lokale Einrichtungsanleitung im Reiter
- [x] Docker-CI auf Backend-CA-Vertrauen, native Proxy-Authentifizierung und kein zusätzliches Browser-Image umstellen
- [x] CI-Integration (Build/Tests/Docker-Smoke) nach dem Refactoring erfolgreich ([PR #3](https://github.com/DerHausmeisterKrause/plenaro/pull/3), [CI #41605224970](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41605224970))
- [ ] Zielhost: DNS/TLS für zwei same-site Plenaro-Origins einrichten und echte Znuny-HTML-Anmeldung sowie parallele Nutzer prüfen
- [ ] Reale Ticketoperationen (Antworten, Schließen, Zeitbuchen) weiterhin ausschließlich anhand eines kontrollierten Testtickets prüfen

**Hinweis:** Der bisherige Chromium-Container wird beim Update mit
`docker compose up -d --build --remove-orphans` entfernt. Die
PostgreSQL-Datenbank und der Vault bleiben unverändert.


### Phase 6 – Native Anmeldung diagnostizieren ohne Architekturwechsel

- [x] Den ursprünglichen serverseitigen Agenten-Autologin beibehalten
- [x] Bei individuellen Loginformularen oder zurückkehrender Loginseite eine reguläre Anmeldung in derselben isolierten HTML-Proxy-Sitzung erlauben
- [x] Transport/TLS, Timeout, nicht unterstützte Redirects und HTTP-Serverfehler durch neutrale, nicht-sensitive Fehlercodes unterscheiden
- [x] Bei nicht-JSON-Gateway-Antworten den HTTP-Status statt einer irreführenden generischen Login-Meldung anzeigen
- [x] Tests mit rein fiktiven Hostnamen für automatische Anmeldung, interaktiven Login und blockierte externe Weiterleitung ergänzen
- [x] GitHub-CI erfolgreich: Backend, Frontend, fiktive Loginfälle und Docker-Smoke ([PR #4](https://github.com/DerHausmeisterKrause/plenaro/pull/4), [CI #41610855241](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41610855241))
- [ ] Am Zielhost Ursache anhand des neutralen Fehlercodes verifizieren sowie native Anmeldung und iframe-Bedienung prüfen


### Phase 6 – HTTP 400 hinter vorgeschaltetem HTTPS-Proxy

- [x] Öffentliche Browser-Origin statt intern normalisiertem Host zur Prüfung des nativen Sitzungsstarts verwenden
- [x] HTTPS-, CSRF- und Browser-Fetch-Metadata-Prüfungen beibehalten; fremde/opaque Origins weiterhin verweigern
- [x] Neutrale Fehlercodes bei falschem HTTPS oder nicht passender Browser-Origin ausgeben
- [x] Automatisierte Tests für umgeschriebenen Frontproxy-Host und Cross-Origin-Abwehr ergänzen
- [x] GitHub-CI (Backend, Frontend, Znuny-Vertrag und Docker-Smoke) erfolgreich ([PR #5](https://github.com/DerHausmeisterKrause/plenaro/pull/5), [CI #41619802978](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41619802978))
- [ ] Vor Ort die beiden HTTPS-Proxy-Hostnamen und automatische Anmeldung verifizieren


### Phase 6 – Agenten-Webseite trotz ungewöhnlichem Content-Type erkennen

- [x] Unberechtigte harte `text/html`-Prüfung am ursprünglichen Agenten-Webseiten-GET durch begrenzte Prüfung auf tatsächliche HTML-Struktur ersetzen
- [x] Fehlender oder abweichender MIME-Typ (`text/plain`, `application/xhtml+xml`, `application/octet-stream`) wird bei echtem HTML akzeptiert
- [x] JSON-/Leerantworten und sonstiges Nicht-HTML weiterhin ablehnen (`page-json`, `page-empty`, `page-not-html`)
- [x] Falsch als `text/plain` ausgelieferte HTML-Einstiegsseite wird im isolierten iframe als HTML gerendert, keine Änderung an Anhängen oder Downloads
- [x] Login-Nachfolgeantworten weiterhin kompatibel mit bisherigen Nicht-HTML-Erfolgsantworten
- [x] Fiktive Tests und vollständige CI erfolgreich ([PR #6](https://github.com/DerHausmeisterKrause/plenaro/pull/6), [CI #41623430116](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41623430116))
- [ ] Gegen echte Agenten-Weboberfläche prüfen, welcher HTTP-Antworttyp vorliegt und ob die native Einbettung vollständig funktioniert

### 03.11.2026 – Native Ticketsystem-HTML und Fenstergröße (Phase 6)

- [x] HTML-Dokumente bei falsch gemeldeten Navigations-MIME-Typen sicher als `text/html` ausgeben
- [x] CGI-Navigation mit Ticket-/Dashboard-Query unterstützen; nicht-HTML-Anhänge unverändert streamen
- [x] CSS des Ticketsystem-Reiters aus dem allgemeinen Maximalbreiten-/Seitenpadding lösen; iframe füllt Arbeitsbereich unter der Topbar
- [x] Synthetische HTML-/Nicht-HTML-/Download-Tests ergänzen, keine echten Ticketdaten übernehmen
- [x] GitHub-CI: Backend/Frontend/Vertrags- und Docker-Smoke-Tests erfolgreich ([PR #7](https://github.com/DerHausmeisterKrause/plenaro/pull/7), [CI #41635960052](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41635960052))
- [ ] Am tatsächlichen Ticketsystem Darstellung, interne Links, AJAX, Anhänge und mehrere Benutzer verifizieren

### 03.11.2026 – Wildcard-Vorbereitung für Favoriten (Phase 7)

- [x] WPF-Favoritenmodell (URL, Benutzerzugang, Sortierung, Auto-Login) ausgewertet
- [x] Optionale `PLENARO_FAVORITES_BASE_DOMAIN`: unbekannte Aliasse vor der Plenaro-Login-Origin gesperrt (404)
- [x] NGINX-Wildcard-Hostname, DNS und TLS dokumentiert
- [x] Contract-Tests für unbekannte Hosts, bekannte Origins und Localhost ergänzt
- [x] CI der neuen Wildcard-Host-Sperre erfolgreich ([PR #10](https://github.com/DerHausmeisterKrause/plenaro/pull/10), [CI #41639354920](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41639354920))
- [x] Admin-Freigabe für Apps, persönliche Favoriten, eigene App-Cookie-Jars und Vault-Auto-Login in PR #12 ergänzt
- [ ] Mit realer Wiki-/Favoriten-Anwendung Browserkompatibilität, Mehrbenutzerbetrieb und Sicherheitsgrenzen prüfen

Die bisherige Znuny-Integration bleibt unverändert. Diese Vorbereitung stellt **keinen** frei konfigurierbaren Universalproxy bereit.

### 03.11.2026 – Favoritenverwaltung und dedizierte Web-App-Proxy-Origins (PR #12)

- [x] Classic-Favoritenmodell (Name, URL, Sortierung, optionales Login) als Web-Favoriten nachgebildet
- [x] PostgreSQL-Migration 0027 für freigegebene Admin-Web-Apps, Benutzer-ACL und persönliche Favoriten
- [x] Administrationsoberfläche für HTTPS-App-Origins, Slug, aktiven Zustand und Benutzerfreigaben
- [x] Persönliche Favoriten-CRUD, Sortierung, optionale verschlüsselte Passwörter und eigener Browser-Reiter
- [x] Getrennter HTTPS-HTML-Proxy pro App-Subdomain mit eigener Benutzer-Session, Browser-CSP, Server-Cookies, Bootstrap-Token und Logout-Widerruf
- [x] Browser-Origin/CSRF, HTTPS/DNS/Peer-Prüfung und laufende Benutzer-/App-Berechtigungsprüfung; keine dynamische Freigabe beliebiger URLs
- [x] Bestehender Znuny-Proxy fachlich unverändert; nur Register-/Logout-Middleware ergänzt
- [x] GitHub-CI: Backend, Frontend, Favoriten-/Znuny-Vertragstests und Docker-Favoriten-Smoke erfolgreich ([PR #12](https://github.com/DerHausmeisterKrause/plenaro/pull/12), [CI #41650719103](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41650719103))
- [ ] Browser-Abnahme mit echtem Wiki: Login, AJAX, CSS/JS, Downloads und zwei parallele Benutzer
- [ ] App-Sonderfälle (SSO/2FA, WebSockets, CSP und Service Worker) nur bei Bedarf implementieren
- [ ] Legacy-Favoriten (inklusive nur sicherer Konfiguration) kontrolliert migrieren



### 09.11.2026 – Phase 6: persönliche Znuny-Queue-Rechte erzwingen (PR #15)

- [x] Gemeinsamen Service-Account-Ticketcache nicht mehr als Benutzerberechtigung behandeln
- [x] Ticketlisten und Agenda auf persönlich autorisierte Znuny-TicketIDs beschränken
- [x] API-Ticketdetails, Artikel, Anhänge, Planungs-/Schreibaktionen und Native-Deep-Links mit persönlichem TicketGet prüfen
- [x] Fail closed bei fehlenden Rechten/Session/Timeout; Cacheänderungen invalidieren
- [x] CI: Backend/Frontend, Znuny-Contract und Docker-Smoke erfolgreich ([Run #42568973461](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42568973461))
- [ ] Live-Test mit mindestens zwei Queue-Berechtigungsprofilen und direktem Fremd-Ticketzugriff
- [ ] Laufzeiten / Serverlast bei größeren Ticketmengen messen und Suchstrategie gegebenenfalls skalieren


### 09.11.2026 – Phase 6: eigene/segmentierte Tickets ohne Queue-API-Failure (PR #17)

- [x] Besitzer- und Verantwortlichen-Tickets ohne persönliche globale TicketSearch weiterhin sichtbar
- [x] Segmentierte oder via Teilnehmer-E-Mail getaggte Tickets in Aufgabenansichten und Heute/Agenda sichtbar
- [x] Erledigte Aufgaben bei persönlicher Historie (Notiz, Zeitbuchung, Segment) weiterhin sichtbar
- [x] Nur unzugewiesene **Neue Aufgaben** anhand der individuellen Znuny-Queue-Leserechte herausgeben
- [x] Fehlende Znuny-API-Berechtigungsbestätigung zeigt keine fremden neuen Tickets, blockiert aber keine eigenen Aufgaben
- [x] Ticket-ID-Routen, Planung und Native-Znuny-Proxy entsprechend nachgezogen
- [x] CI: Build/Contract und Docker-Smoke bestanden ([PR #17](https://github.com/DerHausmeisterKrause/plenaro/pull/17), [Run #42573584221](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42573584221))
- [ ] Echte Queue-Rollen in Znuny/OTRS auf dem Zielhost vergleichen; Performance bei vielen Queues und Ticketdetails prüfen


### 09.11.2026 – Phase 7: Favoriten-Bootstrap zuverlässiger (PR #18)

- [x] Double-Mount-Race beim Favoriten-Start durch pro Favorit/Revision geteilte Start-Promise beseitigt
- [x] Tokenfreier Alias-Health-Check zur Prüfung von DNS, vorgelagerter NGINX, Docker-NGINX und Backend
- [x] Browser- und Backend-Regressionstests; vollständige GitHub-CI bestanden ([Run #42576527912](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42576527912))
- [ ] Live-Probe auf dem Zielhost und Wiki-HTML/Anmeldeablauf abnehmen; ggf. NGINX/Wildcard-TLS korrigieren


### 09.11.2026 – Phase 7: einlagige Favoriten-Hostnamen (PR #19 in main)

- [x] WPF-WebShortcut-ID, URL, Browserprofile und persönliche Sessions gegen Web-Favoriten geprüft
- [x] Optionale `PLENARO_FAVORITES_HOST_TEMPLATE` für `{slug}-plenaro.example.org` implementiert
- [x] Alte `slug.apps.example.org`-Adressen bleiben parallel routbar, falls die Base-Domain gesetzt bleibt
- [x] Host-Guard, URL-Aufbau, Admin-Validierung und Diagnose auf beide Hostformate angepasst
- [x] Docker Compose leitet die neue Host-Vorlage aus .env an ASP.NET weiter; CI prüft die Übergabe
- [x] Beschädigte doppelte NGINX-Dokumentation korrigiert
- [x] Regressionstests für DNS-Alias, alte Favoriten, fehlerhafte Hosts und ungültige Templates ergänzt
- [x] GitHub-CI: .NET/React-Build, Favoriten-/Znuny-Contract-Tests und Docker-Smoke erfolgreich ([PR #19](https://github.com/DerHausmeisterKrause/plenaro/pull/19), [Run #42589498554](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42589498554)); in main gemergt
- [ ] Lokale Windows-DNS-/Wildcard-TLS-/vorgeschaltete-NGINX-Konfiguration einmalig anpassen
- [ ] Neue Wiki-Alias-Healthprobe und parallelen Realbrowser-/ACL-Test durchführen
- [ ] Native Znuny-Antwortfenster/502-Problem separat abnehmen; durch Hostnamenänderung nicht gelöst


### 09.11.2026 – Phase 7: gemeinsamer Favoriten-Pfadhost (PR #20 in main)

- [x] WPF-WebShortcut-URL, persönliche Browser-Sessions und bisherige Web-Favoriten wieder geprüft.
- [x] Opt-in `PLENARO_FAVORITES_PATH_ORIGIN` für `apps-plenaro.example.org/wiki/` implementiert, alte Host-/Template-Varianten bleiben erhalten.
- [x] Ein gemeinsamer Host mit serverseitiger Pfad-/Sitzungsbindung, Cookie-Path pro Slug, unbekannte URLs 404.
- [x] Pfad-Prefix bei Bootstrap, Redirect, Upstream, textuellen Links und Formularen berücksichtigt.
- [x] Path-Routing-Vertragstests und ursprüngliche Favoriten-/Znuny-Tests beibehalten.
- [x] PR #20 kontrolliert nach main gemergt; .NET/React-Build, Contract-Tests und Docker-Smoke erfolgreich ([Run #42601040551](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42601040551)).
- [x] React-Frontend-Bootstrap für `/<slug>/_plenaro/favorite-bootstrap` angepasst, anderer Favoriten-Slug wird abgewiesen.
- [ ] Einmaligen DNS-A-Record, vorgelagerten NGINX-vHost und vorhandenes TLS-Zertifikat am echten Host prüfen.
- [ ] Wiki-/Cloud-AJAX, Login, HTML/JS, Downloads, CSP/Service Worker und zwei Benutzer im Browser abnehmen.
- [ ] Sicherheitsentscheidung: Ein Pfadhost teilt dieselbe Browser-Origin über alle Apps; nur vertrauenswürdige Apps zulassen.
- [ ] Bereits offene Phase-6-Punkte inklusive Znuny-502 und Antwort-Dialog bleiben bestehen.


### 09.11.2026 – Phase 7: CSS/JS und Ressourcen auf dem gemeinsamen Pfadhost (PR #21)

- [x] HTML-Ressourcen einschließlich unquoted src/href/action und srcset-Kandidaten auf /<slug>/ präfixieren.
- [x] JS-Fetch-/Import-Template-Strings und CSS-url()/Import-Pfade umschreiben; kein doppeltes /<slug>/.
- [x] MIME-Korrektur bekannter Asset-Endungen bei text/plain/octet-stream sowie /<slug>-Rootredirect.
- [x] Regressionstests und vollständige CI bestanden; PR #21 in main gemergt ([Run #42604023749](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42604023749)).
- [ ] Echtes Wiki im Browser auf CSS/JS/AJAX/Bilder/Downloads und zwei Benutzer prüfen; spezifische 404/CSP/CORS-Fehler nachziehen.
- [ ] Grenzen für Drittanbieter-CDN, WebSockets, OAuth und Service Worker beachten; keine generelle App-Isolation durch Pfad.


### 09.11.2026 – Phase 7: HTTPS-Chain gegen äußeren NGINX-502 (PR #22 in main)

- [x] Optionales PEM-Intermediate-Bundle per PLENARO_TLS_CHAIN_PATH eingeführt, Leaf/Chain im Docker-NGINX kombiniert und bei Leaf-only gewarnt.
- [x] Kompatibilität mit vorhandenem Fullchain-PEM durch gesonderten NGINX-Container-Test geprüft.
- [x] CI insgesamt erfolgreich ([Run #42623296351](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42623296351)); PR #22 in main gemergt.
- [ ] Lokale Zertifikatskette in Host-.env ergänzen oder PLENARO_TLS_CERT_PATH auf echtes fullchain.pem stellen, Docker-NGINX neu erstellen; strikt ohne -k verifizieren.
- [ ] Realen Favoriten im Browser (CSS, JS, AJAX) erneut prüfen und verbleibende Fehler separat behandeln.


### 11.11.2026 – Phase 7: Wiki-Oberfläche und Ressourcen im Pfadhost (PR #23)

- [x] Confluence-kompatible HTML-Kontextpfad-Metadaten gezielt auf gemeinsamen /<slug>-Pfad aktualisiert.
- [x] Textressourcen-Limit für CSS/JS auf 24 MiB begrenzt erweitert; HTML/JSON bleibt 6 MiB.
- [x] Regressionstests und vollständige CI erfolgreich ([Run #42986077812](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42986077812)); PR #23 in main.
- [ ] Liveprüfung echter Wiki-Ressourcen/Browser-Konsole, Benutzeranmeldung, Buttons, Logos und Formularfelder.
- [ ] Einschränkungen von Path-Rewriting für WebSockets, fremde CDNs, OAuth und Service Worker weiterhin beachten; Browser-Origin-Sicherheit nicht aufweichen.


### 11.11.2026 – Phase 7: Confluence-JavaScript und Inline-Skript-Schutz (PR #24)

- [x] Defekten allgemeinen JS-Textpfad identifiziert: Root-URL-Rewrite beschädigte RegEx-Literale in `batch.js`, obwohl Ressourcen HTTP 200 zurücklieferten.
- [x] JS und JSON unverändert streamen; Inline-HTML-Skripte gegen Regex-Rewrite geschützt; HTML-/CSS-/Logo- und Confluence-Kontextpfadkorrektur bleibt erhalten.
- [x] Regressionstests und komplette CI erfolgreich ([Run #42990397008](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42990397008)); PR #24 in main gemergt.
- [ ] Echte Browser-Abnahme mit Menüs, Bildern, Buttons und Formularen nach neuem Build; ggf. dynamische Root-/WebSocket-URLs prüfen.


### 11.11.2026 – Phase 7: dedizierter Wiki-Host parallel zum Shared-Path-Host (PR #25)

- [x] HAR-Erkenntnis: Confluence-Plugin-Root-REST/JS-URLs bleiben unter /wiki/ inkompatibel, HTTP 401/404, Login-POST nicht belegt.
- [x] `PLENARO_FAVORITES_DEDICATED_SLUGS` erlaubt für ausgewählte Favoriten Root-Host-Cookies und Root-API-Routing ohne globale Weiterleitung fremder /rest-Requests; andere Favoriten bleiben auf /<slug>/.
- [x] Tests/CI vollständig erfolgreich ([Run #42995835637](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42995835637)); PR #25 in main.
- [ ] DNS/NGINX-Hostalias und .env auf Zielserver, Liveprüfung der Confluence-Anmeldung.

### 11.11.2026 – Phase 7: Confluence-Login auf apps-plenaro.example.org/wiki/ (PR #26)

- [x] Bereinigten HAR geprüft: Wiki-Login-HTML 200, Confluence-Plugin-Root-REST 401 und -Asset 404, echter Passwort-POST nicht beobachtet
- [x] Opt-in PLENARO_FAVORITES_BROWSER_SHIM_SLUGS=wiki für browserseitige /wiki/rest/ und /wiki/s/ Korrektur ohne JS-Bundle-Veränderung
- [x] Cookie-Pfad /wiki, serverseitige ACL und gemeinsam genutzte Root-Routen unverändert; kein neuer DNS-Name/NGINX-Host nötig
- [x] Regressionstests, Backend- und Frontend-Build, TLS-Test und Docker-Smoke grün; PR #26 gemergt
- [ ] Auf Zielserver .env setzen, Web + Docker-NGINX neu erstellen und Browser-Anmeldung prüfen (SSO/MFA nicht durch Shim implementiert)

PR: https://github.com/DerHausmeisterKrause/plenaro/pull/26
CI: https://github.com/DerHausmeisterKrause/plenaro/actions/runs/43001365087

### 11.11.2026 – Phase 7: Confluence-Browser-Shim für Request-Objekte und dynamische Assets (PR #27)

- [x] Opt-in-Browser-Shim ergänzt: `new Request("/rest/...")` und native DOM-Eigenschaften `script.src`, `link.href`, `img.src` korrigieren bekannte Root-Routen auf `/wiki/rest/...` oder `/wiki/s/...`
- [x] Fremde Origins, andere Favoriten, Proxy-Cookies, vorhandene Zugriffsprüfung und externe JS-Bundles bleiben unverändert
- [x] Regressionstest für Shim-Einbindung und neue Abfangpunkte ergänzt
- [ ] GitHub-CI und PR-Merge bestätigen
- [ ] Auf Zielserver Deploy inkl. `PLENARO_FAVORITES_BROWSER_SHIM_SLUGS=wiki`, Browser-Netzwerk/Console prüfen; Benutzername-/Passwort-Eingabe und interaktiven Login wirklich abnehmen
- [ ] Wenn weiterhin fehlgeschlagen: neue bereinigte Browser-Netzwerkdaten (URL-Pfad/Status und JS-Fehler), nicht blind zusätzliche Proxy-Routen freischalten


### 11.11.2026 – Phase 7: Confluence blockierte Frames nach dem Login (PR #28)

- [x] Ursache im Quellcode gefunden: Favoriten-`frame-ancestors` enthielt nur Plenaro-Origin; Confluence kann nach Login eigene verschachtelte `/wiki/`-Frames laden.
- [x] Restriktiv `'self'` als zusätzlich erlaubten Vorfahren ergänzt, analog zu Znuny; kein globales `*`, kein Öffnen fremder Hosts, ACL/Cookies/CSRF unverändert.
- [x] Regressionstests für dedizierten Favoritenhost und gemeinsamen Pfadhost hinzugefügt.
- [x] GitHub-CI vollständig erfolgreich ([Run #43007066314](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/43007066314)); [PR #28](https://github.com/DerHausmeisterKrause/plenaro/pull/28) nach main gemergt.
- [ ] Live-Abnahme: Nutzername/Passwort absenden und echte Weiterleitung testen, Browser-Frame/Nachlade-Verhalten prüfen. Screenshot allein beweist keine erfolgreiche Anmeldung.
- [ ] Wenn Browser weiterhin blockiert: HTTP-Dokumentnavigation (Status, Location, X-Frame-Options, CSP) und Konsole anonymisiert erfassen; vHost-Header oder fremden Identity-Provider gesondert prüfen.

### 11.11.2026 – Phase 7: Confluence-Login-POST erfolgreich, Navigation in gesperrte Root (PR #29)

- [x] Bereinigte HAR-Metadaten analysiert: erfolgreicher `POST /wiki/rest/tsv/1.0/authenticate` mit HTTP 200 und `{"next":"/"}`, unmittelbar danach `GET /` HTTP 404 im Favoriten-Pfadhost
- [x] Erfolgreiche JSON-Antwort am konkreten Confluence-Anmelde-Endpunkt unter Opt-in `wiki` so korrigiert, dass `next` auf `/wiki/` beziehungsweise `/wiki/<ziel>` zeigt; andere JSON-Antworten und Fehlerstatus bleiben unverändert
- [x] Freigaberegeln, Authentifizierung, Cookie-Jar, Root-404 und fremde Favoriten unverändert; keine globalen Root-Redirects
- [x] Regressionstests für Erfolg/Fehler, Route, MIME, Slug, externe URLs und Idempotenz ergänzt
- [x] GitHub-CI erfolgreich ([Run #43009144307](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/43009144307)); [PR #29](https://github.com/DerHausmeisterKrause/plenaro/pull/29) nach main gemergt
- [ ] Zielhost: Docker-Web und Docker-NGINX aktualisieren, persönliche Session neu verbinden, Anmelde-POST 200 `{"next":"/wiki/"}`, navigierte Wiki-Seite und zwei Benutzer wirklich abnehmen
