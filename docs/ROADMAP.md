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
