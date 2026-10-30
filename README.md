# Plenaro 2.0

**Self-hosted Aufgaben-, Zeit- und ITSM-Plattform.** Dieses Repository ist der neue, plattformunabhängige Web-Nachfolger von [Plenaro Desktop / project_tool](https://github.com/DerHausmeisterKrause/project_tool).

> **Entwicklungsstand:** Phase 5 – Eigene Aufgaben, Timer, Tagesmarker, lokale Zeitsegmente und Monatsberichte funktionieren in der Weboberfläche. Die Znuny/OTRS-Anbindung, Desktop-Datenübernahme und vollständige Abnahme stehen noch aus. Nur in einem vertrauenswürdigen LAN oder mit korrekt eingerichtetem HTTPS betreiben; nicht ungeprüft ins Internet freigeben.

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

## Microsoft Entra ID – optionaler Single-Tenant-SSO-Login (Phase 4)

Neben dem lokalen Login kann die Microsoft-Anmeldung zugeschaltet werden, **nur bei HTTPS**. Per `.env` werden Tenant-ID und Client-ID hinterlegt; der Client-Secret-Wert bleibt ausschließlich in einer separaten, gemounteten Datei unter `secrets/`. Microsoft-Konten werden **nicht** automatisch angelegt oder per E-Mail zugeordnet: Benutzer melden sich zuerst lokal an und verknüpfen ihr bestehendes Konto unter Einstellungen mit ihrer Microsoft-Identität.

Anleitung: [Microsoft Entra SSO](docs/ENTRA_SSO.md). Der CI-Test prüft den standardmäßig deaktivierten Zustand, ein vollständiger Authentifizierungs-Test mit einem echten Entra-Tenant ist noch offen. **Microsoft Graph/Outlook ist noch nicht implementiert.**

## Microsoft Graph – Outlook-Wochenkalender (Phase 4)

Die Ansicht **Woche** kann nach einer zusätzlichen, ausdrücklich bestätigten Microsoft-Freigabe den eigenen Outlook-Kalender **nur lesen**. Das Backend bindet die Kalenderberechtigung an die bereits verknüpfte Entra-`tid:oid`-Identität; ein delegierter Refresh-Token liegt AES-256-GCM-verschlüsselt pro Benutzer im Secret-Vault, **niemals im Browser-Cookie**. Ohne Entra oder Vault erscheint ein normaler Einrichtungszustand.

**Noch nicht live mit Microsoft getestet.** Zur späteren Einrichtung sind die zweite Redirect-URI `/signin-graph`, delegierte `User.Read`/`Calendars.Read`-Berechtigungen und ein konfigurierter Vault nötig.

Anleitung: [Microsoft Graph Outlook-Kalender](docs/GRAPH_CALENDAR.md).

## Eigene Aufgaben verwalten (funktionaler Meilenstein)

Unter **Aufgaben** kannst du eigene Aufgaben mit Titel, Beschreibung, Status (Geplant/In Arbeit/Erledigt/Abgebrochen), Priorität 1–5, Tags und optionalem Planungsdatum anlegen, bearbeiten, erledigen, wieder öffnen und nach Bestätigung löschen. Über die Suchleiste und den Statusfilter lassen sich höchstens 200 Treffer auf einmal abrufen.

Unter **Heute** erscheinen die aktiven Aufgaben, die bis heute geplanten noch offenen Aufgaben und die für heute geplanten erledigten Aufgaben. Aufgaben ohne Datum erscheinen hier nur, wenn sie bereits **In Arbeit** sind; ansonsten in **Aufgaben**. Es werden **keine** Demo-Datensätze angelegt. Aufgaben sind per lokaler Identity-Benutzer-ID serverseitig voneinander isoliert.

API `GET /api/v1/tasks/`, `GET /api/v1/tasks/today?date=YYYY-MM-DD`, `POST /api/v1/tasks/`, `PUT /api/v1/tasks/{id}`, `POST /api/v1/tasks/{id}/status` und `DELETE /api/v1/tasks/{id}`. Änderungen verlangen CSRF. Eine neue additive Migration `0008_task_planning.sql` erhält sämtliche bisherigen Daten.

**Noch nicht fertig:** Abbildung aller Datenfelder des alten Desktopprogramms, historischer SQLite-Datenimport, Zeit-/Segmentplanung, Benachrichtigungen und Mehrbenutzerfreigaben. Aufgabenlöschung ist endgültig; zugeordnete Segmente werden laut Datenbankschema mit entfernt.

## UL / AM / BR – Tagesmarker und Kontingente

Unter **Heute** und **Wochenplanung** gibt es einen Monatskalender mit Tagesmarkern.
`UL` (Urlaub) und `AM` entsprechen den **alternativen Tagesarten** der früheren Desktop-Version. `BR` ist wie im Desktopmodell ein **separater Marker**, der unabhängig an einem Tag gesetzt werden kann. Kommen/Gehen, Pausen und Sollzeit werden **nicht** in diese Funktion übernommen.

Unter **Einstellungen → UL- und BR-Kontingente** legt jeder Benutzer für sich selbst fest:
- **UL-Tage pro Kalenderjahr** (0–366); bei jedem neuen Jahr wird separat gezählt.
- **BR-Tage pro Kalendermonat** (0–31); bei jedem neuen Monat wird separat gezählt.

Leer gelassene Felder bedeuten „nicht konfiguriert“, ausdrücklich `0` bedeutet ein ausgeschöpftes Kontingent. Die Übersicht zeigt verbrauchte und verbleibende Tage und eine Warnung bei **0 Resttagen oder einer Überschreitung**. Die Warnung blockiert keine Eintragungen, da nachträgliche Korrekturen möglich sein müssen. Nur wirklich markierte Tage werden gezählt, keine pauschalen Wochenenden/Feiertage. `AM` besitzt kein Kontingent. **Keine automatische Berücksichtigung von Feiertagen, Vorjahresüberträgen oder anteiligen Urlaubstagen** in dieser Version.

Gespeichert wird pro lokalem Benutzer in PostgreSQL; andere Benutzer können weder Einstellungen noch Marker abrufen oder verändern. Eine neue additive Migration `0009_day_markers_budgets.sql` erhält die alten Tabellen. Die Weboberfläche meldet verbrauchte Kontingente, ohne automatisch echte Urlaubsansprüche zu berechnen.

## Plenaro-Classic-Bedienoberfläche und lokaler Wochenkalender

Die Weboberfläche übernimmt nun das gewohnte Arbeitsmuster des alten Desktop-Programms: **Aufgabenliste links und ausgewählte Aufgabe rechts** (Schnellerfassung, Suche, Filter, Detailaktionen). Das Ticketsystem hat dieselbe Zweiteilung, seine echten Ticketdaten folgen erst mit dem Znuny-Adapter.

**Wochenplanung funktioniert ohne Entra ID oder Outlook:** lokales 06–18-Uhr-Kalenderraster mit PostgreSQL-Aufgaben, UL/AM/BR und Tagesdetails. Microsoft Graph fügt optional weitere Termine hinzu, darf aber die lokale Anzeige nicht blockieren.

Die Einstellungen sind nach Allgemein, Aufgaben & Kalender, Microsoft 365, Ticketsystem, Konto & Sicherheit und Administration gegliedert. Hell-/Dunkelmodus und mobile Navigation bleiben erhalten.

Details und offener Funktionsumfang: [Classic-UI-Referenz](docs/CLASSIC_UI.md).

## Plenaro Classic 2.0 – Heute und Kalender

Der **Heute-Arbeitsbereich** orientiert sich an der Desktop-Ansicht (siehe Benutzer-Screenshots):
links Schnellaufgabe, Tagesmarker Normal/UL/AM/HO/BR und die Reiter Heute/Aktuelle/Neue/Erledigte; rechts ein festes Detailpanel sowie reservierte Bereiche für Timer und Segmente. Die Aktionen der Aufgabenliste und der Tagesmarker verwenden echte persistente APIs. Die Timer-/Segmentbereiche sind als **noch nicht fertig** gekennzeichnet; es werden keine falschen Buchungen erzeugt. Neue Aufgaben aus Znuny sind bis zur Adapteranbindung leer. Die Wochenansicht zeigt standardmäßig **Montag bis Freitag** mit einem 06–18-Uhr-Raster und läuft völlig ohne Entra.

## Microsoft Entra direkt im Admin-Bereich

**Einstellungen → Microsoft 365** enthält für Administratoren ein Formular für Single-Tenant-Tenant-ID, Client-ID und das Client-Secret. Plenaro speichert diese Werte **AES-256-GCM-verschlüsselt** als einen Datensatz im bestehenden Secret-Vault. Keine `PLENARO_ENTRA_*`-Umgebungsvariablen mehr nötig. Admin kann ohne Containerneustart aktivieren, rotieren oder deaktivieren.

Die unabhängige, auf dem Docker-Host geschützte **Vault-Masterkey-Datei** ist weiterhin erforderlich (sie kann nicht in derselben Datenbank liegen, sonst wäre die Verschlüsselung wirkungslos). HTTPS bleibt Voraussetzung. **Hinweis:** `POSTGRES_PASSWORD` ist im aktuellen Compose weiterhin eine eigene Umgebungsvariable – die Abschaffung der Klartext-Datenbankcredentials ist ein separater Härtungspunkt.

Bei einem **bereits mit Plenaro verknüpften** Microsoft-Benutzer fragt die Entra-Anmeldung zusätzlich nach den delegierten Graph-Scopes `Calendars.Read`, `User.Read` und `offline_access`. Nach erfolgreicher Einwilligung wird der Refresh-Token **pro lokalem Benutzer verschlüsselt** abgelegt, und dessen Outlook-Kalender erscheint in der Wochenansicht. Beim **erstmaligen Verknüpfen** bleibt aus Sicherheitsgründen die ausdrückliche Bestätigung notwendig; danach kann Outlook einmalig über „Outlook verbinden“ aktiviert werden, oder bei der nächsten Entra-Anmeldung automatisch. Ohne Zustimmung bleiben lokale Kalenderdaten immer nutzbar.

**Nicht gegen einen echten Tenant abgenommen.** Siehe [Einrichtung](docs/ENTRA_SSO.md).

## Aufgaben-Timer, manuelle Buchung und Zeitsegmente (Phase 2)

Im **Heute-Arbeitsbereich rechts** kann für jede ausgewählte Aufgabe ein persistenter Timer gestartet und gestoppt werden. Der Timer wird in PostgreSQL gespeichert und läuft auch beim Schließen des Browser-Tabs weiter. **Nur ein Timer pro Benutzer gleichzeitig**, doppelte Starts werden serverseitig abgelehnt. Abgeschlossene und abgebrochene Aufgaben können keinen neuen Timer starten; ein bereits laufender Timer kann weiterhin gestoppt werden.

Mit den Schaltflächen **+15/+30/+60 und −15/−30/−60 Minuten** oder über die freie Korrektureingabe mit optionaler Notiz werden separate, nachvollziehbare Korrekturbuchungen gespeichert. Der Anzeigewert wird niemals negativ; die Korrekturen selbst werden nicht verschleiert. Das ist **aufgabenbezogene Zeit**, keine Kommen-/Gehen-, Pausen- oder Sollzeiterfassung. Vorhandene ältere `time_entries` bleiben erhalten.

Unter **Planung (Segmente)** lassen sich je Aufgabe Uhrzeit, Datum, Kurzbeschreibung und bis zu 20 Teilnehmeradressen speichern, bearbeiten und löschen. Die lokale **Mo–Fr-Wochenplanung** zeigt die Segmente im entsprechenden Stundenraster sowie in den Tagesdetails, auch ohne Entra ID. **Noch keine Outlook-Termineinladung, Graph-Schreibzugriffe, Konflikterkennung oder Drag-and-drop**; Teilnehmeradressen sind zunächst nur Planungshinweise.

Alle Endpunkte erfordern Anmeldung, Benutzer-Eigentum und bei Mutationen CSRF. Datenbankmigration `0011_task_timer_segments.sql` ergänzt ein Korrekturjournal, Segmentteilnehmer und einen eindeutigen Index für einen laufenden Aufgabentimer pro Benutzer. Keine bestehenden Tabellen werden gelöscht.

Bei einem Update:
```bash
git pull --ff-only
docker compose up -d --build --wait
```

## Monatsberichte und CSV-Export (Phase 5)

Unter **Berichte** (`/reports`) kannst du einen Monat auswählen, die Summe deiner eigenen
aufgabenbezogenen Timer-Sitzungen, manuellen Korrekturen, Aufgabenanzahl und Einzeldetails
einsehen und per **CSV exportieren**. Dieser Bericht beschreibt Aufgabenaktivitäten,
**keine** An- oder Abwesenheit, Pausen oder vertragliche Sollzeit.

- `GET /api/v1/reports/month?month=2026-10` – persönlicher JSON-Monatsbericht (Login erforderlich).
- `GET /api/v1/reports/month.csv?month=2026-10` – persönlicher CSV-Download (Semikolon, UTF-8 mit BOM).
- Kalendermonate werden in `Europe/Berlin` ausgewertet. Über Monatsgrenzen reichende
  Timer werden aufgeteilt, aktive Timer bis zum Abrufzeitpunkt gezählt und manuelle
  Korrekturen dem Kalendermonat ihrer Erfassung zugeordnet.
- Negative Zeit nach Korrektur wird pro Aufgabe auf null begrenzt. Gelöschte Aufgaben
  sind mangels Aufgabenmetadaten keine Tätigkeitsposition im Monatsbericht.
- Aufgabennamen werden beim CSV-Export gegen Spreadsheet-Formel-Injection entschärft.
  Ohne Anmeldung oder für einen anderen Benutzer gibt es keinen Zugriff.
- Ohne Datenbankänderung: Die vorhandenen Tabellen `time_entries` und
  `task_time_adjustments` werden ausgelesen. CI- und Live-Host-Abnahme im PR prüfen.

Die Benutzeroberfläche und Export-API sind in [PR #25](https://github.com/DerHausmeisterKrause/plenaro/pull/25) implementiert.

## Zeitsegmente im Wochenkalender verschieben (Phase 5)

Die lokale **Wochenplanung** zeigt Zeitsegmente deiner eigenen Aufgaben. Ziehe ein Segment
am Griff auf einen anderen Wochentag bzw. in ein Stundenfeld (Mo–Fr, 06–18 Uhr).
Die obere Hälfte eines Stundenfelds steht für `:00`, die untere für `:30`.
Die Dauer des Segments bleibt beim Verschieben vollständig erhalten.

Alternativ Segment anklicken, rechts unter **Zeitsegmente → Zeitsegment verschieben**
den Wochentag und die Startzeit wählen und speichern. Diese Alternative funktioniert
auch ohne Drag-and-drop, etwa auf einem Touchgerät oder mit Tastatur.

Plenaro speichert nur die neuen Zeitgrenzen (gesonderte `PATCH`-API);
Notizen und Teilnehmer bleiben unverändert. Bei einem inzwischen anders
geänderten Termin erscheint ein Konflikthinweis statt eines stillen Überschreibens.
Kein Outlook-/Teams-Schreibzugriff oder Versand von Einladungen.

## Lokale Verfügbarkeitsprüfung beim Planen (Phase 5)

- Eigene Zeitsegmente werden bei Erstellung, Bearbeitung und Verschiebung serverseitig
  auf zeitliche Überschneidungen geprüft. Ein direkter Anschluss (Ende = nächster Beginn)
  bleibt zulässig.
- Das Segmentformular zeigt eine lokale Verfügbarkeitsvorschau. Über
  `GET /api/v1/segments/availability?start=...&end=...&excludeId=...` können nur
  angemeldete Benutzer **ihre eigenen** Konflikte abfragen. `excludeId` darf nur
  auf ein eigenes Segment zeigen.
- POST, PUT und PATCH verweigern lokale Doppelbuchungen mit HTTP 409 und laufen
  unter einer nutzergebundenen Transaktionssperre gegen parallele Änderungen.
  Ein alter, bereits geänderter Segmentstand wird weiterhin mit HTTP 409 abgewiesen.
- Ein optional verbundener Outlook-Kalender kann in der Wochenansicht als **Hinweis**
  auf eigene belegte Termine dienen. Diese Lesedaten können unvollständig sein und
  werden weder gespeichert noch als verbindliche Freigabe interpretiert.
- Noch keine **Teilnehmerverfügbarkeit**, keine Einladungen oder Teams-Termine:
  Microsoft-Graph-Schreibrechte, Consent und Live-Abnahme folgen separat.
