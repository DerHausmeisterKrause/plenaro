# Plenaro 2.0 – aktueller Arbeitsstand

**Stand: 03.11.2026 · aktive Entwicklungsphase: 6 (Znuny/OTRS und ITSM)**

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
