## 26.11.2026 – Phase 7/7: Kompakte Ticketfilter und einheitliche Browser-Kopfzeilen

- [x] WPF-Referenz `DerHausmeisterKrause/project_tool/Views/TodayView.xaml` (Suche/Sortierung) und bestehende Web-Komponenten geprüft; kein Backendverhalten geändert.
- [x] Arbeitsplatz-Reiter **Aktuelle Aufgaben**, **Neue Aufgaben** und **Erledigte Aufgaben**: Volltextsuche und „Filter und Sortierung“ stehen in derselben flexiblen Zeile auf gleicher Höhe. Suchfeld nimmt die verfügbare Breite; die Sortieroptionen klappen darunter auf. Sortieren ist damit auch bei erledigten Tickets erreichbar. Auf sehr schmalen Viewports kann die Zeile umbrechen, ohne Buttons abzuschneiden.
- [x] Auch in der eigenständigen `TicketsWorkspacePage` Suche und Filter auf eine Zeile gebracht, einschließlich der Ansicht „Erledigte Aufgaben“.
- [x] Die eingebetteten **Ticketsystem-** und **Wissensdatenbank**-Seiten verwenden die gleiche flache, dezente Browser-Kopfzeile wie die Favoriten (kleines Symbol, Name, knappe Zusatzinfo rechts/ daneben). Alle bisherigen „Neu verbinden“- und Wiki-Zugangsdatenaktionen bleiben verfügbar; iframe-Sitzungen, Proxys und Sicherheitsgrenzen sind unverändert.
- [x] „Favoriten verwalten“-Schaltfläche **nur aus dem Favoriten-Browserkopf** entfernt, weil die Verwaltung bereits über Einstellungen/Navigationsleiste erfolgt. Persönliche Favoritenverwaltung selbst bleibt unverändert.
- [x] Automatisierte UI-Regressionstests für Such-/Sortierzeile in drei Reitern, Wissensdatenbank-/Znuny-Portal-Kopf sowie fehlenden „Favoriten verwalten“-Button.
- [x] [PR #67](https://github.com/DerHausmeisterKrause/plenaro/pull/67) erfolgreich nach `main` gemergt (Squash-Commit `d855605ad782df462b44c4586f52e0c8351d6475`); [GitHub-CI #45770100589](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/45770100589) vollständig erfolgreich (Frontend-/Backend-Build, UI-Regressionstests, Znuny-Vertragstests, TLS, Docker-Smoke und Favoriten-Browser-Login).
- [ ] Produktive Sichtprüfung: Such-/Filterzeile in allen Ticketreitern und kompaktes Kopf-Layout in Favoriten, Ticketsystem und Wiki.
- [x] Keine Datenbankmigration, keine zusätzlichen Container, Ports, Domains oder Nginx-Änderungen.

### Deployment nach grüner CI und Merge

```bash
cd /opt/plenaro
git switch main
git pull --ff-only origin main
docker compose up -d --build --no-deps web
docker compose restart nginx
docker compose ps
```

---

## 26.11.2026 – Phase 7/7: Kompakter Arbeitstag, KW, Scrollweitergabe, „Heute“ im Kalender

- [x] Alte WPF-Tages-/Wochenansicht `project_tool/Views/TodayView.xaml` / `WeekView.xaml` und aktuelle React-Komponenten als Referenz geprüft.
- [x] „Arbeitstag heute“-Karte vereinfacht: Überschrift links (16 px wie bisher), Datum rechts (11 px wie bisher) ergänzt um **ISO-8601-Kalenderwoche**; bei Jahreswechseln (KW 52/53/1) UTC-stabile Berechnung.
- [x] Doppelte Normal/UL/AM/HO/BR-Schaltflächen, Planungs-Hinweis und optionalen Homeoffice-Nachtrags-Button aus der Arbeitsplatz-Karte entfernt; Tagesmarker inklusive Bestätigung, Mail- und Outlook-Prüfung bleiben in der Wochenplanung direkt rechts bearbeitbar. Die Arbeitsplatzseite lädt hierfür keine Marker mehr.
- [x] In der Aufgabenlistenkarte `overscroll-behavior-y: contain` zu `auto` geändert: Ist die Liste kurz/ohne eigenen Scrollbalken oder am Rand, kann das Mausrad wieder die äußere Seite scrollen; bei vielen Tickets scrollt die Liste weiterhin intern.
- [x] Im Wochenkalender den weißen Punkt/Marker auf der aktuellen-Zeit-Linie entfernt, die weiße Linie selbst und ihre Zeitaktualisierung beibehalten.
- [x] Heutigen Kalendertag in der Kopfzeile zusätzlich zur bestehenden Farbmarkierung deutlich mit „Heute“ gekennzeichnet (nur innerhalb der angezeigten Woche).
- [x] Regressionstests für Kalenderwochen (inkl. Jahresübergang), kompakten Arbeitsplatz, Scrollweitergabe per CSS-Vertrag und „Heute“-Kennzeichnung ohne Zeitlinienpunkt.
- [x] [PR #66](https://github.com/DerHausmeisterKrause/plenaro/pull/66) erfolgreich nach `main` gemergt (Squash-Commit `17a1a714686aa5605395fe607bcda95eecbdf812`); [GitHub-CI #45767928503](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/45767928503) vollständig grün (Frontend-/Backend-Build, UI-Regressionstests, Znuny-Vertragstests, TLS, Docker-Smoke und Favoriten-Browser-Login).
- [ ] Live-Abnahme: Arbeitstagskarte, Maus-Scrollen über kurzen Listen, „Heute“-Badge und Weiße-Linie-Optik kontrollieren.
- [x] Keine Backend-/Datenbankänderung, neue Container, DNS-/Nginx-Einstellungen oder Compose-Profile.

### Server-Deployment nach Merge

```bash
cd /opt/plenaro
git switch main
git pull --ff-only origin main
docker compose up -d --build --no-deps web
docker compose restart nginx
docker compose ps
```

---

## 26.11.2026 – Phase 7/7: Listen-Scrollposition, Inline-Tagesmarker und weiße Jetzt-Linie

- [x] Desktop-Referenz `DerHausmeisterKrause/project_tool/Views/WeekView.xaml` geprüft: `NowIndicatorCanvas` zeigt eine 2-px-weiße Linie über dem Zeitraster und einen weißen Marker auf dem aktuellen Tag; `DayMarkerSegment` bietet direkte Buttons für Normal/UL/AM sowie HO/BR. `TodayView.xaml` verwendet ScrollViewer für die Liste.
- [x] Znuny-Ticketlisten in `ClassicTodayPage` bleiben während regelmäßiger oder manueller Aktualisierungen im DOM; statt Ausblenden der bestehenden Liste zeigt Plenaro eine kleine „Aktualisiere …“-Meldung. Gespeicherter `scrollTop` wird nach neuen Daten wiederhergestellt, bei bewusstem Tabwechsel zurückgesetzt.
- [x] Lokale Aufgaben in `ClassicTasksPage` behalten ihren Scrolloffset auch nach Aktualisierung, Speichern und Statuswechsel. Der Wochenkalender bewahrt `scrollTop` und `scrollLeft` bei Daten-Neuladen.
- [x] In der Wochenplanung den separaten, großflächigen aufklappbaren Tagesmarkereditor samt zusätzlichem Aktualisieren-Button entfernt. Rechts im Tagesdetail stehen direkte, statusanzeigende Buttons für Normal/UL/AM und HO/BR; Änderungen werden für den ausgewählten Tag sofort gespeichert und ohne Reload im Kalenderkopf angezeigt.
- [x] Bestehende HO-/BR-Sicherheitsabläufe beibehalten: neue HO-/BR-Anträge erfordern explizite Bestätigung; vor Speicherung erfolgt Preflight für Outlook-/Mail-Voraussetzungen; neue HO-Anträge prüfen/erstellen den ganztägigen Outlook-Termin und lösen die Mail aus; BR ohne HO versendet nur E-Mail. Bestehende HO-Termine lassen sich weiterhin nachtragen/prüfen. Keine doppelten Sendungen bei vorhandenen akzeptierten Mailstatus.
- [x] WPF-ähnliche weiße 2-px-Jetzt-Linie mit Tag-Punkt durch das 06–18-Uhr-Zeitraster integriert; nur in der angezeigten aktuellen Arbeitswoche sichtbar, Position nach lokaler Browserzeit, Aktualisierung ohne Kalender-Reload alle 30 Sekunden, ohne Einfluss auf Drag-and-drop.
- [x] Frontend-Regressionstests für Inline-Marker, HO-Bestätigung und API-Aufrufreihenfolge, Scrollpositionen von Ticketliste/lokalen Aufgaben/Wochenkalender sowie reine Jetzt-Linien-Zeitberechnung.
- [x] [PR #65](https://github.com/DerHausmeisterKrause/plenaro/pull/65) mit Squash-Commit `fa5c96b7a3a50b4711b9432bcb33b56e591d0d2b` in `main` gemergt; [GitHub-CI #45766414210](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/45766414210) vollständig erfolgreich (Frontend-/Backend-Build, UI- und Kalender-Regressionstests, Znuny-Vertragstests, TLS, Docker-Smoke, Favoriten-Browser-Login).
- [ ] Live-Sichtprüfung: Scrollposition bei tatsächlichem automatischen Sync, neue Marker im rechten Tagespanel und weiße Jetzt-Linie auf der produktiven Wochenansicht.
- [x] Keine Datenbankmigration, keine neuen Container, Ports, DNS-Namen oder Browserdienste.

### Deployment nach grüner CI/Merge

```bash
cd /opt/plenaro
git switch main
git pull --ff-only origin main
docker compose up -d --build --no-deps web
docker compose restart nginx
docker compose ps
```

---

# Web-Oberfläche nach Plenaro Classic – Referenzkonzept

Quellen: [Classic / project_tool](https://github.com/DerHausmeisterKrause/project_tool), vor allem `Views/TodayView.xaml`, `Views/WeekView.xaml`, `Views/TaskDetailPanel.xaml` und `Views/SettingsView.xaml`. Zusätzlich der explizite Nutzerwunsch: Ticketliste links, ausgewählter Datensatz rechts; neues React-Theme mit Hell-/Dunkelmodus erhalten.

## Navigation und Zuständigkeiten

| Bereich | Zugeordnete Funktionen | Zustand |
| --- | --- | --- |
| **Arbeitsplatz** (`/workspace`) | Heute-Agenda, Aktuelle/Neue/Erledigte Znuny-Tickets, lokale Aufgaben in einem Unterreiter, WPF-Ticketdetails rechts | Implementiert, Live-Abnahme offen |
| Wochenplanung | Lokaler Mo–Fr-Kalender, Segmente, UL/AM/BR/HO, Outlook optional | Implementiert, Graph-Live-Abnahme offen |
| Berichte | Monatsreports und CSV-Export | Implementiert |
| Wissensdatenbank und KI | Wissen, Dokumente, Assistent | Offen |
| Einstellungen | Allgemein / Aufgaben & Kalender / Microsoft 365 / Ticketsystem / Konto & Sicherheit / Administration | Implementiert |

Die separaten Navigationseinträge **Aufgaben** und **Ticketsystem** entfallen.
Bestehende Links `/today`, `/tasks` und `/tickets` werden nach `/workspace`
umgeleitet. Ticketdaten und lokale Aufgaben werden weiterhin separat
persistiert und durch die bestehende Benutzerisolation geschützt.

## Kalender ohne Microsoft

Der **lokale Wochenkalender ist immer verfügbar**. Er lädt die eigenen geplanten Tasks mit `plannedFor` und UL-/AM-/BR-Markierungen direkt aus PostgreSQL. 06–18 Uhr ist ein sichtbares Stundenraster als Grundlage für spätere Zeitsegmente. Aufgaben ohne Uhrzeit stehen in der Tageskopfzeile / den Tagesdetails, nicht fiktiv um 08:00. Alle Termine / Uhrzeiten werden in der Browser-Zeitzone dargestellt.

Ist ein Outlook-Konto verbunden, kommen Microsoft-Graph-Termine als **zusätzliche** Quelle dazu. Ohne Entra, bei ausgelaufener Zustimmung oder bei Graph-Ausfall bleibt das lokale Raster mitsamt lokaler Aufgaben und Marker benutzbar. Bestehende Graph-Leseberechtigung bleibt unverändert. Die Kalenderintegration ist noch nicht gegen einen echten Microsoft-Tenant abgenommen.

Die Tagesmarker werden nach Auswahl eines Kalendertags **direkt rechts im Tagesdetail** per Normal/UL/AM/HO/BR-Schaltflächen gesetzt und sofort gespeichert. Im Arbeitsplatz zeigt die „Arbeitstag heute“-Karte nur die Überschrift sowie Datum inklusive ISO-Kalenderwoche (rechts), keine doppelten Markerbuttons. Neue HO-/BR-Anträge benötigen weiterhin eine Bestätigung sowie die bestehenden Outlook-/Mail-Sicherheitsprüfungen. Individuelle Kontingente stehen unter **Einstellungen → Aufgaben & Kalender**. Die aktuelle Uhrzeit ist im 06–18-Uhr-Kalender als weiße Linie mit Tagespunkt sichtbar – wie im alten WPF-Plenaro.

## Ticketliste und Details

Beim klassischen TaskTool ist `TicketSystemView.xaml` derzeit ein eingebetteter WebView2-Browser. Der Nutzer wünscht für die neue Weboberfläche **ausdrücklich** eine native Liste/Details-Zweiteilung; dieses Bedienmuster hat deshalb Vorrang. Der Ticketcache, WPF-ähnliche Details und erste Live-Ticketaktionen sind implementiert. Ausstehend sind vollständige Znuny-Live-Abnahme, Remote-Zeitbuchungen und weitere Adapter. Neue Kandidaten-Tickets sind nur mit administrativ zugewiesener Agenten- und Kandidaten-Benutzer-ID sichtbar.

## Anzeige und Erhalt bestehender Funktionen

- Die neue UI nutzt ausschließlich vorhandene CSS-Variablen für Farben, Hintergründe, Kontraste und Rahmen: bestehender Hell-/Dunkelmodus funktioniert weiterhin.
- Bei schmalen Viewports wird die Liste über dem Detailbereich angeordnet; der Wochenkalender scrollt horizontal, statt Stunden/Spalten wegzulassen.
- Nur tatsächlich vorhandene APIs werden aufgerufen. Kein automatisches Anlegen von Demodaten, keine Änderung an Login, Rollen, Secret-Vault oder NGINX.
- Das aufgabenbezogene Planen von Zeitsegmenten und der SQLite-Import bleiben separate offene Aufgaben.
