# Plenaro 2.0

**Self-hosted Aufgaben-, Zeit- und ITSM-Plattform.** Dieses Repository ist der neue, plattformunabhängige Web-Nachfolger von [Plenaro Desktop / project_tool](https://github.com/DerHausmeisterKrause/project_tool).

> **Entwicklungsstand:** Phase 2 – Responsive Weboberfläche mit React/TypeScript, ASP.NET-Core-Backend und PostgreSQL. Die Navigation ist nutzbar, aber **noch keine Benutzeranmeldung und keine produktiven Aufgaben, Tickets oder Benutzer-Daten**. Nur in einem vertrauenswürdigen LAN verwenden; nicht ins Internet freigeben.

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

Weboberfläche: **http://HOST-IP:8080** (zum Beispiel `http://192.168.178.50:8080`). Der Compose-Port ist standardmäßig auf **0.0.0.0** gebunden, also auf allen Interfaces des Docker-Hosts. `PLENARO_BIND_IP=127.0.0.1` begrenzt auf den lokalen Host; `PLENARO_BIND_IP=192.168.178.50` begrenzt auf diese Host-IP. Die Host-IP muss tatsächlich auf dem Host vorhanden sein. **Kein Portforwarding ins Internet einrichten und keinen öffentlichen Server ohne HTTPS und Authentifizierung betreiben!** Falls der LAN-Zugriff fehlschlägt, Host-Firewall/TCP 8080 für das vertrauenswürdige LAN prüfen.

### Betrieb und Daten

```bash
docker compose logs -f web
docker compose restart web
docker compose down
```

Die Datenbank bleibt im benannten Docker-Volume `plenaro_postgres_data` bestehen. **`docker compose down -v` löscht auch Datenbankdaten!** Backups und Restore-Tests werden vor dem Produktivbetrieb ergänzt. PostgreSQL ist nicht auf dem Host veröffentlicht.

### Frontend-Entwicklung

Voraussetzung: Node.js 22 und npm. Die Browser-App liegt in `frontend/`.

```bash
cd frontend
npm install
npm run dev
# neues Terminal: Backend fuer /health auf http://127.0.0.1:5080 starten:
# im Repository-Root:
ASPNETCORE_URLS=http://127.0.0.1:5080 dotnet run --project src/Plenaro.Api
```

Für Tests: `cd frontend && npm test && npm run build`. Die Node-Abhängigkeiten bekommen in einem nachfolgenden Schritt einen festen Lockfile-Stand (derzeit `npm install`).

## Entwicklung (ohne Docker)

```bash
dotnet restore src/Plenaro.Api/Plenaro.Api.csproj
dotnet run --project src/Plenaro.Api
```

Für `/health/ready` ist die Umgebungsvariable `ConnectionStrings__Plenaro` mit einer erreichbaren PostgreSQL-Verbindung nötig. Ohne Datenbank meldet nur `/health/live` erfolgreich. SDK: .NET 10.

## Architektur und Migration

- Backend: ASP.NET Core (.NET 10, C#), versionierte API wird in den nächsten Phasen aufgebaut.
- Persistenz: PostgreSQL (mit persistentem Docker-Volume), später versionierte Migrationen.
- Web: React 19 + TypeScript + Vite 8, vom ASP.NET-Core-Webserver als statische SPA ausgeliefert. Responsive Navigation, Dunkelmodus, Live-Status und Modul-Platzhalter für die noch nicht vorhandenen Funktionen.
- Authentifizierung: später lokaler Admin und lokale Accounts, rollenbasierte Berechtigungen, optional Microsoft Entra ID (OIDC).
- Outlook: Microsoft Graph / Entra statt Windows-COM-Interop.
- Ticketsysteme: Znuny/OTRS als erster Adapter; Adapter-Schnittstelle für weitere Systeme.
- Der Datenbestand und die Desktop-Logik werden **gezielt migriert**, nicht unbesehen kopiert.

Details: [Roadmap](docs/ROADMAP.md), [Architektur](docs/ARCHITECTURE.md), [Funktionsinventar](docs/DESKTOP_MIGRATION.md), [Sicherheit](docs/SECURITY.md).

## Lizenz

**Plenaro Community License v0.1**: private Nutzung kostenlos; betriebliche, behördliche und sonstige organisatorische Nutzung bedarf einer gesonderten Vereinbarung. Das ist eine **Source-Available-**, keine Open-Source-Lizenz. Der Vertragstext ist vor externer Vermarktung juristisch zu prüfen. Siehe [LICENSE](LICENSE) und [Lizenzmodell](docs/LICENSING.md). Die **alte** Desktop-Version unter MIT bleibt weiterhin MIT-lizenziert.

## Entwicklungsstand

Die sieben Phasen, die erledigten Teilaufgaben und die Abnahmekriterien stehen in [docs/ROADMAP.md](docs/ROADMAP.md). Ein Häkchen bedeutet **Code umgesetzt**, nicht zwangsläufig schon produktiv abgenommen. Phase 2 zeigt derzeit nur die Web-Shell, keine Geschäftsfunktionen.
