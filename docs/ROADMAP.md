# Plenaro 2.0 – 7-Phasen-Roadmap

Stand: 28.10.2026. Die ersten vier Meilensteine folgen dem bisherigen Gespräch (Docker → Weboberfläche → lokale Benutzer → Entra). Die späteren Phasen sind anhand des tatsächlich vorhandenen Desktop-Codes für eine vollständige Webmigration **konkretisiert**, nicht wörtlich aus einer früheren Liste übernommen.

Legende: `[x]` = implementierter Code / vorhandene Datei; `[ ]` = offen. Ein grüner CI-Lauf und ein echter Starttest sind zusätzliche Abnahmekriterien.

## Phase 1 – Repository & Docker-Basis (CI bestanden, Zielhost teilweise geprüft)
- [x] Neues getrenntes Plenaro-2.0-Repository, README und Architektur dokumentieren
- [x] Source-Available-Community-Lizenz und gewerbliche Zweitlizenzierung dokumentieren (Vertragstext vor Vermarktung juristisch prüfen)
- [x] .gitignore, .dockerignore, .env.example und Secret-Grundregeln
- [x] .NET-10-Webdienst mit Live-/Datenbank-Readiness-Endpoints
- [x] PostgreSQL 17, Docker-Volume, internes Netzwerk und Healthcheck
- [x] Docker Compose mit Web/DB, konfigurierbarer LAN-Bindung und Startabhängigkeiten
- [x] Minimale Browser-Statusseite
- [x] GitHub Actions CI: Build, Docker-Compose-Syntax, Build & HTTP-Smoke-Test (Workflow hinzugefügt, Lauf noch zu prüfen)
- [x] GitHub Actions: .NET-Build, Compose-Syntax und Docker-HTTP-Smoke-Test erfolgreich ([CI #1](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/40755261974))
- [x] Nutzer bestätigt: Docker läuft, Webseite per curl auf dem Host erreichbar (28.10.2026)
- [ ] LAN-Zugriff via Host-IP nach Update und Datenpersistenz/Neustart am Zielhost prüfen
- [ ] Lizenz juristisch final prüfen (vor kommerziellem Release)

**Abnahme:** `http://127.0.0.1:8080` zeigt den Webstatus, `/health/ready` prüft PostgreSQL und Persistenz über Neustarts.

## Phase 2 – Browseroberfläche
- [ ] Frontend-Framework, Routing und Layout-Grundstruktur aufbauen
- [ ] Responsive Navigation: Heute, Woche, Aufgaben, Tickets, Wiki, KI, Berichte, Einstellungen
- [ ] Login-, Logout- und Fehlerseiten gestalten (noch kein Auth-Backend aus Phase 3)
- [ ] Geschützte Startseite und API-Client mit Fehler-/Loading-States
- [ ] Barrierearme Desktop-/Mobile-Darstellung und Themes
- [ ] UI-Tests und E2E-Grundtests

**Abnahme:** Navigierbare Weboberfläche, geschützte Bereiche ohne Session nicht erreichbar.

## Phase 3 – Benutzer, Admin & Rechte
- [ ] PostgreSQL-Schema/Versionierung für Nutzer, Rollen, Einstellungen, Sessions
- [ ] Ersteinrichtung mit lokalem Admin
- [ ] Lokale Benutzer anlegen, sperren, Kennwort wechseln/resetten
- [ ] Passwort-Hashing, Session-Cookies und sichere API-Autorisierung
- [ ] Rollen: Admin / User; später erweiterbare Berechtigungen
- [ ] Pro-User-Datentrennung, Einstellungen und sichere Secret-Speicherung
- [ ] Session-/Login-/Berechtigungstests

**Abnahme:** Zwei Nutzer sehen ohne ausdrückliche Freigabe nicht gegenseitig Daten.

## Phase 4 – Microsoft Entra & Outlook
- [ ] Entra App Registration: Tenant, Redirect URI, OIDC/PKCE, Scopes dokumentieren
- [ ] Optionaler Microsoft-SSO-Login neben lokalen Benutzern
- [ ] Entra-Identität mit lokalem Benutzerkonto verknüpfen
- [ ] Tenant-Admin-Konfiguration und sichere Graph-Tokenverwaltung
- [ ] Microsoft Graph: Kalender lesen, Termin erstellen/ändern/löschen
- [ ] Busy-Anzeige, Teams-Einladungen und Fokusblock-Synchronisation
- [ ] Konflikt- und Berechtigungstests

**Abnahme:** Lokal-/SSO-Login parallel; Outlook funktioniert ohne lokal installiertes Outlook.

## Phase 5 – Aufgaben, Zeit, Berichte
- [ ] Heute-Ansicht und Aufgaben-CRUD inkl. Status, Priorität, Tags und Suche
- [ ] Tages-/Wochenplanung, Segmente und Kalenderdarstellung
- [ ] Zeitbuchungen, Pausen, Arbeitszeiterfassung und Korrekturen
- [ ] Monatsberichte und Export
- [ ] Homeoffice-Workflows
- [ ] Aufgaben teilen, Einladungen und Besitzerrechte
- [ ] Desktop-SQLite-Datenimport mit Validierungs-/Rollback-Strategie

**Abnahme:** Kernabläufe der Desktop-Aufgaben-/Zeitverwaltung funktionieren im Browser.

## Phase 6 – Ticketsysteme / ITSM
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
- [ ] Favoriten und Benachrichtigungen
- [ ] Erweiterungs-/Add-in-Schnittstelle mit Sicherheitsmodell
- [ ] DSGVO-Konzept, Audit, Backups/Restore-Tests und Betriebsdokumentation
- [ ] End-to-End- und Migrationstests auf Funktionsparität mit Desktop
- [ ] Versionierung, Releases, Docker-Image-Publishing und Upgradepfade
- [ ] Kommerzielle Lizenz- und Betriebsbedingungen finalisieren

**Abnahme:** Nachvollziehbar getestete produktionsreife Webversion mit Dokumentation und Updatepfad.
