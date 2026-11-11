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
