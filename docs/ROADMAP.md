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
- [ ] Desktop-SQLite-Datenimport mit Validierungs-/Rollback-Strategie

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
- [ ] Desktop-Datenimport von Zeitbuchungen und Zeitsegmenten; lokale Browser-Abnahme
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
