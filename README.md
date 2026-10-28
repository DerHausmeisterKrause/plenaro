# Plenaro 2.0

**Self-hosted Aufgaben-, Zeit- und ITSM-Plattform.** Dieses Repository ist der neue, plattformunabhängige Web-Nachfolger von [Plenaro Desktop / project_tool](https://github.com/DerHausmeisterKrause/project_tool).

> **Entwicklungsstand:** Phase 3 – Responsive Weboberfläche mit React/TypeScript, ASP.NET-Core-Backend und PostgreSQL. Die Navigation ist nutzbar, aber **noch keine produktiven Aufgaben oder Tickets**. Nur in einem vertrauenswürdigen LAN verwenden; nicht ins Internet freigeben.

## Lokale Benutzeranmeldung und einmaliger Administrator

Sobald du Plenaro erstmals öffnest, erscheint **automatisch der Einrichtungsassistent**, sofern noch keine Benutzerkonten vorhanden sind. Für die Einrichtung brauchst du einen Einmalcode, den du direkt auf dem Docker-Host erzeugst:

```bash
docker compose exec web dotnet Plenaro.Api.dll --setup-token
```

Kopiere den ausgegebenen Code in den Assistenten. Er ist **15 Minuten gültig** und kann nur für die erste Admin-Erstellung verwendet werden. Ein erneut erzeugter Code macht den vorherigen ungültig. Auf der Webseite gibst du Anzeigename, E-Mail, Passwort und den Code ein. Ein beliebiger Netzwerkbesucher kann dadurch keinen Administrator ohne Hostzugriff anlegen. Existiert bereits ein Benutzer, erscheint kein Assistent mehr.

Nach erfolgreicher Einrichtung erscheint unter `/login` **nur die Loginmaske**, ohne Seitenleiste oder sonstige App-Navigation.

**HTTPS ist jetzt direkt über den NGINX-Container in Compose aktivierbar:** siehe [HTTPS und Secret-Vault](docs/HTTPS_AND_VAULT.md). HTTP im LAN ist unverschlüsselt. Vor Eingabe echter Passwörter einen HTTPS-Reverse-Proxy mit gültigem Zertifikat nutzen; keine WAN-Portweiterleitungen!

## Schnellstart (Docker Compose – NGINX inklusive)

Voraussetzung: Docker Engine inklusive Docker Compose Plugin. NGINX, ASP.NET Core und PostgreSQL werden mit **einem** Befehl gestartet.

```bash
git clone https://github.com/DerHausmeisterKrause/plenaro.git
cd plenaro
cp .env.example .env
# In .env ein starkes POSTGRES_PASSWORD setzen (z. B. openssl rand -hex 32).
docker compose up -d --build --wait
docker compose ps
curl -f http://127.0.0.1/health/ready
```

Die Seite ist standardmäßig direkt unter **http://HOST-IP/** erreichbar, ohne `:8080`. Bei optional aktiviertem HTTPS unter **https://HOST-IP/**; Anfragen an HTTP werden dann automatisch nach HTTPS umgeleitet. Ein optionaler FQDN kann über `PLENARO_SERVER_NAME` angegeben werden, sofern der DNS-Eintrag auf die Server-IP zeigt.

**Bereits installierte Version:** Vor der Umstellung die bisherigen Container mit `docker compose down --remove-orphans` stoppen (ohne `-v`!), dann `git pull --ff-only` und `docker compose up -d --build --wait --remove-orphans`. Wer zuvor den Caddy-Overlay gestartet hat, beendet vor dem Update dessen Stack mit `docker compose -f compose.yaml -f compose.secure.yaml down`.

Die HTTP-/HTTPS-Wahl sowie Hostpfade für Zertifikat und Schlüssel werden vollständig in der `.env` konfiguriert. Beispiele stehen in [NGINX, HTTPS und Secret-Vault](docs/HTTPS_AND_VAULT.md). Keine Internetfreigabe ohne Sicherheitsabnahme.

### Betrieb und Daten

```bash
docker compose logs -f web
docker compose restart web
docker compose down
```

Beim Web-Start werden neue SQL-Migrationen automatisch und transaktional eingespielt. **Keine Produktivdaten importieren**, solange Login/Rollen fehlen.

### Prüfen des Datenbankschemas

```bash
docker compose exec -T database psql -U plenaro -d plenaro -c 'SELECT migration_id, applied_at FROM schema_migrations ORDER BY migration_id;'
curl -f http://127.0.0.1/health/ready
```

Wenn Benutzername/DB-Name in `.env` angepasst wurden, auch den obigen psql-Befehl entsprechend anpassen. Die Migrationen liegen unter `src/Plenaro.Api/Database/Migrations`. Beim nächsten Code-Update bitte **vorher ein Datenbankbackup erstellen**. Ein fehlgeschlagenes Upgrade wird nicht automatisch rückwärts migriert.

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
- Persistenz: PostgreSQL mit persistentem Docker-Volume und versionierten, transaktionalen SQL-Migrationen. Basistabellen: Aufgaben, Segmente, Zeitbuchungen, globale Einstellungen. Benutzer/Identity kommen als nächster Schritt. Details unter [Datenbankschema](docs/DATABASE.md).
- Web: React 19 + TypeScript + Vite 8, vom ASP.NET-Core-Webserver als statische SPA ausgeliefert. Responsive Navigation, Dunkelmodus, Live-Status und Modul-Platzhalter für die noch nicht vorhandenen Funktionen.
- Authentifizierung: lokale ASP.NET-Identity-Anmeldung mit einmaligem Administrator, serverseitiger Adminrolle, CSRF-geschütztem Login/Logout, Cookie-Sitzung und Login-Rate-Limits. Verwaltung und Entra folgen.
- Outlook: Microsoft Graph / Entra statt Windows-COM-Interop.
- Ticketsysteme: Znuny/OTRS als erster Adapter; Adapter-Schnittstelle für weitere Systeme.
- Der Datenbestand und die Desktop-Logik werden **gezielt migriert**, nicht unbesehen kopiert.

Details: [Roadmap](docs/ROADMAP.md), [Architektur](docs/ARCHITECTURE.md), [Funktionsinventar](docs/DESKTOP_MIGRATION.md), [Sicherheit](docs/SECURITY.md).

## Lizenz

**Plenaro Community License v0.1**: private Nutzung kostenlos; betriebliche, behördliche und sonstige organisatorische Nutzung bedarf einer gesonderten Vereinbarung. Das ist eine **Source-Available-**, keine Open-Source-Lizenz. Der Vertragstext ist vor externer Vermarktung juristisch zu prüfen. Siehe [LICENSE](LICENSE) und [Lizenzmodell](docs/LICENSING.md). Die **alte** Desktop-Version unter MIT bleibt weiterhin MIT-lizenziert.

## Entwicklungsstand

Die sieben Phasen, die erledigten Teilaufgaben und die Abnahmekriterien stehen in [docs/ROADMAP.md](docs/ROADMAP.md). Ein Häkchen bedeutet **Code umgesetzt**, nicht zwangsläufig schon produktiv abgenommen. Phase 2 zeigt derzeit nur die Web-Shell, keine Geschäftsfunktionen.

## CI

Ein einziger GitHub-Actions-Workflow startet **nur für Pull Requests oder manuell**. Er führt einen Build mit den vorhandenen Frontendtests und einen kompakten Docker-Smoke-Test mit Admin-Einrichtung und Login aus. Kein zusätzlicher Lauf beim Merge nach `main`.


## Benutzerverwaltung

Nach Anmeldung als Administrator unter **Einstellungen** Benutzer anlegen, Anzeigenamen bearbeiten, Rollen Admin/User vergeben und Konten sperren/entsperren. Admins können Passwörter zurücksetzen; alle Benutzer ihr eigenes Passwort ändern. Benutzer können nicht gelöscht werden, um spätere Besitzerbezüge zu erhalten. Der eigene Administratoraccount kann nicht deaktiviert oder herabgestuft werden.

**Vor echten Passwort-Eingaben HTTPS einrichten!** Über die Host-IP mit HTTP sind Zugangsdaten unverschlüsselt.

API: \`GET/POST /api/v1/admin/users/\`, \`PUT /api/v1/admin/users/{id}\`, \`POST /api/v1/admin/users/{id}/reset-password\`, \`POST /api/v1/auth/change-password\`. Alle Mutationen mit CSRF, Admin-Funktionen serverseitig rollenbeschränkt.


## Persönliche und globale Einstellungen (Phase 3)

Unter **Einstellungen** kann jeder angemeldete Benutzer sein Hell-/Dunkel-Design und den künftigen Wochenbeginn dauerhaft speichern. Die Werte liegen in PostgreSQL pro Benutzer, unabhängig vom verwendeten Browser. Administratoren können zusätzlich Instanzname, optionalen Supportkontakt und Standard-Wochenbeginn ändern. Normale Benutzer dürfen diese Instanzwerte nur einsehen.

- Persönlich: `GET/PUT /api/v1/settings/me`
- Global: `GET/PUT /api/v1/settings/global`; **PUT nur für Admin**
- Änderungen verlangen einen CSRF-Token; die Datenbankmigration `0006_user_preferences.sql` wird beim Containerstart automatisch ausgeführt.

Der Wochenbeginn dient als vorbereitete Einstellung für die spätere Planung, eine Kalenderansicht nutzt ihn noch nicht. **Keine Passwörter, Graph-Tokens, API-Schlüssel oder andere Secrets in die Einstellungen eingeben!** Deren Verschlüsselung und sichere Speicherung erfolgen als gesonderter Schritt. Betrieb mit echten Konten nur über HTTPS.

## NGINX, HTTPS und Secret-Vault

NGINX läuft **immer mit Docker Compose**. Über `PLENARO_HTTPS_ENABLED=false` (Standard) wird Port 80 für HTTP verwendet. Bei `true` werden das in der `.env` angegebene Zertifikat und der Schlüssel geladen, HTTPS auf 443 bedient und HTTP automatisch auf HTTPS umgeleitet. Kein zweiter Compose-Overlay und kein Caddy nötig.

Der verschlüsselte Integrations-Vault nutzt bei Aktivierung eine separate Schlüsseldatei unter `./secrets/vault.key`. Sie muss separat gesichert werden.

Siehe [NGINX, HTTPS und Secret-Vault](docs/HTTPS_AND_VAULT.md).
