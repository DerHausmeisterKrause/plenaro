# Zielarchitektur – Plenaro 2.0

```text
Browser --(HTTP 80 / HTTPS 443)--> NGINX --> ASP.NET Core Web/API
                                                   |
                                      PostgreSQL (Docker internal)
                                                   |
                  +--------------------------------+----------------+
                  |                                |                |
            Aufgaben/Zeiten                  Ticketadapter     Microsoft Graph
                                             (OTRS/Znuny, ...)  (Entra/Outlook)
```

## Im ersten Commit

- ASP.NET Core 10 als eigenständiger, plattformneutraler Container.
- PostgreSQL 17 mit persistentem Volume und Healthcheck, internem Docker-Netz ohne Host-Port.
- Webserver stellt Statusseite und `/health/live` sowie `/health/ready` bereit.
- Docker Compose, lokale Konfiguration per `.env`, CI prüft Build/Compose/HTTP.
- Web/API nur im Docker-Netz erreichbar; NGINX ist der einzige öffentlich gemappte Web-Einstiegspunkt.

## Geplante Schnittstellen

- `/api/v1/`: später versionierte CRUD-API für Aufgaben, Segmente, Zeitbuchungen und Einstellungen.
- OIDC Authorization Code + PKCE für Microsoft Entra ID; lokale Accounts bleiben möglich.
- Serverseitige Rollenprüfung auf **allen** privaten API-Endpunkten (nicht bloß Buttons ausblenden).
- Adapter-Interface (Ticketsystem) mit pro Nutzer/Organisation konfigurierten Verbindungen.
- Connector-Geheimnisse werden geschützt gespeichert, nicht im Browser ausgeliefert.
- Für Microsoft-Graph-Delegated Permissions Einwilligung pro Nutzer; kein COM und kein lokales Outlook im Container.
- Datenmodell mit durchgängigem Besitzer-/Mandantenbezug, Migrationsskripten und Audit-/Zeitstempeln.

## Wichtige Grenzen

Eine WPF-XAML-Oberfläche kann nicht direkt im Browser ausgeführt werden. Outlook COM, Windows Dispatcher, lokale Taskleiste, Clipboard und Autoupdate-EXE müssen ersetzt oder konzeptionell angepasst werden. Die Desktop-MIT-Lizenz bleibt unverändert.


## Umsetzung Phase 2 – Frontend
- React 19 + TypeScript + Vite 8 unter `frontend/`.
- Vite baut in einem Node-22-Builder; fertige JS/CSS-Dateien liegen im ASP.NET-Core-Runtime-Image unter `/app/wwwroot`.
- Browser-Routen werden durch `MapFallbackToFile("index.html")` auf die React-SPA umgeleitet.
- `/health/live` und `/health/ready` bleiben separate Backend-Endpunkte und werden vom Browser abgefragt.
- Navigation und Login-/Logout-Seiten sind Platzhalter **ohne Credentials, Session oder personenbezogene Daten**.
- NGINX-Hostportbindung ist per `PLENARO_BIND_IP` steuerbar; im vertrauenswürdigen LAN 0.0.0.0, bei Einzelhost 127.0.0.1.


## Datenbankschema und Migrationen (Phase-1-Abschluss)

- PostgreSQL-SQL-Dateien unter `src/Plenaro.Api/Database/Migrations`, als EmbeddedResources in der API enthalten.
- `SchemaMigrator` führt ausstehende Migrationen beim Start **innerhalb einer Transaktion** mit PostgreSQL Advisory Lock aus; alle Versionen und SHA-256-Hashes in `schema_migrations`.
- Migrationen werden **nicht nachträglich bearbeitet**. Zusätzliche Tabellen/Spalten über eine neue nummerierte Datei einführen.
- `/health/ready` verlangt DB-Verfügbarkeit, exakte Versionshistorie und Kern-Tabellen. Die Anwendung startet bei inkonsistenter History bewusst nicht.
- Basistabellen `tasks`, `task_segments`, `time_entries`, `system_settings` ohne Demo- oder Produktivdaten.
- `owner_user_id` ist UUID und derzeit ohne User-FK: **ASP.NET Identity und die User-FK folgen als eigener Schritt**. Noch keine Schreib-API freischalten.
- Details: [DATABASE.md](DATABASE.md).


## Authentifizierung (Phase 3)

- `0003_identity.sql` ergänzt Identity-Tabellen, Rollen, Claims, Logins, Token, `DataProtectionKeys` und Owner-Fremdschlüssel.
- EF-Core `PlenaroIdentityDbContext` implementiert den ASP.NET-Identity-Store, Session-Cookie-Keys werden in PostgreSQL persistiert.
- Einmaliges Admin-Bootstrap per `docker compose exec web dotnet Plenaro.Api.dll --bootstrap-admin`.
- Endpunkte: `GET /api/v1/auth/csrf`, `POST /api/v1/auth/login`, `GET /api/v1/auth/me`, `POST /api/v1/auth/logout`, `GET /api/v1/admin/ping`.
- Fachliche CRUD-Endpunkte dürfen erst mit geeigneter Autorisierung, Ownership-Prüfung und Rollenfreigabe online gehen.


## Browserbasierter First-Run (Phase 3)

- Neue Migration `0004_setup_tokens.sql` verwaltet einen einzigen zeitlich beschränkten Einrichtungscode-Hash.
- Die React-App fragt `/api/v1/setup/status` ab und schaltet bei fehlenden Benutzern ausschließlich den Setup-Assistenten frei. Login und Assistent sind unabhängig von der Workspace-Shell.
- Der Administrator wird im Browser angelegt, jedoch nur mit einem vom Host erzeugten, einmaligen Code. Nach Erstellung ist das Setup serverseitig gesperrt.
- GitHub Actions: ein PR-only Workflow mit Build und kleinem Docker-Smoke-Test.


## Benutzerverwaltung (Phase 3)

Migration \`0005_user_management.sql\` ergänzt das administrative Deaktivierungsflag \`AspNetUsers.IsDisabled\`. Rollen- und Statusänderungen laufen transaktional unter Advisory Lock, um parallel ausgeführte Änderungen des letzten aktiven Admins zu verhindern. Cookies werden per Security-Stamp-Rotation widerrufen. Alle Admin-User-Endpunkte verlangen serverseitige Rolle Admin und CSRF für Schreibzugriffe. Eigenes Passwort kann über \`/api/v1/auth/change-password\` geändert werden. Keine Benutzerlöschung, keine fachlichen CRUD-Endpunkte.


## Phase 3 – Persistente persönliche und globale Einstellungen

- SQL-Migration `0006_user_preferences.sql`: `user_preferences` pro Identity-Benutzer mit `user_id` als Primär- und Fremdschlüssel, `preferences` als JSONB. Bei Benutzerlöschung werden nur dessen Einstellungen gelöscht.
- Bereits vorhandene Tabelle `system_settings` speichert unter `workspace.general` ausschließlich öffentliche, nicht geheime Instanzwerte.
- `GET/PUT /api/v1/settings/me`: Identität wird aus dem gültigen Sitzungscookie ermittelt, keine frei wählbare Benutzer-ID in Query oder Payload.
- `GET /api/v1/settings/global`: nur authentifiziert. `PUT /api/v1/settings/global`: zusätzliche `AdminOnly`-Policy. Alle PUT-Endpunkte mit CSRF.
- Strikt definierte Felder: persönlich `theme` (light/dark), `weekStart` (inherit/monday/sunday); global `instanceName`, `supportEmail`, `defaultWeekStart`.
- React lädt die gespeicherte Designvorgabe bei Anmeldung und zeigt für Admins bearbeitbare globale, für normale User nur lesbare globale Einstellungen. Wochenbeginn wird zunächst nur gespeichert; die Kalenderfunktion wird erst in Phase 5 implementiert.
- Keine Passwörter, Tokens, Integrationssecrets oder beliebigen JSON-Schlüssel in dieser API; dafür ist ein separates verschlüsseltes Konzept erforderlich.

## Phase 3 – NGINX als Standard-Frontend (ersetzt Caddy)

- `compose.yaml` startet bei jedem normalen `docker compose up` PostgreSQL, ASP.NET Core und NGINX.
- Nur NGINX veröffentlicht Host-Port 80/443. ASP.NET Core nutzt weiterhin Port 8080, aber **nur intern**.
- `PLENARO_HTTPS_ENABLED=false`: HTTP-Template; `true`: separates HTTPS-Template mit TLS 1.2/1.3, Zertifikaten aus Hostpfaden und 301-Redirect HTTP → HTTPS.
- `PLENARO_SERVER_NAME` ist optional, Standard `_` für Host-IP. Ein FQDN benötigt DNS und ein passendes TLS-Zertifikat.
- NGINX verwendet die feste Edge-Netz-IP `172.29.213.10`; ASP.NET Core vertraut nur dieser IP für X-Forwarded-For/Proto, auch für korrektes Login-Rate-Limiting im HTTP-Entwicklungsmodus.
- Keine zweite Proxy-Infrastruktur, keine externe CA-Automatik. Verschlüsselter Vault unverändert, optionale Hostdatei `./secrets/vault.key`.
