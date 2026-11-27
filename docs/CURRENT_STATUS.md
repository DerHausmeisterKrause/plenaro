## 26.11.2026 – Phase 7/7: Favoriten-Öffnungsmodus und Outlook-Segmentplanung

- [x] Persönlicher Öffnungsmodus pro Favorit (Proxy als Standard, direkt eingebetteter Browser, eigener Tab), Wechsel in Einstellungen und Favoritenleiste. Keine Domain-Sonderfälle oder neuen Container.
- [x] Bestehende Proxy-Engine und Schutzprüfungen beibehalten; lokale Modi verwenden keine gespeicherten Zugangsdaten. Direkte Frames respektieren Sandbox und Sicherheitsvorgaben der Zielseite.
- [x] Classic-Referenz TaskService.SyncSegmentOutlook/DeleteSegmentOutlook geprüft. Eigene Aufgaben- und Znuny-Segmente werden beim Anlegen, Ändern, Verschieben und Löschen über delegiertes Calendars.ReadWrite mit Outlook synchronisiert.
- [x] Dauerhafte, transaktionale Auftragswarteschlange, eindeutige Outlook-Identität, sichtbarer Status und explizites Nachtragen bestehender Segmente. Kein erneuter POST nach unklarem Erstellungsversuch.
- [x] Wochenplanung enthält eigene Znuny-Segmente; Deduplizierung anhand der Outlook-ID und Berücksichtigung eigener Blocker beim Bearbeiten.
- [ ] CI und produktiver Outlook-Test, ggf. Microsoft 365 erneut verbinden. Kein automatisches Massennachtragen historischer Segmente oder Versand alter Einladungen.

---

## 26.11.2026 – Phase 7/7: Einheitliche Favoriten und HTTPS-Adresskompatibilität

- [x] SharePoint, OneDrive und Excel wieder wie normale Favoriten über den bestehenden Proxy öffnen; Originalseite weiterhin manuell verfügbar.
- [x] URL-Fragmente (SPA-Hashrouten) und zulässige HTTPS-IP-Adressen (IPv4/IPv6, einschließlich LAN) unterstützen. Hash bleibt bei Bootstrap/Weiterleitung erhalten.
- [x] DNS-, TLS-, TCP-, Timeout- und HTTP-403-Fehler verständlich unterscheiden; tatsächliche Firewall-Sperren können nicht allein aus einem Timeout bewiesen werden.
- [x] HTTPS, Zertifikatsprüfung, tatsächliche TCP-Peer-Prüfung, reservierte Ziele, Same-Origin- und CSRF-Schutz bleiben bestehen. Keine zusätzlichen DNS-Aliase, Container oder Firewall-Umgehung.
- [x] Classic-WebShortcut-Referenz und bestehende Proxy-Routen geprüft; API-/URL-/UI-Regressionstests ergänzt.
- [ ] CI und produktiver Test; Microsoft-SSO und Cross-Origin-Weiterleitungen sind nicht durch eine Firewall-Freigabe allein lösbar.

---

## 26.11.2026 – Phase 7/7: Persistenter Web-Arbeitsbereich

- [x] Classic-Referenz `Services/WebShortcutBrowserSessionManager.cs` geprüft: stabile WebView-Instanzen statt Neuaufbau beim Reiterwechsel.
- [x] Favoriten, Wiki und native Ticketsystem-Weboberfläche nach Erstaufruf als stabile, benutzerspezifische Iframes im DOM behalten; nur aktive Ansicht sichtbar. Maximal acht Favoriten plus Wiki und Ticketsystem; ältester Favorit wird bei Überschreitung entfernt.
- [x] Normales Öffnen des Ticketsystem-Reiters setzt die zuletzt sichtbare Seite fort; explizite Ticketlinks navigieren weiterhin zum gewünschten Ticket. Kein Wiederverwenden abgelaufener Bootstrap-Tokens.
- [x] Plenaro-Sitzung beim Wiederaufruf prüfen, bei Logout/Benutzerwechsel oder Öffnen der Einstellungen alle Frames verwerfen. Proxy-Sicherheitsgrenzen und Session-Timeouts unverändert. Keine Hintergrundanmeldung, keine persistent gespeicherten Seiteninhalte.
- [x] Diagnose-Polling in inaktiven Favoriten pausiert. Native CGI-AJAX-/Asset-MIME-/Antwortzeit-Diagnose ohne URLs oder Inhalte ergänzt.
- [x] React-Regressionen und Chromium-Test für Zustandserhalt ergänzt; keine neuen Betriebscontainer.
- [ ] CI und Live-Abnahme. Tatsächlicher OTRS-Loading-Dropdown-Fehler bleibt ohne reale Browser-/AJAX-Diagnose unbestätigt.

---

## 26.11.2026 – Phase 7/7: Dauerhafter Ticketansicht-Umschalter

- [x] Vorhandene breite/normale Ticketdetailansicht als persönliche Browser-Einstellung je Benutzer speichern; kein Reset beim Tabwechsel.
- [x] Gewählte Ansicht gilt für alle Tickets im Arbeitsplatz (Heute, Aktuelle, Neue und Erledigte Aufgaben), auch nach Neuladen und erneutem Öffnen.
- [x] Outlook-Termine/Leerzustand, separate bereits breite Ticketansicht und responsive mobile Darstellung unverändert.
- [x] Regressionstests für Ticket-/Tabwechsel und Neuladen in beiden Modi ergänzt; keine Datenbankmigration.
- [ ] CI und produktive Sichtprüfung.

---

## 26.11.2026 – Phase 7/7: Kopierbare direkte Ticket-URL

- [x] WPF-Referenz `Services/TicketSystemService.cs` geprüft: direkter AgentTicketZoom-Link mit interner Ticket-ID.
- [x] Kanonische HTTPS-URL aus konfigurierter Znuny-Webadresse berechnet; konfigurierte CGI-Pfade (/otrs, /znuny) bleiben bewusst erhalten. Keine Zugangsdaten oder Bootstrap-Tokens.
- [x] Ticket-URL als schreibgeschütztes, markierbares Textfeld; automatische Auswahl bei Fokus. Geschützter „Ticket öffnen“-Reiter unverändert.
- [x] Historische lokale URL bleibt in der Datenbank erhalten; generierte URL wird nicht gespeichert und verändert keine Zugriffsrechte.
- [x] UI- und URL-Vertragstests ergänzt; keine Migration.
- [ ] CI und produktive Sichtprüfung.

---

## 26.11.2026 – Phase 7/7: Kontrolliertes Stack-Update

- [x] Ein Befehl für GitHub-Pull, aktuelle Docker-Images und vollständigen Neubau aktiver Build-Dienste.
- [x] Bestehende optionale Profile beibehalten, PostgreSQL-Major 17 absichern, automatisches DB-Backup vor Deployment, Healthchecks, NGINX-Neustart.
- [x] Tests für Pull-/Build-/Backupfehler, Major-Upgrade-Sperre, Profile und frischen Git-Stand ergänzt.
- [x] [Update-Dokumentation](UPDATE.md); keine neuen dauerhaften Container oder Datenbankmigrationen.
- [ ] CI und produktive Abnahme; Abhängigkeits-Audit und Host-Docker-Update separat.

---

## 26.11.2026 – Phase 7/7: Bildvorschau-Korrektur

- [x] Rasterbilder direkt aus geprüften Bytes per Canvas darstellen, keine blob:-Bildnavigation; Scan und Cache unverändert.
- [x] Dekodierfehler separat mit MIME und Dateigröße anzeigen; GIF als Standbild.
- [x] UI-Regression und Chromium-Canvas-Test ergänzt.
- [ ] CI und produktive PNG-/PDF-Abnahme; tatsächliche Ursache des bisherigen Bildladefehlers ohne Browsertrace nicht eindeutig bestätigt.

---

## 26.11.2026 – Phase 7/7: Geprüfte Anhänge in der Ticketdetailansicht

- [x] PNG/JPEG/GIF/WebP/PDF als Vorschau-Dialog mit Ladeindikator; lokal gerenderte PDF-Seiten ohne interaktive Dokumentaktionen.
- [x] Lokaler ClamAV-Scanner (optionales Compose-Profil `attachment-scan`), INSTREAM nur für ausgewählte Datei, restriktiver Dateityp und blockierende Fehlerbehandlung.
- [x] Benutzerspezifischer RAM-Cache, drei Minuten Inaktivität, Keepalive bei geöffneter Vorschau, 64 MiB/32 Einträge, absolute Scan-Gültigkeit 30 Minuten.
- [x] Regressionstests und [Betriebsdokumentation](ATTACHMENT_PREVIEW.md); bestehendes 5-MiB-Dateilimit beibehalten.
- [ ] CI-Ergebnis und produktive ClamAV-/PDF-/Bild-Abnahme.
- [x] Keine Datenbankmigration oder öffentliche Portfreigabe. Ein zusätzlicher optionaler Scanner-Container; Signaturen persistent, Anhänge nicht.

---

## 26.11.2026 – Phase 7/7: Nachrichtenliste – Nummerierung

- [x] Älteste Nachricht Nr. 1, neueste oben mit höchster Nummer; Datum DD.MM.YYYY HH:mm:ss, ungelesen-Stern und Auswahl bleiben erhalten.
- [x] Regressionstest ergänzt; keine Migration oder Konfigurationsänderung.
- [ ] CI und produktive Sichtprüfung.

---

## 26.11.2026 – Phase 7/7: Nachrichtenliste und native Antwortauswahl (Nachbesserung)

- [x] WPF-Referenz `Views/TaskDetailPanel.xaml` und Znuny-7.3.8-/6.5-Artikelaktionsquellen verglichen.
- [x] Nachrichten-Dropdown in Plenaro durch eine native scrollbare Listbox mit vier Zeilen ersetzt; absteigend nach Datum, Artikel-ID als Tie-Breaker, neueste ungelesene Nachricht initial selektiert, einzelne Lesebestätigung unverändert.
- [x] Antwortformular-Erkennung über echte Action/TicketID/ArticleID statt feste DOM-IDs, auch bei text/plain- und JSON-HTML-AJAX-Antworten; HTTP-CGI-Links des konfigurierten Upstreams auf HTTPS-Proxy umgeschrieben. Sicherheitsgrenzen unverändert.
- [x] AJAX-Status und erfolgreiche Antwortformular-Anpassungen ohne URLs oder Ticketinhalte protokolliert.
- [x] Vorherigen CI-Guard-Fehler (synthetische Credential-URL) korrigiert; Regressionstests angepasst.
- [ ] CI-Ergebnis und produktiver Test. Hängende Antwortauswahl nicht abschließend diagnostiziert; bei erneutem Fehler Browser-Netzwerkstatus und Proxy-Diagnose prüfen.
- [x] Keine neuen Container, Domains oder Migrationen.

---

## 26.11.2026 – Phase 7/7: SharePoint-/Excel-Favoriten und Znuny-Antwortauswahl

- [x] Fehler `upstream-redirect` nachvollzogen: generischer Favoritenproxy blockiert fremde Origins absichtlich. Microsoft dokumentiert reguläre Cross-Origin-iFrame-Blockaden: https://learn.microsoft.com/de-de/troubleshoot/sharepoint/security/cross-domain-iframe-requests-are-blocked .
- [x] Echte SharePoint-/OneDrive-HTTPS-Dokumentlinks öffnen aus der Seitenleiste im Browser-Tab mit unveränderter Dokumentadresse und Microsoft-Anmeldung. Keine fremden Redirects, Cookies oder Passwörter werden über den Favoritenproxy weitergegeben. Andere Favoriten bleiben eingebettet.
- [x] WPF-WebView2-Referenz und Znuny 7.3.8 `AgentTicketCompose.pm`, `ArticleActionMenu.tt`, `Core.Agent.TicketZoom.js` geprüft. Antwort-/Allen-antworten-Listen werden bereits serverseitig als native Optionen erzeugt; im Proxy nur deren Modernize-Darstellung deaktiviert. Auswahlwerte und Original-Handler bleiben erhalten.
- [x] AJAX von Dokumentnavigation unterscheiden, Content-Type bei leeren POSTs erhalten, gezielte CSRF-Header und gleichen Origin-Referer weiterreichen. Fehlerdiagnose nur Status/Exception-Typ ohne URL/Anfrageinhalte.
- [x] Optionaler Button „Separat öffnen“ verwendet dieselbe geschützte native Proxy-Sitzung, ohne erneuten Bootstrap-Token oder direkte interne Adresse.
- [x] Frontend-/Backend-Regressionstests ergänzt.
- [ ] CI-Ergebnis und produktiver Test: konkrete SharePoint-Adresse, Microsoft-Anmeldung, OTRS-Antwortdropdown und echter Versand auf Testticket. Die genaue Ursache des hängenden Dropdowns kann ohne produktiven Netzwerktrace nicht abschließend bestätigt werden.
- [x] Keine Migration, keine neuen Container, Aliase oder Nginx-Änderungen.

---

## 26.11.2026 – Phase 7/7: Ticket-Benachrichtigungen in der Glocke

- [x] WPF-Referenz `Services/NotificationService.cs` und vorhandene Web-Stern-/Reminder-Logik geprüft.
- [x] Glocke zählt jedes sichtbare Ticket mit ungelesenen Artikeln genau einmal, zusätzlich zu bestehenden Aufgabenerinnerungen; keine neue Lesezustandsdatenbank.
- [x] Autorisierte Ungelesen-Liste ohne Seitenlimit, direkter Link zum nativen Ticketdetail und sofortiger Neuabruf nach erfolgreicher Artikelquittierung; andere ungelesene Artikel bleiben bestehen.
- [x] Ja/Nein-Dialog beim ersten Öffnen einer Browsersitzung, wenn Browserhinweise nicht aktiv sind. Browserberechtigung erst nach Ja; Nein gilt für die Sitzung. Native Browserhinweise und kurzer Ton, soweit Browser/OS es zulassen.
- [x] UI-Regressionstests für Zähler, Direktlink, einzelne Artikelquittierung und Berechtigungsdialog ergänzt.
- [ ] CI-Ergebnis und produktive Abnahme (zwei Benutzer, neue Nachrichten, Browserfreigabe und Ton bei offenem Plenaro).
- [x] Keine Datenbankmigration, keine zusätzlichen Container oder Nginx-Konfigurationsänderungen.

Deployment nach Merge:

```bash
cd /opt/plenaro
git switch main
git pull --ff-only origin main
docker compose up -d --build --no-deps web
docker compose restart nginx
docker compose ps
```

---

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

## 26.11.2026 – Phase 7/7: Aufgabenliste an dynamische Kartenhöhe anpassen

- [x] Arbeitsplatz-/Ticket-Split im aktuellen Web-Frontend und WPF-Referenz `DerHausmeisterKrause/project_tool/Views/TodayView.xaml` auf Listenscrollen geprüft. WPF nutzt einen eigenen `ScrollViewer` für die Agenda; im Web-Layout wuchs die Karte mit den rechten Ticketdetails, während `.plenaro-rows` bei `66vh` endete.
- [x] Desktop: `.plenaro-today-list > .plenaro-rows` füllt per Flex-Growth und nullbasierter flexibler Höhe den gesamten verfügbaren Bereich **innerhalb** der Aufgaben-Karte. Das künstliche `66vh`-Limit entfällt für den Desktop; Listeninhalt scrollt in der Karte statt das Zwei-Spalten-Layout auf die gesamte Listenlänge aufzublähen.
- [x] Änderungen an der rechten Ticketdetailansicht oder beim Auf-/Zuklappen werden durch CSS-Grid/Flex automatisch berücksichtigt, ohne ResizeObserver, Polling oder DOM-Messungen.
- [x] Mobil bis 1050 px bleibt das bisherige einspaltige Layout mit `max-height:66vh` für die Liste; Pagination, Tagesmarker, Filter und Ticket-Auswahl bleiben unverändert.
- [x] CSS-Vertragstest `frontend/src/workspaceListLayout.test.ts` überprüft Flex-Füllung, Scrollbarkeit, Entfernung des Desktop-Limits und mobile Wiederherstellung.
- [x] [PR #64](https://github.com/DerHausmeisterKrause/plenaro/pull/64) nach `main` gemergt (Squash-Commit `28868e082c215d75d462b9483fa48d5848f2b332`); [GitHub-CI #45764732435](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/45764732435) vollständig grün (Frontend-/Backend-Build, CSS-Regressionstests, Znuny-Vertragstests, TLS, Docker-Smoke und Favoriten-Browser-Login).
- [ ] Produktive Sichtprüfung: Liste reicht bis zum unteren Kartenrand, passt sich geöffneten langen Tickets und Expand-/Restore-Modus an, unabhängig von der Anzahl der Einträge.
- [x] Keine Backend-/Datenbankänderung, zusätzliche Container, DNS-Namen oder Compose-Anpassungen.

### Produktiv-Deployment nach Merge

```bash
cd /opt/plenaro
git switch main
git pull --ff-only origin main
docker compose up -d --build --no-deps web
docker compose restart nginx
docker compose ps
```

---

## 26.11.2026 – Phase 7/7: Kompakte Favoritenansicht, Diagnose nur bei Admin-Opt-in

- [x] Vorhandene Favoritenansicht und Admin-Diagnose geprüft; Referenz WPF `DerHausmeisterKrause/project_tool/Services/WebShortcutBrowserSessionManager.cs` konsultiert (eigenständige Browseransicht ohne dauerhafte Debugkonsole).
- [x] `FavoriteDebugConsole` rendert **überhaupt kein Element**, solange die serverseitige persönliche Admin-Einstellung nicht ausdrücklich `enabled: true` meldet. Kein „Debugmodus deaktiviert“-Platzhalter oder Lade-Kasten; reguläre Benutzer bleiben wie zuvor vollständig ausgeschlossen.
- [x] Bei aktiviertem Admin-Debug bleiben Ereignisanzeige, Aktualisieren, Filter, Leeren und serverseitige Zugriffskontrolle unverändert. Kein Eingriff in Proxy-Sitzungen, gespeicherte Anmeldedaten oder Debug-Berechtigungen.
- [x] Mehrzeiligen „Plenaro / Favoriten“-Kopf samt redundantem Beschreibungstext durch eine dezente einzeilige Favoritenleiste ersetzt. Name, „Neu verbinden“, „Originalseite öffnen“ und „Favoriten verwalten“ bleiben vorhanden.
- [x] Eng auf `.favorite-browser-page` begrenzte Styles: geringere Höhe, kompaktere Buttons, responsive Umbrüche. Größerer sichtbarer iframe-Bereich ohne Änderung seiner Sicherheitsattribute.
- [x] Regressionstests für Administrator mit deaktivierter Diagnose, Administrator mit aktiver Diagnose, Nichtadministrator und kompakte Kopfleiste samt Aktionen.
- [x] [PR #63](https://github.com/DerHausmeisterKrause/plenaro/pull/63) nach `main` gemergt (Squash-Commit `481d933564ff169ace4c27588e6991fd4050f458`); [GitHub-CI #45763444813](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/45763444813) erfolgreich: Build, Frontend-Regressionstests, Znuny-Vertragstests, TLS, Docker-Smoke und Favoriten-Browser-Login.
- [ ] Live-Abnahme im Browser: ohne Admin-Debug keine Karte, nach Aktivierung Konsole sichtbar, Höhe der Webanwendung und mobile Breite prüfen.
- [x] Keine neuen Dockercontainer, DNS-Einträge, Hosts, Ports, Migrationen oder Compose-Profile.

### Deployment nach Merge

```bash
cd /opt/plenaro
git switch main
git pull --ff-only origin main
docker compose up -d --build --no-deps web
docker compose restart nginx
docker compose ps
```

---

## 26.11.2026 – Phase 7/7: Arbeitsplatz-Hinweis entfernen und Ticketdetails aufklappen

- [x] Bestehende Web-Ansicht `frontend/src/ClassicTodayPage.tsx` und `ClassicTicketDetailPanel.tsx` mit dem Desktop-Vorgänger `DerHausmeisterKrause/project_tool/Views/TodayView.xaml` und dem dokumentierten klassischen Zwei-Spalten-Konzept verglichen.
- [x] Überflüssigen Hinweis „Hier bearbeitest du deine Znuny-Tickets und Termine. Lokale Aufgaben findest du direkt im Reiter „Lokale Aufgaben“.“ aus dem JSX vollständig entfernt, inklusive ungenutzter Schnellbereich-CSS-Regeln.
- [x] Arbeitsplatz-Tabs und ersten Inhaltsblock mit einheitlichem Abstand (12 px) ausgerichtet; vorhandene Tagesmarker und Aufgabenliste bleiben unverändert.
- [x] Im Header eines **geöffneten** Znuny-Tickets einen beschrifteten Expand-/Restore-Schalter hinzugefügt (Maximize/Minimize, `aria-label`, `aria-controls`, `aria-pressed`, Tastaturfokus).
- [x] Breite umschaltbar: normale Aufteilung ca. 61 % Aufgabenliste / 39 % Ticketdetails, aufgeklappt ca. 25 % Aufgabenliste / 75 % Ticketdetails. Kein Verschieben oder Löschen von Ticketdaten.
- [x] Bei Wechsel des Arbeitsplatz-Tabs normalisiert sich die Breite wieder; ohne ausgewähltes Ticket existiert kein Schalter. Unter 1050 px bleibt das bestehende gestapelte Ein-Spalten-Layout und der dort wirkungslose Schalter wird ausgeblendet.
- [x] Frontend-Regressionstest prüft entfernten Text, Schalter nur bei Ticket-Auswahl, Aufklappen, Zurückklappen sowie Tab-Reset.
- [x] [PR #62](https://github.com/DerHausmeisterKrause/plenaro/pull/62) erfolgreich nach `main` gemergt (Squash-Commit `a5e86ad194aeba9604538238a3abaa303e0c6dd5`); [GitHub-CI #45761769740](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/45761769740) vollständig grün (Frontend-/Backend-Build, UI-Regression, Znuny-Vertragstests, TLS, Docker-Smoke, Favoriten-Browser-Login).
- [ ] Live-Abnahme nach Deployment: Desktop-Abstände, Layoutwechsel, Lesbarkeit längerer Ticketinhalte und Responsive-Verhalten prüfen.
- [x] Keine Backend-/Datenbankmigration, keine Docker-/Compose-/DNS-Änderungen.

### Deployment nach CI/Merge

```bash
cd /opt/plenaro
git switch main
git pull --ff-only origin main
docker compose up -d --build --no-deps web
docker compose restart nginx
docker compose ps
```

---

## 24.11.2026 – Phase 7/7: ATOSS ZK Dialoginitialisierung vor Anmeldung

- [x] Nutzer-Livelog 15:29 geprüft: ATOSS ZK-Seite wird abgerufen, `atoss-zk-login-unconfirmed` vor der Plenaro-Proxy-Navigation. Der Code fasste Fehler bei Passwort-/Klickereignis und späterem Login-Check zusammen.
- [x] Konkrete Abweichung vom ATOSS-HAR identifiziert: der echte Browser sendet `onClientInfo` und `onDialogRendered` (mit aktueller DialogPanel-UUID), anschließend `onDialogFocus` vor der Interaktion; der bisherige serverseitige Login übersprang diese ZK-Initialisierung.
- [x] Initialisierung im vorhandenen ASP.NET-Backend mit dem serverseitig ausgegebenen aktuellen `DialogPanel`-Widget ergänzt; ZK-SID und die erlaubte HTTPS-Upstream-Sitzung bleiben unverändert. Passworteingabe erst nach erfolgreichem Bootstrap.
- [x] Anmeldefehler in Dialoginitialisierung, Benutzername, Kennwort, Anmeldeklick und anschließende Sitzungsbestätigung getrennt; keine Geheimnisse im Debuglog.
- [x] Sitzungsprüfung nach dem Anmeldeklick zuerst gegen den bereits aktiven dynamischen ATOSS-Iframe statt des `/SES/html`-Einstiegs, da dessen erneutes Laden eine neue UUID-Sitzung anlegen kann.
- [x] Synthetische Vertragstests simulieren `onClientInfo` + `onDialogRendered`, `onDialogFocus`, zwei `onChange` und einen `onClick`; Initialisierungsfehler darf keine Zugangsdaten versenden. Kein produktives ATOSS-Konto wird getestet.
- [x] [PR #61](https://github.com/DerHausmeisterKrause/plenaro/pull/61) nach `main` gemergt; [CI #45415987832](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/45415987832) nach Wiederholung eines unabhängigen flüchtigen Znuny-Frontendtests erfolgreich (Build, ATOSS-ZK-Vertragstest, TLS, Docker-Smoke und Browser-Login).
- [ ] Live-Abnahme: ATOSS neu verbinden und tatsächliche Anmeldung prüfen; falls weiterhin Fehler, neuen eindeutigen ZK-Schritt aus Admin-Diagnose auswerten. Kein behaupteter erfolgreicher Login ohne Bestätigung.
- [x] Keine neuen Dockercontainer, DNS-/TLS-Aliasse, Compose-Profile, Migrationen, Ports oder Browserworker.

### Nach grünem CI-Merge aktualisieren

```bash
cd /opt/plenaro
git switch main
git pull --ff-only origin main
docker compose up -d --build --no-deps web
docker compose restart nginx
docker compose ps
docker compose logs --tail=80 web
```

---

## 24.11.2026 – Phase 7/7: ATOSS-ZK-Login, dynamischen Iframe-Abruf korrigieren

- [x] HAR `atoss1` nach gescheitertem Liveversuch untersucht: Browser-Proxy lädt `/SES/html` und anschließend die dynamische `/SES/uuid.../html`-Seite mit HTTP 200, inklusive ZK-Widget-Baum und Loginmaske. Separater serverseitiger Anmeldeversuch meldete `atoss-zk-login-page-unavailable`; Passwort wurde dabei noch nicht eingereicht.
- [x] Konkrete Protokolldifferenz gefunden: Backend-GET auf den dynamischen Iframe übertrug weder `Accept: text/html` noch `User-Agent` oder `Accept-Language`, obwohl der Browser diese Navigationsheader setzt. Anpassung im bestehenden Webprozess, weiter mit isoliertem HTTPS-Upstream und Cookiejar.
- [x] Same-Origin-HTTP-Redirects für die SES-Anmeldeseite auf höchstens zwei Hops begrenzt; fremde Hosts, andere Pfadbereiche, unsichere URLs weiterhin abgewiesen.
- [x] Admin-Fehlerdiagnose unterscheidet nun echte Upstream-HTTP-Fehler (mit Statuscode), Non-HTML, übergroße Antwort und unerlaubte Weiterleitung; nie vollständige URLs, Cookies, Anmeldedaten oder Antwortbodies protokollieren.
- [x] Synthetische Integrationstests prüfen Browser-Navigationsheader, HTTP 404, HTTP 200 mit unerwartetem Inhalt, fremde Redirects und gültigen Same-Origin-Redirect; vorhandene ZK-Login-Vertragstests bleiben erhalten.
- [x] [PR #60](https://github.com/DerHausmeisterKrause/plenaro/pull/60) in `main` gemergt; [CI #45413826256](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/45413826256) erfolgreich (Build, ZK-/Znuny-Vertragstests, TLS, Docker-Smoke, Favoritenbrowser).
- [ ] Live-Abnahme am produktiven ATOSS: `Neu verbinden`, ZK-Login-Diagnose und tatsächliche Anmeldung prüfen. Ob fehlende Browser-Navigationsheader die alleinige Ursache waren, kann nur der Live-Test bestätigen.
- [x] Kein neuer Dockercontainer, keine Änderungen an DNS, TLS, Compose, Datenbank, Hostnamen oder Passwortsicherheit.

### Deployment nach Merge

```bash
cd /opt/plenaro
git switch main
git pull --ff-only origin main
docker compose up -d --build --no-deps web
docker compose restart nginx
docker compose ps
docker compose logs --tail=80 web
```

---

## 24.11.2026 – Phase 7/7: ATOSS ZK-Autologin direkt im bestehenden Backend

- [x] Den vom Nutzer bereitgestellten ATOSS-HAR mit 55 Requests analysiert: `/SES/html` liefert dynamische `uuid.../html`-Iframe-Adresse; die Loginmaske ist ein `zkmx`-ZK-Widgetbaum, kein HTML-Formular. Testdaten und Referenzen sind synthetisch; keine privaten URLs, Sessions, Passwörter oder Cookies übernommen.
- [x] WPF-WebView2-Referenz und bestehende Confluence-Serverlogin-Implementierung geprüft: serverseitiger HTTPS-Request mit privater Cookiejar-Sitzung, niemals Passwortinjektion in den gemeinsamen Favoriten-Host.
- [x] ATOSS-ZK-Login für persönliche Favoriten mit `/SES/html` ergänzt: originalen HTML-Wrapper abrufen, exakt eine aktuelle UUID-Iframe-Adresse entdecken, `dt`/`uu` und eindeutige dynamische Widgets für Benutzername/Kennwort/Anmelden bestimmen.
- [x] Gebundene ZK-AU-Nachrichten zum vom Server gelieferten, validierten `/SES/uuid.../zkauA10[;jsessionid]` über dieselbe HTTPS-Sitzung versenden: `onChange` Benutzername, `onChange` Kennwort, `onClick` Anmelden; kein hartkodierter Mandant, Passwort, Session, Widget- oder Zielwert.
- [x] Fehler- und Authentisierungsprüfung: genau ein Anmeldeversuch, niemals Doppel-POST bei unklarer Antwort; nur eine nachfolgend abrufbare ATOSS-ZK-Seite ohne dieselbe Loginmaske gilt als bestätigt. Andere Zielwebseiten behalten den bisherigen Login.
- [x] Private Admin-Diagnose für ZK-Loginversuch, erkannten Login, nicht bestätigte Anmeldung, fehlende ZK-Seite oder eindeutige Loginwidgets; keine Geheimnisse im Log.
- [x] Mock-Vertragstest für den vollständigen loginartigen ZK-Ablauf, dynamische IDs, HTTPS-Origin und abgelehnte fremde ZK-Endpunkte. Kein echter ATOSS-Login im Test.
- [x] [PR #59](https://github.com/DerHausmeisterKrause/plenaro/pull/59) in `main` gemergt; [CI #45411725497](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/45411725497) vollständig grün (Backend, Frontend, ATOSS-ZK-Vertragstest, TLS, Docker-Smoke und Favoritenbrowser).
- [ ] Produktiv-Abnahme am echten ATOSS: Favorit neu verbinden, neues ZK-Login-Diagnoseereignis und tatsächliche Sitzung testen. Der Nutzer-Mitschnitt enthält keinen echten `onClick`-Login, weshalb das exakte ATOSS-Loginereignis bis zur Abnahme nicht verifiziert ist.
- [x] Keine neuen Container oder Compose-Profile, DNS-Einträge, Zertifikate, Ports, Browserworker, Datenbankmigrationen oder zusätzlichen API-Endpunkte.

### Live-Update nach CI und Merge

```bash
cd /opt/plenaro
git switch main
git pull --ff-only origin main
docker compose up -d --build --no-deps web
docker compose restart nginx
docker compose ps
docker compose logs --tail=80 web
```

---

## 24.11.2026 – Phase 7/7: OrgManager-API HTTP 400 aus HAR gezielt behoben

- [x] Nutzer-HAR untersucht: **45 Anfragen**, dreimal HTTP 400 (GET /orgmgmt/api/simulation, POST /orgmgmt/api/data/views/objectList, POST /orgmgmt/api/data/views/2305/root/withDynamicLists/1), alle mit `CSRF` und `CurrentLanguage`; erfolgreiche API-Anfragen ohne diese Header. Keine HAR-Daten oder Geheimnisse ins Repository übernommen.
- [x] Tatsächliche Ursache im Proxy gefunden: bisherige Header-Allowlist kannte `X-CSRF-Token`, nicht aber `CSRF` oder `CurrentLanguage`.
- [x] Diese beiden Header unverändert an den freigegebenen HTTPS-Upstream der jeweiligen Favoritensitzung weiterleiten; weiterhin keine Browser-Cookies, Host, Proxy-Authorization oder unbekannten Header.
- [x] WPF-Referenz `DerHausmeisterKrause/project_tool/Services/WebShortcutBrowserSessionManager.cs` verglichen: native WebView2-Anfragen übernehmen Webseiten-Header; Web-Proxy braucht gezielte Header-Parität.
- [x] Vertragstests für GET und POST mit OrgManager-Headern und negative Kontrollen für Cookie-/Host-/unbekannte Header.
- [x] [PR #58](https://github.com/DerHausmeisterKrause/plenaro/pull/58) nach `main` gemergt; [CI #45399813797](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/45399813797) vollständig erfolgreich (Builds, Vertragstests, TLS, Docker-Smoke und Browser-Login).
- [ ] Live-Abnahme: OrgManager-Favorit neu verbinden und alle drei API-Anfragen überprüfen. Ein anderer JavaScript-only-Autologin ohne Browserworker bleibt unverändert.
- [x] Keine neuen Container, DNS-Einträge, Aliasnamen, Zertifikate, Datenbankmigrationen oder Compose-Profile.

### Deployment nach grünem CI-Merge

```bash
cd /opt/plenaro
git switch main
git pull --ff-only origin main
docker compose up -d --build --no-deps web
docker compose restart nginx
docker compose ps
docker compose logs --tail=80 web
```

---

## 24.11.2026 – Phase 7/7: Favoriten-HTTP-400 und Browser-/HTML-Login präzise diagnostizieren

- [x] Live-Logs analysiert: Favorit A liefert HTML/Skripte/Stile mit HTTP 200, aber einzelne API-GET/POSTs mit HTTP 400; Favorit B erreicht den Browser-/HTML-Fallback, aber nicht eindeutig einen erfolgreichen Login.
- [x] Keine neuen Container, Docker-Profile, DNS-/TLS-Aliasse, Proxy-Namen, Zusatzzertifikate oder Umgebungsvariablen.
- [x] Leere POST-Anfragen behalten den vorhandenen Content-Type beim Upstream-Forwarding; behebt die bisherige Verluststelle.
- [x] HTTP-400-Antworten nach festen technischen Hinweis-Kategorien (CSRF/XSRF, Origin/Referer, Sitzung, Validierung, Content-Type) klassifizieren, ohne Antwortkörper zu speichern.
- [x] HMAC-Fingerprint des Ressourcenpfads (ohne Query) zum Vergleich wiederholter Fehler: keine vollständigen URLs, Formdaten, Passwörter, Cookies, Tokens oder Header in Logs.
- [x] Klassischer HTML-Login unterscheidet kein Formular, kein HTML, abgelehnten Login-POST, blockierte Cross-Origin-Weiterleitung und gesendeten, aber nicht bestätigten POST.
- [x] Privater Browserlogin unterscheidet Worker nicht erreichbar, Formular/Aktion fehlen, Login nicht bestätigt und tatsächlich importierte Session-Cookies.
- [x] Irreführende, bei jedem Seitenabruf wiederholte Autofill-DNS-Warnung entfernt; einmalige INFO erklärt die Sicherheitsgrenze des gemeinsamen Hostnamens. Keine Passwortinjektion auf Shared-Origin.
- [x] Neue Frontend-/Vertragstests für Ursachenhinweise, sichere HMAC-Kennung und Worker-Ergebnisse ergänzt.
- [x] [PR #57](https://github.com/DerHausmeisterKrause/plenaro/pull/57) in `main` gemergt; [CI #45394182767](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/45394182767) vollständig grün (Backend, Frontend, Tests, Docker-Smoke, Browser-Login).
- [ ] Live-Abnahme: favoritenweise Neu verbinden, Fehlermeldungen nach Kennung (#...) und Ursachenhinweisen prüfen. Ohne eindeutigen Ursachehinweis lässt sich HTTP 400 nicht allein aus Statuscodes reparieren. JavaScript-only-Login kann ohne verfügbaren Browserworker im Shared-Host nicht automatisch ausgefüllt werden; manuelle Anmeldung bleibt möglich.

### Deployment nach CI/PR-Merge

```bash
cd /opt/plenaro
git switch main
git pull --ff-only origin main
docker compose up -d --build --no-deps web
docker compose restart nginx
docker compose ps
docker compose logs --tail=80 web
```

---

## 24.11.2026 – Phase 7/7: Admin-Debugmodus für Favoriten/Proxy

- [x] WPF-Referenz `WebShortcutBrowserSessionManager` geprüft (Navigation, WebResource-Statuscodes, Netzwerkfehler); web-spezifische Diagnose läuft direkt im vorhandenen FavoriteProxy.
- [x] Eigenständiger **pro Admin-Konto** gespeicherter Opt-in-Schalter unter **Einstellungen → Webseiten / Favoriten** (Migration `0033_favorite_debug_settings.sql`, initial `false`).
- [x] Server prüft Adminrolle und aktiven Benutzer; Toggle und Log-Löschen sind CSRF-geschützt; Log-Lesen prüft Favoritenbesitz. Normale Nutzer bekommen niemals Adminlogs.
- [x] Sichere kategorisierte Proxy-Ereignisse: Verbindungsaufbau/Bootstrap, Auto-Login-Modus, HTML-/Headless-Fallback, HTTP-Status nach Ressourcentyp, Navigation/Redirect, blockierte Anfragen, Timeouts und Autofill-Zustand.
- [x] Nur fest definierte Codes und HTTP-Metadaten; **keine** URLs/Pfade/Queryparameter, Benutzernamen, Passwörter, Cookies, Tokens, Inhalte, Header, Post-Bodies oder HARs.
- [x] Browserseite zeigt Administratoren eine Diagnosekonsole beim **ausgewählten Favoriten** (3-Sekunden-Aktualisierung, Fehlerfilter, Log löschen); einfache Benutzer sehen keine Konsole und senden keine Diagnoseanfragen.
- [x] Ereignisse bleiben nur im RAM dieses Webprozesses (max. 250 pro Admin/Favorit, max. 30 Minuten), kein dauerhaftes Zugriffsprotokoll; beim Abschalten sofort löschen.
- [x] Keine neuen Container, DNS-Aliasse, Upstream-Änderungen oder Browser-Login-Profile.
- [x] Frontend- und Integrationstests für Sichtbarkeit, Umschalter, Adminrolle, CSRF, Favoritenbesitz, Redaction und Pufferlimit.
- [x] [PR #56](https://github.com/DerHausmeisterKrause/plenaro/pull/56) nach `main` gemergt; [CI #45364499104](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/45364499104) vollständig grün (Frontend, Backend, Vertragstests, Docker-Smoke und Browser-Login).
- [ ] Produktivtest am Zielhost mit betroffenen Favoriten (ATOSS/Wiki): HTTP-Fehler, CSS/JS-Ressourcen, Login-Ablauf. Die fremde JavaScript-Konsole und Browser-CSP/CORS-Fehler vor dem Proxy können **nicht** vollständig serverseitig erfasst werden.

### Live-Deployment nach CI/PR-Merge

```bash
cd /opt/plenaro
git pull --ff-only origin main
docker compose up -d --build --no-deps web
docker compose restart nginx
docker compose ps
docker compose logs --tail=80 web
```

Migration 0033 erfolgt automatisch beim Webstart; bestehende Daten und Favoritensitzungen anderer Nutzer werden nicht über die Diagnose-API freigegeben.

---

## 24.11.2026 – Phase 5/7: Gelber Stern bei neuen Znuny-Ticketnachrichten

- [x] Desktop-Referenz `TicketArticleReadStateService`, `TicketSystemService.ApplyArticleReadPresentation`, `TodayViewModel.SelectedTicketArticle` und `TodayView.xaml` geprüft.
- [x] Migrationsdatei `0032_znuny_article_read_state.sql`: personalisierte Artikel-Wasserstände, ungelesene Artikel und Bestätigungen persistieren; keine Änderungen an Znuny.
- [x] Bestehenden zentralen Znuny-Artikelindex wiederverwenden: nach initialer Baseline werden nur zuverlässig neuere Artikel (Zeitstempel, numerische ID) ungelesen; wiederholte Synchronisation oder ältere Artikel erzeugen keinen Doppelstern. Bereits indexierte Tickets erhalten ihren initialen Wasserstand beim Nachindexieren.
- [x] Sternanzeige in Heute, Aktuelle, Neue, Erledigte sowie Ticket-System-Listen; manuelles Ticket-Anpinnen bleibt davon unabhängig.
- [x] Die ausgewählte sichtbare Nachricht wird einzeln gelesen; bei mehreren neuen Nachrichten bleibt der Stern bis zur letzten ungelesenen Nachricht stehen.
- [x] Benutzergebundener CSRF-geschützter Lesestatus-Endpunkt, persönliche Ticketberechtigung weiterhin über bestehende Endpoint-Filter erzwungen.
- [x] Unit-/Frontend- und Docker-Smoke-Regressionen für Vergleich, Lesestatus, CSRF und Mehrbenutzer-Isolation ergänzt.
- [x] [PR #55](https://github.com/DerHausmeisterKrause/plenaro/pull/55) nach `main` gemergt; [CI #45358932870](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/45358932870) vollständig grün (Frontend, Backend, Znuny-Verträge, Docker und Login).
- [ ] Produktiv-Deployment auf dem Zielhost prüfen.
- [ ] Live-Znuny-Abnahme: neue Kundenartikel bei zwei verschiedenen Benutzern erzeugen, Stern nach zentraler Synchronisation und nach Lesen prüfen.

**Deployment nach erfolgreichem Merge** (bestehende Installation):
```bash
cd /opt/plenaro
git pull --ff-only origin main
docker compose up -d --build --no-deps web
docker compose restart nginx
docker compose ps
docker compose logs --tail=80 web
```
Die Migration läuft automatisch beim Webstart; es wird kein weiterer Container benötigt.
Die ersten neu erkannten Artikel nach Upgrade werden als bestehender Stand initialisiert; neue Nachrichten danach werden markiert.

---

## 23.11.2026 – Phase 5/7: Homeoffice-Tage im Monatsreport

- [x] Desktop-Referenz geprüft: `ReportsViewModel` verwendet `WorkDayService.GetMonthlyMarkerStatistics()` und zählt Tage mit effektivem HO-Marker genau einmal.
- [x] Web-Report `GET /api/v1/reports/month` zählt pro angemeldetem Benutzer und ausgewähltem Kalendermonat die gespeicherten `day_markers.is_ho=true`-Tage; Monatsgrenzen sind datumsbasiert und unabhängig von der Uhrzeit.
- [x] Neue Kennzahl „Homeoffice-Tage“ in der Monatsübersicht inklusive Singular/Plural.
- [x] CSV-Export enthält Monat und die Anzahl der Homeoffice-Tage.
- [x] Frontend-Test für die neue Kennzahl sowie CI-Smoke-Assertions für zwei HO-Tage, Monatsabgrenzung, Benutzertrennung und CSV ergänzt.
- [x] [PR #54](https://github.com/DerHausmeisterKrause/plenaro/pull/54) nach `main` gemergt; [CI #45150899295](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/45150899295) erfolgreich (Frontend, Backend, Docker-Smoke und Browserprofil).
- [ ] Produktiv-Deployment auf dem Zielhost prüfen.
- [ ] Browser-Abnahme mit tatsächlichen Homeoffice-Markern; Outlook-only-HO ohne Plenaro-Marker wird derzeit nicht automatisch in die Statistik übernommen (die Desktopversion berücksichtigt zusätzlich synchronisierte Outlook-Marker).

Keine DB-Migration, kein zusätzlicher Container und keine Outlook-Abfrage für den Report.

---

## 23.11.2026 – HO-Antrag mit Outlook-Ganztagstermin: vorhandene Marker nachtragen

**Desktop-Referenz:** `project_tool/Services/HomeOfficeService.cs` und
`OutlookInteropService.UpsertHomeOfficeAppointment`. Die Web-Version
führt bereits bei **neu bestätigtem HO** zuerst eine Microsoft-365-
Vorabprüfung, dann Tagesmarker, einen ganztägigen Outlook-Termin mit
`showAs=workingElsewhere` („An anderem Ort tätig“) und danach genau
einen Mailversand aus. Auch der „Heute“-Schnellbutton verwendet den
Antragsablauf. Die Kalendereinrichtung wurde zuvor in
`HomeOfficeCalendarService` und Migration 0031 umgesetzt.

**Verbleibende Lücke:** Bereits als HO gespeicherte Tage (z. B. vor
Migration 0031 oder nach fehlgeschlagenem Kalender-Consent) zeigen die
HO-Markierung, lösen bei erneutem Speichern aber absichtlich keinen
zweiten Antrag aus. Für solche Fälle gab es keine kalenderexklusive
Nachtragungsfunktion ohne Mailversand.

- [x] `POST /api/v1/br-requests/{day}/calendar` verlangt angemeldeten,
  aktiven Benutzer, CSRF und vorhandenen **eigenen** HO-Tagesmarker.
- [x] Vorhandenen `HomeOfficeCalendarService.EnsureCreatedAsync` verwenden:
  dediziertes delegiertes `Calendars.ReadWrite`, Abgleich mit exakter
  WPF-Signatur und `(user_id,day)`-Journal samt Transaktions-ID gegen
  Duplikate und wiederholte unklare Graph-POSTs.
- [x] **Kein** Mailversand beim Nachtragen, kein Ändern des Tagesmarkers.
  BR-only-Tage können keinen HO-Termin über diese Route anlegen.
- [x] UI-Aktion „Outlook-Homeoffice-Termin prüfen / nachtragen“ für
  bestehende HO-Marker unter „Arbeitstag heute“ und im Tagesmarker-Editor.
- [x] Frontend-Regressionstests für vorhandenen HO-Marker mit angenommener
  Mail und BR-only ohne Kalenderaktion ergänzt.
- [x] [PR #53](https://github.com/DerHausmeisterKrause/plenaro/pull/53) nach `main` gemergt; [CI #45149184028](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/45149184028) vollständig grün (Frontend, Backend, Verträge, Docker-Smoke und Browserprofil).
- [ ] Live-Test mit persönlichem Microsoft-365-Konto.
- [ ] Microsoft-365-Einwilligung: `Calendars.ReadWrite` muss
  delegiert freigegeben sein. Bei fehlendem Recht
  Einstellungen → Aufgaben & Kalender →
  „Microsoft-365-Berechtigungen aktualisieren“.

Keine zusätzliche Infrastruktur, Container oder DNS-Einträge.

---

## 19.11.2026 – Bestätigter HO-Antrag erzeugt WPF-kompatiblen Outlook-Ganztagstermin (PR #52 gemergt)

**Desktop-Vorlage geprüft:**
`project_tool/Services/HomeOfficeService.cs::SubmitAsync/RemoveAsync`
und `Services/OutlookInteropService.cs::UpsertHomeOfficeAppointment`.
Die WPF-Version erstellt **vor dem E-Mail-Versand** einen ganztägigen
Kalendereintrag und wiederverwendet einen bereits passenden Termin.
Beim Abwählen von HO wird nur ein eigener, eindeutig signierter
Homeoffice-Termin gelöscht.

- [x] `HomeOfficeCalendarService` implementiert:
  `POST /v1.0/me/calendar/events` im persönlichen delegierten Graph-Kontext.
  Titel und Inhalt `Homeoffice am yyyy-MM-dd`; Start am HO-Tag
  00:00, Ende am Folgetag 00:00 in `W. Europe Standard Time`,
  `isAllDay=true`, `isReminderOn=false`,
  `showAs="workingElsewhere"`, Standort `An anderem Ort tätig`,
  keine Teilnehmer oder Kategorien.
- [x] Vor jedem HO-Antrag wird `Calendars.ReadWrite` zusammen mit
  bestehendem `Mail.Send` geprüft; der explizite Microsoft-365-OAuth-
  Verknüpfungsfluss fordert Kalender-Schreibrechte neu an.
  Der gewöhnliche Entra-Login wird nicht hochgestuft.
- [x] Suche in genau dem betreffenden Outlook-Tag nach der vollständigen
  Plenaro-/WPF-Termin-Signatur, Reuse bei einem Treffer, Abbruch bei
  mehreren Treffern; keine Änderung an fremden Terminen.
- [x] Migration `0031_homeoffice_calendar_events.sql`:
  einzigartiges `(user_id,day)`-Journal mit Transaktions-ID und
  `processing/created/unknown/removed` gegen doppelte POSTs nach
  parallel gestarteten Anträgen, Abstürzen und unklaren Graph-Antworten.
- [x] `/api/v1/br-requests/{day}/send`: Bei HO erst Termin anlegen/
  wiederverwenden, anschließend wie bisher genau einen Mailversand.
  BR **ohne HO** bleibt Mail-only. Auch bei bereits akzeptierter BR-Mail
  wird eine neu hinzugefügte HO-Markierung im Outlook-Kalender angelegt,
  ohne die alte Mail erneut zu versenden.
- [x] Abwählen von HO entfernt über das Marker-PUT nur Outlook-Termine
  mit **passender Plenaro-Homeoffice-Signatur**; erneutes HO kann
  einen neuen, deduplizierten Graph-Transaktionsversuch anlegen.
- [x] Beide HO-Oberflächen: Tagesmarker-Dialog und Schnellschalter
  `Heute` verlangen Bestätigung vor Kalender/Mail. Kein Neben-
  effekt bei `Nein`, keine erneute Aktion beim bloßen Bearbeiten.
- [x] Reconnect-Link zu expliziter Einwilligung
  `Calendars.ReadWrite` in den Benutzer-Einstellungen.
- [x] Frontend- und .NET-Vertragstests für Kalenderfelder, bestehende
  WPF-Terminsignatur, bestehende BR-Mail und fehlende Doppelerstellung.
- [x] [PR #52](https://github.com/DerHausmeisterKrause/plenaro/pull/52) nach `main` gemergt; [CI #44553467789](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44553467789) grün: Backend/Frontend, Graph-Payload-/WPF-Verträge, vollständiger Docker-Smoke inklusive Migration 0031 und vorhandene Tests.
- [ ] Produktiv-Deployment, delegierte Freigabe `Calendars.ReadWrite` und echter M365-Test mit einem Testdatum. Die reale Tenant-Freigabe ist durch CI nicht ersetzbar.

**Kein** neuer Container, DNS-Alias, Outlook-COM oder App-only-Graph-
Schreibrecht. Kalenderzugriff ausschließlich auf das eigene delegierte
Postfach nach einer bestätigten HO-Aktion. `Mail.Send` wird
weiterhin maximal einmal pro Benutzer und Datum ausgeführt.

---

## 19.11.2026 – Outlook-Ganztagstermine einen Tag zu lang in „Heute“ (PR #51 gemergt)

**Gemeldeter Fehler:** Der Ganztagstermin „Buß- und Bettag (§)“ vom
18.11.2026 wurde fälschlich auch am 19.11.2026 angezeigt. Der Outlook-Graph-
Datensatz hat für `isAllDay=true` eine **exklusive End-Datumsgrenze**,
z. B. `2026-11-19T00:00:00Z`. In Deutschland ist das bereits 01:00 Uhr
am 19.11.; die bisherige Zeitintervall-Überschneidung `end > dayStart`
zählte damit den Folgetag dazu. Das Datum des Ganztagstermins ist
hingegen nur der 18.11.

- [x] Die WPF-Referenz
  `project_tool/ViewModels/Calendar/OutlookAllDayMarkerMapper.cs`
  geprüft: `StartLocal.Date <= day.Date && EffectiveEndDateExclusive > day.Date`.
- [x] Dieselbe **kalendertägliche, exklusive Enddatumsregel** als
  `outlookAllDayOccursOnDay` in `frontend/src/todayAgenda.ts` übernommen.
- [x] Reiter `Heute`: Outlook-Ganztagstermine nur an tatsächlich belegten
  Tagen anzeigen; UTC-/DST-Stundenüberlappungen am Folgetag ignorieren.
- [x] Wochenplanung: ganztägige Outlook-Termine einschließlich mehrtägiger
  Termine konsistent auf den betreffenden Tagen anzeigen.
- [x] **Normale** Termine weiterhin nach echten Uhrzeitüberschneidungen
  filtern (Mitternachtstermine bleiben an beiden Tagen sichtbar).
- [x] Regressionsfälle für 18./19.11.2026, Berliner Mitternacht,
  Sommerzeitwechsel, mehrtägige Ganztagstermine, fehlerhafte Zeiten und
  echte Terminüberschneidungen ergänzt.
- [x] [PR #51](https://github.com/DerHausmeisterKrause/plenaro/pull/51) nach `main` gemergt; [CI #44537370389](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44537370389) vollständig erfolgreich (Frontend-Regressionen, Backend, Znuny-Verträge, NGINX, Docker-Smoke und Browserworker-Job).
- [ ] Produktiver Blick in den Reiter „Heute“ nach Aktualisierung: Buß- und Bettag darf am 19.11.2026 nicht erscheinen. Keine neue Infrastruktur, kein Container, keine Graph-/Entra-Änderung.

---

## 19.11.2026 – ATOSS-Autologin ohne zusätzliche DNS-Aliasse (PR #50 gemergt)

**Explizite Nutzerentscheidung:** Der bereits funktionierende Favoriten-Host
`https://apps-plenaro.example.org/site-<UUID>/` bleibt erhalten. KEINE
neuen CNAMEs, Wildcard-DNS, TLS-SAN-Namen oder dedizierte Favoriten-Origins
als Voraussetzung. Die Warnkarte wurde bereits mit PR #49 entfernt.

**Technischer Ansatz:** Optionaler interner Einmal-Browserdienst
`favorites-login` auf Basis der offiziellen Playwright-Chromium-Laufzeit.
Beim persönlichen Favoriten-Start mit Auto-Login und verschlüsseltem Vault-
Passwort startet er einen echten Chromium-Browser im Docker-Netz, öffnet
**ausschließlich die vom bestehenden HTTPS-Favoriten-Proxy bereits genehmigte
Upstream-Origin**, füllt dynamische ATOSS/ZK-Eingabefelder, führt die echte
Anmeldeaktion aus und gibt **nur bestätigte Upstream-Sitzungscookies** an
die benutzergebundene HttpClient-CookieJar weiter. Der reguläre Proxy
liefert danach wie zuvor die Seiten unter dem geteilten `/site-.../` aus.

- [x] Kein Anmeldeskript mit Klartext-Passwort auf dem gemeinsamen Browser-
  Host. Kein Eintrag in HTML, Browser-JS, HAR oder Browserkonsole.
- [x] Normaler HTML-Autologin als Fallback, wenn das optionale Browser-
  Profil nicht gestartet ist. Keine doppelte Anmeldung nach einem
  tatsächlichen Chromium-Versuch (auch bei unklarer Antwort).
- [x] Neues Compose-Profil `browser-login` ohne Portfreigabe: der
  interne Dienst ist nur im Docker-Netz erreichbar. Start:
  `docker compose --profile browser-login up -d --build`.
- [x] ZK-/ATOSS-Feldsuche nach Benutzername, Kennwort und dynamischen
  ZK-Buttons, auch in same-origin-Frames. Kein Klicken auf beliebige
  Elemente, fremde SSO-Origin, MFA und unbekannte Loginformate.
- [x] HTTPS-Zertifikatsvalidierung bleibt aktiv. Interne
  `PLENARO_TRUSTED_CA_FILE` wird vor Chromium-Start ins OS-Truststore
  eingebunden; keine globale TLS-Deaktivierung.
- [x] Same-origin-HTTP/WebSocket-Netzwerkfilter und IP-Sperren (Loopback,
  Link-Local, Metadaten), maximal zwei parallele Logins. Aufgerufen wird
  nur die bereits durch Plenaro validierte Ziel-Origin.
- [x] Rückgabe ausschließlich zustandsloser, validierter Cookies
  (Host-/Domainprüfung, sichere Pfade, maximale Größe). Für das bestehende
  HttpClient-Jar werden sie erneut als **HTTPS-only und host-only** gesetzt;
  der Chromium-Browser wird nach dem Versuch zerstört.
- [x] Separater CI-Job baut den optionalen Worker und führt einen
  synthetischen Test in echtem Chromium aus: nachgeladenes ZK-Login,
  Sitzungscookie, kein fremder Reset-Button. Dotnet-Vertragstest prüft
  zulässige/abgelehnte Sessioncookies.
- [x] [PR #50](https://github.com/DerHausmeisterKrause/plenaro/pull/50) gemergt; [CI #44532005260](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44532005260) vollständig grün, einschließlich Backend-/Frontend-Verträge, vollständigem Docker-Smoke, echtem Chromium-ZK-Login und Start des Workers mit beschränkten Rechten.
- [ ] Optionales Profil `browser-login` auf dem produktiven Host starten und echten ATOSS-Login bestätigen. Keine DNS-/TLS-Aliasse nötig.

**Abgrenzung:** Echte Apps verwenden u. U. lokale Browser-Speicher,
unbekannte zusätzliche APIs, externe SSO und MFA. Nur nach beobachteter
echter ATOSS-Anmeldung als vollständig funktionsfähig bezeichnen.
Browser-Worker wird nur für den Login, nicht für jede Folgeseite benötigt,
solange die Session cookies-basiert funktioniert.

---

## 19.11.2026 – ATOSS: Browser-Login und Favoriten-Hinweis (PR #49 gemergt)

- [x] Unerwünschten dauerhaften Hinweis `Automatisches Ausfüllen noch
  nicht aktiv` aus `FavoritePage` entfernt, ohne die funktionierende
  Shared-Favoritenansicht oder vorhandene gespeicherte Passwörter
  zu verändern.
- [x] ATOSS/ZK-DOM-Login-Helfer auf dedizierter HTTPS-Origin kann neben
  HTML-Submitbuttons auch explizit als `a.z-button`, `[role=button]`
  bzw. ARIA-Label gerenderte **eindeutige** Loginaktionen bedienen;
  andere Buttons oder Passwortänderungen werden nicht geklickt.
- [x] Vertragstests: dynamischer ATOSS/ZK-Anmeldebutton wird genau
  einmal ausgelöst, fremde Buttons nicht, Shared-Host-Warnung fehlt.
- [x] Diagnose `tools/check_favorite_autologin_host.sh` prüft ohne
  Zugangsdaten dedizierten Host auf DNS, gültiges HTTPS und die
  exakt erwartete Proxy-Health-Antwort.
- [x] [PR #49](https://github.com/DerHausmeisterKrause/plenaro/pull/49) nach `main` gemergt; [CI #44524726181](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44524726181) vollständig grün (Backend, Frontend, ZK-DOM-Tests, NGINX und Docker-Smoke).
- [ ] Deployment auf echtem Plenaro-Host, DNS/TLS/NGINX-Alias und Live-ATOSS-Abnahme.
- [ ] Infrastrukturvoraussetzung für **tatsächlich funktionierenden**
  ATOSS-Browser-Autofill: interner DNS-Eintrag (bei genau einem
  Favoriten einzelner CNAME zulässig) für den generierten Host,
  passendes TLS-Wildcard/SAN-Zertifikat, äußerer NGINX-VHost und
  `PLENARO_FAVORITES_PERSONAL_DEDICATED=true`. Ohne diese Schritte
  bleibt der sichere Shared-Host-Fallback mit klassischem HTML-Login;
  die bloße Entfernung des Hinweises aktiviert noch kein DOM-Autofill.
- [ ] Echte ATOSS-Anmeldung auf der isolierten Origin testen.
  `X-Plenaro-Autofill: dedicated-dom` bestätigt nur die Skriptauslieferung.

**Sicherheitsgrenze:** Kein JavaScript-Passwort-Autofill auf dem
gemeinsamen Favoriten-Host und keine stillschweigende Umschaltung auf
nicht auflösbare Subdomains. Kein automatisches Umgehen von MFA/SSO.

---

## 19.11.2026 – Favoriten-NXDOMAIN und täglicher 24h-Fortschrittsbalken (PR #48 gemergt)

**Favoriten-Regression:** PR #45 wählte für jeden persönlichen `site-<UUID>`-
Favoriten automatisch die aus `PLENARO_FAVORITES_HOST_TEMPLATE` abgeleitete
dedizierte Origin. Eine Template-Angabe allein stellt aber **weder DNS**,
**noch gültiges TLS oder NGINX-VHost-Routing** für jeden generierten
Host bereit. Fehlermeldung aus der Praxis:
`site-0f496700e7e94e4c979464058a4d5810-plenaro.intern.test.org`
konnte nicht aufgelöst werden. Die vorher funktionierende
`PLENARO_FAVORITES_PATH_ORIGIN` blieb ungenutzt.

- [x] Sobald ein Shared-Favoriten-Host konfiguriert ist, bleiben persönliche
  Favoriten unabhängig von der Hostvorlage **standardmäßig unter
  `/<slug>/` auf der gemeinsamen Origin**.
- [x] Expliziter Schalter
  `PLENARO_FAVORITES_PERSONAL_DEDICATED=true` im Docker-Compose /
  `.env.example`; erst aktivieren, **nachdem** Wildcard-DNS,
  Zertifikat und Reverse-Proxy für das generierte Muster funktionieren.
  Bestehende explizit dedizierte Wiki-/Plugin-Hosts bleiben dediziert.
- [x] Keine Klartext-DOM-Zugangsdaten auf dem gemeinsamen Favoriten-Host.
  ATOSS-JavaScript-DOM-Autofill benötigt weiterhin echte dedizierte
  Origin und die DNS-/TLS-Konfiguration. Serverseitige klassische
  Login-Methoden und manuelles Login bleiben als Fallback erhalten.
- [x] Backend-Vertragstests: vorhandene Hostvorlage ohne Opt-in ergibt
  den bekannten Shared-Pfadhost; mit Opt-in dedizierte Host-URL.
- [x] Wiederverwendbare barrierearme 24h-Zeitbuchungsleiste im **Zeitreport
  (immer heutiger Tag, unabhängig von gewähltem Monat)** und kompakt
  neben der Zeitbuchung. Grün bis 90 %, gelb bis unter 100 %, rot voll.
  Zeigt gebucht plus noch ungeklärt, sowie verfügbaren Rest. Backend bleibt
  unverändert verbindliche Quelle (PostgreSQL-Guard).
- [x] UI-Tests zu Minutenwerten, vollständiger Belegung,
  Accessibility-Attributen und Warnung bei Überschreitung.
- [x] [PR #48](https://github.com/DerHausmeisterKrause/plenaro/pull/48) in `main` gemergt; [CI #44508791675](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44508791675) vollständig grün (Backend, Frontend, Znuny- und Favoriten-Kontrakte, NGINX und Docker-Smoke).
- [ ] Zielhost-Docker-Neustart, Favoritenöffnung und ATOSS-Abnahme im realen Netzwerk.
- [ ] Für funktionsfähiges ATOSS-DOM-Autofill wildcardfähige DNS-/TLS-
  Infrastruktur einrichten und dann ausschließlich mit Opt-in
  dedizierte Hosts auswählen.

---

## 19.11.2026 – KRITISCH: maximale 24h Znuny-Zeitbuchungen pro Agent/Kalendertag (PR #47 gemergt)

**Sicherheits-/Geschäftsregel:** Plenaro darf pro admin-freigegebenem
Znuny-Agenten, über **alle Tickets und alle diesem Agenten zugewiesenen
Plenaro-Benutzer** gemeinsam, pro **Europe/Berlin-Kalendertag**
maximal **1.440 Minuten** an Znuny übertragen. Geprüft werden die
**tatsächlich an Znuny gesendeten, auf 15 Minuten aufgerundeten Minuten**
statt der Sekunden des lokalen Timers.

- [x] Backend-Sperre in `/api/v1/znuny/tickets/{ticketId}/time/book`
  direkt vor dem Einfügen der ausgehenden `pending`-Buchung. Bei Überschreitung
  HTTP 409 mit `code=znuny-agent-daily-limit`, Datum, Limit, reservierten
  und verbleibenden Minuten – **kein** `TicketUpdate` an Znuny.
- [x] PostgreSQL-Advisory-Transaktionslock je Agent+Kalendertag:
  parallele Buchungen auf verschiedenen Tickets können das Tagesbudget
  nicht doppelt nutzen.
- [x] `pending` und `confirmed` reservieren Zeit, `rejected` nicht.
  Unklare/Timeout-Buchungen bleiben bewusst blockierend, weil Znuny den
  Auftrag möglicherweise angenommen hat. Keine automatische Wiederholung.
- [x] Migration `0030_znuny_agent_daily_booking_limit.sql` ergänzt
  unveränderliche Schnappschüsse von Agent-ID und Berliner Buchungsdatum
  für neue Buchungen; bestehende Buchungen werden anhand aktiver
  Admin-Zuordnungen nachgetragen, wenn möglich. Indizes für die Tagesabfrage.
- [x] Lese-Endpunkt `/api/v1/znuny/tickets/time/daily-limit` plus
  Anzeige des verbleibenden Tageskontingents und klarer Warnhinweis im Timer.
- [x] Vertrags- und Browser-Tests für genau 24h, Überschreitung,
  15-Minuten-Rundung, Sommer-/Winterzeit, unterschiedliche Agenten,
  offene/abgelehnte Buchungen und Datenbank-/HTTP-Verhalten ergänzt.
- [x] [PR #47](https://github.com/DerHausmeisterKrause/plenaro/pull/47) gemergt; [CI #44505339958](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44505339958) vollständig grün einschließlich realem PostgreSQL-/HTTP-Test, Backfill-Migration, Backend, Frontend und Docker-Smoke. Anzeige-Endpunkt wurde so korrigiert, dass kein bereits konfigurierter Remote-Znuny-Server erforderlich ist.
- [ ] Produktivabnahme nach Deployment mit mehreren Tickets und zugeordneten Agenten.

**Reichweite:** Diese Sperre schützt alle **über Plenaro vorgenommenen**
Zeitbuchungen. Buchungen, die ein Agent außerhalb von Plenaro direkt in
Znuny/OTRS oder über das alte WPF-Tool tätigt, sind ohne durchgängige
autoritative Prüfung auf dem Znuny-Server nicht zwingend in Plenaros
Buchungsjournal vorhanden. Eine **systemweite** 24h-Grenze über alle
externen Schreibkanäle benötigt eine zusätzliche Znuny-seitige
Validierung/Regel. Dies nicht als bereits implementiert ausgeben.

---

## 17.11.2026 – Dashboard-Znuny: sporadische HTTP 429 (Phase 6, PR #46 gemergt)

**Ursache im Code nachgewiesen:** `UseRateLimiter()` stand vor
`UseAuthentication()`. Alle Policies mit `ClaimTypes.NameIdentifier`
fielen daher auf die Remote-IP zurück. Anwender hinter derselben
Client-IP/NAT oder bei nicht korrekt aufgelöster Proxy-IP teilten sich
ein Kontingent von nur 40 Lesezugriffen in fünf Minuten – besonders
problematisch bei mehreren Dashboard-Tabs, Ticketnachrichten und
Inline-Bildern. Die echten Ticketlisten/Agenda werden aus dem zentralen
Cache gelesen; nicht jede Dashboard-Ansicht verursacht remote OTRS-Aufrufe.

- [x] Reihenfolge korrigiert: Routing → Authentication → RateLimiter →
  Authorization. Angemeldete Benutzer werden anhand ihres stabilen
  Plenaro-IDs partitioniert, IP-Fallback nur für nicht angemeldete
  Anfragen. Login-Rate-Limit bleibt absichtlich IP-basiert.
- [x] Rate-Limits für interaktive Ticketlesevorgänge auf 80/5 Min.
  je Benutzer angepasst; Inline-Anhänge/Bilder erhalten ein eigenständiges
  Limit von 120/5 Min. pro Benutzer. Znuny-Schreiblimits bleiben separat
  und unverändert, keine automatischen Schreibwiederholungen.
- [x] Von Plenaro ausgelöste 429 erhalten `X-Plenaro-RateLimit:
  application`, nach Möglichkeit `Retry-After` und eine sichere
  Fehlermeldung. Das Frontend unterscheidet diese Meldung von einem
  äußeren NGINX-/Proxy-429; normales Dashboard wird nicht automatisch
  wiederholt oder mit leeren Daten überschrieben.
- [x] Backend-Regressionstest: zwei authentifizierte Nutzer mit
  derselben Proxy-IP müssen unterschiedliche Zähler erhalten; dieselbe
  Nutzer-ID über verschiedene IPs denselben Zähler; Anonyme IP-Partition.
- [x] Frontend-Regressionstests für 429-Quellen und erfolgreiche Abfrage.
- [x] [PR #46](https://github.com/DerHausmeisterKrause/plenaro/pull/46) in `main` gemergt; [CI #44075326407](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44075326407) vollständig grün (Backend, Frontend, Znuny-Vertragstests, NGINX und Docker-Smoke).
- [ ] Auf echtem Docker-Host aktualisieren und mit zwei parallel angemeldeten Benutzern überprüfen.
- [ ] Falls danach noch 429: Im F12-Netzwerk den **Pfad**, die Antwortheader
  `X-Plenaro-RateLimit` und `Retry-After` (ohne Tokens) prüfen.
  Weitere 429 von einem äußeren NGINX sind separat zu beheben.

# Plenaro 2.0 – aktueller Arbeitsstand

**Stand: 17.11.2026 · aktive Entwicklungsphasen: 6 (Znuny/OTRS-Abnahme) und 7 (Wiki/Favoriten/Sicherheit)**

Dies ist die aktuelle Arbeitsliste. Die ausführliche [Roadmap](ROADMAP.md) enthält zusätzlich historische, teils **abgelöste** Implementierungsschritte. `[x]` bedeutet: Code/CI vorhanden, **nicht automatisch** am realen Zielserver abgenommen.

## Architektur und verbindliche Entscheidungen
- Webanwendung im lokalen Docker Compose mit ASP.NET Core 10, React/TypeScript, PostgreSQL und NGINX; öffentliche Oberfläche unter HTTPS.
- Lokale Benutzer und Single-Tenant-Entra-SSO; neue, validierte SSO-Nutzer erhalten automatisch ein einfaches lokales Konto. Znuny-Agenten- und Kandidaten-ID dürfen ausschließlich Administratoren zuweisen.
- Ein Arbeitsplatz statt doppelter Aufgaben-/Ticket-Navigation. In Heute nur tagesbezogene Outlook-Termine und heute geplante Tickets; Aktuelle = Owner oder Responsible; Neue = freie/Standardagent-Tickets; Erledigte = eigene geschlossene Tickets. Lokale Aufgaben als Unterreiter.
- Keine Kommen-/Gehen-/Pausen- oder Sollzeitaufzeichnung. UL/AM/BR/HO-Kennzeichnungen, Aufgaben-/Ticket-Timer und Projektzeitbuchung bleiben erhalten.
- Znuny als Standard-Connector, spätere fremde Ticketsysteme über Adapter. Ticket-Weboberfläche nur über nativen HTML-Reverse-Proxy auf **eigener same-site HTTPS-Origin**; kein Selenium/Chromium-/Screenshot-Fallback.
- Unbekannte externe Webseiten niemals unbeschränkt durch die Znuny-Bridge proxyn. Keine Betriebs-, Zugangsdaten, echten internen Domains oder Ticketinhalte im öffentlichen Repository.

## Phase 1 – Repository, Docker und Sicherheit
- [x] Neues Web-Repository getrennt vom WPF-Referenzprojekt; Grunddokumentation, Lizenz-Entwurf, CI, Container, Datenbank und Migrationen.
- [x] NGINX, HTTPS-Modus, geschützte DB, persistente Volumes, Healthchecks und Secret-Vault.
- [ ] LAN-Host/IP-, Zertifikats-, Neustart- und Persistenzprüfung am tatsächlichen Zielhost abschließen.
- [ ] DB-Passwort über Docker-Secrets, Masterkey-Rotation, Data-Protection-Keyring-Backup und belastbaren Restore-Test abschließen.
- [ ] Source-Available-/kommerzielle Lizenz vor Vertrieb juristisch prüfen.

## Phase 2 – Browseroberfläche
- [x] React/TypeScript-Web-Shell, Classic-Listen-/Detailansichten, Responsive UI, Light/Dark und Einstellungen.
- [x] Arbeitsplatz/Ticketsystem-Navigation und korrekte linke/rechte Arbeitsflächen im Code.
- [ ] Browser-E2E- und Accessibility-Abnahme, Desktop-/Mobil-Layout, Touch und Größenänderungen testen.
- [ ] Restliche Felder, Tastaturbedienung und Ansichtsparität anhand der WPF-Referenz prüfen.

## Phase 3 – Benutzer und Rechte
- [x] Ersteinrichtungs-Assistent, Admin/Benutzer/Rollen, lokale Anmeldung, CSRF und Login-Schutz.
- [x] Nutzerverwaltung, Benutzerlöschung, erneute sichere Entra-Provisionierung, Vault und Admin-only Agenten-/Kandidaten-Zuordnung.
- [ ] Realtest mit zwei Benutzern einschließlich Daten-/Sitzungsisolation, Sperrung, Re-Login und paralleler Anmeldung.
- [ ] Bedrohungsmodell, systematische Rechteprüfung und Backup-/Restore-Sicherheitsabnahme.

## Phase 4 – Entra/Outlook
- [x] Tenantgebundene OIDC-Anmeldung und verschlüsselte pro-Benutzer Graph-Refresh-Tokens.
- [x] Microsoft-Kalender lesend in den lokalen Kalender integriert; Plenaro-Kalender bleibt ohne Entra nutzbar.
- [ ] Echtes SSO-/Token-/Neubenutzer-/Lösch-/Wiederanmeldeszenario testen.
- [ ] Graph-Pagination, Mehrkalender, Termine erstellen/ändern/löschen, Free/Busy und Teilnehmer-/Teams-Einladungen ergänzen.
- [ ] Segmente mit Outlook-Kalender und tatsächlichen Verfügbarkeiten synchronisieren; erforderliche delegierte Schreibrechte gezielt prüfen.

## Phase 5 – Lokale Aufgaben, Kalender, Zeit und Migration
- [x] Aufgaben-CRUD, persönliche Timer, Zeitkorrekturen, Segmente, Drag-and-drop, lokale Konfliktsperren und Wochen-/Tagesplanung.
- [x] UL/AM/BR/HO-Marker, Web-Reminder bei geöffneter Seite, Monatsreport und CSV.
- [x] Kontrollierter Teilimport der Classic-SQLite-Daten inklusive historischer Archive/Segmente.
- [ ] Echten Classic-Datenbestand kontrolliert importieren und Rollback/Parität prüfen.
- [ ] Homeoffice-Workflows, Aufgabenfreigaben, Einladungen, Besitzerrechte und vollständige historische Datenmigration vervollständigen.
- [ ] Mobile-/Browser-Abnahme, Segment-Drag-and-drop, Berichte und Web-Push bei geschlossenem Browser.

## Phase 6 – Znuny/OTRS: aktueller Schwerpunkt
- [x] Server-Konfiguration, persönlicher Login und Vault; zentrale Delta-/Historien-Synchronisation und Wiederanlauf des Erstimports.
- [x] Agentenzuordnungen, Kandidaten-/Ausschlussfilter, indizierte Nachrichtenvolltextsuche und Arbeitsplatz-Ticketkategorien.
- [x] Classic-Ticketdetail mit Artikeln, HTML-Mail-Vorschau, Anhängen/CID-Bildern, Agenten, Timern, Segmenten und Antwortvorlage.
- [x] Serverseitige APIs für Antworten/Schließen/Wiederöffnen, Besitzer/Verantwortlichen, Kostenstelle/Auftrag und Zeitbuchungen; Tests/Fehlerdiagnose und Schutz gegen Doppelbuchung.
- [x] Native HTML-Browser-Einbettung mit zwei sicheren HTTPS-Origins, getrennten Cookie-Jars und serverseitigem Auto-Login; Chromium endgültig entfernt (PR #2–#6).
- [x] Falsch deklarierte Agenten-HTML-Navigation inklusive CGI-Query normalisieren, binäre Downloads erhalten, iframe vollflächig darstellen; PR #7 gemergt, CI #41635960052 erfolgreich.
- [ ] **Unmittelbar:** PR #7 lokal aktualisieren und verifizieren, dass Agenten-GUI tatsächlich als HTML (nicht als Rohtext) und auf voller Breite erscheint.
- [ ] Login, Menü, CSS, JS/AJAX, Formulare, Ticket-Deep-Link, Downloads und **zwei parallele Benutzer** auf der echten Znuny-Instanz testen.
- [ ] Echtes kontrolliertes Znuny-Testticket: TicketGet/SessionCreate, Empfängerauflösung, Antwortversand, Zuweisen, Schließen, Wiederöffnen und 15-Minuten-Zeitbuchung gegen die tatsächlich konfigurierte GenericTicketConnectorREST-Route durchtesten.
- [ ] Bei tatsächlichem HTTP 502, Timeout oder unbestätigtem Schreibstatus **keinen automatischen zweiten Schreibversuch** durchführen; den Remote-Effekt zuvor anhand Ticket-/Artikelhistorie prüfen.
- [ ] Kostenstelle/Auftrag-DynamicFields und optional fehlende Optionsroute anhand einer realen Znuny-Instanz abnehmen; Pending-Buchungsfreigabe für dauerhafte Unklarheiten entwerfen.
- [ ] Historische 365-Tage-Suche, Trefferindex, tatsächliche Status-/Owner-/Standard-Agenten-Semantik, Rate-Limits und Sync-Laufzeit prüfen.
- [ ] Alte WPF-Feldparität, Nachrichtentypen, Buchungen und Navigation mit realen Tickets abschließend vergleichen.
- [ ] Provider-Adapter für weitere Ticketsysteme, Incident/Problem/Change, Kundenportal und optional KI-Intake später ergänzen.

## Phase 7 – Wissen, KI, Erweiterungen und Freigabe
- [ ] Wiki-Anbindung/Ticket-Wiki-Treffer, dokumentenbasierte Suche und KI-Chat mit konfigurierbaren lokalen/OpenAI-kompatiblen Anbietern.
- [ ] Sichere Favoriten-/Integrations-Origins, Add-in-Store und Erweiterungsschnittstelle entwerfen.
- [ ] Ticketbenachrichtigungen, optional PWA/Web-Push, DSGVO/Audit/Monitoring und Betriebsdokumentation.
- [ ] End-to-End-Funktionsparität, vollständige Migration, Backups/Recovery, Versions- und Docker-Image-Releases sowie kommerzielle Lizenz endgültig abnehmen.

## Verbindlicher Vergleich mit dem WPF-Vorgänger
- Referenz: [project_tool](https://github.com/DerHausmeisterKrause/project_tool). Ticketdetail: `Views/TaskDetailPanel.xaml`, Ticket-WebView: `Views/TicketSystemView.xaml(.cs)`, Logik: `Services/TicketSystemService.cs`, Buchungen/Tests: `Tests/TicketTimeBookingAccountingTests.cs`, Ticket-Antwortvorlagen: `Services/TicketReplyTemplateRenderer.cs`.
- Web-Gegenstücke: `frontend/src/ClassicTicketDetailPanel.tsx`, `frontend/src/ZnunyWebPortalPage.tsx`, `frontend/src/ZnunyTicketConversationPanel.tsx`, `frontend/src/ZnunyTicketTimerPanel.tsx`, `src/Plenaro.Api/Znuny/ZnunyTicketRemoteEndpoints.cs`, `src/Plenaro.Api/Znuny/ZnunyNativeProxy.Http.cs`.
- Bei jeder neuen Funktion erst WPF-Verhalten/Felder/Tests lesen, dann für Web-/API-/Mehrbenutzerbetrieb fachlich gleichwertig umsetzen und automatisiert testen. Sicherheitsunterschiede von Web und WPF bewusst berücksichtigen.

## Unmittelbare Reihenfolge
1. Docker-Deployment des aktuellen `main` am Zielhost, inklusive `docker compose up -d --build --remove-orphans` (bestehendes DB-Volume/Vault erhalten).
2. Browser-Netzwerkprüfung: CGI-Navigation muss `Content-Type: text/html` liefern; Ticketseite, Skripte, Links, Formulare, Anhänge und volle Fläche prüfen.
3. Parallele persönliche Sessions und Auto-Login testen.
4. API-Remote-Schreibaktionen **getrennt** mit freigegebenem Testticket live prüfen; keine unkontrollierten Wiederholungen bei 502.
5. Erst nach Live-Abnahme verbleibende Classic-/Kalender-/Wiki-Funktionen in Phase 6/7 priorisieren.

**Achtung:** Historische erledigte Checklisten zum Chromium-/Screenshot-Modus in ROADMAP.md beschreiben ersetzte Lösungen und dürfen nicht als aktuelle Architektur interpretiert werden.
## 03.11.2026 – Favoriten-Wildcard (Phase 7, Vorbereitung)

- [x] WPF-Favoritenmodell und Browser-Login als Referenz ausgewertet.
- [x] Optionaler Host-Guard: unbekannte Wildcard-Hosts werden vor Plenaro-UI/API mit HTTP 404 abgewiesen; bekannte Origins bleiben unverändert.
- [x] NGINX `PLENARO_SERVER_NAME` akzeptiert bereits Wildcards; optionale `PLENARO_FAVORITES_BASE_DOMAIN`, DNS und TLS dokumentiert.
- [x] Administratorfreigabe, Favoriten-API, Vault-Zugänge und isolierter Browser-Proxy in PR #12 implementiert.
- [ ] Echte Wiki-/Webseiten und parallele Benutzer live prüfen.

## 03.11.2026 – Favoriten-Web-Apps (Phase 7, PR #12)

- [x] Admin-Apps und Benutzer-ACL im PostgreSQL-Schema; zentrale freigegebene HTTPS-Ziel-Origins
- [x] Benutzerfavoriten, Sortierung, persönliche verschlüsselte Zugänge, separater Browser-Reiter
- [x] Dedizierte App-Subdomains im vorhandenen NGINX-Wildcard-Host, individuelle serverseitige Sessions, CSRF und Origin-Isolation
- [x] CI für PR #12 erfolgreich (Run 41650719103).
- [ ] Echte Wiki-/Webseiten und parallele Benutzer auf dem Zielhost prüfen.
- [ ] Vollständige Legacy-Favoritenmigration und mehr unterstützte Authentifizierungstypen bei Bedarf



## 03.11.2026 – Znuny-Nachrichtenanzeige in verschachtelten Frames (PR #13)

- [x] Ursache im Znuny-HTML-Proxy korrigiert: `Content-Security-Policy: frame-ancestors` erlaubt jetzt sowohl die eigene Proxy-Origin (`'self'`) als auch den konfigurierten Haupt-Plenaro-Origin; fremde Origins bleiben gesperrt.
- [x] Regressionstest für verschachtelte Nachrichten-Frames und unveränderte Frame-Isolation ergänzt.
- [x] PR #13 nach erfolgreicher GitHub-CI gemergt ([CI #41653066707](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41653066707)).
- [ ] Auf dem Zielhost nach Deployment E-Mail-/Notiz-Inhalte im Ticket prüfen; gegebenenfalls Browser-Entwicklerkonsole und Response-Header der inneren iframe-URL gegenprüfen.


## 03.11.2026 – Znuny-Antworten und Aktionsfenster (PR #14)

- [x] Native Znuny-Fenster: Popups dürfen aus der eingebetteten iframe-Sandbox ausbrechen, sodass Antwort-/Notiz-/Weiterleitungsformulare normal funktionieren können; Parent-/Top-Navigation bleibt im eingebetteten Hauptfenster gesperrt.
- [x] Echte POST-Dokumentnavigationen können HTML korrekt auch bei fehlerhaftem upstream MIME-Typ anzeigen; AJAX/JSON-POST und Downloads bleiben unverändert.
- [x] Frontend- und Znuny-Regressionstests sowie vollständige GitHub-CI/Docker-Smoke erfolgreich: [PR #14](https://github.com/DerHausmeisterKrause/plenaro/pull/14), [CI #41654613158](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41654613158).
- [ ] Echten Antwort-Dialog, Notiz, Weiterleiten, Zuweisung und POST-Formular im Zielsystem live prüfen. Gegebenenfalls Browser-Popup-Freigabe und F12-Konsole/Netzwerk überprüfen.


## 09.11.2026 – Security-Fix: Queue- und Ticketrechte (Phase 6, PR #15)

- [x] Ursache bestätigt: zentraler technischer Znuny-Sync brachte alle Queues in einen Cache; Besitzer-/Kandidaten-ID genügte zuvor als Browser-Sichtbarkeitsregel.
- [x] Ticketlisten und Wochenplan-Agenda autorisieren nun per persönlicher Agenten-Session und Znuny TicketSearch; nur die Schnittmenge freigegebener Ticket-IDs wird ausgeliefert.
- [x] Direkte Ticket-ID-API inklusive Artikel, Anhänge, Planung und Ticketaktionen sowie native Ticket-Deep-Links zusätzlich durch persönlichen TicketGet abgesichert; keine Weitergabe über den globalen Service-Account.
- [x] Bei fehlender persönlicher Authentifizierung, nicht prüfbarem TicketSearch oder Zeitüberschreitung: 503 statt ungeprüfter Ticketfreigabe. Kein approved agent / keine eigenen Cachekandidaten: leere Liste.
- [x] Sichtbarkeit für maximal 60 Sekunden zwischengespeichert; Passwort-, Konto-/Konfigurationsänderungen invalidieren den Cache; direkte Ticket-Zugriffe prüfen live.
- [x] CI bestanden: Backend/Frontend, Znuny-Vertragstests und Docker-Smoke ([PR #15](https://github.com/DerHausmeisterKrause/plenaro/pull/15), [CI #42568973461](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42568973461)).
- [ ] **Live-Abnahme:** zwei Agenten mit unterschiedlichen Queue-Berechtigungen vergleichen, direkte Fremd-Ticket-IDs/Artikel testen, Queue-Wechsel und Rollenentzug prüfen.
- [ ] **Performance-Abnahme:** große Ticketmengen und Laufzeit der persönlichen TicketSearch kontrollieren; bei hohen Laufzeiten gezielt optimieren, ohne Rechtefilter aufzuweichen.

Die globale Rechteprüfung aus PR #15 wurde in PR #17 gezielt auf **neue unzugewiesene Tickets** begrenzt. Eigene/zugewiesene und geplante oder historisch bearbeitete Aufgaben dürfen ohne persönliche API-Rechteabfrage angezeigt werden. Live-Abnahme: [Issue #16](https://github.com/DerHausmeisterKrause/plenaro/issues/16).

## 09.11.2026 – Korrigierte Aufgabenrechte nach Nutzerfeedback (Phase 6, PR #17)

- [x] **Aktuelle Aufgaben:** Besitzer, Verantwortlicher, persönliche Planung und exakt auf lokales Konto passende Segment-Teilnehmer werden angezeigt – ohne störende globale TicketSearch-/Queue-503.
- [x] **Erledigte Aufgaben:** eigener Besitz, Verantwortung, Segmentbeteiligung oder nachgewiesene frühere Plenaro-Bearbeitung (Notizen, Zeiten, Buchungen) erlaubt die Anzeige; nicht pauschal alle geschlossenen Tickets.
- [x] **Heute / Wochenplanung:** eigene Segmente und Segmente mit der persönlichen Teilnehmer-E-Mail sichtbar.
- [x] **Neue Aufgaben:** Queue-Leserechte allein über den persönlichen Znuny-Agenten nachgewiesen. Pro Kandidaten-Queue wird ein unzugewiesenes Ticket mit persönlichem TicketGet geprüft; nicht belegte Queues bleiben verborgen. Bei defekter/mangelnder API-Anmeldung leere neue Aufgaben statt globalem HTTP 503 oder Freigabe aller Queues.
- [x] Ticket-ID-Endpunkte, Native-Deep-Links und lokale Planungs-/Detailrechte an diese Trennung angepasst.
- [x] Docker-Smoke testet den Fallback, Segment-Teilnehmer, eigene/verantwortete Tickets und historische Bearbeitung; Build/Contract/CI erfolgreich ([PR #17](https://github.com/DerHausmeisterKrause/plenaro/pull/17), [CI #42573584221](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42573584221)).
- [ ] Live mit zwei verschieden berechtigten Znuny-Agenten testen, besonders neue Tickets aus freigegebener vs. fremder Queue.
- [ ] Größere Queuelisten/zahlreiche TicketGet-Proben unter realer Last testen; maximal 60 Sekunden Queue-Proof-Cache und 35 Sekunden Gesamtbudget dokumentieren.


## 09.11.2026 – Favoriten-Bootstrap und Wildcard-Routing-Diagnose (Phase 7, PR #18)

- [x] React.StrictMode-Mount kann nicht mehr zwei konkurrierende einmalige Favoriten-Bootstraps starten; explizites Neuverbinden erstellt weiterhin neue Sitzung.
- [x] Tokenfreier Routing-Probe `/_plenaro/favorite-health` für App-Subdomains integriert; gibt ausschließlich `plenaro-favorite-proxy-ready` zurück, wenn Host/HTTPS-Weiterleitung beim richtigen Proxy ankommt.
- [x] Regressionstests für StrictMode (nur ein Start), Neuverbinden und HTTPS-Proxy-Routing; GitHub-CI inklusive Docker erfolgreich ([PR #18](https://github.com/DerHausmeisterKrause/plenaro/pull/18), [Run #42576527912](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42576527912)).
- [ ] Live-Alias der vorgeschalteten NGINX und Windows-DNS mittels `curl -i https://wiki.apps.example.org/_plenaro/favorite-health` abnehmen.
- [ ] Bei korrektem Alias-Health, aber fehlerhaftem Bootstrap: HTTP-Status des frischen Einmal-Links prüfen; danach Wiki-Aufruf/Anmeldung und Cookies untersuchen. **Niemals den Token in Issues oder Logs teilen.**


## 09.11.2026 – Phase 7: rückwärtskompatible Alias-URL-Vorlage (PR #19 in main)

- [x] Optionale Vorlage `PLENARO_FAVORITES_HOST_TEMPLATE={slug}-plenaro.example.org` implementiert; Standard bleibt unverändert.
- [x] Alte `slug.apps.example.org`-Favoriten bleiben bei gesetzter Base-Domain verfügbar; keine DB-/Credential-Migration.
- [x] Host-Sperre, Admin-App-Freigabe, Bootstrap, Link-Rewriting und tokenfreier Healthcheck für beide Hostformen abgedeckt.
- [x] Compose gibt PLENARO_FAVORITES_HOST_TEMPLATE an den Webcontainer weiter; im Docker-Smoke geprüft.
- [x] Contract-Tests für Alt-/Neu-Aliasse, Host-Isolation und fehlerhafte Templates ergänzt.
- [x] PR #19 nach erfolgreichem Build, Contract-Tests und Docker-Smoke in main gemergt ([CI #42589498554](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42589498554)).
- [ ] Echten Livehost mit Zertifikat, DNS, beiden Proxy-Layern und zwei Benutzern abnehmen.
- [ ] Betrieb: `PLENARO_SERVER_NAME`, DNS-Wildcard und vorgeschalteten NGINX nur **einmal** für den Alias-Bereich konfigurieren.
- [ ] Offene Phase-6-/Znuny-Punkte bleiben unverändert, insbesondere 502/Antwort-Formulare.


## 09.11.2026 – Phase 7: optionaler Pfadhost für Favoriten

- [x] `PLENARO_FAVORITES_PATH_ORIGIN=https://apps-plenaro.example.org` als opt-in und rückwärtskompatible Route.
- [x] Host-Isolation von Plenaro-Login und Znuny, Cookie-Pfad pro Favorit, serverseitige Rechte unverändert.
- [x] Pfad-Präfix für Bootstrap, GET/POST-Upstream und Umleitungen implementiert; Vertragstests ergänzt.
- [x] Frontend prüft den pfadgebundenen Bootstrap `/<slug>/_plenaro/favorite-bootstrap` auf die aktive Favoriten-ID/Slug, fremde Slugs werden abgelehnt.
- [x] PR #20 in main gemergt; CI inklusive Frontend-Regressions- und Docker-Smoke-Tests erfolgreich ([Run #42601040551](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42601040551)).
- [ ] Windows-DNS (nur ein A-Record), NGINX, Zertifikat, Host-Header sowie echtes Wiki/Cloud gegenprüfen.
- [ ] Sicherheit: gemeinsame Origin der Pfad-Favoriten und Einschränkungen bei AJAX/SSO/WebSockets/Service Workern live abnehmen.
- [ ] WPF-/Web-Parität, offener Znuny-502 und alle bisherigen Phase-1–7-Offenpunkte bleiben offen.


## 09.11.2026 – Phase 7: Ressourcen-Rewriting im Pfadhost (PR #21)

- [x] CSS-/JS-/HTML-Rootpfade samt JavaScript-Template-Strings, unquoted HTML-Attributen, srcset-Kandidaten und CSS-url()-Ressourcen erweitert; doppelte Präfixe vermieden.
- [x] Bekannte CSS/JS/JSON/SVG/WASM-Ressourcen erhalten bei generischem falschem Upstream-MIME den passenden Content-Type für nosniff.
- [x] /wiki wird auf /wiki/ normalisiert, damit relative Ressourcen in der Anwendung bleiben.
- [x] PR #21 nach erfolgreichem .NET-/Frontend-Build, Contract-Tests und Docker-Smoke in main gemergt ([Run #42604023749](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42604023749)).
- [ ] Echte Wiki-Ressourcen im Browser prüfen; bei weiterhin fehlenden Assets die konkrete URL, HTTP-Status und Konsolenfehler (ohne Tokens/Cookies) erfassen.
- [ ] Komplexe Laufzeit-URLs, externe CDNs, WebSockets, Service Worker und OAuth/SSO sind weiterhin nicht generisch garantiert. Sicherheitsgrenze einer gemeinsamen Browser-Origin bleibt bestehen.


## 09.11.2026 – Phase 7: HTTPS-Upstream-Zertifikatskette (PR #22 in main)

- [x] Docker-NGINX unterstützt optional PLENARO_TLS_CHAIN_PATH zusätzlich zu PLENARO_TLS_CERT_PATH (Leaf + Intermediates, nur bei gesetzter Datei).
- [x] Bestehendes Fullchain-Deployment bleibt unverändert; Warnung bei Leaf-only-Zertifikatsdateien.
- [x] Test beider Zertifikats-Varianten im echten NGINX-Container, .NET-/Frontend-/Znuny-Tests und Docker-Smoke erfolgreich ([Run #42623296351](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42623296351)).
- [x] PR #22 nach main gemergt; Diagnoseverfahren für TLS-Fehler 20 dokumentiert.
- [ ] **Betrieb:** tatsächliche Host-.env auf vollständige Chain verweisen lassen, erneuten Docker-NGINX-Start durchführen, Curl ohne -k auf Port 8443 und auf äußeren HTTPS-Host testen.
- [ ] Echte Wiki-/Favoriten-Browsersitzung und CSS/JS nach Behebung des 502 testen.


## 11.11.2026 – Phase 7: Wiki-Kontext und große CSS-/JS-Ressourcen (PR #23)

- [x] HTML-Metadaten für Confluence-Kontextpfade (`ajs-context-path`, `confluence-context-path`) im gemeinsamen Favoriten-Pfadhost `/<slug>/` umgesetzt, bestehende App-Pfade bleiben erhalten.
- [x] Separate, begrenzte 24-MiB-Obergrenze für text/css und JavaScript-MIME-Typen; HTML/API-JSON und sonstige Texte bleiben bei 6 MiB.
- [x] Regressionstests für Metadaten, Pfad-Idempotenz, Limits sowie .NET/Frontend, NGINX-TLS und Docker-Smoke grün ([Run #42986077812](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42986077812)).
- [x] PR #23 nach main gemergt.
- [ ] Echtes Wiki auf JS/CSS/Formulare/Logos prüfen; verbleibende 401/403/404/413-, CSP-/CORS- oder WebSocket-Fehler im Browser-Netzwerk-Tab feststellen, ohne Token/Cookies zu teilen.
- [ ] Nur wenn Pfadhost weiterhin inkompatibel bleibt: bevorzugt dedizierten Favoriten-Host statt ungesichertem Root-Fallback erwägen.


## 11.11.2026 – Phase 7: beschädigte Confluence-JavaScript-Bundles (PR #24)

- [x] Realen Fehler mit bereinigter HAR-Auswertung lokal reproduziert: Confluence `batch.js` enthält nach Plenaro-Rewriting eine ungültige JS-RegEx (beispielsweise `.replace(/"/wiki/g, ...)` statt `.replace(/"/g, ...)`); die Folgefehler `define is not defined` und `AJS is not defined` erklären fehlende UI-Elemente.
- [x] JavaScript und JSON werden vom Favoriten-Proxy jetzt unverändert ausgeliefert; HTML-Rewriting erhält Inline-`<script>`-Inhalte und korrigiert weiterhin externe JS-/CSS-/Bildpfade und Wiki-Kontextmetadaten.
- [x] Regressionstests auf JS-Regex-Literale und HTML-Skripte ergänzt; Gesamt-CI einschließlich Build, Contracts, NGINX-TLS-Test und Docker-Smoke grün ([Run #42990397008](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42990397008)).
- [x] [PR #24](https://github.com/DerHausmeisterKrause/plenaro/pull/24) nach main gemergt.
- [ ] Live-Nachtest mit frisch gestarteter Favoriten-Session (Confluence-Menüs, Logos, Buttons, Formulare, JS-Konsole). Hartkodierte absolute JS-URLs bleiben mögliche Pfadhost-Inkompatibilität und müssen bei Bedarf separat behandelt werden.


## 11.11.2026 – Phase 7: Confluence-Login im dedizierten Host (PR #25)

- [x] Lokale HAR-Analyse: Login-Seite unter `/wiki/login.action` HTTP 200, Atlassian-Plugin-Root-Requests `/rest/...` HTTP 401 und `/s/...` HTTP 404; kein echter Anmelde-POST im Mitschnitt.
- [x] Neues Opt-in `PLENARO_FAVORITES_DEDICATED_SLUGS=wiki` erlaubt dedizierten Host aus `PLENARO_FAVORITES_HOST_TEMPLATE` für Confluence, während der gemeinsame Favoriten-Pfadhost für alle anderen Apps erhalten bleibt. Fehlende Alias-Konfiguration führt für ausgewählten Slug zu Fehler statt stillem unsicherem Fallback.
- [x] Regressionen für gemischten Hostmodus, erwartete Hostnamen und Fehlkonfiguration sowie sämtliche CI-Schritte (Build, Znuny-Contracts, TLS, Docker-Smoke) grün ([Run #42995835637](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42995835637)).
- [x] [PR #25](https://github.com/DerHausmeisterKrause/plenaro/pull/25) nach main gemergt.
- [ ] Betreiber: dedizierten DNS-Alias und äußeren NGINX-vHost (HTTP-Bridge) einrichten; .env um selektive Dedicated-Slug-Option und Hostnamen ergänzen; Login live testen.
- [ ] Potenziell blockierende globale X-Frame-Options für den dedizierten Favoriten-Host gezielt korrigieren, sofern die Browser-Einbettung betroffen ist. Automatische Anmeldung über Confluence-SSO/JS-Formular weiterhin nicht implementiert.

### 11.11.2026 – Phase 7: Confluence-Login auf apps-plenaro.example.org/wiki/ (PR #26)

- [x] Bereinigten HAR geprüft: Wiki-Login-HTML 200, Confluence-Plugin-Root-REST 401 und -Asset 404, echter Passwort-POST nicht beobachtet
- [x] Opt-in PLENARO_FAVORITES_BROWSER_SHIM_SLUGS=wiki für browserseitige /wiki/rest/ und /wiki/s/ Korrektur ohne JS-Bundle-Veränderung
- [x] Cookie-Pfad /wiki, serverseitige ACL und gemeinsam genutzte Root-Routen unverändert; kein neuer DNS-Name/NGINX-Host nötig
- [x] Regressionstests, Backend- und Frontend-Build, TLS-Test und Docker-Smoke grün; PR #26 gemergt
- [ ] Auf Zielserver .env setzen, Web + Docker-NGINX neu erstellen und Browser-Anmeldung prüfen (SSO/MFA nicht durch Shim implementiert)

PR: https://github.com/DerHausmeisterKrause/plenaro/pull/26
CI: https://github.com/DerHausmeisterKrause/plenaro/actions/runs/43001365087

### 11.11.2026 – Phase 7: Confluence-Browser-Shim für Request-Objekte und dynamische Assets (PR #27)

- [x] Opt-in-Browser-Shim ergänzt: `new Request("/rest/...")` und native DOM-Eigenschaften `script.src`, `link.href`, `img.src` korrigieren bekannte Root-Routen auf `/wiki/rest/...` oder `/wiki/s/...`
- [x] Fremde Origins, andere Favoriten, Proxy-Cookies, vorhandene Zugriffsprüfung und externe JS-Bundles bleiben unverändert
- [x] Regressionstest für Shim-Einbindung und neue Abfangpunkte ergänzt
- [ ] GitHub-CI und PR-Merge bestätigen
- [ ] Auf Zielserver Deploy inkl. `PLENARO_FAVORITES_BROWSER_SHIM_SLUGS=wiki`, Browser-Netzwerk/Console prüfen; Benutzername-/Passwort-Eingabe und interaktiven Login wirklich abnehmen
- [ ] Wenn weiterhin fehlgeschlagen: neue bereinigte Browser-Netzwerkdaten (URL-Pfad/Status und JS-Fehler), nicht blind zusätzliche Proxy-Routen freischalten


### 11.11.2026 – Phase 7: Confluence blockierte Frames nach dem Login (PR #28)

- [x] Ursache im Quellcode gefunden: Favoriten-`frame-ancestors` enthielt nur Plenaro-Origin; Confluence kann nach Login eigene verschachtelte `/wiki/`-Frames laden.
- [x] Restriktiv `'self'` als zusätzlich erlaubten Vorfahren ergänzt, analog zu Znuny; kein globales `*`, kein Öffnen fremder Hosts, ACL/Cookies/CSRF unverändert.
- [x] Regressionstests für dedizierten Favoritenhost und gemeinsamen Pfadhost hinzugefügt.
- [x] GitHub-CI vollständig erfolgreich ([Run #43007066314](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/43007066314)); [PR #28](https://github.com/DerHausmeisterKrause/plenaro/pull/28) nach main gemergt.
- [ ] Live-Abnahme: Nutzername/Passwort absenden und echte Weiterleitung testen, Browser-Frame/Nachlade-Verhalten prüfen. Screenshot allein beweist keine erfolgreiche Anmeldung.
- [ ] Wenn Browser weiterhin blockiert: HTTP-Dokumentnavigation (Status, Location, X-Frame-Options, CSP) und Konsole anonymisiert erfassen; vHost-Header oder fremden Identity-Provider gesondert prüfen.

### 11.11.2026 – Phase 7: Confluence-Login-POST erfolgreich, Navigation in gesperrte Root (PR #29)

- [x] Bereinigte HAR-Metadaten analysiert: erfolgreicher `POST /wiki/rest/tsv/1.0/authenticate` mit HTTP 200 und `{"next":"/"}`, unmittelbar danach `GET /` HTTP 404 im Favoriten-Pfadhost
- [x] Erfolgreiche JSON-Antwort am konkreten Confluence-Anmelde-Endpunkt unter Opt-in `wiki` so korrigiert, dass `next` auf `/wiki/` beziehungsweise `/wiki/<ziel>` zeigt; andere JSON-Antworten und Fehlerstatus bleiben unverändert
- [x] Freigaberegeln, Authentifizierung, Cookie-Jar, Root-404 und fremde Favoriten unverändert; keine globalen Root-Redirects
- [x] Regressionstests für Erfolg/Fehler, Route, MIME, Slug, externe URLs und Idempotenz ergänzt
- [x] GitHub-CI erfolgreich ([Run #43009144307](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/43009144307)); [PR #29](https://github.com/DerHausmeisterKrause/plenaro/pull/29) nach main gemergt
- [ ] Zielhost: Docker-Web und Docker-NGINX aktualisieren, persönliche Session neu verbinden, Anmelde-POST 200 `{"next":"/wiki/"}`, navigierte Wiki-Seite und zwei Benutzer wirklich abnehmen

### 11.11.2026 – Phase 7: Confluence-Login-iframe navigiert noch auf Root, vorgelagerter Frame-Header (PR #30)

- [x] Bestehende HAR-Metadaten nochmals kontrolliert: erfolgreicher POST liefert `{"next":"/"}`; anschließend GET `/` als iframe mit Referer `/wiki/login.action` und 404
- [x] Einmaliger enger Root-Navigationsfallback implementiert: nur GET, iframe, same-origin, Referer exakt `/wiki/login.action`, Opt-in wiki und Shared-Pfadhost → 303 `/wiki/`; andere Root-Routen weiter 404
- [x] Diagnostik für Login-JSON-Rewrite und Root-Fallback über nicht-sensitive Header; Vertragstests für zulässigen und abgelehnten Browser-Routenfall
- [x] In HAR sichtbarer `X-Frame-Options: SAMEORIGIN`-Header zusätzlich als wahrscheinlicher Einbettungsblocker erfasst; laut Projektcode nicht durch Favoriten-API oder Docker-NGINX gesetzt
- [x] GitHub-CI erfolgreich ([Run #43011123820](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/43011123820)); [PR #30](https://github.com/DerHausmeisterKrause/plenaro/pull/30) nach main gemergt
- [ ] Auf echtem Zielserver nach Deployment prüfen, ob `X-Frame-Options` von vorgeschalteter NGINX gesetzt wird, und ggf. nur für Favoritenhost korrigieren; keine globalen Sicherheitsheader lockern
- [ ] Interaktiver Login, Folgeseite nach erfolgreichem POST sowie zwei getrennte Nutzer weiterhin offen

## 11.11.2026 – Phase 7: Wissensdatenbank als Wiki-Browser (PR #31)

- [x] WPF-`WikiBrowserView`, `WikiBrowserViewModel` und `WikiSettingsView` als Verhaltensreferenz ausgewertet: Standardquelle, Startseite, eingebetteter Browser, persönliche Zugangsdaten.
- [x] `/wiki` von einem Platzhalter zu einer funktionierenden Wissensdatenbank-Seite mit auswählbarer Wiki-Quelle, Neu-verbinden und persönlicher Proxy-Sitzung ausgebaut.
- [x] Einstellungen → Wiki: bestehenden Wiki-Favoriten als Standard wählen oder neue persönliche Wiki-Quelle aus adminfreigegebener Anwendung mit Startpfad und optionalem Browserlogin anlegen.
- [x] Adminseitige Freigabe einer HTTPS-Wiki-Origin mit optionaler Startseite und Benutzer-ACL eingebaut, ohne bestehende Favoriten-/NGINX-Konfiguration zu verändern.
- [x] Persönliche Standardquelle serverseitig unter `/api/v1/wiki/settings` gespeichert; Login + CSRF + Eigentümer/ACL-Prüfung und geschützter Favoriten-Bootstrap, vorhandenes Vault statt neuer Passwortspeicher.
- [x] Personal-Settings-JSONB-Merge verhindert unbeabsichtigtes Löschen des gewählten Wiki-Favoriten durch Theme-/Wochenbeginn-Änderungen.
- [x] Tests für bereits angebundene Quelle, neue Wiki-Quelle, Admin-URL-Prüfung, StrictMode, Wiederverbinden und Ablehnung fremder Proxy-URLs ergänzt.
- [x] GitHub-CI komplett erfolgreich ([Run #43015995547](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/43015995547)); [PR #31](https://github.com/DerHausmeisterKrause/plenaro/pull/31) nach main gemergt.
- [ ] Host-Abnahme der realen Confluence-Seite: Wiki-Einstellungen, persönliche Anmeldung, Navigation nach Reload und zwei getrennte Benutzer.
- [ ] Noch offen: REST-Wiki-Suche, Space-Key-Einschränkungen, Ticket-Wiki-Treffer, Dokumenten-/Attachment-Index und KI-Knowledge-Retrieval (WPF-Parität).

## 11.11.2026 – Phase 7: Admin-Wiki für alle, verpflichtender Auto-Login je Benutzer (PR #32)

- [x] Zielmodell überarbeitet: genau ein zentral durch Admin freigegebener Wiki-Server mit globalem Startpfad; keine individuellen Server-/Favoriten-Zieladressen für Benutzer.
- [x] Admin-API `GET/PUT /api/v1/wiki/admin/` mit RBAC und CSRF implementiert; vorhandene `favorite_apps`-Wiki-Origin wiederverwenden, für alle Benutzer freigeben, bestehende persönliche Favoriten/Secrets nicht löschen.
- [x] Benutzer-API für Wiki-Benutzername, verschlüsseltes persönliches Passwort und Entfernen; automatischer privater Wiki-Favorit, serverseitige User-ID und keine Secrets in GET.
- [x] Spezialisierter Wiki-Start nur bei gespeicherten Credentials und durch Confluence-JSON-Login bestätigter Anmeldung; bei 401/Captcha keine vermeintlich erfolgreiche Session; generischer Favoriten-Proxy unverändert.
- [x] Wiki-Browser und Wiki-Einstellungen für Admin-/Benutzer-Trennung umgebaut; keine manuelle Wiki-Favoriteneinrichtung mehr erforderlich.
- [x] Frontend-Regressionen und synthetische Confluence-Login-/URL-Validierungs-Vertragstests ergänzt; keine echten Credentials oder HAR-Daten in GitHub.
- [x] CI vollständig erfolgreich ([Run #43018570330](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/43018570330)); [PR #32](https://github.com/DerHausmeisterKrause/plenaro/pull/32) nach main gemergt.
- [ ] Echte Confluence-Autoanmeldung (inkl. Cookies, Logout, abgelaufenem Passwort) sowie parallele Benutzer auf Zielhost live abnehmen.
- [ ] WPF-Restfunktionen Wiki-Suche, Ticket-Wiki-Treffer, Dokumenten- und KI-Index später.

## 12.11.2026 – Phase 5/7: Bestätigte BR-Beantragung per persönlicher E-Mail (PR #33)

- [x] Desktop-Verhalten geprüft: `HO` löst im WPF `ToggleHomeOfficeAsync` mit Bestätigung, Outlook-Termin und `SendHomeOfficeMail` an bis zu zwei Empfänger aus; `BR` bleibt separater Marker.
- [x] Benutzerwunsch ausdrücklich als abweichende Web-Semantik umgesetzt: erstmaliges Setzen von `BR` zeigt „Willst du wirklich für den DD.MM.YYYY Bereitschaft beantragen?“; „Nein“ speichert/sendet nichts.
- [x] Bei „Ja“ BR-Marker für den Tag speichern und erst dann einen E-Mail-Versuch an selbst konfigurierte Empfänger starten; alte BR-Tage und UL/AM/HO senden niemals ungefragt.
- [x] `Einstellungen → Aufgaben & Kalender`: bis zu zwei persönliche Mail-Empfänger, serverseitige E-Mail-Validierung, getrennte Speicherung je Benutzer und JSONB-Merge.
- [x] Persönliches Microsoft-Graph `Mail.Send` (optional über explizite OAuth-Neuverbindung); Mailtext „Servus, ich mache am DD.MM.YYYY Homeoffice. mfg. Name“, obwohl BR auslöst – ausdrücklich vom Nutzer so vorgegeben, fachlich noch zu klären.
- [x] Neue Migration `0028_br_mail_requests.sql`, persönliche Idempotency-Key-Reservierung (user_id+day), kein automatischer Doppelversand bei 202 oder unbekanntem Status.
- [x] Automatisierte Tests für Bestätigung/Abbruch, zwei Empfänger, Mailtext, Graph-202/403, Wiederholsperre und Empfängervalidierung ergänzt.
- [x] GitHub-CI vollständig erfolgreich ([Run #43166817977](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/43166817977)); [PR #33](https://github.com/DerHausmeisterKrause/plenaro/pull/33) nach main gemergt.
- [ ] Live-Abnahme: Outlook-Neueinwilligung Mail.Send, Gesendete Elemente, zwei Benutzer, bei Timeout keine Wiederholung.
- [ ] Fachliche Klärung: BR-Bereitschaftsmarker mit ausdrücklich gewünschtem Homeoffice-Text.

## 12.11.2026 – Korrektur Homeoffice-Mail: HO/BR, Ja/Nein und Microsoft-Verbindung (PR #34)

- [x] Ursache der fehlenden Mail beim HO-Marker gefunden: bisher hat nur BR die Mail ausgelöst; WPF verwendete ausdrücklich HO.
- [x] Auch beim erstmaligen **HO**-Marker (BR bleibt kompatibel) wird nach „Willst du wirklich am TT.MM.JJJJ Homeoffice beantragen?“ gefragt.
- [x] Eigener responsiver Dialog mit **„Ja, beantragen“** und **„Nein“**, statt nicht lokalisierbarem `window.confirm`.
- [x] Bei Nein keine Änderung und kein Mailversuch.
- [x] Bei Ja Vorabprüfung von persönlichen Empfängern und delegiertem Microsoft-365-Token inklusive `Mail.Send`; wenn ungültig, **kein** neuer HO-/BR-Marker und Fehlermeldung statt scheinbar erfolgreicher Beantragung.
- [x] Bei freigegebenem Konto Marker speichern und danach genau einen Mailversuch per Graph; vorhandener Doppelversandschutz bleibt erhalten.
- [x] In Benutzer-Einstellungen die Microsoft-Verbindung anzeigen. Der Button zum erstmaligen Verbinden erscheint nur bei fehlendem gespeicherten Graph-Token, nicht pauschal bei jedem Benutzer.
- [x] Ergänzte Frontendtests für HO, BR, Ja/Nein, Vorabprüfung und M365-Status.
- [ ] Auf dem Zielhost prüfen, ob delegiertes `Mail.Send` tatsächlich erteilt wurde und ein gültiges persönliches Token vorhanden ist; Graph 202 und E-Mail im Ordner „Gesendete Elemente“ prüfen.
- [ ] Zeitfenster, Ausfallszenarien und zwei parallele Benutzer real testen; keine unkontrollierten Mehrfachsendungen.

- [x] [PR #34](https://github.com/DerHausmeisterKrause/plenaro/pull/34) nach `main` gemergt; [GitHub-CI #43199978295](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/43199978295) vollständig erfolgreich (Backend, Frontend, Tests, Docker-Login). Der echte Microsoft-Graph-Mailversand am Zielhost ist noch offen.

## 12.11.2026 – Zeitreport: vollständige OTRS-Zeitbuchungen und lokale Buchungsjournal-Übernahme (PR #35)

- [x] WPF-`ReportsViewModel` mit Web-`ReportsEndpoints` abgeglichen: Der Webreport verwendete nur lokale Timer/Korrekturen und ignorierte alle Znuny-Buchungen.
- [x] Offizielle Znuny-7-`TimeAccountingGet`-Operation für jeden gebundenen Agenten angebunden (REST `TimeAccountingREST`, persönliche Credentials, nur freigegebene HTTPS-Origin, sicheres Response-Limit).
- [x] Serverseitige Buchungszeit in Sekunden aus `TimeUnit` (Minuten, einschließlich negativer Korrekturen), Monatsgrenzen Europe/Berlin.
- [x] Lokal bestätigtes `znuny_ticket_time_bookings`-Journal mit der vollständigen OTRS-Buchungsliste abgeglichen (Ticketnummer + Zeitwert mit Vielfachzählung); fehlende lokale bestätigte Buchungen ergänzen, übertragene Buchungen nicht doppelt zählen.
- [x] Zeitreport-UI und CSV um OTRS-Buchungspositionen, Remote-Summe, nur-lokal-Summe und sichtbare Unvollständigkeitswarnung erweitert. Lokale Aufgaben-Timer getrennt, keine ungeprüfte Doppelsumme.
- [x] Znuny-Admin-Einstellungen um sicheren Namen des TimeAccounting-Webservices ergänzt, ohne vorhandene Ticket-API-Routen zu ändern.
- [x] Synthetische Tests für REST-Request, Benutzerbindung, negative Remote-Zeitwerte, doppelte Einträge und fehlende lokale Buchungen ergänzt.
- [x] [PR #35](https://github.com/DerHausmeisterKrause/plenaro/pull/35) nach main gemergt; [CI #43226009165](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/43226009165) vollständig erfolgreich (Backend, Frontend, Vertrags- und Docker-Tests).
- [ ] Znuny `TimeAccountingREST` + `timeaccounting_webservice` (rw) bei der konkreten Instanz einrichten; Live-Test mit direkten OTRS-Zeiten, Plenaro-Buchungen und verschiedenen Benutzern.
- [ ] Noch offen: WPF-Monats-Nachbuchung als separater Web-Datensatz und optionale Übersicht lokaler, noch nicht nach OTRS übertragener Ticket-Timer.

## 17.11.2026 – Phase 5: Zeit-Schnellkorrektur im Znuny-Ticket wiederhergestellt (PR #36 – gemergt)

- [x] WPF-Referenz geprüft: `Views/TaskDetailPanel.xaml` enthält zwei Reihen +15/+30/+1h sowie -15/-30/-1h, verbunden mit `TodayViewModel.AdjustBookedMinutes`; `TaskService.AddTicketMinutes` begrenzt negative Zeiten bei 0.
- [x] Dieselben sechs Presets in `ZnunyTicketTimerPanel` direkt unter Start/Stop wiederhergestellt, positive Buttons grau, negative rot.
- [x] Über `PUT /api/v1/znuny/tickets/{ticketId}/time` jeweils ausschließlich **ungebuchte Sekunden** korrigieren; bereits bestätigte Transfers nach Znuny bleiben unverändert. Keine unbeabsichtigte neue OTRS-Zeitbuchung.
- [x] Vorhandene Sekunden beim Hinzufügen/Abziehen ganzer Minuten erhalten; Untergrenze 0, Obergrenze 240 Stunden.
- [x] Während API-Änderungen, manueller HH:MM:SS-Eingabe, beim Laden und bei offenen ungeklärten OTRS-Zeitbuchungen Presets sperren.
- [x] Frontendtests für alle sechs Schaltflächen, korrekte API-Nutzlast, laufendes Buchungsjournal, Grenzwerte und Pending-Sperre ergänzt.
- [x] [PR #36](https://github.com/DerHausmeisterKrause/plenaro/pull/36) erfolgreich gemergt; [CI #44032906303](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44032906303) vollständig grün.
- [ ] Live-Test nach Docker-Deployment auf einem echten Znuny-Ticket; insbesondere laufender Timer und bestätigte Altbuchungen.

## 17.11.2026 – Phase 5/7: Angepinnte Tickets und globaler OTRS-Report-Schalter (PR #37 – gemergt)

- [x] WPF-Referenz `TodayViewModel.TogglePinTaskCommand` + `TaskService.SetPinned` abgeglichen: Pin-Status persistent, angepinnte Tickets kommen zuerst.
- [x] Neue PostgreSQL-Migration `0029_znuny_ticket_pins.sql`: Pins pro Benutzer und Ticket getrennt, bei Ticket-/Benutzerlöschung automatisch bereinigt.
- [x] Authentifizierter CSRF-geschützter `PUT /api/v1/znuny/tickets/pins`-Endpunkt: Nur eigene aktuelle, zugewiesene Tickets anpinnen; eigene Pins wieder entfernen; kein Schreibzugriff auf OTRS.
- [x] Zentrale `/api/v1/znuny/tickets`-Sortierung `mine`: angepinnte Tickets **vor** Sortierung/Paginierung, danach bisherige Filter-/Sortierreihenfolge. Pin-Kennung wird pro Benutzer zurückgegeben.
- [x] Eigener Pin-Schalter mit Zustandsanzeige unter **Aktuelle Aufgaben** und **Meine Tickets**, keine ineinander verschachtelten Buttons; Benutzer-Pins beeinflussen keine andere Person.
- [x] Globale `workspace.general.fetchOtrsAccounting`-Einstellung **nur vom Admin änderbar**, Standard aktiv zur Wahrung des bisherigen Betriebsverhaltens.
- [x] Deaktiviert: Report lädt **nur** lokale bestätigte Plenaro-Ticketbuchungen und getrennt die lokalen Timer; keine Znuny-TimeAccountingGet-Anfrage, kein Lesen persönlicher Znuny-Token oder API-Konfiguration.
- [x] Aktiviert: Vorhandene Live-Abfrage `TimeAccountingGet` und das deduplizierte Zusammenführen mit Plenaro-Buchungen unverändert.
- [x] JSON- und CSV-Monatsreport verwenden dieselbe serverseitige Einstellung und denselben Auswertungspfad; Browser meldet einen administrativ deaktivierten OTRS-Abgleich als normalen Betriebszustand, nicht als Verbindungsfehler.
- [x] UI-Integrationstests für Anpinnen/Abpinnen und Admin-Schalter ergänzt.
- [x] [PR #37](https://github.com/DerHausmeisterKrause/plenaro/pull/37) nach `main` gemergt; [CI #44035220540](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44035220540) vollständig erfolgreich (Build, Frontend-Tests, Vertrags- und Docker-Smoke-Tests).
- [ ] Live-Test: 2 Benutzer, Pin-Reihenfolge auch über Seiten hinweg; Report mit Schalter an/aus vergleichen und **Netzwerk-/Znuny-Logs** prüfen, dass bei Aus wirklich kein OTRS-Zeitabruf erfolgt.

## 17.11.2026 – Persönliche Favoriten ohne Admin-Freigabe, Wiki unabhängig (PR #38 – gemergt)

- [x] WPF-Favoritenprinzip umgesetzt: jeder Benutzer kann eigenständig Webseite, vollständige HTTPS-URL, Name, optionalen Login, Aktivierung und individuelle Reihenfolge bestimmen.
- [x] Favoriten-Editor von Admin-Anwendungskatalog auf `/api/v1/favorites/personal` umgestellt. Keine Webseite muss vorab durch einen Administrator angelegt oder benutzerbezogen freigegeben werden.
- [x] Ein persönlicher Favorit besitzt seinen eigenen zufällig abgeleiteten Proxy-Slug (`site-<favorite UUID>`). Der HTTPS-Zielserver bleibt je Favorit getrennt und nachträglich bearbeitbar.
- [x] Persistentes benutzereigenes Verschieben über `PUT /api/v1/favorites/personal/order`, serverseitige Eigentümerprüfung, Transaktion und CSRF.
- [x] Sicherheit: nur HTTPS mit DNS-Host, keine URL-Credentials, Blockade von Selbst-Proxies, internen/privaten TCP-Zielen und DNS-Rebinding. Die benutzerdefinierte Webseite erhält keine Administrations- oder andere Plenaro-Rechte.
- [x] Migration `0030_personal_favorites_independent_wiki.sql`: frühere freigegebene Benutzerfavoriten in persönliche Apps umhängen (bereits vertrauenswürdig freigegebene interne Origins bleiben auf exakt der bisherigen Adresse erreichbar; beim Wechsel der Origin entfällt diese Ausnahme), bestehende Fav-IDs/Anmeldenamen/Sortierung/Vault-Secrets erhalten; alte Admin-ACLs werden für die eigenen Webseiten nicht mehr benötigt.
- [x] **Wissensdatenbank wird niemals mehr als `user_favorites`-Eintrag angelegt.** Bestehende Wiki-Benutzer werden in `wiki_user_connections` übernommen, während Serveradresse weiter ausschließlich vom Wiki-Admin konfiguriert wird.
- [x] Wiki-Login nutzt weiterhin die bestehende `FavoriteProxy.StartAsync(...,requireWikiAutoLogin:true)`-Verifikation mit eigenem CookieContainer und genau einer Benutzer-Sitzung. Die Wiki-Proxy-Autorisierung verwendet jetzt eigenständige Wiki-Verbindungen statt Favoritenfreigaben.
- [x] Vorhandene AES-256-GCM-Wiki-Passwörter werden über ihren ursprünglichen Namen gelesen und bei neuer Benutzung unter `wiki.password` neu verschlüsselt; kein unsicheres Umbenennen des Ciphertexts.
- [x] Favoriten und Wissensdatenbank in den Einstellungen getrennt, globale Freigabe-UI für Webseiten entfernt.
- [x] Synthetische UI- und HTTP-Smoke-Tests für eigene Webseiten, URLs, Reihenfolge, CSRF und bestehendes Favoriten-Frame ergänzt.
- [x] [PR #38](https://github.com/DerHausmeisterKrause/plenaro/pull/38) nach `main` gemergt; [CI #44040160724](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44040160724) vollständig erfolgreich inklusive Frontend/Backend, Znuny- und Proxy-Vertragstests, Datenbankmigration und Docker-Smoke-Tests.
- [ ] Live-Abnahme mit zwei Benutzern, bestehenden Favoriten/Passwörtern, Wiki-Bootstrap und URLs mit komplexen JS-/CSP-Pfaden durchführen.
- [ ] Private Intranet-Webseiten benötigen weiterhin einen gesonderten, ausdrücklich freigeschalteten sicheren Netzwerkpfad; freie persönliche Proxy-Ziele erlauben aus SSRF-Schutzgründen nur öffentlich routbare HTTPS-IPs. Das zentral angebundene interne Wiki bleibt davon unberührt.

## 17.11.2026 – Fix: weiße persönliche Favoriten-Seiten (PR #39 – gemergt)

- [x] Ursache im Proxy nach PR #38 geprüft: Wiki besitzt eigene Pfadkompatibilität; persönliche `site-*`-Favoriten bekamen keine Shim-Injektion für origin-root `fetch/XHR`, `Request`, `script.src` oder dynamische Styles/Images. Viele moderne Single-Page-Apps laden deshalb unter dem Shared-Pfad keine Ressourcen.
- [x] Dynamische root-relative Browser-URLs werden für persönliche Seiten auf den aktuellen `/site-<id>/`-Pfad abgebildet. Bereits korrekt präfixierte URLs, externe Origins und die internen `/_plenaro/`-Bootstrap-Endpunkte werden nicht umgeschrieben; separate Wiki-Shim-Regeln bleiben erhalten.
- [x] Statt leerem Frame bei 401/403 (abgelaufene/entzogene Sitzung) und Proxy-502/504/413 (gesperrte private Netzwerkadresse, Timeout, externer Redirect, zu große Antwort) ein lesbares, begrenztes Fehlerdokument ausgeben; für CSS/JS bleibt der Fehler leer und mit korrektem Status.
- [x] Im Favoriten-Tab die Funktion **Originalseite öffnen** in einem neuen Tab ergänzen, wenn eine fremde Seite im iframe nicht eingebettet werden kann. `noopener noreferrer` schützt den Plenaro-Tab.
- [x] Contract-Tests für dynamische Pfade, unverändertes Wiki, HTML-Fehlerstatus und korrekte nicht-HTML-Ausgaben ergänzt. Frontendtest für Original-Link.
- [x] [PR #39](https://github.com/DerHausmeisterKrause/plenaro/pull/39) nach `main` gemergt; [CI #44044531957](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44044531957) nach einem erneuten Lauf vollständig erfolgreich (Build, Frontendtests, Znuny-/Proxy-Vertragstests, NGINX und Docker-Smoke).
- [ ] Live-Test mit echter interner/public HTTPS-Seite, unterschiedlichen SPAs, CSP/X-Frame-Options des äußeren NGINX und 2 Benutzerkonten. Manche fremden Websites können selbst mit Korrekturen keine eingebettete Ansicht unterstützen (SSO, CSP, CDN, JavaScript, Anti-Bot).
- [ ] **Sicherheitspriorität [Issue #40](https://github.com/DerHausmeisterKrause/plenaro/issues/40):** Benutzergesteuerte Favoriten auf dedizierte Browser-Origins migrieren. Der Shared-Path-Host trennt Browser-Cookies nach Pfad, aber keine Same-Origin-JavaScript-Zugriffe zwischen verschiedenen Pfaden. Bis zur vollständigen Origin-Isolation keine beliebigen fremden Skript-Webseiten als vertrauenswürdig einstufen.

## 17.11.2026 – Persönliche Intranet-Webseiten ohne .env-Freigabe (Phase 7)

- [x] WPF-Referenz erneut geprüft: Webfavoriten waren für interne Webseiten vorgesehen.
- [x] PR #41 mit exakter Betreiber-Freigabeliste wird funktional ersetzt: Im HTTPS-Favoriten-Proxy benötigen eigene `site-*`-Webseiten ab jetzt keine zusätzlichen `.env`-Origins mehr.
- [x] Der bestehende sichere TCP-Handler akzeptiert RFC1918-LAN und IPv6-ULA, blockiert weiterhin Loopback, Link-Local, bekannte Metadaten-IPs, Multicast und nicht spezifizierte Ziele; HTTPS-Zertifikatsvalidierung und Eigentümer-/Sitzungsprüfungen bleiben aktiv.
- [x] Überflüssige Allowlist-Datei, Compose-Variable und `.env`-Beispiele entfernt; Vertragsprüfungen für private und gesperrte Ziel-IPs ergänzt.
- [x] [PR #42](https://github.com/DerHausmeisterKrause/plenaro/pull/42) nach `main` gemergt; [CI #44050034477](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44050034477) erfolgreich im zweiten Lauf; erster Lauf hatte einen flüchtigen Fehler in einem HO-Frontendtest.
- [ ] Produktionsabnahme: Interne und öffentliche HTTPS-Favoriten, CSS/JS, Anmeldungen, Wiki und zwei getrennte Benutzer testen.
- [ ] **Bewusstes Sicherheitsrisiko:** Jeder Plenaro-Nutzer kann serverseitig interne HTTPS-Dienste erreichen; Firewall/Segmentierung und vertrauenswürdige Benutzer sicherstellen. Dedizierte Browser-Origin je frei angelegtem Favoriten bleibt als [Issue #40](https://github.com/DerHausmeisterKrause/plenaro/issues/40) offen.

Historie: Die Origin-Allowlist aus [PR #41](https://github.com/DerHausmeisterKrause/plenaro/pull/41) ist durch diese Änderung abgelöst.

## 17.11.2026 – Persönliche Favoriten: Bilder und Login-Kompatibilität (PR #43 – gemergt)

- [x] Favoriten-Proxy auf dynamische Bildquellen, initiale/JS-generierte Login-Routen, fehlende Same-Origin-Auth-/CSRF-Header, Bild-MIME und unkontrollierten Referer-Teilstring geprüft.
- [x] Gewöhnliche Bild-/Font-CDNs unter HTTPS erlaubt; dynamische persönliche `site-*`-Bildquellen, Original-Origin-URLs, `form.action`, SPA-History und `innerHTML` auf persönlichen Proxy-Pfad korrigiert; zentralen Wiki-Shim nicht erweitert.
- [x] Scoped iframe-Login-GETs unter persönlichem Favoriten wieder erreichbar; andere Favoriten, Root-POST, unbefugte Origins und Bootstrap-Routen bleiben isoliert.
- [x] Contract-Tests, dynamischer JavaScript-Laufzeittest des Browser-Shims und Betriebsdokumentation ergänzt.
- [x] [PR #43](https://github.com/DerHausmeisterKrause/plenaro/pull/43) gemergt; [CI #44053732245](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44053732245) vollständig grün.
- [ ] Betroffene Webseiten wie ATOSS live mit Bildern und Anmeldung abnehmen; externe SSO-/MFA-Systeme sind nicht automatisch kompatibel.
- [ ] Sicherheitspriorität [Issue #40](https://github.com/DerHausmeisterKrause/plenaro/issues/40) bleibt offen.


## 17.11.2026 – Klassische Loginfelder / ATOSS-Kompatibilität (Phase 7; PR #44 gemergt)

- [x] WPF-Referenz WebView2-Laufzeitautofill mit DOM-/Frame-Erkennung gegen aktuelle Web-Implementierung verglichen: die Web-Variante füllt im Browser derzeit keine Felder aus.
- [x] Sicheres serverseitiges HTML-Login unterstützt jetzt weitere Username-Merkmale, Hidden-CSRF- und Submit-Felder, Same-Origin-Loginlinks und bounded GET-Weiterleitungen.
- [x] Ausschluss von fremden URLs, JS-only, SSO, Passwortänderungs-/Registrierungsformularen; gespeicherte Klartextpasswörter bleiben vom HTML-Proxy fern.
- [x] Synthetic Contract-Tests und verständliche Einschränkungen in den Einstellungen und der Betriebsdokumentation ergänzt.
- [x] [PR #44](https://github.com/DerHausmeisterKrause/plenaro/pull/44) nach `main` gemergt; [CI #44058573710](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44058573710) vollständig erfolgreich (Frontend-/Backend-Build, Login-Vertragstests, NGINX und Docker-Smoke). npm `csstype` auf veröffentlichte Version 3.2.3 fixiert.
- [ ] Reale ATOSS-Abnahme; Login-Varianten außerhalb klassischen HTML bleiben offen.
- [ ] **Issue #40**: echte Browser-Origin-Isolation vor einer browserseitigen Passwort-Autofill-Funktion.


## 17.11.2026 – ATOSS-DOM-Autofill aus dem WPF-Vorgänger (Phase 7; PR #45 gemergt)

- [x] Ursache der fehlenden Eingabefüllung konkret belegt: bisheriger HTML-Proxy hat keinerlei WPF-äquivalentes Browser-DOM-Autofill, nur serverseitigen Formular-POST.
- [x] `z-focustextbox-real`, `Benutzername`/`Kennwort`, native Input-Setter, Events, Browser-Nachladen, sichere Submit-Auswahl und One-Shot-Reconnect in dedizierten persönlichen Hosts übernommen.
- [x] Bestehende Wildcard-Aliasse für neue persönliche Favoritensitzungen bevorzugt; ohne Alias kein Credential-DOM-Injection und erklärender Hinweis; zentraler Wiki-Host bleibt erhalten.
- [x] Verschlüsselten Vault-Secret nur nach aktiver Benutzer-/Favoritenprüfung auf dedizierten HTTPS-Host injizieren, niemals auf Shared-Origin. Host-only Proxy-Cookies und `Origin-Agent-Cluster` bleiben erhalten.
- [x] C#- und Browser-Laufzeitkontrakte sowie Dokumentation ergänzt.
- [x] [PR #45](https://github.com/DerHausmeisterKrause/plenaro/pull/45) in `main` gemergt; [CI #44062037953](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44062037953) vollständig erfolgreich (Backend, Frontend, Browser-DOM-/Frame-Tests, Proxy-/Znuny-Verträge und Docker-Smoke).
- [ ] Realer ATOSS-Test im Zielnetz: HTTPS-Wildcard, DNS, eigener Origin, Login-Felder und anschließende Authentifizierung; bei externem SSO/MFA bleibt der passende Adapter offen.
- [ ] Issue #40: ältere/nicht isolierte Shared-Path-Favoriten bleiben ein gesondertes Sicherheitsproblem.
