# Zielarchitektur – Plenaro 2.0

```text
Browser --(später HTTPS Reverse Proxy / SSO)--> ASP.NET Core Web/API
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
- Web-Port aus Sicherheitsgründen nur an `127.0.0.1`, keine Geschäftsdaten ohne Auth.

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
