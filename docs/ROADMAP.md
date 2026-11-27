## 27.11.2026 – Phase 7/7: ATOSS-Login-Iframe

- [x] Live-Log: Shared-DOM-Opt-in aktiv und einmalige Injektion gemeldet, weitere HTML-Dokumente geladen; tatsächliche Felderkennung/Anmeldung nicht nachgewiesen.
- [x] ATOSS-Einstieg `/SES/html` von generiertem `/SES/uuid…/html` getrennt; einmalige Shared-DOM-Injektion nur im eigentlichen ZK-Dokument.
- [x] Native Fokus-/Blur-Ereignisse für ZK; feste, geheime-freie Browserdiagnose für Start, Felder, Ausfüllen, Absenden und Timeout.
- [x] Routing- und DOM-Vertragstests sowie echter Chromium-Test mit Produktionsskript und verschachteltem synthetischem ATOSS-Iframe ergänzt.
- [ ] CI, Merge, produktive Abnahme. Keine neuen Container, DNS-Einträge oder Datenbankmigrationen. Shared-Origin-Risiko bleibt unverändert.

---

## 27.11.2026 – Phase 7/7: Favoriten-Öffnungsmodus und alternative Anmeldung

- [x] WPF `WebShortcutBrowserSessionManager` als Referenz geprüft; vorhandene DOM-Logik wiederverwendet.
- [x] Externen Browser als Checkbox im Editor und direkten Link beim Öffnen abbilden; vorhandenen Modus und Sidebar-Link beibehalten.
- [x] Explizite, standardmäßig deaktivierte persönliche Shared-Origin-DOM-Option, Warnung und Bestätigung; nur einmal pro Proxy-Sitzung auf Browsernavigation. Keine zusätzliche parallele Server-Anmeldung.
- [x] Migration und Frontend-/Backend-Vertragstests ergänzt. Zentraler Wiki-Login unverändert.
- [ ] CI, Merge und produktiver ATOSS-Test. Shared-Origin-Opt-in kann Zugangsdaten anderen Webseiten auf demselben Host offenlegen; keine Sicherheitsisolierung.

---

## 27.11.2026 – Phase 7/7: Wiki-Bereichsnavigation

- [x] Netzwerkprotokoll bestätigt: `GET /wiki/rest/ia/1.0/spacesmenu` liefert JSON mit HTML im Feld `template`; Bereichslinks und Logo-Pfade ohne /wiki erzeugen HTTP 401.
- [x] Nur erfolgreiche JSON-Antwort dieses zentralen Wiki-Endpunkts und nur dessen HTML-Template mit dem bestehenden HTML-Rewriter korrigieren. Keine generische JSON-Umschreibung, keine Browser-Shim-Erweiterung.
- [x] Sitzungsdiagnose unterscheidet fehlendes Cookie, fehlende Sitzung, Ablauf, falschen Pfad und ersetzte Sitzung ohne URLs oder Geheimnisse.
- [x] Vertragstest mit synthetischem Menü, Logo, externem Link, bereits korrektem Pfad und Endpoint-Gating.
- [ ] CI und produktive Abnahme. Ablaufzeiten, Cookies, Anmeldung und andere Favoriten unverändert.

---

## 27.11.2026 – Phase 7/7: Zentraler Znuny-Lesecache

- [x] Classic-Referenz `Services/TicketDetailCacheService.cs` und `Models/ZnunySyncPolicy.cs` berücksichtigt.
- [x] Ticketdetails mit Nachrichten, MIME-Body, Anhangsmetadaten und aktuellen Buchungsfeldwerten zentral in PostgreSQL speichern. Keine Anhangsdateien oder persönlichen Zugangsdaten im Cache.
- [x] Agenten und Buchungsoptionen gemeinsam für berechtigte Benutzer zwischenspeichern (5 Minuten); die Hintergrundsynchronisierung füllt jeweils bis zu vier Ticketdetails pro Zyklus, offene und kürzlich angeforderte Tickets zuerst.
- [x] Normale Cache-Treffer und lokales Tagesbuchungslimit vom bisherigen Znuny-Leselimit trennen. Nur tatsächliche interaktive Nachladevorgänge begrenzen (120 pro Benutzer/5 Minuten, bis zu sechs parallel pro Webprozess).
- [x] Berechtigungsfilter, personenbezogene Lesemarkierungen, Live-Anhänge, persönliche Schreibzugriffe, Buchungsvalidierung und explizite Nachrichten-/Agentenaktualisierung erhalten. Cache nach bestätigten Schreibvorgängen invalidieren; alte laufende Abrufe dürfen den invalidierten Stand nicht überschreiben.
- [x] Cache an Quelle, Servicekonto und Konfiguration binden; ohne Servicekonto persönliche Live-Abfragen beibehalten. Letzten Stand bei vorübergehendem Fehler kennzeichnen.
- [x] PostgreSQL-Regressionstest mit simuliertem Znuny, 150 Cache-Treffern, Aktualisierung, Buchungsfeldern, Konto-/Quellenwechsel und ungültigen Snapshots ergänzt.
- [ ] Vollständige CI und produktive Abnahme. Bestehender Artikelumfang und Berechtigungsmodell bleiben erhalten; zusätzliche Speicherung von Nachrichtentexten beim Backup und der Aufbewahrung berücksichtigen.

---

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

- [x] Chronologische Nummerierung mit ältester Nachricht Nr. 1 und deutschem Datum inkl. Sekunden; Anzeige weiterhin neueste zuerst.
- [x] Regressionstest und Dokumentation.
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

## 23.11.2026 – Phase 5/7: HO-Outlook-Termin aus Desktop auch für vorhandene HO-Marker

- [x] Altversion `HomeOfficeService.SubmitAsync` und
  `OutlookInteropService.UpsertHomeOfficeAppointment` geprüft.
- [x] Bestehende Web-Implementierung bestätigt: Neuer bestätigter HO-
  Antrag erstellt bereits Ganztagstermin (Status `workingElsewhere`,
  Ort „An anderem Ort tätig“) und sendet danach die Mail.
- [x] Lücke für **bereits** gespeicherte HO-Marker geschlossen:
  kalenderexklusive, CSRF-geschützte Nachtragungsroute, ohne nochmalige Mail.
- [x] Auslösung in „Heute“ und Tagesmarker-Editor; BR-only kein Nachtrag.
- [x] Vorhandenen Journal-/Doppelerstellungsschutz wiederverwendet.
- [x] Frontend-Regression für Nachtragung ohne E-Mail ergänzt.
- [x] [PR #53](https://github.com/DerHausmeisterKrause/plenaro/pull/53) gemergt und [CI #45149184028](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/45149184028) erfolgreich.
- [ ] Live-M365-Test, ggf. delegiertes `Calendars.ReadWrite` erneut freigeben.
- [ ] Keine neue Infrastruktur, kein zusätzlicher Container.

---

## 19.11.2026 – Phase 5/7: Homeoffice-Antrag legt Outlook-Termin „An anderem Ort tätig“ an (PR #52 gemergt)

- [x] WPF-Vorlage `HomeOfficeService` und `OutlookInteropService`
  mit dem Web-HO-Ablauf verglichen (ganztägig, Arbeitsort, Verfügbarkeit,
  eindeutiger Termin, E-Mail danach und sichere Terminlöschung).
- [x] `Calendars.ReadWrite` nur im expliziten OAuth-Kalenderfluss
  ergänzt, separate Vorabprüfung neben `Mail.Send`.
- [x] Serverseitigen Graph-Homeoffice-Kalendereintrag mit
  `workingElsewhere`, `An anderem Ort tätig`, `isAllDay=true`,
  lokalem 00:00 bis exklusivem 00:00 am Folgetag und ohne Erinnerung
  implementiert.
- [x] Vorhandene exakt passende WPF-/Plenaro-Termine erkennen, bei
  Mehrdeutigkeit abbrechen; nach unklarem POST keine automatische
  Erzeugungswiederholung.
- [x] Eindeutiges DB-Journal pro Benutzer/Datum in Migration 0031;
  HO-Termin vor Mail, BR-only bleibt unverändert; keine zweite Mail
  nach bereits akzeptiertem BR-Antrag.
- [x] Automatische sichere Kalenderlöschung beim Abwählen von HO,
  erneute Anmeldung mit neuer Graph-Transaktions-ID erlaubt.
- [x] HO-Bestätigung im Schnellschalter `Heute` und im Tagesmarker-
  Dialog einheitlich; UI und Outlook-Berechtigungshinweise ergänzt.
- [x] Frontend- und Backend-Kontrakte mit synthetischen Testdaten.
- [x] [PR #52](https://github.com/DerHausmeisterKrause/plenaro/pull/52) gemergt, [CI #44553467789](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44553467789) vollständig grün (inkl. Docker-Smoke und Migration 0031).
- [ ] Produktiv-Deployment.
- [ ] Delegierte `Calendars.ReadWrite`-Freigabe im Zieltenant,
  echter HO-Test (Mail und Outlook-Termin), erneutes Speichern,
  Entfernen und Nachprüfen des Termins im Outlook-Kalender.

---

## 19.11.2026 – Outlook-Ganztagstermine: exklusive Enddatumsgrenze (PR #51 gemergt)

- [x] Ursache der doppelten Tagesanzeige eingegrenzt: Ein UTC-Midnight-
  Ganztags-`end` wird durch die deutsche Zeitzone 01:00/02:00 Uhr am
  nächsten Tag; bisheriger Zeitoverlap berücksichtigt dies falsch.
- [x] WPF-Bestand geprüft: Outlook `IsAllDay` verwendet Start-/Enddatum,
  `EffectiveEndDateExclusive` statt Uhrzeitüberlappung.
- [x] `frontend/src/todayAgenda.ts`: Ganztagstermine nach inklusivem
  Startkalendertag und exklusivem Endkalendertag filtern.
- [x] `frontend/src/WeekCalendarPage.tsx`: dieselbe Regel verwenden,
  auch für mehrtägige Ganztagstermine.
- [x] Regressionsfälle für 18./19.11.2026, Sommer-/Winterzeit und
  echte Mitternachtstermine ergänzt.
- [x] [PR #51](https://github.com/DerHausmeisterKrause/plenaro/pull/51) gemergt; [CI #44537370389](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44537370389) grün (UI, Backend, Verträge und Docker-Smoke).
- [ ] Im echten Outlook-Konto am 19.11. prüfen:
  „Buß- und Bettag“ darf nicht mehr erscheinen. Kein API-/Container-Umbau.

---

## 19.11.2026 – Phase 7: ATOSS ohne DNS-Alias, vorhandenen Shared-Favoriten-Host behalten (PR #50 gemergt)

- [x] Anforderung präzisiert: Keine Hostnamen, DNS-Aliasse oder
  Wildcard-Zertifikate anlegen. Vorhandenes Shared-Proxy-Routing behalten.
- [x] WPF-Anmeldelogik im Desktop `WebShortcutBrowserSessionManager`
  (DOM-Ausfüllen nach JavaScript-Rendern) mit Web-Proxy-Variante abgeglichen.
- [x] Optionalen Playwright-Chromium-Worker nur im internen Docker-Netz
  als neues Compose-Profil `browser-login` implementiert.
- [x] Über den bestehenden Personal-Favoritenstart Vault-Anmeldedaten nur
  intern dem Browserworker übergeben und bestätigte Upstream-Cookies im
  getrennten HttpClient-Jar wiederverwenden.
- [x] Bisherigen klassischen HTML-Login beim deaktivierten Worker erhalten;
  ohne DNS-Bereitstellung niemals auf dedizierten Host umleiten.
- [x] HTTPS-CA-Trust, Cross-Origin-/WebSocket-/Metadaten-Sperren
  und kontrollierte Cookie-Übernahme ohne Klartext im Shared-Browser.
- [x] Node/Chromium- und .NET-Vertragstests ergänzt.
- [x] [PR #50](https://github.com/DerHausmeisterKrause/plenaro/pull/50) in `main`, [CI #44532005260](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44532005260) grün mit echten Chromium-Vertragstests, Worker-Health-/Nonroot-Smoke und vollständigem Docker-Smoke.
- [ ] Produktiv-Deployment `docker compose --profile browser-login up -d --build`.
- [ ] ATOSS live prüfen (reine Cookie-Sitzung versus WebStorage, dynamische
  Login-Elemente); nötigenfalls gezielte ATOSS-ZK-Anpassungen vornehmen.
- [ ] Externe SSO/MFA-Logins nicht automatisch umgehen; weiter getrennt.

---

## 19.11.2026 – Phase 7: ATOSS-DOM-Anmeldung und Favoriten-Banner (PR #49 gemergt)

- [x] WPF-Vorlage
  `project_tool/Services/WebShortcutBrowserSessionManager.cs`
  erneut mit aktuellem Browser-DOM-Helper verglichen.
- [x] Persistente Statuskarte `Automatisches Ausfüllen noch nicht aktiv`
  aus der Favoritenansicht entfernt.
- [x] ATOSS/ZK-`a.z-button`/`role=button` mit exakt passender
  Login-Beschriftung ergänzt, ohne unbeteiligte Aktionen auszuführen.
- [x] Browser-Regressionsfälle für ZK-Button und
  nicht-relevante Buttons ergänzt.
- [x] Einfache dedizierte DNS-/HTTPS-/Proxy-Health-Diagnose
  in `tools/check_favorite_autologin_host.sh` ergänzt.
- [x] [PR #49](https://github.com/DerHausmeisterKrause/plenaro/pull/49) gemergt; [CI #44524726181](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44524726181) grün inklusive ATOSS/ZK-Regressionsfällen, Web-Build und Docker-Smoke.
- [ ] Echter ATOSS-Test nach DNS-/TLS-Setup.
- [ ] Infrastruktur für dedizierten Fav-Host mit DNS/TLS/NGINX
  einrichten; erst danach explizites Opt-in aktivieren.
- [ ] Erst dann echtes WPF-kompatibles DOM-Autofill unter ATOSS bestätigen.

---

## 19.11.2026 – Phase 7/6: Favoriten-DNS-Fallback und Znuny-Tagesanzeige (PR #48 gemergt)

- [x] Ursache der `site-...-plenaro.intern.test.org`-DNS-Fehler
  aus PR #45 lokalisiert: automatische Umleitung auf generierte Alias-Domain
  ohne tatsächliche DNS-/TLS-Bereitstellung.
- [x] Persönliche Favoriten bei vorhandenem Shared-Origin-Pfadhost
  automatisch wieder unter `apps-plenaro.../site-.../` öffnen.
  Die vorhandene Template-Konfiguration reicht **nicht** zum Opt-in.
- [x] `PLENARO_FAVORITES_PERSONAL_DEDICATED` als explizite Option für
  fertig eingerichtete Wildcard-DNS-/TLS-Origins ergänzen.
- [x] ATOSS-Passwortschutz erhalten: kein Browser-DOM-Autofill auf
  gemeinsamer Origin; bisheriges klassisches HTML-Login bleibt bestehen.
- [x] Backend-Kontrakttests für opt-in / nicht opt-in konfiguriert.
- [x] Einheitliche, barrierearme 24h-Zeitbuchungsanzeige für heute
  sowohl im Zeitreport als auch kompakt beim Ticket-Timer ergänzt.
- [x] Dynamischer Rest aus API `/api/v1/znuny/tickets/time/daily-limit`;
  kein Verwechseln mit gewähltem Reportmonat oder externen Znuny-Zeiten.
- [x] React-Tests für Fortschritt, volle Auslastung, Rest und Warnungen.
- [x] [PR #48](https://github.com/DerHausmeisterKrause/plenaro/pull/48) gemergt; [CI #44508791675](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44508791675) erfolgreich mit Backend-/Frontend-/Favoriten-/Znuny-Verträgen und Docker-Smoke.
- [ ] Produktiver DNS-/Ticket-Test nach dem Deployment.
- [ ] Dedizierten DNS-/TLS-Host für vollständigen JavaScript-ATOSS-
  Autologin im Zielnetz bereitstellen; Shared-Origin unsicher für
  clientseitige Credential-Autofill-Skripte.

---

## 19.11.2026 – Höchste Priorität: Znuny-Agent je Tag maximal 24h buchen (PR #47 gemergt)

- [x] Altes WPF-Tool `Services/TicketSystemService.cs` und
  `ViewModels/TodayViewModel.cs` als Referenz geprüft:
  Ticket-Zeitbuchung über die 15-Minuten-`TimeUnit`, aber keine
  agentenweite Tages-Sperre.
- [x] Buchungsjournal `znuny_ticket_time_bookings` als bereits
  vorhandene Wahrheit für alle **Plenaro**-Buchungsversuche verwendet.
- [x] Agent-ID und Europe/Berlin-Tag für jede Buchung festhalten;
  Migration `0030` mit Backfill und Index.
- [x] Backend-Limit exakt 1.440 über alle Tickets und Plenaro-Benutzer
  eines Znuny-Agenten, auf gerundete `TimeUnit` anwenden, 409 vor
  remote Znuny-Schreibversuch.
- [x] Parallele Transfers mithilfe eines Agenten-/Tag-Advisory-Locks
  serialisiert; `pending` blockiert, `rejected` nicht, auch nach Reboot.
- [x] Tageskontingent unter `GET /tickets/time/daily-limit` sichtbar;
  Timer zeigt Restminuten und Grenzverletzung.
- [x] C#-Regressionstests und realer PostgreSQL-/HTTP-CI-Smoke mit
  verschiedenen Tickets, Pending/Confirmed/Rejected und zwei Agenten
  ergänzt.
- [x] [PR #47](https://github.com/DerHausmeisterKrause/plenaro/pull/47) nach `main` übernommen; [CI #44505339958](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44505339958) grün mit realem Datenbank-/HTTP-Quota-Smoke, Frontend, Znuny-Vertragstests und Docker-Start.
- [ ] Produktivabnahme nach Deployment.
- [ ] Übergreifende externe Buchungen direkt aus OTRS oder dem WPF-Tool:
  Für absolut systemweite Begrenzung ist zusätzlich eine
  Znuny/OTRS-seitige Validierung auf `TimeUnit` nötig.

---

## 17.11.2026 – Phase 6: Znuny-Dashboard 429 beheben (PR #46 – gemergt)

- [x] Fehlermeldung im Frontend zugeordnet: `znunySyncApi.ts`
  verwendet die HTTP-Antwort des Plenaro-/Gateway-Endpunkts;
  remote Znuny-API-Fehler werden dagegen auf Gateway-Status umgesetzt.
- [x] Fehler in Middleware-Reihenfolge gefunden:
  `UseRateLimiter()` vor `UseAuthentication()` verursachte
  gemeinsame IP-Quotas statt individueller Account-Quotas.
- [x] Quotas nach Benutzer partitioniert; anonyme Login-Drosselung
  bleibt nach IP; ein eigener stabiler Key für jeden Plenaro-Account.
- [x] Interaktive Ticketreads 80/5 Minuten pro Benutzer und
  Anhänge/Inlinebilder gesondert 120/5 Minuten pro Benutzer,
  bestehende Schreiblimits unverändert.
- [x] `X-Plenaro-RateLimit`-/`Retry-After`-Diagnostik und klare
  429-Meldung im Frontend ergänzt.
- [x] C#-Contract-Test zu separaten User-Limits trotz gleicher
  Reverse-Proxy-IP und Frontend-Tests zu HTTP-429-Ursachen ergänzt.
- [x] [PR #46](https://github.com/DerHausmeisterKrause/plenaro/pull/46) in `main` gemergt; [CI #44075326407](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44075326407) erfolgreich inkl. Backend, Frontend, Rate-Limit-Regression, NGINX und Docker-Smoke.
- [ ] Echter Zwei-User-Dashboard-Test am Zielhost.
- [ ] Falls weiterhin 429 ohne Plenaro-Kennung auftreten,
  NGINX-Limits und Upstream-Gateway getrennt diagnostizieren.

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
- [x] Erster SQLite-v1–v32-Importpfad: lokaler bereinigender Export, Web-Vorschau, transaktionaler Import, ID-Deduplizierung und snapshotgeschützte Rücknahme ([PR #29](https://github.com/DerHausmeisterKrause/plenaro/pull/29), [CI erfolgreich](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41136074441); Host-Abnahme offen)
- [ ] **Vollständige** Datenmigration/Classic-Feldparität (Ticket-/Outlook-Verknüpfungen, Buchungen, weitere Einstellungen und Legacy-Datenmengen)

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
- [x] Abgeschlossene Desktop-Aufgabenzeitbuchungen und geplante Segmente über SQLite-Exportpfad übertragbar (CI/Host-Abnahme getrennt)
- [ ] Vollständige Altimport-Feldparität und Browser-Abnahme
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

## Classic-Import in Phase 5 – Teilmigration

- [x] `tools/export_classic.py`: SQLite 1–32 schreibgeschützt öffnen und als bereinigtes JSON exportieren, optional `settings.json` mit strikt erlaubtem `ReminderLeadMinutes`-Wert. Keine Zugangsdaten exportieren.
- [x] Eigene Aufgaben, geplante Zeitsegmente mit Teilnehmern, abgeschlossene `time_logs` und UL/AM/BR/HO aus `work_days`; keine Anwesenheit, Pausen oder offene Timer.
- [x] Web-Einstellungen → Datenimport mit Vorschau, Hinweisen, Zählung, expliziter Bestätigung und Importhistorie.
- [x] In einer PostgreSQL-Transaktion importieren, UUIDs/Benutzerbesitz absichern, Wiederholungsimporte ohne Duplikate, bestehende eigene Marker nicht überschreiben.
- [x] Manuelle batchbezogene Rücknahme mit Daten-Snapshots; bei bearbeiteten Datensätzen/abhängigen Einträgen verweigern.
- [ ] Volle Classic-Übernahme von Ticketbuchungen, Zeitanpassungen, Outlook-/Znuny-/Wiki-Einstellungen und Zuordnungen.
- [ ] Host-Abnahme mit einer echten Kopie der bisherigen `TaskTool.db` und `settings.json` nach PostgreSQL-Backup.

## Classic-Import Phase 5 – historisches Archiv (PR #30)

- [x] Exportformat 2 ergänzt Desktop-Aufgabenfelder (Pin, Znuny-Zuweisung, Status, Ticketadresse, gebuchte Classic-Zeit und alte Outlook-ID), historische Ticketbuchungen und monatsbezogene Nachkorrekturen.
- [x] Historische Daten liegen in separaten PostgreSQL-Archivtabellen mit Benutzertrennung. **Keine** automatische Rückbuchung nach Znuny, keine aktive Outlook-Bindung und **keine** Verdopplung der normalen Aufgabenzeiterfassung.
- [x] Aus `settings.json` werden nur nichtgeheime Planungs-/Hinweis-Einstellungen als inaktive Referenzdaten archiviert (z. B. Wochenenden, Standardsegmentdauer, Kalender-Zeitzone). Einzig der zuvor implementierte `ReminderLeadMinutes` wird als aktive Einstellung übernommen.
- [x] Anzeige alter Aufgabenfelder/Ticketbuchungen im Aufgaben-Detail, Archivübersicht und importweise Einsicht gespeicherter Settings; Batch-Rücknahme und Quell-Deduplizierung erweitert.
- [x] Exporter entfernt in Ticket-URLs Credentials und fremde Query-Parameter; Zugangsdaten/API-Schlüssel bleiben ausgeschlossen.
- [ ] Historische Ticketbuchungen erst nach fachlicher Zuordnung zu Znuny/OTRS in eine echte Buchungsintegration übernehmen; keine automatische Wiederholung alter Buchungen.
- [ ] Monatskorrekturen als eigene historische Berichtsdaten anzeigen bzw. mit verifizierter, nicht doppelter Zählung in Berichte integrieren.
- [ ] Migration mit echter Classic-Datenbankkopie/Host prüfen und weitere individuelle/SSO/Outlook-Konfiguration manuell einrichten.

## Znuny-/OTRS-Konfiguration aus Classic (Phase 6)

- [x] Die serverweiten GenericTicketConnectorREST-Standardwerte aus `project_tool/Models/AppSettings.cs` und `ZnunySyncPolicy.cs` übernommen: `https://SERVER/index.pl` und `https://SERVER/nph-genericinterface.pl/Webservice/GenericTicketConnectorREST` als Platzhalter; SessionCreate `POST /Session`, TicketSearch `GET /Ticket`, TicketGet `GET /Ticket/{TicketID}`; zusätzliche TicketUpdate/Create-/Agentenlisten-/Dynamikfeld-Routenvorlagen dokumentiert und konfigurierbar.
- [x] Default: Session-Authentifizierung; Intervall 15 Min., Kandidatenintervall 5 Min., SearchLimit 100 und ArticleLimit 20; nur offene Tickets, Owner/Responsible aktiv. Erstellen, Update und Zeitbuchung werden **nicht** durch Hinterlegen der Einstellungen aktiviert.
- [x] Systemweite Serverkonfiguration ausschließlich durch Administrator; persönliche Agenten-ID, Kandidaten-ID und Suchfilter individuell. Znuny-Passwort AES-GCM-verschlüsselt im bestehenden Secret-Vault, nie auslesbar über Browser.
- [x] Auf Benutzeraktion beschränkter SessionCreate-Verbindungstest mit TLS, ohne Weiterleitungen, ohne Rückgabe/Logging von SessionID oder Passwort. Sicherheit: kein HTTP-/Loopback-/Metadata-Ziel, begrenzte Antwortgröße und Zeitlimit.
- [ ] Echter TicketSearch/TicketGet-Datenfluss und Anzeige in Neue Aufgaben; danach Delta-Synchronisation/Benachrichtigungen und Ticketaktionen.
- [ ] Verbindungstest gegen eine echte Znuny-/OTRS-Instanz des Benutzers (Platzhalteradresse wird nicht verbunden).

## Zentraler Znuny-Sync (Phase 6, PR #32)

- [x] **Eine** technische Znuny-Verbindung pro Instanz, zentraler verschlüsselter Serviceaccount und PostgreSQL-Ticketcache. Benutzer führen **keinen eigenen** Remote-TicketSync aus.
- [x] Wiederaufnehmbarer Erstimport der letzten 365 Tage (administrativ 7–3650 Tage einstellbar) in maximal acht Tagesschritten pro Hintergrundlauf. Der Cursor wird erst nach erfolgreich gelesenen Details aktualisiert.
- [x] Danach Delta-Search mit `TicketLastChangeTimeNewerDate`, 24 Stunden Überschneidung, Upsert nach TicketID; ältere offene Tickets werden in einem gesonderten fortsetzbaren 90-Tage-Fenster-Sweep ab Jahr 2000 aufgenommen und regelmäßig neu geprüft.
- [x] Zeitfenster bei vollständig erreichtem SearchLimit rekursiv unterteilen, niemals abgeschnittenen Suchlauf als vollständig markieren; API-Request-Budget, Status und Fehlercodes in Admin-Ansicht sichtbar.
- [x] Admin aktiviert/deaktiviert Sync; konfiguriert 365-Tage-Import, Delta-/Open-Intervalle, Budget und Service-Credentials. Expliziter Reset bei Serverwechsel; zentraler DB-Advisory-Lock verhindert parallele Instanzläufe.
- [x] Persönlicher Ticketcache-Zugriff ausschließlich über **administrativ genehmigte** Zuordnung Benutzer→Znuny-Agent, nicht über ein manipulierbares persönliches Agenten-ID-Feld. Owner/Responsible-Sicht und neuer-Ticket-Reiter im Ticketarbeitsplatz.
- [ ] Integration mit realem Znuny-Server und breit abweichenden REST-Mappings; API-Endpunkte können je Webservice-Konfiguration variieren.
- [ ] Kandidatenpool über Queue-/Rollenfreigaben und Ticketaktionen, Benachrichtigungen, Ticketartikeleinsicht.
- [ ] Große Alt-Datenbestände und Laufzeiten auf echtem Server messen, Suchlimit/Sessionfehler verifizieren.

## Znuny-Agenten-ID bei Benutzeranlage (Phase 6, PR #34)

- [x] In der **Administration → Benutzerverwaltung** die Znuny-Agenten-ID beim
  Anlegen und beim Bearbeiten direkt hinterlegen (optional; null = kein Zugriff).
- [x] Die Zuordnung atomar mit der Benutzeranlage/-bearbeitung in
  `znuny_approved_agent_bindings` speichern, Eindeutigkeit erzwingen
  und Doppelbelegungen als HTTP 409 zurückweisen, ohne halbfertige Benutzer.
- [x] Benutzer können ihre Agenten-ID weder über das persönliche Formular
  noch über die persönliche API ändern; GET zeigt nur die Admin-Freigabe.
- [x] Obsoletes Benutzerfeld aus PostgreSQL entfernen, vorhandene
  Adminfreigaben erhalten und separaten Bindings-Editor/API abschaffen.
- [x] Ticketcache für frisch angelegte Benutzer ohne persönliche Znuny-Settings
  mit sicheren Standardfiltern für Owner/Responsible lesbar machen.
- [ ] Live-Abnahme von SSO-Provisionierung und tatsächlichem Znuny-System
  unabhängig von den geprüften lokalen Admin-/API-Flows.

## Classic-Heute-Reiter nach Znuny/OTRS-Workflow (PR #35)

- [x] **Heute**: ausschließlich persönliche Outlook-Termine des lokalen Tages sowie lokal geplante Znuny-Ticketsegmente, chronologisch sortiert; keine unsegmentierten lokalen Aufgaben
- [x] **Aktuelle Aufgaben**: offene Tickets, bei denen die administrativ freigegebene Agenten-ID Owner **oder** Responsible ist, unabhängig von heute
- [x] **Neue Aufgaben**: offene Tickets ohne echten Owner/Responsible oder mit dem administrativ konfigurierten Standard-OTRS-Agenten (standardmäßig ID 1); keine irreführende Suche nur nach Znuny-State `new`
- [x] **Erledigte Aufgaben**: geschlossene Tickets (Znuny `state_type=closed`) mit eigener Owner-/Responsible-Zuordnung
- [x] Persönliche Znuny-Ticket-Zeitsegmente lokal anlegen, anzeigen und entfernen; Datenbank mit Besitzerrechten; Tagesagenda per Europe/Berlin-Zeitfenster
- [x] Identische offene/neu/erledigte Filter im separaten Ticketsystem; keine externen Ticketänderungen ohne implementierte Znuny-Schreibaktionen
- [x] Serverprüfungen für CSRF, Agentenbindung, fremde Tickets, geschlossene Tickets, lokale Segmente und Ticket-Pool-Trennung
- [ ] Gegen echten Znuny-Server prüfen: Default-/Root-Agenten-ID, Status-Typen, tatsächliche Ticketdaten und neue Kandidaten
- [ ] Bereits im WPF gespeicherte Znuny-Ticket-Segmente ggf. gesondert importieren/migrieren
- [ ] Remote-Ticketaktionen, Nachrichten/Artikel, Dynamic Fields und Zeitbuchungen umsetzen

## WPF-Taskdetail in Heute / Aktuelle Aufgaben / Ticketsystem (PR #36)

- [x] Gemeinsam genutzter, responsiver Ticketdetailbereich nach
  `project_tool/Views/TaskDetailPanel.xaml`: Titel, Beschreibung,
  Ticket-URL, Status, Owner/Responsible, Timer/Buchung, Planung (Segmente)
- [x] Persönliche lokale Ticketbeschreibung und eigene Ticket-URL
  in PostgreSQL gespeichert; je Benutzer und Ticket autorisiert, CSRF und URL-Prüfung
- [x] Serverseitig persistenter Ticket-Timer (Start/Stop auch beim
  Schließen des Browsers) und manuelle Minutenkorrekturen (+15/+30/+1h und −)
  mit eindeutigem aktivem Ticket-Timer pro Benutzer
- [x] Segment-Editor nach Desktop-Schema: Datum, Beginn, Ende,
  Kurzbeschreibung, Teilnehmer, visuelle lokale Zeitübersicht, bestehende
  Segmente bearbeiten und löschen (lokale Daten, ohne Outlook-Einladungen)
- [x] Identische Ansicht in Heute, Aktuelle Aufgaben, Neue Aufgaben und
  im zentralen Ticketsystem; geschlossene Tickets behalten bearbeitbare
  lokale Notizen und Zeitdaten
- [x] WPF-Bereiche für Nachrichten, Zuweisungen, Wiki-Treffer und
  Znuny-Zeitbuchungen im Layout gekennzeichnet, aber ohne falsche
  Remote-Schreibaktionen: externe Funktionen bis zur sicheren Integration deaktiviert
- [ ] Artikelabruf, Ticketantwort, Übernahme/Zuweisung, Remote-Schließen,
  Dynamic Fields/Kostenstelle/Auftrag und Remote-Zeitbuchung
- [ ] Wiki-Verbindungen und echte automatische Wiki-Treffer pro Ticket
- [ ] Echte systemweite Graph-Kalender-/Teilnehmerverfügbarkeit und
  Termineinladungen für Segmente (Anzeige derzeit nur für dieses Ticket)
- [ ] Real-Znuny-Live-Abnahme der Detailfelder, WPF-Feldparität und
  Legacy-Import bisheriger Ticketzeitdaten

## Znuny live: Artikel, Antworten, Zuweisungen (Phase 4 / PR #37)

- [x] Gezielt einzelne Ticketartikel über Znuny GenericInterface lesen, nur mit administrativ freigegebener Agentenbindung; reine Textanzeige, kein fremdes HTML
- [x] Ticketantwort per Mail: Empfänger ausschließlich aus Kundenartikeln auf dem Server ermitteln, Nutzereingabe HTML-escaped, eigene Znuny-Zugangsdaten und Benutzerbestätigung
- [x] Besitzer und Verantwortlichen aus Znuny-Agentenliste auswählen, remote bestätigen und anschließend Cache aktualisieren; „Mir zuweisen“ separat
- [x] CSRF, Einzel-Ticket-Zugriff, geschlossene Tickets, begrenzter Requestumfang, getrennte Read-/Write-Rate-Limits, keine automatischen Wiederholungen unbestätigter Schreibaktionen
- [ ] Live-Znuny-Integration mit konkretem GenericTicketConnectorREST testen
- [x] Ticket-Schließen und Wiederöffnen mit persönlicher Znuny-Session und bestätigter Remote-Statusänderung; Cache-Update nach Bestätigung
- [ ] Wiki-Provider, Dynamic Fields und echte Znuny-Zeitbuchung ergänzen


## Anpassung 02.11.2026 – automatischer Entra-Zugang und gemeinsamer Arbeitsplatz (PR #38)

- [x] Entra-Single-Tenant-Identität anhand validiertem `tid:oid` beim ersten Login als neuen lokalen `User` ohne Passwort automatisch provisionieren; keine E-Mail-Zuordnung und keine Znuny-Freigaben
- [x] Manuelle Kontoverknüpfungs-UI/-Endpunkte für neue Anmeldungen entfernen; bestehende explizite Entra-Logins beibehalten
- [x] Kandidaten-Benutzer-ID aus persönlichen Znuny-PUT-Anfragen entfernen und serverseitig verbieten; nur Administrator darf die ID bei Benutzeranlage/-bearbeitung setzen oder entfernen
- [x] Individuelle Kandidaten-ID in Ticketliste, Detail, Segmentplanung und Remote-Berechtigungsprüfung verwenden; fehlende ID erteilt keine Kandidatenrechte
- [x] Arbeitsplatz als zentrale Ansicht statt separater Aufgaben-/Ticketsystem-Navigation; lokale Aufgaben als Unterreiter, Legacy-Pfade als Weiterleitungen
- [ ] Vollständigen GitHub-CI-/Docker-Test für PR #38 abschließen
- [ ] Microsoft-SSO-Erstlogin mit echtem Tenant samt Admin-Sichtbarkeit und Outlook-Token live abnehmen
- [ ] Echten Znuny-Ticketbestand gegen Owner/Responsible/Standard-Agenten-IDs, offene und geschlossene Ticketstatus testen
- [ ] Browser-/Hostabnahme inklusive bestehenden lokalen Konten und Altlinks durchführen


## 02.11.2026 – Benutzer löschen (Phase 3, nach PR #38)
- [x] Admin-only DELETE-API mit CSRF, Benutzer-ID und exakt eingegebener Kontomail als Bestätigung
- [x] Eigenes Administratorkonto und letzten aktiven Administrator vor Löschung schützen
- [x] Benutzer ohne geschäftliche Aufgaben löschen; Entra-Logins, Benutzerrollen, persönliche Einstellungen, Znuny-Zuordnungen, Kalendertokens und Zeitplanungen über FK-Cascades entfernen
- [x] Aktive Aufgaben und Zeitbuchungen vor unbeabsichtigter Löschung schützen (HTTP 409); vorhandene DB-RESTRICT-FKs beibehalten
- [x] **Korrigiert:** Gelöschte Entra-Konten dürfen sich erneut anmelden und werden als neue Standardbenutzer ohne die alten Znuny-Rechte erstellt; bisherige Sperrliste mit Migration 0022 aufheben (PR #40)
- [x] Benutzerverwaltung mit zweistufiger Löschbestätigung und klaren Hinweisen
- [x] Ursprünglicher CI/Docker-Smoke-Test zu PR #39 erfolgreich (einschließlich mittlerweile zurückgenommener Entra-Sperre); Regressionstest für erlaubte Neuanmeldung ergänzt in PR #40
- [ ] Live-Abnahme an lokaler Plenaro-Instanz einschließlich echtem Entra- und Znuny-Zugriff

### Korrektur 02.11.2026 – Entra-Neuanmeldung nach administrativer Löschung (PR #40)
- [x] Entra-Sperrprüfung aus der OIDC-Provisionierung entfernen
- [x] Permanente Sperreinträge beim Löschen von Entra-Konten vermeiden
- [x] Bestehende Einträge aus PR #39 per Migration 0022 vollständig entfernen
- [x] Beim erneuten SSO-Login bleibt die `tid:oid`-Prüfung erhalten; ein neues Benutzerkonto erhält neue Plenaro-ID und nur Rolle `User`, keine vorherigen Ticketfreigaben
- [x] UI-Hinweis im Löschdialog korrigieren
- [x] CI inkl. Regressionstest bestanden ([Run #41443551269](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41443551269))
- [ ] Reale Entra-Anmeldung mit zuvor gelöschtem Benutzer auf dem Host bestätigen

### 02.11.2026 – Historischen Znuny-Erstimport neu starten (Phase 6)
- [x] Admin-Button „Erstimport neu starten“ in der zentralen Synchronisationskonfiguration
- [x] Geänderten Rückblickzeitraum (z. B. 7 → 365 Tage) beim Buttonklick zunächst speichern
- [x] CSRF-geschütztes Admin-API für erneuten Bootstrap-Scan mit konfliktfreier PostgreSQL-Advisory-Sperre
- [x] Bootstrap-Datum, Cursor und Abschluss zurücksetzen; bestehende zentral zwischengespeicherte Tickets, persönliche Segmente, Notizen und Zeiterfassungen bleiben erhalten
- [x] Vorherigen Delta-Watermark bei einem erneuten Bootstrap bewahren, um während des Nachladens geänderte Tickets nachträglich abzugleichen
- [x] Fehlertolerantes Nachladen in bestehenden Tagesabschnitten; nach einem unmittelbaren Lauf fährt der Hintergrunddienst weiter
- [x] Frontend-Test für Ein-Klick-Änderung des Rückblickzeitraums und nicht-destruktiven Neustart ergänzt
- [x] Automatisierte Docker-/API-Prüfung und Gesamt-CI erfolgreich ([CI #41446503580](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41446503580), [PR #41](https://github.com/DerHausmeisterKrause/plenaro/pull/41))
- [ ] Live-Abnahme am echten Znuny-Server (historischer Ticketimport, Dauer, Rate-Limits)


### 02.11.2026 – E-Mail-Darstellung im Znuny-Ticketdetail (Phase 6)
- [x] MIME-`ContentType` der Znuny-Artikel zum Frontend durchreichen; HTML nicht mehr per Regex in unformatierten Text umwandeln
- [x] Originale HTML-Tabellen, Absätze und Zitate in einer isolierten `iframe sandbox`-Vorschau darstellen
- [x] Restriktive Content-Security-Policy für E-Mails (keine Skripte, Remote-Bilder, Formulare, Webanfragen oder externe Schriften); kein `dangerouslySetInnerHTML` im App-DOM
- [x] Plain-Text-E-Mails mit erhaltenen Zeilenumbrüchen, korrekt umgebrochenen langen Zeilen, lesbaren Absender-/Empfängerdaten anzeigen
- [x] HTML-Erkennung auch ohne von Znuny gelieferten MIME-Typ und Vergrößern der Vorschau für lange E-Mails
- [x] Automatisierte Tests für HTML-Mails, Plaintext, Sicherheit und MIME-Erkennung ergänzt
- [x] CI-Build + Docker-Smoke-Test erfolgreich ([CI #41449089718](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41449089718), [PR #42](https://github.com/DerHausmeisterKrause/plenaro/pull/42))
- [ ] Echtes Znuny-Livebeispiel mit HTML, Inline-Bildern, Tabellen und Sonderzeichen prüfen


### 02.11.2026 – Znuny-E-Mail-Anhänge und eingebettete CID-Bilder (Phase 6)
- [x] Znuny-TicketGet mit `Attachments=1` und `GetAttachmentContents=0` für reine Datei-Metadaten ohne automatische Base64-Downloads
- [x] `FileID`, `ContentID`, Dateiname, Dateigröße, MIME-Typ und Disposition je Artikel verarbeiten
- [x] Anhänge pro E-Mail anzeigen und über Plenaro mit erneuter Benutzer-/Ticketberechtigungsprüfung herunterladen (Dateiname serverseitig entschärft; Content-Disposition attachment, nosniff, no-store)
- [x] Dateiinhalte nur auf Anforderung abrufen, Größenlimit 5 MiB je Datei und 8 MiB gesamte Znuny-Antwort; übergroße Anhänge mit HTTP 413 ablehnen
- [x] Eingebettete referenzierte PNG/JPEG/GIF/WebP-Bilder bis 512 KiB über begrenzte authentifizierte CID-Endpunkte als Daten-URIs in die bereits isolierte HTML-E-Mail-Vorschau einbetten
- [x] Keine externe Bildernachladung, kein SVG oder direktes Ausführen von Anhängen; Zuordnungen bei Nachrichtenwechsel neu prüfen
- [x] Frontend-Regressionstests und Docker-Zugriffskontrollen für die neuen Endpunkte
- [x] Vollständige CI inklusive Docker-Smoke-Tests erfolgreich ([CI #41478462307](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41478462307), [PR #43](https://github.com/DerHausmeisterKrause/plenaro/pull/43))
- [ ] Live-Test mit echten Znuny-Artikeln (`ContentID`, Inline-Bilder, Dateien, Größenlimits, Berechtigungen) ausstehend

### 02.11.2026 – Automatische Znuny-Agentennamen und Segmentverfügbarkeit (Phase 6)
- [x] Besitzer und Verantwortlicher mit Namen direkt im Ticketdetail, auch vor dem Editieren; Fallback Agent-ID bei nicht erreichbarer Znuny-Agentenliste
- [x] Znuny-Agentennamen automatisch beim Ticketöffnen und alle fünf Minuten bei sichtbarem Tab abrufen (manueller Button optional)
- [x] Ticket-Nachrichten automatisch im Hintergrund alle 60 Sekunden aktualisieren, während ein Entwurf nicht unterbrochen wird
- [x] 48 farbige 15-Minuten-Slots von 06:00 bis 18:00 wie im WPF-Vorgänger: grün frei in bekannten Quellen, rot belegt, grau unbekannt; ausgewähltes Zeitfenster markiert
- [x] Zentrale Plenaro-Verfügbarkeit mit eigenen lokalen Aufgaben- und Znuny-Segmenten, zusätzlich eigener Outlook-Kalender bei bestehender Graph-Verbindung; keine unbelegte Aussage über fremde Teilnehmer-Kalender
- [x] Ticket- und nutzerberechtigter API-Endpunkt für eigene Tagesbelegungen, ohne Fremdtermine offenzulegen
- [x] Bestehenden Segmenteditor mit Echtzeit-Konfliktwarnungen, sicheren Zeitfeldern und Konfliktsperre erweitern; serverseitige atomare Überschneidungsprüfung für Znuny-Segmente gegen eigene lokale Aufgaben und andere Znuny-Tickets
- [x] Frontend-Tests für Farbe, Zeitraster, Outlook-Frei/Belegt, Bearbeitung und Backend-Docker-RBAC-/Konflikttests ergänzt
- [x] GitHub-CI vollständig erfolgreich ([Run #41483017559](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41483017559), [PR #44](https://github.com/DerHausmeisterKrause/plenaro/pull/44))
- [ ] Live-Abnahme mit echtem Znuny-Agentenverzeichnis und verbundenem Outlook
- [ ] Outlook-Blocker-Erstellung/Löschung für Segmente und Teilnehmer-Free/Busy: erfordert getrennt zu prüfende Microsoft-Graph-Schreibrechte (`Calendars.ReadWrite` bzw. delegierte Berechtigungen). Noch nicht implementiert, keine vermeintliche Teilnehmerverfügbarkeit.


### 02.11.2026 – Znuny-Kostenstellen, Aufträge und Desktop-Zeitbuchung (Phase 6)
- [x] Backend-Laden der tatsächlichen Znuny-DynamicField-Optionen für Kostenstelle und Auftrag sowie deren aktuellem Ticketwert; WPF-kompatible administrativ konfigurierte Fallback-Werte (`key=label;...`)
- [x] Kostenstellen-/Auftragsauswahl mit Suchfeld im Znuny-Ticketdetail und expliziter Auswahlschlüssel-Validierung vor dem Schreiben
- [x] Alte separate Minuten-„Zeitkorrektur“-UI entfernen: direkte Bearbeitung der angezeigten **ungebuchten** Zeit per Klick im `HH:MM:SS`-Format; sekundengenaue persistente Korrektur
- [x] Znuny-Zeitbuchung als interner Artikel mit `TimeUnit`, dynamischen Feldern, Buchungsbeschreibung, Notiz und eindeutiger `TaskTool-Booking-ID`
- [x] Immer auf volle 15 Minuten **aufrunden** (`ceil(source_seconds/900)*15`), während exakte Quellsekunden lokal erhalten bleiben
- [x] Vor dem Buchen laufenden Timer stoppen, bereits gebuchte Quellsekunden bei der ungebuchten Timerzeit abziehen
- [x] Zeitbuchungsjournal mit Pending-Sperre gegen doppelte Übertragungen, manueller Abgleich anhand der Buchungs-ID und Buchungshistorie
- [x] Backend-CSRF/Benutzer-/Ticketberechtigung und nur persönliche Znuny-Zugangsdaten für Schreiben; kein automatisches Retry bei unklarer Remoteantwort
- [x] React-Rundungstests anhand der WPF-Vorgängertests und Docker-API-Checks für Timer/Kontrollen ergänzt
- [x] Gesamte GitHub-CI grün (Frontend-/Backend-Build, Tests, Docker) ([CI #41486074954](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41486074954), [PR #45](https://github.com/DerHausmeisterKrause/plenaro/pull/45))
- [ ] Produktiven Znuny-Connector auf Optionen-Lieferung und echte `TimeUnit`-Buchungen (Status, ArticleID, korrekte Feldwerte) prüfen
- [ ] Spätere Admin-Funktion zur bewussten Auflösung eines dauerhaft unklaren Pending-Status ergänzen, falls die Znuny-Artikelhistorie den Eintrag nicht mehr liefert


### 02.11.2026 – Classic-Kandidatenfilter, Volltextsuche und Antwortvorlage (Phase 6)
- [x] „Alle neuen Aufgaben“ als direkt in der Ticketansicht schaltbarer, benutzerbezogen gespeicherter Pool-Filter (aktiv: alle freien Tickets; inaktiv: nur Suchkriterien **ohne** Ausschlusstreffer)
- [x] Classic-Suchbegriffe mit Komma, Semikolon und Zeilenumbrüchen trennen; Titel und indizierte Artikelinhalte auf Teilstring prüfen, Großschreibung ignorieren
- [x] Zentrale schrittweise Artikel-Volltextindizierung durch technischen Znuny-Dienst, getrennt vom Metadaten-Sync und mit kleinem Request-Budget; Suche bleibt immer auf freigegebene Tickets beschränkt
- [x] Server-seitige Volltextsuche über Nummer, Titel, Queue, Status, indizierte Artikel und persönliche Ticketnotiz; Wörter kombinieren (UND)
- [x] Filter für Queue und Status sowie Sortierfeld/Richtung in „Aktuelle“ und „Neue Aufgaben“ samt Rücksetzen und serverseitiger Pagination
- [x] Vorhandene konfigurierbare Antwortvorlage vor dem Antworten automatisch ausfüllen, WPF-Variablen `$Customer`, `$CustomerFirstName`, `$CustomerLastName`, `$TicketNumber`, `$TicketTitle`, `$Date` ersetzen, ungelöste markieren
- [x] „Erledigt“ öffnet wie Classic zuerst eine bearbeitbare Antwort samt „Ist erfolgt. Ticket wird geschlossen.“; Antwort und Ticketstatus in einem einzigen Znuny-TicketUpdate senden
- [x] Bestehende Fremdticket-/Adminberechtigungen, CSRF und keine automatische Wiederholung unklarer Znuny-Schreibaufrufe
- [x] Browser- und Docker-Integrationstests für Kandidatenmodus, Such-/Ausschlusslogik und Vorlagenvariablen ergänzt
- [x] Vollständige CI erfolgreich ([CI #41493282973](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41493282973), [PR #46](https://github.com/DerHausmeisterKrause/plenaro/pull/46))
- [ ] Live-Abnahme mit echten Kunden-Mails und vollständiger Artikelindizierung; zunächst noch nicht eingelesene historische Nachrichtentexte erscheinen erst nach der schrittweisen Indexierung


### 02.11.2026 – Znuny-Persönlicheinstellungen und eingebettete Weboberfläche (Phase 6)
- [x] Falsche Benutzer-Speicheranforderung korrigieren: schreibgeschützte `candidateUserId` aus dem PUT-Body entfernen, Agenten-ID wird weiterhin nur administrativ vergeben; keine Absenkung der API-Berechtigungen
- [x] Queue- und Statusfilter in den Ticketlisten „Aktuelle Aufgaben“ und „Neue Aufgaben“ entfernen; Volltextsuche und Sortierung bleiben
- [x] Sidebar unter „Wochenplanung“ um „Ticketsystem“ und eigenen Browser-Reiter `/ticket-system` ergänzen
- [x] „Ticket öffnen“ von Classic-Ticketdetails auf den Plenaro-Ticketsystem-Reiter mit Ticket-ID verlinken, statt eine neue Znuny-Browserseite zu öffnen
- [x] Znuny-spezifischen, an die administrativ konfigurierte HTTPS-Webadresse gebundenen Server-Bridge-Endpunkt mit DNS-/Peer-Schutz statt beliebigem Open Proxy erstellen; externe Domain-Redirects sperren
- [x] Native Znuny-Webanmeldung serverseitig mit eigenem verschlüsseltem Benutzerpasswort versuchen; getrennte serverseitige Cookie-Jars je Plenaro-Benutzer; niemals Znuny-Passwort oder Native-Cookies an den Browser weiterreichen
- [x] Native HTML in opak-origin sandboxed iframe rendern (kein `allow-same-origin`); fremdes JavaScript kann nicht auf Plenaro-Sitzung, API-Daten oder Eltern-DOM zugreifen
- [x] Grundlegende relative URL-/Asset-/Formular-Linkanpassung, um die Webansicht innerhalb des Proxy-Pfads zu halten
- [x] CI-Build und Docker-Smoke-Test erfolgreich ([Run #41499453099](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41499453099), [PR #47](https://github.com/DerHausmeisterKrause/plenaro/pull/47))
- [ ] Live-Abnahme gegen tatsächliche Znuny-Weboberfläche, inklusive Login-CSRF, Assetpfaden, Sessions, Formularen und Ticket-Deep-Links; benutzerdefinierte Plugins/Skripte und Entra-SSO/2FA können unter iframe-Sandbox nicht garantiert funktionieren
- [ ] Für frei konfigurierbare Favoriten/Wiki eine **separate isolierte Origin** mit expliziter Website-Freigabe und Authentifizierungskonfiguration entwerfen. Bestehende Znuny-Bridge ist ausdrücklich kein offener Universal-Proxy.


### 02.11.2026 – Regression: Antworten bei fehlenden Znuny-Kundenartikel-Metadaten (Phase 6)
- [x] „Antworten“ und „Erledigt“ dürfen die Eingabemaske nicht mehr allein wegen fehlender `SenderType`-/`CommunicationChannel`-Angaben oder rein interner Artikel sperren
- [x] Wie in Classic Empfänger ausschließlich serverseitig bestimmen: letzte passende Kunden-E-Mail, alternativ gültige E-Mail aus `Ticket.CustomerUserID` (`CustomerUser` als API-Alias); kein clientseitig angegebener `To`-Wert
- [x] Znuny-Article-Parsing um `SenderTypeID=3`, `ArticleType` und alternative Kanalfelder ergänzen
- [x] Antwortvorlage wird auch ohne Kundenartikel angezeigt; nicht auflösbare Personennamen bleiben markiert, kein geratener Empfänger
- [x] Frontend-Regressionstests für fehlende Kundenmetadaten, eingehende `email-external`-Artikel und ausschließliche Agentenartikel
- [x] CI (Frontend, Backend, Docker) erfolgreich ([Run #41503261769](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41503261769), [PR #48](https://github.com/DerHausmeisterKrause/plenaro/pull/48))
- [ ] Liveprüfung der Empfängerauflösung und Antwortversand am echten Znuny-Ticket (keine produktive Nachricht durch CI)


### 02.11.2026 – Persönliche Znuny-Antwortvorlage und Browser-Logo (Phase 6)
- [x] Persönliche Antwortvorlage in den eigenen Znuny-Einstellungen bearbeitbar machen (auch ohne Administratorrolle), mit Benutzerprofil in PostgreSQL speichern (Migration 0025)
- [x] Globale Admin-Antwortvorlage als Fallback behalten, falls der Agent keine eigene Vorlage festlegt; keine Änderung an Agenten-ID-/Kandidaten-ID-Berechtigungen
- [x] Beide Antwortvorlagen-Felder über volle Formularbreite als mehrzeilige Textfelder mit Dark/Light-Theme darstellen; Beispiel mit echten Zeilenumbrüchen statt sichtbaren `\\n`-Zeichen
- [x] Persönliche Vorlage bei „Antworten“ und „Erledigt“ priorisieren; Variablen aus Ticket und Kundenartikel wie Classic ersetzen
- [x] Bereits gespeicherte `\\n`-Zeichenfolgen beim Rendern in normale Absatzumbrüche umwandeln
- [x] Vorhandenes Desktop-Plenaro-Logo `project_tool/Assets/Plenaro.png` als Browser-Tab-Favicon verlinken (aktuell aus öffentlichem GitHub-Repository geladen)
- [x] Frontend- und Docker-Regressionstests für Vorlagenspeicherung, -Priorität und Zeilenumbrüche
- [x] Vollständige GitHub-CI erfolgreich ([Run #41509498760](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41509498760), [PR #49](https://github.com/DerHausmeisterKrause/plenaro/pull/49))
- [ ] Live-Verifikation der Vorlage beim Antworten und Schließen am Znuny-Server
- [ ] Optional Favicon-Binärdatei in das Webrepo lokal übernehmen, um keine externe GitHub-Bildanforderung zu benötigen


### 02.11.2026 – Znuny-Schreibaktionen: 502-ACK-Fix und durchgängige Tests (Phase 6)
- [x] TicketUpdate akzeptiert jetzt bestätigtes Znuny-`{"Success":1}`/`true` sowie klassische `TicketID`-/`ArticleID`-ACKs, auch in `Data`; ein HTTP-200 mit leeren/ungültigen Daten wird weiterhin **nicht** als Bestätigung gewertet
- [x] Eindeutige Znuny-API-Ablehnungen (`Success=0`, `ErrorCode`, HTTP 4xx) von unklaren Zuständen (Timeout, HTTP 5xx, kaputte Antwort) trennen; nur kurze sichere Fehlercodes ohne E-Mail-/Ticketinhalt zurückgeben
- [x] Fehler bei Session-Aufbau / TicketGet vor dem Schreiben von unklaren Übertragungsfehlern unterscheiden; **nie** einen Remote-Write automatisch wiederholen
- [x] Das gilt für Antwort, Antwort und gleichzeitiges Schließen, Besitz-/Verantwortlich-Zuweisung, direkte Statusänderung und Znuny-Zeitbuchung
- [x] SQL-Migration 0026: explizit abgelehnte Buchungen als `rejected` auditieren und für korrigierte erneute Buchung entsperren. Timeouts/5xx bleiben `pending` gegen Doppelbuchung
- [x] Zeitbuchungs-UI stoppt jetzt bei einem laufenden Timer tatsächlich vor der Übertragung; abgelehnte Buchungen sichtbar von ungeklärten getrennt
- [x] POST/PATCH als Administrator-Einstellung für TicketUpdate hinzugefügt (Default Classic POST /Ticket/{TicketID}/Update; Standard Znuny kann PATCH /Ticket/{TicketID} nutzen), kein automatischer Wechsel oder zweiter Schreibversuch
- [x] Zusätzlicher mockbasierter .NET-HTTP-Kontrakttest für Reply, Reply+Close, Owner/Responsible, Internal-Zeitbuchung mit `TimeUnit`, DynamicFields und unterschiedlichen Znuny-ACKs/Fehlercodes; keine echte API-Nachricht oder Buchung
- [x] CI grün: Frontend-/Backend-Build, Znuny-Mock-Integration und Docker ([Run #41513985836](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41513985836), [PR #50](https://github.com/DerHausmeisterKrause/plenaro/pull/50))
- [ ] Echte Znuny-Installation: TicketUpdate-Mapping, API-Version, Artikel-Versand-Erweiterung `ArticleSend`, tatsächliche E-Mail-Zustellung, Schließen und 15-Minuten-Buchung produktionsnah verifizieren


### 02.11.2026 – Browser-Egress für Znuny auf Plenaro beschränken (Security-Hardening)
- [x] Gespeicherte Ticket-URLs bleiben als persönliche Referenz erhalten, werden aus dem Ticketdetail aber nicht mehr als externe Browser-Links geöffnet. „Ticket öffnen“ verwendet ausschließlich `/ticket-system?ticket=ID`.
- [x] Die native Znuny-Webbridge ersetzt fremde oder nicht genehmigte Link-/Asset-URLs durch `about:blank` statt sie im Browser direkt anzufordern.
- [x] Auf eingebettetes Znuny-HTML kommt eine restriktive CSP für Skripte, Styles, Bilder, Formulare und Netzwerkzugriffe: als Netzwerkziel ist ausschließlich die aktuelle Plenaro-Proxy-Origin freigegeben; Sitzungen und Zugangsdaten bleiben auf dem Backend. Im Fallback bleibt die opaque iframe-Sandbox ohne `allow-same-origin`; am dedizierten HTTPS-Port ist `allow-same-origin` erlaubt, weil dort keine normalen Plenaro-APIs erreichbar sind.
- [x] Frontend-Regressionstest für internen Ticket-Deep-Link und fehlenden externen Direktlink ergänzt.
- [ ] GitHub-CI nach PR-Erstellung prüfen; den neuen Egress-Schutz zusätzlich im Browser-Netzwerkmonitor bestätigen.
- [x] Getrennte, konfigurierte HTTPS-Origin auf eigenem NGINX-Port 8444 mit unveränderten Znuny-AJAX-/Root-URL-Anfragen über die serverseitige Bridge und exakt beschränkter Origin-Freigabe implementieren; bei fehlender Portal-Origin bleibt der sichere Fallback.
- [ ] Vollständige interaktive Znuny-Web-GUI (AJAX/Formulare, ggf. SSO) sowie die Freischaltung des separaten Ports über Teleport auf dem Zielhost testen.
- [ ] Antwort/Schließen/Zeiten mit der tatsächlich in Znuny konfigurierten TicketUpdate-Methode und -Route gegen ein Testticket live prüfen. Kein zweiter automatischer Schreibversuch bei HTTP 502 oder Timeout.

- [x] Sicheren persönlichen Znuny-API-Test pro Ticket implementieren: SessionCreate + TicketGet ohne TicketUpdate, mit Anzeige der tatsächlichen Schreibroute/-methode und eindeutiger Stufendiagnose
- [x] SessionCreate-Fehler trotz HTTP 200 (keine SessionID oder Znuny ErrorCode) von TicketGet und echten 502-Schreib-Timeouts unterscheidbar machen; ohne sensibles Response-Logging
- [x] Optional fehlende DynamicFieldOptions-Route (404 / nicht freigeschaltet) blockiert die Zeitbuchungsvorbereitung nicht mehr; nur Admin-Fallback-Optionswerte verwenden
- [x] Mockkontrakttests für die fehlgeschlagene persönliche Session und die Zeitbuchungs-Fallbackwerte ergänzen
- [ ] Die bislang fehlschlagenden produktiven Antworten, Schließen, Zuweisungen und Zeitbuchungen am echten Znuny-Testticket mit persönlichem Konto verifizieren; der Browser-/Docker-Test simuliert diese Aktionen ausdrücklich nur

### 03.11.2026 – Echte Znuny-WebView aus Docker ohne zusätzlichen öffentlichen Port (Phase 6)

Die in PR #51 eingesetzte HTML-iframe-Bridge mit Port 8444 ist **abgelöst**:
Sie konnte native Znuny-Skripte und AJAX nicht zuverlässig in einer
fremden/opaque Browser-Origin darstellen; der Reiter blieb auf dem
Zielhost weiß. Die historische Checkliste dokumentiert den Zwischenstand.

- [x] Eigenen öffentlichen Port 8444, zweites HTTPS-NGINX-VHost und separate Browser-Origin entfernen
- [x] Defekte HTML/JS-Umschreib-Bridge sowie den direkten iframe aus dem Ticketsystem-Reiter entfernen
- [x] Echten Chromium-Browser im zusätzlichen internen Docker-Container `znuny-browser` einrichten, ohne Host-Portfreigabe
- [x] Private Chromium-Sitzungen pro authentifiziertem Plenaro-Benutzer mit Agentenprüfung, Sessionlimit, Inaktivitätslimit und verschlüsselten persönlichen Zugangsdaten
- [x] Direkte Ticket-Deep-Links nur bei serverseitig bestätigtem Ticketzugriff an interne Chromium-Sitzung übergeben
- [x] Interaktive browserseitige Bildansicht mit Maus-, Scroll-, Tastatur-, Navigations- und Paste-Eingaben über die geschützte Plenaro-API
- [x] Kein Znuny-Zugriff, keine Znuny-Cookies, keine Znuny-JavaScript-Ausführung und kein Selenium-WebDriver-Zugriff am Benutzer-PC
- [x] Start-/Fehlerstatus statt rein weißer Oberfläche und Frontend-Regressionstest für in Plenaro gerendertes Browserbild
- [x] GitHub-CI Frontend-, .NET- und Docker-Tests inklusive echtem Chromium-Screenshot erfolgreich ([PR #52](https://github.com/DerHausmeisterKrause/plenaro/pull/52), [CI #41585802921](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41585802921))
- [ ] Echter Zielhost: Netzverbindung und Zertifikatsvertrauen des Chromium-Containers zu `ticketsystem.intern.example` bestätigen
- [ ] Echtes Znuny-Webportal: Login, CSS, JavaScript/AJAX, Ticket-Deep-Links, Formularbedienung, Anhänge und Bedienbarkeit prüfen
- [ ] Reale Ticketantworten, Schließen, Zuweisungen und Zeitbuchungen weiterhin separat gegen ein sicheres Testticket bestätigen; die WebView ersetzt **nicht** die API-Write-Verifikation

**Phase:** 6 / Issue #7; Desktop-Ansicht funktional statt HTML eingebettet.

### 03.11.2026 – Gemeinsames internes CA-Trust und JPEG-Streaming (Phase 6)

- [x] `.env`-Variable `PLENARO_TRUSTED_CA_FILE` für eine vertrauenswürdige interne Root-CA auf dem Host bereitstellen
- [x] Persönlichen Backend-Znuny-HTTPS-Client um interne CA ergänzen und die öffentlichen CA-Roots erhalten (OpenSSL/`SSL_CERT_FILE`)
- [x] Interne CA beim Chromium-Start in den NSS-Store importieren; keine unsicheren TLS-Flags
- [x] Backend und Selenium-Container bleiben unprivilegiert; keine weiteren öffentlichen Ports
- [x] Serverseitige Screenshot-Übertragung von großen PNG-Bildern auf komprimiertes JPEG mit PNG-Fallback umstellen
- [x] CI: CA-Mounts, NSS-Trust, .NET-Bundle und Chromium-JPEG-Screenshot erfolgreich ([PR #53](https://github.com/DerHausmeisterKrause/plenaro/pull/53), [CI #41588658814](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41588658814))
- [ ] Auf dem Zielhost echtes Znuny-HTTPS gegen die interne interne Root-CA verifizieren und Zertifikatswarnung in Chromium prüfen
- [ ] Die Responsivität über die tatsächliche Teleport-Verbindung im Alltag bestätigen

### 03.11.2026 – Interaktive Browseransicht beschleunigen (Phase 6)

- [x] Echte Chromium-CDP-Bildereignisse über serverseitige WebSocket-Verbindung abonnieren und als geschützten, same-origin SSE-Stream an die Weboberfläche liefern
- [x] Vollständige Screenshot-Polling-Rundreisen durch kontinuierliche komprimierte JPEG-Ereignisse ersetzen; bei fehlender CDP-Unterstützung kompatiblen Bildmodus beibehalten
- [x] Chromium-Viewport bei Größenänderungen an die verfügbare Fläche der Plenaro-Seite anpassen und nicht mehr auf 1280 Pixel Breite begrenzen
- [x] Unnötiges zusätzliches Text-Einfügeformular unter der Browseransicht entfernen; Einfügen über die reguläre Zwischenablage bleibt möglich
- [x] Keine internen WebDriver-/CDP-Ports veröffentlichen; nur der authentifizierte Benutzer erhält Bilddaten, persönliche Zugangsdaten bleiben serverseitig
- [x] End-to-End-CI: Frontend, Backend, Chromium-WebSocket-Screencast und Docker-Smoke erfolgreich ([PR #1](https://github.com/DerHausmeisterKrause/plenaro/pull/1), [CI #41596808475](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41596808475))
- [ ] Echtes Zielsystem: Geschwindigkeit, Darstellung, Tastatur, Navigation und Scrollen im Browser überprüfen

### Phase 6 – Native HTML-Einbettung über isolierte Ticketsystem-Origin

- [x] Separate HTTPS-Hostnamen für Plenaro-Frontend und Ticketsystem-Proxy konfigurieren – gleicher Server, gleicher Port 443
- [x] Interne HTTP-Requests, Redirects, HTML/CSS/JS/JSON und binäre Antworten serverseitig weiterleiten
- [x] Automatisches Agenten-Login mit individuellen Vault-Zugangsdaten, Loginformular und getrennten Upstream-Cookie-Jars
- [x] Einmaliges Bootstrap-Token, host-exklusives HttpOnly-Cookie, CSP und iframe-Sandbox für getrennte Browser-Origin
- [x] Je Nutzer eigene parallele Proxy-Sitzung mit Ablaufzeiten, Autorisierungsprüfung und Widerruf beim Plenaro-Logout
- [x] Native HTML-Weboberfläche füllt die Ticketsystem-Fläche, ohne Server-Screenshot-Streaming
- [x] Vorherigen Chromium-Modus als manuell auswählbaren Fallback behalten
- [x] CI: .NET-Build, React-UI-Tests, isolierte Loginformular-/Origin-Prüfungen und Docker-Start erfolgreich ([PR #2](https://github.com/DerHausmeisterKrause/plenaro/pull/2), [CI #41601862700](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41601862700))
- [ ] Zwei HTTPS-DNS-Namen und gemeinsames Zertifikat am echten Plenaro-Host eingerichtet
- [ ] Native Anmeldung, Navigation, AJAX, Formulare, Download und Mehrbenutzertrennung am echten Ticketsystem geprüft
- [ ] Nach bestätigter Funktionalität optional den bisherigen Chromium-Container aus Compose entfernen


### 03.11.2026 – Chromiums Remote-Browser vollständig entfernt (Phase 6)

- [x] Serverseitigen Selenium-/Chromium-Docker-Service inklusive CA-Importskript und Image-Build entfernen
- [x] WebDriver-Backend, Screenshot-API und CDP-Screencast vollständig entfernen
- [x] React-Ticketsystem-Reiter ausschließlich mit nativer HTML-Einbettung über die isolierte HTTPS-Proxy-Origin betreiben
- [x] Screenshot-Bildmodus, unnötiges Paste-Feld und zugehörige CSS-Klassen entfernen
- [x] Ohne separate HTTPS-Origin keine unsichere Einbettung oder stillschweigenden Rückfall: klare lokale Einrichtungsanleitung im Reiter
- [x] Docker-CI auf Backend-CA-Vertrauen, native Proxy-Authentifizierung und kein zusätzliches Browser-Image umstellen
- [x] CI-Integration (Build/Tests/Docker-Smoke) nach dem Refactoring erfolgreich ([PR #3](https://github.com/DerHausmeisterKrause/plenaro/pull/3), [CI #41605224970](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41605224970))
- [ ] Zielhost: DNS/TLS für zwei same-site Plenaro-Origins einrichten und echte Znuny-HTML-Anmeldung sowie parallele Nutzer prüfen
- [ ] Reale Ticketoperationen (Antworten, Schließen, Zeitbuchen) weiterhin ausschließlich anhand eines kontrollierten Testtickets prüfen

**Hinweis:** Der bisherige Chromium-Container wird beim Update mit
`docker compose up -d --build --remove-orphans` entfernt. Die
PostgreSQL-Datenbank und der Vault bleiben unverändert.


### Phase 6 – Native Anmeldung diagnostizieren ohne Architekturwechsel

- [x] Den ursprünglichen serverseitigen Agenten-Autologin beibehalten
- [x] Bei individuellen Loginformularen oder zurückkehrender Loginseite eine reguläre Anmeldung in derselben isolierten HTML-Proxy-Sitzung erlauben
- [x] Transport/TLS, Timeout, nicht unterstützte Redirects und HTTP-Serverfehler durch neutrale, nicht-sensitive Fehlercodes unterscheiden
- [x] Bei nicht-JSON-Gateway-Antworten den HTTP-Status statt einer irreführenden generischen Login-Meldung anzeigen
- [x] Tests mit rein fiktiven Hostnamen für automatische Anmeldung, interaktiven Login und blockierte externe Weiterleitung ergänzen
- [x] GitHub-CI erfolgreich: Backend, Frontend, fiktive Loginfälle und Docker-Smoke ([PR #4](https://github.com/DerHausmeisterKrause/plenaro/pull/4), [CI #41610855241](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41610855241))
- [ ] Am Zielhost Ursache anhand des neutralen Fehlercodes verifizieren sowie native Anmeldung und iframe-Bedienung prüfen


### Phase 6 – HTTP 400 hinter vorgeschaltetem HTTPS-Proxy

- [x] Öffentliche Browser-Origin statt intern normalisiertem Host zur Prüfung des nativen Sitzungsstarts verwenden
- [x] HTTPS-, CSRF- und Browser-Fetch-Metadata-Prüfungen beibehalten; fremde/opaque Origins weiterhin verweigern
- [x] Neutrale Fehlercodes bei falschem HTTPS oder nicht passender Browser-Origin ausgeben
- [x] Automatisierte Tests für umgeschriebenen Frontproxy-Host und Cross-Origin-Abwehr ergänzen
- [x] GitHub-CI (Backend, Frontend, Znuny-Vertrag und Docker-Smoke) erfolgreich ([PR #5](https://github.com/DerHausmeisterKrause/plenaro/pull/5), [CI #41619802978](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41619802978))
- [ ] Vor Ort die beiden HTTPS-Proxy-Hostnamen und automatische Anmeldung verifizieren


### Phase 6 – Agenten-Webseite trotz ungewöhnlichem Content-Type erkennen

- [x] Unberechtigte harte `text/html`-Prüfung am ursprünglichen Agenten-Webseiten-GET durch begrenzte Prüfung auf tatsächliche HTML-Struktur ersetzen
- [x] Fehlender oder abweichender MIME-Typ (`text/plain`, `application/xhtml+xml`, `application/octet-stream`) wird bei echtem HTML akzeptiert
- [x] JSON-/Leerantworten und sonstiges Nicht-HTML weiterhin ablehnen (`page-json`, `page-empty`, `page-not-html`)
- [x] Falsch als `text/plain` ausgelieferte HTML-Einstiegsseite wird im isolierten iframe als HTML gerendert, keine Änderung an Anhängen oder Downloads
- [x] Login-Nachfolgeantworten weiterhin kompatibel mit bisherigen Nicht-HTML-Erfolgsantworten
- [x] Fiktive Tests und vollständige CI erfolgreich ([PR #6](https://github.com/DerHausmeisterKrause/plenaro/pull/6), [CI #41623430116](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41623430116))
- [ ] Gegen echte Agenten-Weboberfläche prüfen, welcher HTTP-Antworttyp vorliegt und ob die native Einbettung vollständig funktioniert

### 03.11.2026 – Native Ticketsystem-HTML und Fenstergröße (Phase 6)

- [x] HTML-Dokumente bei falsch gemeldeten Navigations-MIME-Typen sicher als `text/html` ausgeben
- [x] CGI-Navigation mit Ticket-/Dashboard-Query unterstützen; nicht-HTML-Anhänge unverändert streamen
- [x] CSS des Ticketsystem-Reiters aus dem allgemeinen Maximalbreiten-/Seitenpadding lösen; iframe füllt Arbeitsbereich unter der Topbar
- [x] Synthetische HTML-/Nicht-HTML-/Download-Tests ergänzen, keine echten Ticketdaten übernehmen
- [x] GitHub-CI: Backend/Frontend/Vertrags- und Docker-Smoke-Tests erfolgreich ([PR #7](https://github.com/DerHausmeisterKrause/plenaro/pull/7), [CI #41635960052](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41635960052))
- [ ] Am tatsächlichen Ticketsystem Darstellung, interne Links, AJAX, Anhänge und mehrere Benutzer verifizieren

### 03.11.2026 – Wildcard-Vorbereitung für Favoriten (Phase 7)

- [x] WPF-Favoritenmodell (URL, Benutzerzugang, Sortierung, Auto-Login) ausgewertet
- [x] Optionale `PLENARO_FAVORITES_BASE_DOMAIN`: unbekannte Aliasse vor der Plenaro-Login-Origin gesperrt (404)
- [x] NGINX-Wildcard-Hostname, DNS und TLS dokumentiert
- [x] Contract-Tests für unbekannte Hosts, bekannte Origins und Localhost ergänzt
- [x] CI der neuen Wildcard-Host-Sperre erfolgreich ([PR #10](https://github.com/DerHausmeisterKrause/plenaro/pull/10), [CI #41639354920](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41639354920))
- [x] Admin-Freigabe für Apps, persönliche Favoriten, eigene App-Cookie-Jars und Vault-Auto-Login in PR #12 ergänzt
- [ ] Mit realer Wiki-/Favoriten-Anwendung Browserkompatibilität, Mehrbenutzerbetrieb und Sicherheitsgrenzen prüfen

Die bisherige Znuny-Integration bleibt unverändert. Diese Vorbereitung stellt **keinen** frei konfigurierbaren Universalproxy bereit.

### 03.11.2026 – Favoritenverwaltung und dedizierte Web-App-Proxy-Origins (PR #12)

- [x] Classic-Favoritenmodell (Name, URL, Sortierung, optionales Login) als Web-Favoriten nachgebildet
- [x] PostgreSQL-Migration 0027 für freigegebene Admin-Web-Apps, Benutzer-ACL und persönliche Favoriten
- [x] Administrationsoberfläche für HTTPS-App-Origins, Slug, aktiven Zustand und Benutzerfreigaben
- [x] Persönliche Favoriten-CRUD, Sortierung, optionale verschlüsselte Passwörter und eigener Browser-Reiter
- [x] Getrennter HTTPS-HTML-Proxy pro App-Subdomain mit eigener Benutzer-Session, Browser-CSP, Server-Cookies, Bootstrap-Token und Logout-Widerruf
- [x] Browser-Origin/CSRF, HTTPS/DNS/Peer-Prüfung und laufende Benutzer-/App-Berechtigungsprüfung; keine dynamische Freigabe beliebiger URLs
- [x] Bestehender Znuny-Proxy fachlich unverändert; nur Register-/Logout-Middleware ergänzt
- [x] GitHub-CI: Backend, Frontend, Favoriten-/Znuny-Vertragstests und Docker-Favoriten-Smoke erfolgreich ([PR #12](https://github.com/DerHausmeisterKrause/plenaro/pull/12), [CI #41650719103](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/41650719103))
- [ ] Browser-Abnahme mit echtem Wiki: Login, AJAX, CSS/JS, Downloads und zwei parallele Benutzer
- [ ] App-Sonderfälle (SSO/2FA, WebSockets, CSP und Service Worker) nur bei Bedarf implementieren
- [ ] Legacy-Favoriten (inklusive nur sicherer Konfiguration) kontrolliert migrieren



### 09.11.2026 – Phase 6: persönliche Znuny-Queue-Rechte erzwingen (PR #15)

- [x] Gemeinsamen Service-Account-Ticketcache nicht mehr als Benutzerberechtigung behandeln
- [x] Ticketlisten und Agenda auf persönlich autorisierte Znuny-TicketIDs beschränken
- [x] API-Ticketdetails, Artikel, Anhänge, Planungs-/Schreibaktionen und Native-Deep-Links mit persönlichem TicketGet prüfen
- [x] Fail closed bei fehlenden Rechten/Session/Timeout; Cacheänderungen invalidieren
- [x] CI: Backend/Frontend, Znuny-Contract und Docker-Smoke erfolgreich ([Run #42568973461](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42568973461))
- [ ] Live-Test mit mindestens zwei Queue-Berechtigungsprofilen und direktem Fremd-Ticketzugriff
- [ ] Laufzeiten / Serverlast bei größeren Ticketmengen messen und Suchstrategie gegebenenfalls skalieren


### 09.11.2026 – Phase 6: eigene/segmentierte Tickets ohne Queue-API-Failure (PR #17)

- [x] Besitzer- und Verantwortlichen-Tickets ohne persönliche globale TicketSearch weiterhin sichtbar
- [x] Segmentierte oder via Teilnehmer-E-Mail getaggte Tickets in Aufgabenansichten und Heute/Agenda sichtbar
- [x] Erledigte Aufgaben bei persönlicher Historie (Notiz, Zeitbuchung, Segment) weiterhin sichtbar
- [x] Nur unzugewiesene **Neue Aufgaben** anhand der individuellen Znuny-Queue-Leserechte herausgeben
- [x] Fehlende Znuny-API-Berechtigungsbestätigung zeigt keine fremden neuen Tickets, blockiert aber keine eigenen Aufgaben
- [x] Ticket-ID-Routen, Planung und Native-Znuny-Proxy entsprechend nachgezogen
- [x] CI: Build/Contract und Docker-Smoke bestanden ([PR #17](https://github.com/DerHausmeisterKrause/plenaro/pull/17), [Run #42573584221](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42573584221))
- [ ] Echte Queue-Rollen in Znuny/OTRS auf dem Zielhost vergleichen; Performance bei vielen Queues und Ticketdetails prüfen


### 09.11.2026 – Phase 7: Favoriten-Bootstrap zuverlässiger (PR #18)

- [x] Double-Mount-Race beim Favoriten-Start durch pro Favorit/Revision geteilte Start-Promise beseitigt
- [x] Tokenfreier Alias-Health-Check zur Prüfung von DNS, vorgelagerter NGINX, Docker-NGINX und Backend
- [x] Browser- und Backend-Regressionstests; vollständige GitHub-CI bestanden ([Run #42576527912](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42576527912))
- [ ] Live-Probe auf dem Zielhost und Wiki-HTML/Anmeldeablauf abnehmen; ggf. NGINX/Wildcard-TLS korrigieren


### 09.11.2026 – Phase 7: einlagige Favoriten-Hostnamen (PR #19 in main)

- [x] WPF-WebShortcut-ID, URL, Browserprofile und persönliche Sessions gegen Web-Favoriten geprüft
- [x] Optionale `PLENARO_FAVORITES_HOST_TEMPLATE` für `{slug}-plenaro.example.org` implementiert
- [x] Alte `slug.apps.example.org`-Adressen bleiben parallel routbar, falls die Base-Domain gesetzt bleibt
- [x] Host-Guard, URL-Aufbau, Admin-Validierung und Diagnose auf beide Hostformate angepasst
- [x] Docker Compose leitet die neue Host-Vorlage aus .env an ASP.NET weiter; CI prüft die Übergabe
- [x] Beschädigte doppelte NGINX-Dokumentation korrigiert
- [x] Regressionstests für DNS-Alias, alte Favoriten, fehlerhafte Hosts und ungültige Templates ergänzt
- [x] GitHub-CI: .NET/React-Build, Favoriten-/Znuny-Contract-Tests und Docker-Smoke erfolgreich ([PR #19](https://github.com/DerHausmeisterKrause/plenaro/pull/19), [Run #42589498554](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42589498554)); in main gemergt
- [ ] Lokale Windows-DNS-/Wildcard-TLS-/vorgeschaltete-NGINX-Konfiguration einmalig anpassen
- [ ] Neue Wiki-Alias-Healthprobe und parallelen Realbrowser-/ACL-Test durchführen
- [ ] Native Znuny-Antwortfenster/502-Problem separat abnehmen; durch Hostnamenänderung nicht gelöst


### 09.11.2026 – Phase 7: gemeinsamer Favoriten-Pfadhost (PR #20 in main)

- [x] WPF-WebShortcut-URL, persönliche Browser-Sessions und bisherige Web-Favoriten wieder geprüft.
- [x] Opt-in `PLENARO_FAVORITES_PATH_ORIGIN` für `apps-plenaro.example.org/wiki/` implementiert, alte Host-/Template-Varianten bleiben erhalten.
- [x] Ein gemeinsamer Host mit serverseitiger Pfad-/Sitzungsbindung, Cookie-Path pro Slug, unbekannte URLs 404.
- [x] Pfad-Prefix bei Bootstrap, Redirect, Upstream, textuellen Links und Formularen berücksichtigt.
- [x] Path-Routing-Vertragstests und ursprüngliche Favoriten-/Znuny-Tests beibehalten.
- [x] PR #20 kontrolliert nach main gemergt; .NET/React-Build, Contract-Tests und Docker-Smoke erfolgreich ([Run #42601040551](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42601040551)).
- [x] React-Frontend-Bootstrap für `/<slug>/_plenaro/favorite-bootstrap` angepasst, anderer Favoriten-Slug wird abgewiesen.
- [ ] Einmaligen DNS-A-Record, vorgelagerten NGINX-vHost und vorhandenes TLS-Zertifikat am echten Host prüfen.
- [ ] Wiki-/Cloud-AJAX, Login, HTML/JS, Downloads, CSP/Service Worker und zwei Benutzer im Browser abnehmen.
- [ ] Sicherheitsentscheidung: Ein Pfadhost teilt dieselbe Browser-Origin über alle Apps; nur vertrauenswürdige Apps zulassen.
- [ ] Bereits offene Phase-6-Punkte inklusive Znuny-502 und Antwort-Dialog bleiben bestehen.


### 09.11.2026 – Phase 7: CSS/JS und Ressourcen auf dem gemeinsamen Pfadhost (PR #21)

- [x] HTML-Ressourcen einschließlich unquoted src/href/action und srcset-Kandidaten auf /<slug>/ präfixieren.
- [x] JS-Fetch-/Import-Template-Strings und CSS-url()/Import-Pfade umschreiben; kein doppeltes /<slug>/.
- [x] MIME-Korrektur bekannter Asset-Endungen bei text/plain/octet-stream sowie /<slug>-Rootredirect.
- [x] Regressionstests und vollständige CI bestanden; PR #21 in main gemergt ([Run #42604023749](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42604023749)).
- [ ] Echtes Wiki im Browser auf CSS/JS/AJAX/Bilder/Downloads und zwei Benutzer prüfen; spezifische 404/CSP/CORS-Fehler nachziehen.
- [ ] Grenzen für Drittanbieter-CDN, WebSockets, OAuth und Service Worker beachten; keine generelle App-Isolation durch Pfad.


### 09.11.2026 – Phase 7: HTTPS-Chain gegen äußeren NGINX-502 (PR #22 in main)

- [x] Optionales PEM-Intermediate-Bundle per PLENARO_TLS_CHAIN_PATH eingeführt, Leaf/Chain im Docker-NGINX kombiniert und bei Leaf-only gewarnt.
- [x] Kompatibilität mit vorhandenem Fullchain-PEM durch gesonderten NGINX-Container-Test geprüft.
- [x] CI insgesamt erfolgreich ([Run #42623296351](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42623296351)); PR #22 in main gemergt.
- [ ] Lokale Zertifikatskette in Host-.env ergänzen oder PLENARO_TLS_CERT_PATH auf echtes fullchain.pem stellen, Docker-NGINX neu erstellen; strikt ohne -k verifizieren.
- [ ] Realen Favoriten im Browser (CSS, JS, AJAX) erneut prüfen und verbleibende Fehler separat behandeln.


### 11.11.2026 – Phase 7: Wiki-Oberfläche und Ressourcen im Pfadhost (PR #23)

- [x] Confluence-kompatible HTML-Kontextpfad-Metadaten gezielt auf gemeinsamen /<slug>-Pfad aktualisiert.
- [x] Textressourcen-Limit für CSS/JS auf 24 MiB begrenzt erweitert; HTML/JSON bleibt 6 MiB.
- [x] Regressionstests und vollständige CI erfolgreich ([Run #42986077812](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42986077812)); PR #23 in main.
- [ ] Liveprüfung echter Wiki-Ressourcen/Browser-Konsole, Benutzeranmeldung, Buttons, Logos und Formularfelder.
- [ ] Einschränkungen von Path-Rewriting für WebSockets, fremde CDNs, OAuth und Service Worker weiterhin beachten; Browser-Origin-Sicherheit nicht aufweichen.


### 11.11.2026 – Phase 7: Confluence-JavaScript und Inline-Skript-Schutz (PR #24)

- [x] Defekten allgemeinen JS-Textpfad identifiziert: Root-URL-Rewrite beschädigte RegEx-Literale in `batch.js`, obwohl Ressourcen HTTP 200 zurücklieferten.
- [x] JS und JSON unverändert streamen; Inline-HTML-Skripte gegen Regex-Rewrite geschützt; HTML-/CSS-/Logo- und Confluence-Kontextpfadkorrektur bleibt erhalten.
- [x] Regressionstests und komplette CI erfolgreich ([Run #42990397008](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42990397008)); PR #24 in main gemergt.
- [ ] Echte Browser-Abnahme mit Menüs, Bildern, Buttons und Formularen nach neuem Build; ggf. dynamische Root-/WebSocket-URLs prüfen.


### 11.11.2026 – Phase 7: dedizierter Wiki-Host parallel zum Shared-Path-Host (PR #25)

- [x] HAR-Erkenntnis: Confluence-Plugin-Root-REST/JS-URLs bleiben unter /wiki/ inkompatibel, HTTP 401/404, Login-POST nicht belegt.
- [x] `PLENARO_FAVORITES_DEDICATED_SLUGS` erlaubt für ausgewählte Favoriten Root-Host-Cookies und Root-API-Routing ohne globale Weiterleitung fremder /rest-Requests; andere Favoriten bleiben auf /<slug>/.
- [x] Tests/CI vollständig erfolgreich ([Run #42995835637](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/42995835637)); PR #25 in main.
- [ ] DNS/NGINX-Hostalias und .env auf Zielserver, Liveprüfung der Confluence-Anmeldung.

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

## 17.11.2026 – Phase 7: persönliche Intranet-Favoriten gezielt wieder zulassen (PR #41 – gemergt)

- [x] Regression nach PR #38 eingegrenzt: frei angelegte `site-*`-Favoriten aktivieren stets den öffentlichen IP-Filter; frühere Admin-Freigaben gelten nur noch für ihre ursprüngliche Origin.
- [x] Neue Betreiber-Einstellung `PLENARO_FAVORITES_INTERNAL_ORIGINS` für **exakte** HTTPS-Hosts und Ports (keine Wildcards, kein URL-Pfad) ergänzt, ohne Benutzerrechte zur Freigabe beliebiger privater IPs.
- [x] In den bestehenden `SecureHandler` integriert: Ausnahmen nur für vom Betreiber explizit genehmigte Origins oder migrierte Bestandsfreigaben; sonst bleibt `publicOnly` unverändert; Wiki-Verhalten bleibt unverändert.
- [x] Compose, .env-Beispiel und Sicherheits-/Deployment-Dokumentation erweitert; Regressionstests für exakte Origin-Übereinstimmung, Porttrennung, nicht passende Domains und fehlerhafte Konfiguration ergänzt.
- [x] [PR #41](https://github.com/DerHausmeisterKrause/plenaro/pull/41) nach `main` gemergt; [CI #44048722860](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44048722860) erfolgreich (Backend, Frontend, Proxy-Vertragstests, Docker-Smoke).
- [ ] Auf Zielhost exakte interne HTTPS-Origins in `.env` eintragen, Web-Container neu erstellen, betroffenen Favoriten neu verbinden und `private-upstream-blocked`/TLS/DNS live abnehmen.
- [ ] **Issue #40:** Eigene Browser-Origin pro benutzergesteuertem Favoriten statt gemeinsamer `/site-...`-Pfade; anschließend reelle Mehrbenutzer-/CSP-/JS-Sicherheitsabnahme.

## 17.11.2026 – Phase 7: interne HTTPS-Favoriten ohne Betreiber-Variablen (PR #42 – gemergt)

- [x] Erneuter WPF-Abgleich: favorisierte Intranet-Seiten werden vom Benutzer selbst konfiguriert.
- [x] Zusätzliche Freigabeliste `PLENARO_FAVORITES_INTERNAL_ORIGINS` aus PR #41 wieder entfernt; interne HTTPS-Favoriten laufen standardmäßig über den bestehenden sicheren TCP-Connector.
- [x] Selbstproxy-/Origin-/TLS-/Sitzungs-/Eigentümerschranken bleiben bestehen, ebenso Sperren für Loopback, Link-Local, bekannte Metadaten-Endpunkte, Multicast und nicht spezifizierte Adressen.
- [x] Testabdeckung für private IPv4-/IPv6-Adressen und weiterhin gesperrte Ziele ergänzt; Betriebs- und Sicherheitsdokumentation angepasst.
- [x] [PR #42](https://github.com/DerHausmeisterKrause/plenaro/pull/42) in `main` gemergt; [CI #44050034477](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44050034477) beim zweiten Lauf erfolgreich (inkl. Backend, Frontend, Proxy-Vertragstests und Docker-Smoke). Der erste Versuch scheiterte an einem nicht zu diesem Fix gehörenden, beim Wiederholen erfolgreichen Homeoffice-Frontend-Test.
- [ ] Deployment und Live-Abnahme: interne Favoriten, Wiki-Login, CSS/JS und zwei Benutzer; DNS-Auflösung und CA-Vertrauen des Containers prüfen.
- [ ] **Sicherheitspriorität:** Jeder authentifizierte Nutzer kann durch den Server erreichbare private HTTPS-Netzwerkziele ansprechen. Firewall/Netzsegmentierung und Berechtigungsmodell absichern; [Issue #40](https://github.com/DerHausmeisterKrause/plenaro/issues/40) (echte Browser-Origin-Isolation) offen.

Diese Änderung **ersetzt** die in PR #41 dokumentierte Pflicht, einzelne Origins in der `.env` freizugeben.

## 17.11.2026 – Phase 7: Persönliche Favoriten – Bilder und interaktive Logins (PR #43 – gemergt)

- [x] Gemeinsam genutzten Favoriten-Pfadhost analysiert: dynamische Root-/Origin-URLs, `srcset`, `innerHTML`, clientseitige Login-Navigation, Bild-MIME und fehlende Auth-/CSRF-Header als potenzielle Ursachen.
- [x] Persönlichen Browser-Shim für genau die freigegebene Upstream-Origin sowie responsive/dynamische Bilder, Formulare, SPA-Pfade und DOM-Änderungen erweitert; Wiki-Shim und unberechtigte Fremdhosts bleiben unberührt.
- [x] HTTPS-Bilder/-Fonts aus CDN per eng erweitertem CSP `img-src`/`font-src` zulassen, externe JS-/CSS-Quellen nicht pauschal freigeben; MIME-Fallback für häufige Bilder ergänzt.
- [x] Same-origin-Login-/CSRF-Header gezielt weitergeben, Referer sicher auf den eigenen Favoritenpfad begrenzen und nur dokumentierte iframe-Navigation per 307 zurückführen.
- [x] Synthetische Contract-Tests für personal vs. Wiki, Bild-MIME, sichere Reroutes, Referer-/Cookie-Grenzen und CSP ergänzt; zusätzlicher JavaScript-Laufzeittest des direkt aus C# extrahierten Browser-Shims gegen synthetische Bild-/Login-Anfragen.
- [x] [PR #43](https://github.com/DerHausmeisterKrause/plenaro/pull/43) nach `main` gemergt; [CI #44053732245](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44053732245) vollständig erfolgreich (Backend, Frontend, Proxy-Vertragstests, JS-Runtime-Test und Docker-Smoke).
- [ ] Live-Abnahme gegen ATOSS und weitere betroffene Apps: Bilder, interaktiver Login, Session/Cookie, 2 Benutzer, Konsole/Netzwerkanfragen, externe SSO-Grenzen; keine produktiven Anmeldedaten in GitHub.
- [ ] [Issue #40](https://github.com/DerHausmeisterKrause/plenaro/issues/40) dedizierte Browser-Origin pro freiem Favoriten; Sicherheit eigener und fremder Skripte noch nicht final gewährleistet.


## 17.11.2026 – Phase 7: Erkennung klassischer Login-Felder (PR #44 – gemergt)

- [x] Ursachenvergleich WPF ↔ Web: WPF-WebView2 füllt echte DOM-Felder nach JS-Rendern; Web schickt bislang nur ein serverseitiges Standardformular ab.
- [x] Serverseitige Erkennung für `autocomplete=username`, IDs, Platzhalter, `j_username`-/Enterprise-Namen und eindeutige Eingaben erweitert.
- [x] Vorhandene CSRF-/Hidden-Felder und Login-Submit-Werte mitnehmen, Same-Origin-Anmeldeseiten anhand einer HTML-Verlinkung oder begrenzter GET-Redirects finden, maximal ein Passwort-POST; keine fremden SSO-Domains.
- [x] Schutz vor GET-Passwortversand, Registrierung, Passwortänderung, Mehrfach-Passwortfeldern, fremden Action-URLs und JS-only-Formularen.
- [x] Contract-Tests mit synthetischen Enterprise-Loginformularen und Redirect-/SSO-Blockade ergänzt; UI-Hinweis zu Sicherheits- und JavaScript-Grenzen präzisiert.
- [x] [PR #44](https://github.com/DerHausmeisterKrause/plenaro/pull/44) in `main` gemergt; [CI #44058573710](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44058573710) erfolgreich; Frontend-Dependency `csstype` wegen fehlerhafter npm-Auflösung auf veröffentlichte 3.2.3 fixiert.
- [ ] Live-Abnahme der realen ATOSS-Anmeldung.
- [ ] **Issue #40 zuerst schließen**, bevor WPF-artiges Browser-Autofill Klartextpasswörter in beliebige Drittseiten am geteilten Host schreiben dürfte.


## 17.11.2026 – Phase 7: WPF-ATOSS-Autofill im Browser mit Host-Isolation (PR #45 – gemergt)

- [x] WPF-Referenz `Services/WebShortcutBrowserSessionManager.cs` geprüft: exakte ATOSS-ZK-Selektoren, native Eingabesetter, Events, wiederholte DOM-Abfragen, Frame-Kontext und Submit-Logik nachvollzogen.
- [x] Für persönliche Favoriten existierende Wildcard-DNS-/Hostvorlage automatisch als dedizierte Browser-Origin verwenden; Wiki bleibt auf Shared-Path, Pfad-Fallback ohne Domain bleibt bestehen.
- [x] Web-DOM-Autofill mit ATOSS-Selektoren, React-kompatiblen Value-Setter-/Input-/Change-Events, Ladeverzögerungen, MutationObserver und einmaligem Submit portiert.
- [x] Passwortübergabe nur nach Session-, Benutzer- und Host-Berechtigungsprüfungen auf genau einem dedizierten HTTPS-Host; auf gemeinsamem Favoriten-Host ausdrücklich untersagt, Header für sichere Diagnose.
- [x] Doppelte Server-/DOM-Loginversuche bei dediziertem Host vermieden; `Origin-Agent-Cluster: ?1` zur Absicherung der Browser-Origins.
- [x] C#-Contract-Tests für Host-Routing, Passwort-Escaping und Isolations-Gates sowie Browser-JSdom-Tests mit verzögert gerenderten ATOSS-Loginfeldern, Ereignissen und Ausschlüssen ergänzt.
- [x] UI-Warnung, wenn trotz aktiver Auto-Anmeldung nur der unsichere Shared-Path-Host verfügbar ist.
- [x] [PR #45](https://github.com/DerHausmeisterKrause/plenaro/pull/45) in `main` gemergt; [CI #44062037953](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/44062037953) grün, inklusive C#-Vertragstest, ATOSS-DOM-/Frame-Laufzeittest und Docker-Smoke.
- [ ] Technische Wildcard-Host-/TLS-Voraussetzungen am Zielhost und echten ATOSS-Login live prüfen.
- [ ] Issue #40: alte Shared-Path-Favoriten weiterhin auf dedizierte Origins migrieren; fremde SSO-/MFA-Provider werden nicht automatisch proxied.
