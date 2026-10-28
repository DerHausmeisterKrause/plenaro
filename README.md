# Plenaro 2.0

**Self-hosted Aufgaben-, Zeit- und ITSM-Plattform.** Dieses Repository ist der neue, plattformunabhängige Web-Nachfolger von [Plenaro Desktop / project_tool](https://github.com/DerHausmeisterKrause/project_tool).

> **Entwicklungsstand:** Phase 1 (Grundlage). Der Docker-Stack startet einen ASP.NET-Core-Webdienst und PostgreSQL. **Noch kein Benutzer-Login, keine Aufgabenverwaltung und keine Migration produktiver Daten.** Nicht öffentlich ohne vorgeschaltete Zugriffskontrolle bereitstellen.

## Schnellstart (Docker Compose)

Voraussetzungen: Docker Engine inkl. Compose Plugin (oder Docker Desktop).

```bash
git clone https://github.com/DerHausmeisterKrause/plenaro.git
cd plenaro
cp .env.example .env
```

In `.env` `POSTGRES_PASSWORD` mit einem **zufällig erzeugten, starken Passwort** befüllen, z. B. unter Linux mit `openssl rand -hex 32`. Die Beispiel-Datei enthält **absichtlich kein Passwort**.

```bash
docker compose up -d --build --wait
docker compose ps
curl -f http://127.0.0.1:8080/health/live
curl -f http://127.0.0.1:8080/health/ready
```

Unter Windows/PowerShell: `Copy-Item .env.example .env` statt `cp`.

Weboberfläche: **http://127.0.0.1:8080** (Statusseite, noch keine Anwendungsfunktionen). Standardmäßig ist der Port **nur an 127.0.0.1** gebunden. Für den Zugriff über das Netzwerk später ausschließlich HTTPS-Reverse-Proxy mit Zugriffsschutz nutzen. Ein bloßes Ändern der Host-Bindung ist noch **keine** sichere Veröffentlichung.

### Betrieb und Daten

```bash
docker compose logs -f web
docker compose restart web
docker compose down
```

Die Datenbank bleibt im benannten Docker-Volume `plenaro_postgres_data` bestehen. **`docker compose down -v` löscht auch Datenbankdaten!** Backups und Restore-Tests werden vor dem Produktivbetrieb ergänzt. PostgreSQL ist nicht auf dem Host veröffentlicht.

### Entwicklung (ohne Docker)

```bash
dotnet restore src/Plenaro.Api/Plenaro.Api.csproj
dotnet run --project src/Plenaro.Api
```

Für `/health/ready` ist die Umgebungsvariable `ConnectionStrings__Plenaro` mit einer erreichbaren PostgreSQL-Verbindung nötig. Ohne Datenbank meldet nur `/health/live` erfolgreich. SDK: .NET 10.

## Architektur und Migration

- Backend: ASP.NET Core (.NET 10, C#), versionierte API wird in den nächsten Phasen aufgebaut.
- Persistenz: PostgreSQL (mit persistentem Docker-Volume), später versionierte Migrationen.
- Web: Browseroberfläche; im Grundgerüst eine einfache Statusseite, später ein separates responsives Frontend.
- Authentifizierung: später lokaler Admin und lokale Accounts, rollenbasierte Berechtigungen, optional Microsoft Entra ID (OIDC).
- Outlook: Microsoft Graph / Entra statt Windows-COM-Interop.
- Ticketsysteme: Znuny/OTRS als erster Adapter; Adapter-Schnittstelle für weitere Systeme.
- Der Datenbestand und die Desktop-Logik werden **gezielt migriert**, nicht unbesehen kopiert.

Details: [Roadmap](docs/ROADMAP.md), [Architektur](docs/ARCHITECTURE.md), [Funktionsinventar](docs/DESKTOP_MIGRATION.md), [Sicherheit](docs/SECURITY.md).

## Lizenz

**Plenaro Community License v0.1**: private Nutzung kostenlos; betriebliche, behördliche und sonstige organisatorische Nutzung bedarf einer gesonderten Vereinbarung. Das ist eine **Source-Available-**, keine Open-Source-Lizenz. Der Vertragstext ist vor externer Vermarktung juristisch zu prüfen. Siehe [LICENSE](LICENSE) und [Lizenzmodell](docs/LICENSING.md). Die **alte** Desktop-Version unter MIT bleibt weiterhin MIT-lizenziert.

## Entwicklungsstand

Die sieben Phasen, die erledigten Teilaufgaben und die Abnahmekriterien stehen in [docs/ROADMAP.md](docs/ROADMAP.md). Ein Häkchen bedeutet **Code umgesetzt**; die vollständige Abnahme von Phase 1 erfordert außerdem einen erfolgreichen Docker-Smoke-Test.
