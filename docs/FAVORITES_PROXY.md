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

## 19.11.2026 – ATOSS-Autologin ohne zusätzliche Favoriten-Aliasse

Der Benutzer möchte den vorhandenen, funktionierenden
`https://apps-plenaro.example.org/site-<UUID>/`-Favoriten beibehalten.
Dafür gibt es jetzt eine **alternative, serverseitige Browser-Anmeldung**:
Ein privater Playwright-Chromium-Prozess führt ausschließlich den Login
am bereits erlaubten HTTPS-Upstream aus und übergibt die abgeschlossene
Cookiesitzung dem bisherigen Favoriten-HttpClient. Die eigentliche
eingebettete Webseite läuft **unverändert** auf dem gemeinsamen Host.
Kein dedizierter Name oder neues TLS-Zertifikat ist notwendig.

Aktivieren mit:

```bash
cd /opt/plenaro
git switch main
git pull --ff-only
docker compose --profile browser-login up -d --build
docker compose --profile browser-login ps
```

Das Browser-Profil ist optional und startet einen zusätzlichen
unprivilegierten Browserprozess ohne freigegebenen Host-Port. Wenn das
Profil nicht aktiv ist, bleibt der bestehende konventionelle HTML-
Auto-Login unverändert. Das Passwort ist weiterhin im Vault gespeichert
und wird **nicht** in die HTML-Ausgabe des gemeinsamen Favoriten-Hosts
injiziert. Keine Änderung an `.env` oder den bestehenden Favoriten-
Adressen notwendig. `PLENARO_FAVORITES_PERSONAL_DEDICATED=false`
beibehalten, wenn noch in `.env` angegeben.

**Funktionsweise:** Chromium übernimmt die typischen Desktop-ATOSS-/
ZK-Selektoren, wartet bis zu zehn Sekunden auf nachgeladene Felder,
füllt per echten Eingaben aus und löst nur eine eindeutige Anmeldung aus.
Bei positivem Formularwechsel und vorhandenen sicheren Upstream-
Sitzungscookies werden diese mit beschränktem Umfang, überprüftem
HTTPS-Zielhost und erneut HTTPS-only in das persönliche Proxy-Cookiejar
übertragen. Der Browser wird nach dem Login geschlossen. Keine
Cookies oder Passwörter im Debug-Log.

Für benutzerdefinierte unternehmensinterne Zertifikate weiterhin
`PLENARO_TRUSTED_CA_FILE` verwenden; derselbe vertrauenswürdige
CA-PEM wird auch vom Browserworker in den Zertifikatsspeicher geladen.

**Live-Abnahme:** Favoriten-Einstellungen: Benutzername, Passwort und
Auto-Login speichern, Webseite öffnen und `Neu verbinden` auswählen.
Bei unerwartet erneutem Anmeldeformular prüfen, ob
`favorites-login` im Docker-Compose-Profil healthy ist, ob das
Browserimage die private CA kennt und ob ATOSS seine Sitzung mit
Cookies statt ausschließlichem Browser-LocalStorage verwaltet.
Authentisierung mit CAPTCHA, externer SSO-Umleitung oder MFA wird
nicht umgangen. Ein synthetischer Chromium-Vertragstest ersetzt
keine Prüfung gegen die echte ATOSS-Installation.

Dieser Modus fügt **keine** DNS-/TLS-Aliasse hinzu; der vorherige
WPF-kompatible dedizierte Hostmodus bleibt optional für andere
Anforderungen bestehen.

---

## 19.11.2026 – ATOSS-Anmeldung: Hinweis weg, isolierten Host betriebsbereit machen

Die störende Karte `Automatisches Ausfüllen noch nicht aktiv` wird
nicht mehr in der persönlichen Favoritenansicht angezeigt. Das
**ändert nicht** die strenge Browser-Origin-Grenze. Der klassische
serverseitige HTML-Login bleibt auf `apps-plenaro...` erhalten.
Für die dynamischen ATOSS/ZK-Formularfelder ist der Browser-Script-
Autofill auf einem eigenen Host erforderlich.

Der AutoFill-Helfer unterstützt jetzt zusätzlich ZK-Loginaktionen
vom Typ `a.z-button`, `[role=button]` und explizite ARIA-Beschriftungen,
die keine nativen Submit-Buttons sind, und schließt fremde Buttons aus.

### Schnellstmögliche Einrichtung für EINEN vorhandenen Favoriten

1. Der gesuchte Host lautet nach aktueller Vorlage:
   `site-<UUID>-plenaro.example.org`.
   **Auf dem intern benutzten DNS-Server** einen A-Eintrag zu derselben
   NGINX-IP wie `apps-plenaro.example.org` einrichten, alternativ
   einen **CNAME für genau diesen Namen** auf
   `apps-plenaro.example.org`. Das muss auch vom Browser-PC aus
   funktionieren; ein Eintrag in `/etc/hosts` des Docker-Hosts
   allein reicht nicht.
2. Das von NGINX ausgelieferte TLS-Zertifikat muss den Namen abdecken.
   Ein Zertifikat `*.example.org` deckt die einstufige
   `site-...-plenaro.intern.test.org`-Vorlage ab;
   `*.plenaro.example.org` **nicht**. Zusätzlich muss
   das äußere NGINX diesen Host an den Plenaro-Proxy weiterleiten.
   Ein einzelner CNAME ersetzt kein gültiges Zertifikat.
3. Auf dem Docker-Host ohne Geheimnisse verifizieren:
   ```bash
   bash tools/check_favorite_autologin_host.sh \
     site-<UUID>-plenaro.example.org
   ```
   Der HTTPS-Health-Pfad muss exakt
   `plenaro-favorite-proxy-ready` zurückgeben.
   Auch am Windows-/Browser-PC DNS und HTTPS verifizieren.
4. Nach erfolgreicher Prüfung in `/opt/plenaro/.env` aktivieren:
   ```dotenv
   PLENARO_FAVORITES_PATH_ORIGIN=https://apps-plenaro.example.org
   PLENARO_FAVORITES_HOST_TEMPLATE={slug}-plenaro.intern.test.org
   PLENARO_FAVORITES_PERSONAL_DEDICATED=true
   ```
   Danach `docker compose up -d --force-recreate web` und im
   Favoriten `Neu verbinden`. Bestehende Shared-Favoriten bleiben
   solange funktional, bis das Opt-in aktiviert ist.
5. In F12 → Network beim ersten HTML-GET die Antwort
   `X-Plenaro-Autofill: dedicated-dom` suchen. Dieser Header
   besagt, dass das echte DOM-Skript übertragen wurde. Die
   erfolgreiche Anmeldung muss danach **an ATOSS selbst** beobachtet
   werden; der Header allein bestätigt sie noch nicht.

Für viele Favoriten statt Einzel-CNAME ein gezieltes Wildcard-DNS,
ein passendes Zertifikat und entsprechendes NGINX-Hostrouting
bereitstellen. Ein globales `*.example.org`-DNS-Rewrite kann
unbeteiligte interne Dienste beeinträchtigen und sollte ohne Prüfung
des internen DNS-Konzepts nicht pauschal eingeführt werden.

Aus Sicherheitsgründen kein `unsafe`-JavaScript-Autofill oder
Passwortübergabe auf der Shared-Origin. Captcha, externe SSO-Domains
oder zusätzliche MFA sind dadurch nicht automatisch gelöst.

---

## 19.11.2026 – Sofort-Fix für nicht auflösbare persönliche Favoriten-Hosts

Der Fehler `DNS_PROBE_FINISHED_NXDOMAIN` bei
`site-<UUID>-plenaro.intern.test.org` wurde dadurch verursacht, dass
ein früherer Stand persönliche Favoriten **ungefragt auf aus
`PLENARO_FAVORITES_HOST_TEMPLATE` erzeugte Subdomains** umleitete.
Ein Template kann einen Hostnamen konstruieren, erstellt aber weder
DNS-Einträge noch TLS-Zertifikate.

**Jetzt standardmäßig:** Wenn `PLENARO_FAVORITES_PATH_ORIGIN`
(z. B. `https://apps-plenaro.intern.test.org`) gesetzt ist,
bleiben persönliche Favoriten selbst bei konfigurierter Hostvorlage unter
`https://apps-plenaro.intern.test.org/site-<UUID>/`. Die
Backend-Session-/Cookie-Prüfungen und Sicherheitsregeln bleiben erhalten.

**Nur für tatsächlich eingerichtete dedizierte Personal-Hosts:**
```dotenv
PLENARO_FAVORITES_PATH_ORIGIN=https://apps-plenaro.intern.test.org
PLENARO_FAVORITES_HOST_TEMPLATE={slug}-plenaro.intern.test.org
PLENARO_FAVORITES_PERSONAL_DEDICATED=true
```

Vor Aktivierung prüfen: Der Browser muss **jeden** neu erstellten
`site-<UUID>-plenaro.intern.test.org`-Host via DNS auflösen können,
das tatsächlich verwendete Zertifikat muss für diese Namen gültig sein,
und NGINX muss die Hostheader an Plenaros Favoriten-Proxy weitergeben.
Ein DNS-Eintrag `*.intern.test.org` kann einstellige Subdomains
wie `site-<UUID>-plenaro.intern.test.org` abdecken;
`*.plenaro.intern.test.org` dagegen **nicht**.
Alternativ für ein engeres Wildcard-DNS
`*.apps-plenaro.intern.test.org` samt passendem Zertifikat und
`PLENARO_FAVORITES_BASE_DOMAIN=apps-plenaro.intern.test.org`
verwenden – dann die Hostvorlage weglassen.

Ein bloßer NGINX-`server_name` oder ein wildcardfähiges Zertifikat
macht DNS-Einträge noch nicht verfügbar. Ohne korrekte DNS-
Infrastruktur das neue Personal-Dedicated-Flag **nicht** setzen.
Die Shared-Origin ist funktional, aber nicht für
JavaScript-Passwort-DOM-Autofill geeignet: das bleibt aus Sicherheits-
gründen deaktiviert. Klassische serverseitige HTTPS-Logins können
weiterhin funktionieren. Explizit ausgewählte dedizierte Wiki-/Plugin-
Hosts bleiben von diesem Personal-Fallback unberührt.

---

## 17.11.2026 – WPF-DOM-Autofill für ATOSS und JavaScript-Favoriten (PR #45)

Die klassische Desktop-Implementierung unter
`DerHausmeisterKrause/project_tool/Services/WebShortcutBrowserSessionManager.cs`
nutzt WebView2 und sucht nach dem tatsächlichen Rendering wiederholt im DOM,
nicht nur im vom Server ausgelieferten HTML. **Das ist der zentrale Grund,
weshalb bisher im Web bei ATOSS die Felder leer blieben:** Die Web-Version
konnte zuvor nur ein einfaches HTML-Formular serverseitig über POST abschicken,
nicht die dynamischen ATOSS-/ZK-Loginfelder im Browser befüllen.

### Wie es jetzt funktioniert

Bei einem persönlichen Favoriten mit `Auto Login`, Benutzername und
gespeichertem Vault-Passwort wird **auf einem separaten HTTPS-Hostname pro
Favorit** ein früher, an diesen Host gebundener Browser-Script-Helper
eingefügt. Er übernimmt die WPF-Selektoren für `Benutzername`,
`Kennwort`, `z-focustextbox-real`, Feldnamen, IDs und `autocomplete`,
benutzt den nativen `HTMLInputElement.prototype.value`-Setter,
löst `focus`/`input`/`change`/`blur` aus und verwendet ein
Login-Submit per Formular, eindeutigem Button oder Enter. Nachladen
per JS wird mit festen Zeitpunkten 0, 250, 750, 1.500, 2.500, 5.000 und
8.500 ms sowie einem zeitlich begrenzten DOM-Beobachter aufgefangen.
Doppelte Anmeldungen werden pro Proxy-Start unterbunden; zusätzlich wird
der frühere serverseitige HTML-Loginversuch für diese dedizierten
Favoriten **nicht** mehr ausgeführt. Bei sichtbarem Passwortwechsel oder
fremden DOM-Hosts findet keine automatische Anmeldung statt.

**Sicherheitsgrenze:** Die Klartext-Zugangsdaten werden aus dem AES-GCM-Vault
nur nach gültiger, zum Besitzer gehörender Favoritensitzung ausgelesen
und **ausschließlich in HTML auf dem eindeutigen Favoriten-Host**
zum Ausfüllen bereitgestellt. Auf dem geteilten
`apps-plenaro.../site-<UUID>/`-Host ist dies ausdrücklich **deaktiviert**.
Passwörter gehören niemals in einen öffentlichen Host, Log, HAR, JS-Console
oder Support-Screenshot. Eine bösartige/kompromittierte **Zielwebseite selbst**
könnte wie bei einer normalen Anmeldung ihre Zugangsdaten sehen. Andere
Favoriten erhalten dank separater Browser-Origin keinen gleichen DOM-Zugriff.
Zusätzlich sendet der Proxy `Origin-Agent-Cluster: ?1`.
Wiki, Ticketsystem und Plenaros eigener Login bleiben davon unberührt.
Issue #40 bleibt für alte und nicht isolierte Pfad-Favoriten offen.

### Einmalige technische Voraussetzung

Um ein persönliches Favoritenfenster auf einen eigenen Host zu verlegen,
muss mindestens **eine** der bestehenden Konfigurationen bereits
bereitgestellt sein:

```dotenv
# Option A – Wildcard-Subdomains
PLENARO_FAVORITES_BASE_DOMAIN=apps.example.org
# erzeugt site-<UUID>.apps.example.org

# Option B – Ein-Label-Template für bestehendes *.example.org
PLENARO_FAVORITES_HOST_TEMPLATE={slug}-plenaro.example.org
# erzeugt site-<UUID>-plenaro.example.org
```

Zusätzlich braucht das externe/innere NGINX eine passende
Wildcard-Hostweiterleitung, DNS-Wildcard auf den Plenaro-Proxy und ein
für die erzeugten Namen gültiges TLS-Wildcard-Zertifikat. Bei vorhandenem
Shared-Pfadhost **und** passendem Alias wird der persönliche Favorit beim
nächsten `Neu verbinden` automatisch auf seinen eigenen Host geleitet,
ohne pro interner Zielwebseite eine neue `.env`-Freigabe anzulegen.
Der zentrale Wiki-Pfadhost bleibt unverändert. Ist kein separater
Alias eingerichtet, funktionieren Favoriten weiterhin im bisherigen
Shared-Pfadmodus und die UI erklärt, weshalb **kein Browser-Passwort-
Autofill** erfolgen darf. Die alte serverseitige HTML-Anmeldung bleibt
dort erhalten.

### Diagnose ohne Geheimnisse

Im Browser F12 → Netzwerk → erste **HTML-Dokumentantwort** des dedizierten
Favoriten kontrollieren: `X-Plenaro-Autofill: dedicated-dom` bedeutet,
dass das Laufzeitskript ausgeliefert wurde (nicht, dass die ATOSS-
Anmeldung bestätigt wurde). Ohne dedizierten Host erscheint statt dessen
der Hinweis zur nötigen Host-Isolation. Weiterhin offene Fälle sind
externe SSO-/OAuth-Weiterleitungen, MFA/CAPTCHA, WebSockets und fremde
Frame-Origins; deren Inhalte oder Passwörter dürfen nicht durch
unsichere Ausnahmen auf die gemeinsame Origin übernommen werden.
Der Live-Test mit echter ATOSS-Version bleibt notwendig.

---

## 17.11.2026 – Verbesserte klassische Auto-Anmeldung, klare Grenze bei JavaScript-Loginfeldern

Ursache: Die Desktop-Vorlage `Services/WebShortcutBrowserSessionManager.cs`
verwendet WebView2, sucht mehrfach **im tatsächlichen DOM** nach
Benutzer-/Passwortfeldern (einschließlich Frames) und setzt Eingabewerte
mithilfe nativer DOM-Setter samt Input-/Change-Events. Die Web-Version
sendet dagegen **nur serverseitige klassische Formular-POSTs** und
hatte bislang einen eingeschränkten Namensvergleich (`user`,
`username`, `email`) – sie füllt keine sichtbaren Browser-Felder.

Die sichere Verbesserung unterstützt nun bei klassischen HTML-Formularen
auch benannte Felder mit `autocomplete=username`, ID,
`placeholder=Benutzername`, andere Namensvarianten und ein einzelnes
eindeutiges Textfeld neben einem Passwortfeld. Vorhandene
CSRF-/Anti-Forgery-Hidden-Felder und der übliche Submit-Button-Wert
werden unverändert mitgesendet. Eine fehlende Loginseite kann über
eine begrenzte, tatsächlich in der HTML-Seite vorhandene Login-Verlinkung
gefunden werden. Reine **same-origin** GET-Redirects vor dem Formular
werden bis zu vier Seiten lang verfolgt; es erfolgt höchstens
**ein** Passwort-POST, mit HTTPS-Origin-/Referer-Prüfung.
Registrierung, Kennwortänderung, mehrere Passwörter, GET-Formulare,
fremde SSO-Domains und rein JS-gerenderte Felder werden nicht
automatisch beschickt.

**Wichtige verbleibende Einschränkung:** Eine zuverlässige visuelle
Autofill-Funktion im Browser wie bei WPF ist damit noch **nicht**
implementiert. Dafür müssten Klartextzugangsdaten in das von der
fremden Webseite kontrollierte DOM gelangen. Bei aktuellem
**Shared-Origin-Host** für mehrere persönliche Favoriten wäre das
ein erhebliches zusätzliches Diebstahlrisiko: andere dort
ausgeführte Skripte könnten auf diese Daten zugreifen.
Voraussetzung für den sicheren Ausbau ist
[Issue #40 – dedizierte Browser-Origin pro Favorit](https://github.com/DerHausmeisterKrause/plenaro/issues/40).
ATOSS-Ausprägungen mit JavaScript-only, iFrames, MFA und externem
SSO benötigen daher bis dahin eine interaktive Anmeldung oder
eine app-spezifische, vom Betreiber vertrauenswürdig integrierte
serverseitige Authentifizierung. Keine Anmeldedaten für Diagnosezwecke
weitergeben.

---

## 17.11.2026 – Bilder, dynamische Assets und Login-Navigation bei eigenen Favoriten

**Beobachtung:** Manche selbst eingetragene Firmenwebseiten (beispielsweise
ATOSS) zeigen unvollständige Bilder oder lassen den Login nicht zuverlässig
durchlaufen. Diese Änderung adressiert konkrete Kompatibilitätslücken im
Shared-Pfadhost `apps-plenaro.../site-<UUID>/`, **ohne** pauschal
fremde Hosts serverseitig zu proxyn.

- Bei `site-*` kann der frühe Browser-Shim jetzt **exakt die konfigurierte
  HTTPS-Origin** in dynamischen `fetch`, `XMLHttpRequest`, `Request`,
  `img.src`, `img.srcset`, `source.srcset`, lazy `data-src`,
  `form.action` und SPA-`history.pushState/replaceState`-Navigationen
  umschreiben. Fremde Server/Protokolle werden nicht auf den Server-Proxy
  umgeleitet. Der zentrale Wiki-Shim bleibt bewusst auf die bisherigen
  Pfade begrenzt.
- Dynamisch per `innerHTML` eingefügte Bild-/Formularelemente werden
  bei persönlichen Favoriten nachträglich pfadkorrigiert; CookieJar,
  Eigentümerprüfung und Bootstrap-Routen bleiben unverändert.
- HTTPS-Bilder und -Fonts von externen CDNs werden in der iframe-CSP
  zugelassen, **nicht** beliebige externe Skripte oder CSS. Keine HTTP-
  Mixed-Content-Freigabe; Frames behalten `Referrer-Policy: no-referrer`.
- Generische `text/plain`-/`application/octet-stream`-Antworten werden
  bei bekannten Bild-Dateiendungen als `image/png`, `image/jpeg`,
  `image/webp`, `image/avif`, `image/gif`, `image/svg+xml`,
  `image/x-icon` oder `image/bmp` ausgeliefert, ohne Bilddaten zu ändern.
- Auth-/CSRF-Header aus Browser-API-Aufrufen werden **nur an den bereits
  fixierten HTTPS-Origin** weitergeleitet; Browser-Cookies werden nie an
  fremde Upstreams übertragen. Ein GET aus einem anderen Favoriten oder
  der gemeinsamen Root wird nicht als Upstream-Referer interpretiert.
- Nur ein vom Browser als `same-origin` ausgewiesener
  **iframe-Dokument-GET** mit Referer aus genau einem bestehenden
  `/site-<UUID>/` erhält bei root-absoluter Login-Navigation einen
  `307` zurück unter seinen eigenen Favoritenpfad.
  Cross-Origin-Navigation, POST, Assets, andere Favoriten, Wiki und
  `/_plenaro` erhalten diese Ausnahme **nicht**.

**Grenzen:** Der Proxy simuliert keinen vollständigen Browser. Anwendungen
mit externer SSO-/OAuth-Login-Domain, CAPTCHA, MFA, WebSockets,
Service-Worker-Abhängigkeit oder Anti-Embedding können weiterhin
scheitern. Eine komplette Unterstützung des konkreten ATOSS-Systems ist
ohne echten Browser-Network-Test nicht behauptbar. Bei Fehlern:
Browser-Netzwerk (nur URL-Pfad, Status, Response-Headers, Console),
keine Login-POST-Bodies, Cookies, Token oder HAR-Dateien mit
Zugangsdaten veröffentlichen. Die weiterhin offene
[Origin-Isolation #40](https://github.com/DerHausmeisterKrause/plenaro/issues/40)
ist eine eigenständige Sicherheitsvoraussetzung, bevor frei gewählte
fremde JS-Anwendungen generell unbedenklich eingebettet werden können.

---

## Persönliche interne HTTPS-Favoriten ohne .env-Freigabeliste (Stand 17.11.2026)

Mit der aktuellen Korrektur dürfen authentifizierte Nutzer unter
**Einstellungen → Webseiten / Favoriten** beliebige HTTPS-Webseiten mit
DNS-Host eintragen, darunter Firmen-Intranetdienste auf privaten IPv4-
oder IPv6-Netzen. Die in PR #41 eingeführte
`PLENARO_FAVORITES_INTERNAL_ORIGINS`-Freigabeliste wurde wieder entfernt:
**Weder neue .env-Variablen noch eine separate Administratorfreigabe pro
internem Favoriten sind erforderlich.** Bereits gespeicherte Favoriten
werden nicht verändert.

Der HTML-Proxy bleibt an die jeweilige Favoriten-ID und den angemeldeten
Besitzer gebunden, verwendet pro Sitzung einen eigenen serverseitigen
Cookie-Container und lässt keine fremden Ziel-Hosts per Redirect zu.
Nur HTTPS mit gültigem Zertifikat/Hostnamen ist zulässig. Beim tatsächlichen
TCP-Verbindungsaufbau werden DNS-Antworten nochmals geprüft: Loopback
(127.0.0.1, ::1), Link-Local (169.254.0.0/16, fe80::/10),
Multicast, nicht spezifizierte Adressen und bekannte Metadata-Ziele bleiben
gesperrt. Private LAN-IPs (RFC1918, IPv6-ULA) sind hingegen zugelassen.

**Sicherheitsfolge:** Jeder Plenaro-Benutzer kann damit **vom Plenaro-
Server erreichbare interne HTTPS-Dienste ansprechen**, auch solche, auf
die sein eigener Rechner keinen direkten Netzpfad hat. Das ist eine
bewusste Erweiterung der Berechtigungen und keine vollständige
SSRF-Abwehr. Plenaro deshalb nur mit vertrauenswürdigen Konten betreiben
und den Server-Netzwerkzugriff auf kritische Verwaltungsdienste per
Netzsegmentierung und Firewall begrenzen. Unverändert bleibt zudem das
separate Sicherheitsproblem einer gemeinsamen Browser-Origin für
verschiedene Favoriten: [Issue #40](https://github.com/DerHausmeisterKrause/plenaro/issues/40).

Beim Update von `main` genügt:

```sh
cd /opt/plenaro
git switch main && git pull --ff-only
docker compose up -d --build --no-deps --force-recreate web
docker compose up -d --no-deps --force-recreate nginx
```

Danach den Favoriten neu verbinden. Wenn der interne Zielserver
ein firmeneigenes Zertifikat besitzt, muss dessen vertrauenswürdige CA
im Webcontainer hinterlegt sein; Hostnamen- und TLS-Prüfung bleiben
aktiv. Ein erneuter 502-Fehler ist anhand des
`X-Plenaro-Favorite-Error`-Headers von der bisherigen privaten
IP-Sperre zu unterscheiden. Nicht jede Webanwendung ist wegen CSP,
SSO, JavaScript-Ressourcen oder WebSockets im iframe kompatibel.

---

## Favoriten nach PR #38: weiße eingebettete Webseiten

Persönliche Webseiten (`site-<UUID>`) haben einen eigenen serverseitigen CookieJar, werden in der derzeitigen Umgebung aber über den Shared-Host `/<slug>/` ausgeliefert. Viele Webapps erstellen dynamische API-/CSS-/JS-Aufrufe direkt gegen `/api` oder `/assets`. Ohne browserseitige Präfixkorrektur gehen sie an den gemeinsamen Hostroot (404). Der Proxy ergänzt jetzt bei **persönlichen** Seiten die allgemeine Root-Pfadkorrektur, während der Wiki-Shim bewusst nur seine bisherigen Endpunkte `/rest` und `/s` verändert.

Ein HTTP-502/504/403/401 als Hauptnavigation wird künftig als lesbares Diagnose-HTML angezeigt. Der Header `X-Plenaro-Favorite-Error` benennt den Fehler ohne Benutzer-/URL-Daten:

- `private-upstream-blocked`: Ziel verweist auf weiterhin gesperrte lokale/reservierte Adressen (Loopback, Link-Local, Metadaten usw.); normale RFC1918-LAN-Ziele sind erlaubt.
- `upstream-redirect`: Zielseite leitet auf anderen Host (z. B. WWW-Canonical-Host) um; korrekte finale HTTPS-Adresse als persönlichen Favoriten speichern.
- `upstream-unavailable`/`upstream-timeout`: Proxy-Netzwerkverbindung nicht möglich.
- `proxy-session-expired`/`proxy-session-revoked`: Sitzungs-Cookie und Appberechtigung nicht mehr gültig, per „Neu verbinden“ neue Sitzung starten.
- `upstream-resource-limit`: HTML/Assets außerhalb der Größenbeschränkung.

Zusätzlich gibt es in jedem Favoriten den Link **„Originalseite öffnen“**. Wenn eine externe Webseite ihre Einbettung durch Browser-/JavaScript-/CSP-Verhalten verhindert, ist die Originalseite direkt im neuen Browser-Tab erreichbar, sofern der Benutzer selbst Zugang hat.

### Live-Test ohne Tokens teilen

1. Ein persönliches Favoritenfenster öffnen. Erwartet: Inhalt oder **sichtbarer** Diagnosehinweis.
2. Im Browser-Entwicklerwerkzeug Network den **Dokument-Aufruf** unter `/site-...` ansehen. HTTP-Status und ggf. `X-Plenaro-Favorite-Error` notieren. **Keine Cookies, CSRF-Token, Einmal-Bootstrap-URLs oder vollständige HAR mit Zugangsdaten weitergeben.**
3. Bei vollkommen weißer Seite prüfen, ob ein äußerer NGINX weiterhin `X-Frame-Options: SAMEORIGIN` hinzufügt oder CSP `frame-ancestors` blockiert. Die normale Plenaro- und die Favoriten-Origin sind verschieden.
4. Im Network/Console defekte Ressourcen prüfen. Zielserver können fremde Skripte, CDN-Abhängigkeiten und Cross-Origin-Authentifizierung nutzen, die mit Proxy-Pfaden nicht garantiert funktionieren.
5. Die Wiki-Seite und persönliche Auto-Logins separat gegen echte Systeme prüfen; CI verwendet nur simulierte Domains.

**Sicherheitsgrenze:** Auch mit individueller Pfad-Cookie bleiben auf einem Shared-Browser-Host mehrere persönliche Webseiten dieselbe Origin. Eine langfristige Isolation mit dedizierter Browser-Origin pro frei angelegter Website ist erforderlich, bevor dieser Modus für beliebige fremde JS-Anwendungen als generell sicher gelten kann.

---

# Stand 17.11.2026 – Self-Service-Favoriten und eigenständige Wissensdatenbank

**Aktueller Stand:** Webseiten werden unter `Einstellungen → Webseiten / Favoriten` direkt mit Namen und vollständiger HTTPS-Adresse pro Benutzer angelegt und sortiert. Eine Einzel-Freigabe durch die Administration ist nicht mehr erforderlich. Persönliche HTTPS-Webseiten dürfen inzwischen auch interne LAN-Ziele erreichen, ohne dass sie vom Administrator einzeln freigegeben werden müssen. Loopback, Link-Local und bekannte Metadaten-Adressen bleiben gesperrt; das Risiko serverseitiger Zugriffe auf andere interne HTTPS-Dienste wird durch Zugriffskontrolle und Firewall-Segmentierung begrenzt.

**Wissensdatenbank:** Die zentrale Serveradresse bleibt unter `Einstellungen → Wissensdatenbank` adminseitig verwaltet. Benutzer speichern nur ihre persönlichen Wiki-Anmeldedaten. `wiki_user_connections` ist getrennt von `user_favorites`; die Wiki-Seite erzeugt beim Einrichten **keinen Favoriten**. Der erprobte FavoriteProxy, Confluence-Autologin, die Einmal-Bootstrap-URL, die Sitzungstrennung und die browserseitigen Wiki-Pfadkorrekturen bleiben bestehen.

**Migration:** Frühere Favoriten behalten ID, Reihenfolge und Passwort. Vorhandene Wiki-Favoriten werden aus der Favoritenliste entfernt und als eigenständige Wiki-Verbindung übernommen. Ein vorhandenes Wiki-Passwort wird über den alten Vault-Schlüsselnamen gelesen und beim ersten Login unter dem neuen Schlüsselnamen `wiki.password` sicher neu verschlüsselt.

Die nachfolgenden historischen Abschnitte dokumentieren ältere PRs und deren inzwischen ersetzte Kopplung zwischen Wiki und Favoriten. Sie beschreiben **nicht** mehr die aktuelle Bedienung.

---

## Confluence-Login weiterhin als abgelehnter Frame: Root-Navigation und NGINX-Header prüfen

In der bereinigten Login-Aufzeichnung findet nach dem erfolgreichen
`POST /wiki/rest/tsv/1.0/authenticate` mit `{"next":"/"}`
ein `GET /` mit **`Sec-Fetch-Dest: iframe`**,
`Sec-Fetch-Site: same-origin` und
`Referer: /wiki/login.action?... ` statt; die Hostroot liefert 404.
PR #29 schreibt den JSON-`next`-Pfad bereits um. Wenn ein Login-Plugin
dennoch origin-root navigiert, ergänzt der Proxy jetzt einen **eng begrenzten**
Fallback: Nur für explizit aktivierten `wiki`-Browser-Shim, HTTPS,
`GET /` ohne Query, echte `iframe`-Navigation von genau
`/wiki/login.action` auf demselben Shared-Favoriten-Host wird eine
`303 Location: /wiki/` gesendet. Es gibt **keine** Root-Inhalte und
keinen Weiterleitungs-Fallback für andere Apps, API-Requests,
Cross-Origin-Referer oder POSTs; `/wiki/` prüft weiterhin die
ursprünglichen Benutzer- und Favoritenrechte.

Zwei ungefährliche Diagnoseheader vereinfachen die Live-Abnahme:
- `X-Plenaro-Login-Next: prefixed` auf dem erfolgreichen Login-JSON,
  wenn `next` tatsächlich korrigiert wurde; `unchanged`, wenn nicht.
- `X-Plenaro-Login-Reroute: wiki-root` auf einer erkannten Root-Navigation
  (HTTP 303).

**Zusätzliche konkrete Auffälligkeit im HAR:** Auch der erfolgreiche
Login-POST und die 404-Root-Antwort tragen
`X-Frame-Options: SAMEORIGIN` sowie **zwei** CSP-Antwortheader.
Der Plenaro-Favoriten-Proxy setzt kein `X-Frame-Options`; ebenso
enthält die mitgelieferte Docker-NGINX-Vorlage diesen Header nicht.
Damit stammt er aus einer anderen Proxy-Schicht. Die Haupt-Plenaro-
Seite und der Favoriten-Host besitzen unterschiedliche Origins:
`X-Frame-Options: SAMEORIGIN` kann daher auch bei korrekt umgeleiteter
Confluence-Seite deren Einbettung blockieren.

**Auf Zielserver prüfen (ohne Anmeldedaten):**

```bash
curl -sS -D - -o /dev/null \
  https://apps-plenaro.example.org/wiki/login.action \
  | grep -Ei '^(HTTP/|x-frame-options:|content-security-policy:|location:)'
sudo nginx -T 2>&1 | grep -nE 'X-Frame-Options|Content-Security-Policy'
```

Ein `401` oder `302` ohne gültige Favoriten-Sitzung ist bei
`curl` erwartbar; entscheidend sind die Sicherheitsheader. Ein
zusätzliches `X-Frame-Options: SAMEORIGIN` muss in der tatsächlich
zuständigen vorgeschalteten NGINX **nur für den Favoriten-Host**
entfernt werden, nicht global. Im Zweifel erst `nginx -T` mit
den wirksamen `server_name`-/`location`-Kontexten ansehen.
`proxy_hide_header` entfernt nur Upstream-Header und hilft **nicht**
gegen einen `add_header X-Frame-Options ...` derselben Proxy-Schicht.
Vor jeder NGINX-Änderung `nginx -t` prüfen.

**Nach dem Deploy:** Favorit neu verbinden, Netzwerk-Tab mit
`Preserve log` prüfen. Erfolgreicher Login-POST sollte
`{"next":"/wiki/"}` liefern, alternativ muss der spezifische
Root-Fallback `303 /wiki/` ausführen. Wenn ein Folgedokument
immer noch mit `SAMEORIGIN` blockiert wird, ist die vorgeschaltete
NGINX separat zu korrigieren. Bitte nur URL-Pfad/Status/Response-
Header und Konsolenfehler teilen, keine Passwörter, Token oder Cookies.

---

## Confluence-Login: HTTP 200, danach / statt /wiki/ (Root 404)

Eine lokal analysierte Browseraufzeichnung zeigt den **entscheidenden**
Fehler nach dem Absenden des Passwortformulars: Ein erfolgreiches
`POST /wiki/rest/tsv/1.0/authenticate` erhält HTTP 200 und die
JSON-Antwort `{"next":"/"}`. Anschließend navigiert das Atlassian-Login-
Plugin in seinem JavaScript zur gemeinsamen Favoriten-Hostroot `/`.
Diese Root ist absichtlich gesperrt (404), weil mehrere Favoriten
unter `/<slug>/` denselben Host teilen. Die Confluence-Session wird
beim erfolgreichen Upstream-POST im privaten Cookie-Jar gespeichert.
Ein zuvor fehlgeschlagener Login-POST (401) ist **nicht** Ursache der
anschließenden leeren Frame-Ansicht.

**Gezielte Korrektur:** Nur bei einem erfolgreichen POST auf exakt
`/rest/tsv/1.0/authenticate`, JSON-Antwort und für den explizit
aktivierten Shared-Path-Browser-Shim (`PLENARO_FAVORITES_BROWSER_SHIM_SLUGS=wiki`)
wird die JSON-Eigenschaft `next` von `/` auf `/wiki/`
(oder von `/display/...` auf `/wiki/display/...`) geändert.
`/wiki/...` bleibt idempotent. Fehlerantworten, andere API-JSON-Antworten,
dedizierte Hosts, unbekannte Favoriten und fremde Domains bleiben
unangetastet. Die Hostroot `/` bleibt absichtlich **404** – keine
globale Root-Weiterleitung, keine DNS- oder NGINX-Änderung.

Nach Deployment per `git pull --ff-only` und
`docker compose up -d --build --no-deps --force-recreate web`, anschließend
`docker compose up -d --no-deps --force-recreate nginx`: Favoriten-Session
neu starten und Confluence-Anmeldung live testen. Im Netzwerk-Tab soll
der erfolgreiche Login-POST jetzt `{"next":"/wiki/"}` antworten und
der folgende Dokumentaufruf `/wiki/` statt `/` erreichen.
Die echte Wiki-Seite nach dem Login und der getrennte Betrieb mehrerer
Benutzer bleiben live zu prüfen.

**Datenschutz:** Ein Browser-HAR kann Passwort-POSTs, Sitzungs-Cookies
und andere vertrauliche Daten enthalten. Nie einen unbereinigten HAR
in GitHub hochladen; Tests verwenden nur synthetische URLs und JSON.

---

## Confluence: Formular vorhanden, nach Anmeldung verweigert ein eingebetteter Frame die Verbindung

Nach PR #27 kann die Confluence-Passwortmaske im Pfadhost angezeigt werden.
Wenn der Browser **erst nach Absenden des Formulars** meldet,
`apps-plenaro.example.org hat die Verbindung abgelehnt`, ist ein
blockierter Frame eine mögliche Ursache – noch **kein** Beweis für eine
erfolgreiche oder fehlgeschlagene Anmeldung.

Der Favoriten-Proxy erlaubte bislang in seiner `Content-Security-Policy`
bei `frame-ancestors` nur die Haupt-Plenaro-Origin, nicht die eigene
Proxy-Origin. Ein von Confluence nach dem Login zusätzlich eingebetteter
Frame unter `apps-plenaro.example.org/wiki/...` hat aber **zwei**
Vorfahren: Confluence auf dem Shared-Favoriten-Host und Plenaro.
Die Browserrichtlinie wird gegen alle Vorfahren ausgewertet. Deshalb
erlaubt der Proxy künftig explizit `frame-ancestors 'self'
https://portal.example.org`; `frame-src 'self'` und alle sonstigen
Origin-/CSRF-/Sitzungsprüfungen bleiben unverändert. Das entspricht
der bestehenden restriktiven Znuny-Frame-Policy.

Nach Deployment: Favorit neu verbinden, Login im Browser erneut testen.
Bei weiterer Ablehnung im Netzwerk-Tab **die HTML-Dokumentanfrage nach
dem Login** kontrollieren: URL-Pfad, Status, `Location`, `X-Frame-Options`
und `Content-Security-Policy` sowie die exakte Konsolenmeldung.
**Nicht** Passwörter, Cookies, Token, POST-Body oder vollständige HARs
in GitHub hochladen. Wenn ein externer Identity-Provider im Frame
`X-Frame-Options: DENY` oder `frame-ancestors 'self'` erzwingt,
kann Plenaro das nicht sicher umgehen; dessen Anmeldung muss dann
außerhalb des eingebetteten Frames stattfinden. Ein vom äußeren NGINX
gesetzter blockierender `X-Frame-Options`-Header ist ebenfalls separat
zu untersuchen, ohne globale Schutzheader anderer Dienste abzuschalten.

---

## Ergänzung: Confluence-Login mit Request-Objekten und dynamischen Assets

Nach dem opt-in Browser-Shim aus PR #26 können einige Atlassian-Plugins
weiterhin Root-Anfragen erzeugen, wenn sie `new Request("/rest/...")` benutzen
oder Ressourcen über `script.src = "/s/..."` beziehungsweise
`link.href = "/s/..."` einsetzen. Der bisherige Shim fing nur
`fetch(string)`, `XMLHttpRequest.open`, `setAttribute` und bestimmte
DOM-Einfügemethoden ab.

Der Shim fängt daher zusätzlich neu erzeugte `Request`-Objekte und
die nativen `src`-/`href`-Setter für Skript-, Stylesheet- und
Bildelemente ab. Er ändert **nur** dieselben bekannten `/rest/`- und
`/s/`-Pfade, ausschließlich beim ausdrücklich freigegebenen Favoriten
`wiki`. Native Request-Optionen (Body, Header, Credentials, Signal) bleiben
erhalten, da der native Request-Konstruktor mit den Originalargumenten
weiterverwendet wird. Fremde Origins und andere Favoriten bleiben unberührt.

**Live-Kontrolle nach Deployment:** Im Browser innerhalb des Wiki-Frames
zeigt `document.querySelector('script[data-plenaro-browser-shim]') !== null`,
ob der Browser-Shim wirklich ankommt. In F12 → Netzwerk sollten interne
Confluence-Requests unter `/wiki/rest/...` bzw. `/wiki/s/...` statt an
der gemeinsam genutzten Host-Root erscheinen. HTTP-401 innerhalb von
`/wiki/rest/...` ist anschließend **ein anderer Fehler** (Upstream-
Autorisierung/Confluence-Sitzung), nicht mehr die vorherige Root-Routen-Sperre.
Bei weiterem Login-Fehler die ersten fehlgeschlagenen Requests inklusive
Status und die JS-Konsolenfehlermeldung sichern (ohne Cookies, Tokens oder
Passwörter). Ein echter Login-Test am Zielsystem bleibt notwendig.

---

## Confluence unter /wiki/ behalten: Browser-Root-URL-Kompatibilität (PR #26)

**Kein eigener DNS-Name erforderlich.** Die Anmeldung unter
`https://apps-plenaro.example.org/wiki/` bleibt erhalten. Nach dem
JavaScript-Syntaxfix aus PR #24 lädt die Confluence-Oberfläche korrekt,
aber einige Atlassian-Plugins ignorieren den HTML-Kontextpfad und senden
XHR/fetch-Anfragen an `/rest/...` (Antwort 401) und Ressourcenaufrufe
an `/s/...` (Antwort 404). Das ist im lokalen HAR direkt belegt; ein
eigentliches Passwort-Login-POST wurde noch nicht mitgeschnitten.

Für solche Apps kann **explizit pro Slug** ein kleiner Browser-Kompatibilitäts-
Shim aktiviert werden. Er wird unmittelbar nach `<head>` **vor dem
originalen JavaScript** eingeblendet und ergänzt `/wiki` bei Requests
an genau diese Root-Pfade:

- `XMLHttpRequest.open` und `fetch` mit String-/URL-Parametern;
- `navigator.sendBeacon`;
- dynamisch per DOM eingesetzte `<script>`, `<link>` und `<img>` für
  URLs unter `/s/` oder `/rest/`.

Die externen JavaScript-Dateien werden unverändert weitergeleitet.
Das vorhandene Cookie `Path=/wiki` bleibt unverändert. `/rest/...` und
`/s/...` auf dem **gemeinsam genutzten** Host werden nach wie vor **nicht**
freigeschaltet. Alle kompatiblen Anfragen laufen über `/wiki/rest/...`
beziehungsweise `/wiki/s/...` und benötigen die normale Plenaro-Session,
gültige Nutzerrechte und denselben freigegebenen Upstream.

**Nur diese Einstellung in `/opt/plenaro/.env`:**

```dotenv
PLENARO_FAVORITES_PATH_ORIGIN=https://apps-plenaro.example.org
PLENARO_FAVORITES_BROWSER_SHIM_SLUGS=wiki
# Nicht setzen, wenn /wiki/ erhalten bleiben soll:
# PLENARO_FAVORITES_DEDICATED_SLUGS=
```

Kein neuer DNS-Alias, kein `PLENARO_FAVORITES_HOST_TEMPLATE`, keine
Änderung der äußeren NGINX. Die vorhandene funktionierende lokale
HTTP-Bridge der äußeren NGINX bleibt unverändert.

Deployment (insbesondere bei dem NGINX-Proxy, der `web` nur beim Start
auflöst):

```sh
git switch main && git pull --ff-only
docker compose up -d --build --no-deps --force-recreate web
docker compose up -d --no-deps --force-recreate nginx
```

Anschließend Wiki-Favorit neu verbinden und Browser-Cache mit
Strg+Shift+R umgehen. Im Netzwerkmitschnitt müssen die vorherigen
`/rest/authconfig/1.0/login-options`-Anfragen nun unter
`/wiki/rest/authconfig/1.0/login-options` erscheinen.

**Einschränkungen:** Dieser selektive Shim deckt absichtlich nur die
belegten Root-Pfade und bestimmte Browser-APIs ab; andere Wege wie
bereits aufgebaute `Request`-Objekte, programmatische Navigationen
oder WebSockets können zusätzliche Anpassungen erfordern.
Same-Origin-Isolation zwischen mehreren Favoriten auf dem Shared Host
ist grundsätzlich nicht garantiert. Nur gemeinsam vertrauenswürdige
Apps unter diesem Host bereitstellen. Interaktiver Confluence-Login
bleibt separat zu testen; Autologin über SSO/MFA wird damit nicht
implementiert.

---
## Confluence-Login trotz intakter Darstellung nicht möglich – dedizierter Favoriten-Host

Eine bereinigte Confluence-Browseraufzeichnung zeigt `/wiki/login.action` mit
HTTP 200, aber dynamische Root-Anfragen an `/rest/authconfig/1.0/login-options`,
`/rest/wrm/2.0/resources`, `/rest/menu/latest/appswitcher` und weitere
`/rest/...` erhalten 401; ein Root-Asset unter `/s/...` erhält 404.
Das ist nicht durch falsche Zugangsdaten belegt: Der Mitschnitt enthält
**keinen eigentlichen Login-POST**. Confluence-/Webpack-Plugins verwenden
teilweise absolute Host-Root-Pfade selbst bei korrigierten HTML-Metatags.

**Sichere Lösung (ab PR #25):** Der gemeinsame Host bleibt für kompatible
Favoriten bestehen; einzelne bekannte inkompatible Apps nutzen einen
bevorzugten eigenen Host (genau **eine** Browser-Origin pro App):

```dotenv
PLENARO_FAVORITES_PATH_ORIGIN=https://apps-plenaro.example.org
PLENARO_FAVORITES_HOST_TEMPLATE={slug}-plenaro.example.org
PLENARO_FAVORITES_DEDICATED_SLUGS=wiki
PLENARO_SERVER_NAME=portal.example.org tickets.example.org apps-plenaro.example.org wiki-plenaro.example.org
```

Beispiel: Wiki-Slug `wiki` startet unter
`https://wiki-plenaro.example.org/_plenaro/favorite-bootstrap`, dann
`https://wiki-plenaro.example.org/login.action`; alle JS-Root-Anfragen
`/rest/...`, `/s/...` tragen die **gleiche Host-Origin** und
finden die Root-Session-Cookie (`Path=/`). Ein zweiter Favorit `cloud`
bleibt unter `https://apps-plenaro.example.org/cloud/` und erhält
weiterhin nur einen pfadbezogenen Cookie. Existieren Alias-Konfiguration
oder DNS nicht, schlägt ein explizit ausgewählter Slug sicher fehl, statt
unbemerkt zum inkompatiblen Pfadhost zurückzufallen.

**Deployment-Schritte:**

1. Nur für `wiki-plenaro.example.org` einen expliziten DNS-A-/CNAME-Eintrag
   zum selben vorgeschalteten NGINX wie `apps-plenaro.example.org` erstellen.
   Bei vorhandener Wildcard-DNS-Zone prüfen, ob sie diese konkrete Adresse
   bereits auflöst. Keine Änderung der anderen Hostnamen notwendig.
2. Auf der **vorgeschalteten** NGINX einen HTTPS-vHost für den neuen Namen
   hinzufügen oder den funktionierenden `apps-plenaro`-vHost um dessen
   `server_name` erweitern. Das öffentliche Zertifikat muss den Namen
   enthalten. Die vorhandene Loopback-Bridge (beispielsweise
   `proxy_pass http://127.0.0.1:18080;`) und `Host $host` weiter verwenden.
   **Kein** erneuter Wechsel zur fehleranfälligen TLS-Upstream-URL.
3. Neue .env-Variablen ergänzen, `PLENARO_SERVER_NAME` um den Alias erweitern,
   `docker compose config` kontrollieren und anschließend Web **und**
   Docker-NGINX zusammen neu erstellen (wegen statischer NGINX-Auflösung der
   Docker-Hostnamen). Äußere NGINX nur nach `nginx -t` reloaden.
4. Browser-Einbettung mit echten Login-Anfragen testen. Globale
   `X-Frame-Options: SAMEORIGIN`-Header können auch auf dem neuen Alias
   iFrames vom Plenaro-Hauptorigin blockieren. Header **nur für diesen**
   Alias entsprechend der geplanten `frame-ancestors`-Policy konfigurieren,
   nicht für alle Sites pauschal deaktivieren.

**Nicht empfohlen:** Root-`/rest`- oder Root-`/s`-Routen auf dem
**gemeinsam** genutzten `apps-plenaro`-Host einfach zu einem Favoriten
weiterleiten. Auf diesem Host können mehrere Apps aktiv sein, deren
Authentifizierung, Session und Origin voneinander getrennt bleiben müssen.

**Wichtig:** Plenaro-Autologin unterstützt aktuell nur traditionelle
HTML-Benutzername/Passwort-POST-Formulare. Confluence-Authentifizierungsplugins
mit dynamischer Login-Seite, SSO oder MFA können dadurch **nicht** automatisch
angemeldet werden. Der dedizierte Host löst das Browser-Routing, nicht die
Automatisierung des Loginvorgangs. Eine normale interaktive Anmeldung muss
separat geprüft werden.

---

## Confluence: HTTP 200, aber Buttons/Logos/Formulare fehlen (PR #24)

Eine echte Browser-Netzwerkaufzeichnung mit Confluence-Web-Resources zeigte
fast ausschließlich HTTP 200; dennoch brachen mehrere `batch.js`-Dateien
mit `Invalid regular expression flags` ab. Ursache: Die bisherige
globale URL-Ersetzung behandelte **jede** Zeichenfolge mit `"/` oder
`'/` als Root-URL. In Confluence-JavaScript wurde beispielsweise
ein regulärer Ausdruck `replace(/"/g, ...)` zu
`replace(/"/wiki/g, ...)` beschädigt. Dadurch initialisierten
`define` (AMD) und `AJS` nicht; komplette UI-Elemente fehlten.

**Korrektur:** Der Favoriten-Proxy liefert JavaScript und JSON jetzt
unverändert aus. Nur HTML, CSS und ausgewählte XML/SVG-Markup-Typen
werden weiterhin pfadbezogen umgeschrieben. Bei HTML bleibt der
**Inhalt von Inline-`<script>`-Elementen** unverändert; externe
`src`-/`href`-Attribute, Bilder, Stylesheets und relevante
Confluence-Kontext-Metatags behalten ihre Pfadkorrektur.

Das verhindert JavaScript-Syntaxbeschädigung, kann aber JS-generierte
absolute Root-URLs nicht generell an `/<slug>/` anpassen. Das muss
die Zielanwendung über korrekten Kontextpfad/öffentliche Base-URL
leisten, oder die App benötigt einen separaten Favoriten-Host.
Nach Deployment neuen Favoriten-Proxy-Sessionstart durchführen,
Browser-Cache deaktivieren und Confluence-Console/Netzwerk prüfen.

Keine Produktions-HAR-Datei in GitHub hochladen: Solche Dateien
können Cookies, Authorization-Header, Querytokens und persönliche
Daten enthalten. Regressionstests verwenden ausschließlich
synthetische, bereinigte JS-Snippets.

---
## Wiki-Oberfläche unvollständig: Buttons, Logos, Eingabemasken (PR #23)

Ein HTTP-200-Healthcheck zeigt lediglich, dass DNS, die beiden NGINX-Schichten
und die Favoriten-Routing-API erreicht werden. Er beweist **nicht**, dass
CSS-/JS-/Bildressourcen sowie dynamische AJAX-Anfragen funktionieren.

Der gemeinsame Pfadhost setzt eine Anwendung unter `/<slug>/`, während
die Originalanwendung oft für `/` konfiguriert ist. Der Proxy schreibt
bereits viele HTML/CSS/JS-Pfade um. Mit PR #23 werden zusätzlich bekannte
Confluence-Kontextpfad-Metadaten (`ajs-context-path` und
`confluence-context-path`) im HTML für `/<slug>` angepasst, damit
JavaScript dynamische Webressourcen und API-URLs nicht an der Host-Root
statt am Favoriten-Pfad anfragt. Dies greift nur bei diesen Meta-Tags,
nicht bei beliebigen JSON-Daten oder fremden Origins.

Bisher wurde jeder textuelle Body über 6 MiB mit 413 abgewiesen.
JS-/CSS-Bundles dürfen jetzt bis 24 MiB groß sein; HTML, API-JSON
und andere Textressourcen behalten ihre 6-MiB-Grenze.

Nach Update Browser im privaten Fenster testen, Favorit **Neu verbinden**,
und in F12 -> Netzwerk/Console vor allem auf 401, 403, 404, 413 und
CSP/CORS-Blockaden achten. URL + Status + Content-Type reichen für
eine gezielte Fehleranalyse; keine Bootstraptokens oder Cookies teilen.

Die Pfadvariante garantiert **keine** vollständige Kompatibilität mit
WebSockets, fremden CDNs, Service Workern, OAuth-Redirects oder
beliebig zusammengebauten JavaScript-URLs. Für Confluence Data Center
ist der offiziell unterstützte Kontextpfad des Originalservers
entscheidend: dessen Basispfad muss korrekt zu Connector/server.xml
und der Confluence-Base-URL passen. Ein Proxy kann die serverseitige
Konfiguration nicht vollständig ersetzen. Eine dedizierte Fav-Subdomain
ist für inkompatible Anwendungen zuverlässiger und bietet echte
Browser-Origin-Trennung.

---
## 502 nach Docker-Neustart: TLS-Zertifikatskette prüfen

Wenn der direkte Healthcheck am Docker-Port **nur mit curl -k** HTTP 200 liefert,
die vorgelagerte NGINX mit proxy_ssl_verify on aber 502 antwortet, liegt das
Problem **vor dem Favoriten-CSS/JS-Proxy**. Ein Git-Rollback behebt keine
fehlende Zertifikatskette oder falsche CA-Konfiguration.

Prüfung **ohne -k** (Beispiel-IP aus TEST-NET-1):

```sh
curl -v --resolve portal.example.org:8443:192.0.2.10 \
  https://portal.example.org:8443/_plenaro/favorite-health
openssl s_client -connect 192.0.2.10:8443 \
  -servername portal.example.org -verify_return_error \
  -CAfile /etc/ssl/certs/ca-certificates.crt </dev/null
```

curl -k bestätigt nur HTTP-Routing, nicht die TLS-Vertrauenswürdigkeit.
Bei Fehler 20 (unable to get local issuer) die vollständige Kette im
Docker-NGINX bereitstellen: zuerst Leaf, dann die nötigen Intermediates.
Insbesondere neue öffentliche CA-Hierarchien benötigen teils mehrere
Zwischenzertifikate. Keinen privaten Schlüssel in die Kette kopieren.

Zwei Varianten in der **lokalen** .env:

```dotenv
# Bevorzugt: CERT_PATH enthält bereits die vollständige Kette
PLENARO_TLS_CERT_PATH=/etc/ssl/plenaro/fullchain.pem
# Falls CERT_PATH nur das Leaf enthält, zusätzlich:
# PLENARO_TLS_CHAIN_PATH=/etc/ssl/plenaro/intermediates.pem
```

Mit PLENARO_TLS_CHAIN_PATH bindet Compose die PEM-Datei read-only ein;
der Container-NGINX kombiniert Leaf und Intermediates. Ohne den Parameter
bleibt ein vorhandenes vollständiges CERT_PATH unverändert.

Danach docker compose up -d --force-recreate nginx und ohne -k testen.
Bleibt 502, aktuelle äußere NGINX-Logs ansehen (.log statt alte .log.1).
Auch untrusted Root-CA, falscher SNI-Name oder ein anderer Upstream können
die Verbindung verhindern. proxy_ssl_verify nicht dauerhaft deaktivieren.

---
## Ein Host für alle Favoriten (optional, Phase 7)

Für eine Umgebung ohne Wildcard-DNS lässt sich jeder freigegebene Favorit
unter **einem** zusätzlichen HTTPS-Namen als Unterpfad betreiben:
`https://apps-plenaro.example.org/wiki/`,
`https://apps-plenaro.example.org/cloud/`.

Lokale `.env` (Plenaro/Native-Znuny bleiben unverändert):

```dotenv
PLENARO_FAVORITES_PATH_ORIGIN=https://apps-plenaro.example.org
PLENARO_SERVER_NAME=portal.example.org tickets.example.org apps-plenaro.example.org
# Optional vorhandene Alias-Varianten parallel behalten:
# PLENARO_FAVORITES_HOST_TEMPLATE={slug}-plenaro.example.org
# PLENARO_FAVORITES_BASE_DOMAIN=apps.example.org
```

`compose.yaml` gibt die neue Einstellung an die API weiter. Das neue
Pfadschema hat für neu gestartete Favoriten Vorrang; alte gespeicherte
Favoriten/Benutzer-ACL/Vault-Zugangsdaten bleiben erhalten, keine DB-Migration.
Bisherige Aliasse können parallel für alte Lesezeichen verwendet werden,
wenn ihr DNS/TLS-Routing bestehen bleibt.

**DNS:** Genau ein expliziter A-Record `apps-plenaro.example.org`, der auf
die **vorgeschaltete** NGINX zeigt; kein globales `*.example.org` nötig.

**TLS:** Zertifikat für `apps-plenaro.example.org` (z. B.
`*.example.org`). Die vorgeschaltete NGINX braucht **einmalig** einen vHost
oder eine Erweiterung des bestehenden vHosts für diesen einen Host.
`proxy_set_header Host $host;` sowie `X-Forwarded-Proto https` müssen
durch beide Proxy-Ebenen korrekt sein. Die bisherige HTTPS-Upstream-CA-Prüfung
bleibt aktiviert; `proxy_ssl_name` muss weiterhin zum Plenaro-Upstream
passen. Nicht den DNS-A-Record auf den eigentlichen Wiki-Server setzen.

**Probetest ohne Benutzer-Token:**

```sh
curl -i https://apps-plenaro.example.org/_plenaro/favorite-health
```

Erwartet HTTP 200 mit `plenaro-favorite-proxy-ready`. Unbekannte Pfade
liefern 404, ungültige/abgelaufene Favorit-Sitzungen 401.

**Sicherheit und Grenzen:** Der gemeinsame Host ist vom Plenaro-Login und
vom nativen Znuny-Proxy getrennt. Jeder Favorit bekommt weiterhin einen
eigenen serverseitigen Cookie-Jar und eine eigene DB-Freigabeprüfung.
Das Browser-Cookie wird unter `/<slug>` statt `/` gesetzt. Das ist **keine
Origin-Isolation zwischen Wiki, Cloud und anderen Favoriten**: JavaScript
eines kompromittierten Favoriten kann Same-Origin-Anfragen an die anderen
Pfade senden; Cookie-Path verhindert das nicht. Daher **nur vertrauenswürdige
Anwendungen mit vergleichbarem Sicherheitsniveau** zusammen betreiben.
Für untrusted Anwendungen bleiben dedizierte Favoriten-Subdomains nötig.

Pfad-Rewriting unterstützt absolute Upstream-Links, typische HTML-/JS-
Rootpfade, CSS-URLs und Upstream-Redirects. Nicht vollständig kompatibel
sind u. a. hart kodierte Service-Worker-Scope, WebSockets, OAuth/SSO mit
festen Callback-URLs oder komplexe JS-Router. Live-Test mit dem tatsächlichen
Wiki (Assets, Login, AJAX, Downloads, POST) sowie zwei Benutzern ist
**Pflicht vor einem produktiven Umstieg**. Ein Containerneustart setzt
die bisher nur im RAM liegenden Favoriten-Sessions zurück.

---

## Neu: einlagige App-Aliasse mit bestehenden Wildcard-Zertifikaten (Phase 7)

Für ein Zertifikat `*.example.org` können freigegebene Favoriten jetzt
`wiki-plenaro.example.org` statt `wiki.plenaro.example.org` verwenden.
Die neue Einstellung ist **optional**; ohne sie verhält sich Plenaro
genauso wie bisher. Beides kann während einer Migration parallel laufen:

```dotenv
PLENARO_PUBLIC_ORIGIN=https://portal.example.org
PLENARO_ZNUNY_PROXY_ORIGIN=https://tickets.example.org
PLENARO_FAVORITES_HOST_TEMPLATE={slug}-plenaro.example.org
# Optional für vorhandene Bookmarks: PLENARO_FAVORITES_BASE_DOMAIN=apps.example.org
# Freigabe des Hostnamens in der Plenaro-NGINX:
PLENARO_SERVER_NAME=portal.example.org tickets.example.org ~^[a-z][a-z0-9-]+-plenaro\.example\.org$
```

Einmalig erforderlich: Wildcard-DNS auf den **vorgeschalteten NGINX**,
passendes Zertifikat `*.example.org`, und ein **gemeinsamer** vHost
oder passendes Regex-`server_name`, der für alle App-Aliasse auf
Plenaro weiterleitet. `Host` muss unverändert durch **beide** Proxies,
`X-Forwarded-Proto` als `https` an die Plenaro-API gelangen.
Die vorgeschaltete NGINX kann die Alias-Namen z. B. einmalig über
`~^[a-z][a-z0-9-]+-plenaro\.example\.org$` routen; für jeden
neuen Favoriten ist dann **keine** weitere vHost-Änderung nötig.
Wenn ein vorhandener NGINX bereits einen passenden vHost für diesen
Wildcard-Bereich hat, keine zweite TLS-Konfiguration anlegen.

**NGINX-Priorität beachten:** Ein vorhandener vHost mit
`server_name *.example.org` hat Vorrang vor einem Regex-`server_name`
für `*-plenaro.example.org`. Falls auf derselben NGINX bereits andere
Anwendungen unter `*.example.org` liegen, nicht einfach einen zweiten
Regex-vHost anlegen: Die Alias-Weiche muss im tatsächlich ausgewählten
Wildcard-vHost erfolgen, ohne bestehende Dienste zu beeinträchtigen.
Vor dem Neuladen mit `nginx -t` und `nginx -T` prüfen.

Die App-Ziele bleiben weiterhin explizit von Admins freigegeben und
sind benutzergebunden. Unbekannte/fehlerhafte Hostnamen erhalten 404;
Znuny und Plenaro-Login bleiben getrennt. Für alte `slug.apps.example.org`
Links bleibt `PLENARO_FAVORITES_BASE_DOMAIN` gesetzt. Legacy-Anfragen
behalten beim Link-Rewriting ihren alten Host; neue Bootstrap-Links nutzen
die neue Vorlage. **Nach einem Containerneustart müssen persönliche
Proxy-Sitzungen neu gestartet werden**, da sie bislang im Arbeitsspeicher
liegen. Favoriten, Freigaben und Vault-Zugänge bleiben gespeichert.
**Kein Datenbank-Migrationsskript erforderlich.**

Test nach DNS/TLS/NGINX-Umstellung:
```sh
curl -i https://wiki-plenaro.example.org/_plenaro/favorite-health
```
Erwartet: `200`, Antworttext `plenaro-favorite-proxy-ready`.
Nicht mit einem echten Bootstrap-Einmal-Token testen.

**Wichtig:** In der öffentlichen Repository-Dokumentation ausschließlich
Beispieldomains verwenden; tatsächliche interne Domains, Zertifikatpfade
und Secrets gehören nur in die lokale `.env` und den lokalen NGINX.

---

## Troubleshooting: Favorit-iframe erreicht Bootstrap nicht (PR #18)

Seit PR #18 lässt sich der komplette **öffentliche** Alias-Routingweg
ohne Benutzerkonto, Wiki-Passwort oder einmaligen Sitzungstoken prüfen:

```sh
curl -i https://wiki.apps.example.org/_plenaro/favorite-health
```

**Erwartet:** HTTP `200` und exakt `plenaro-favorite-proxy-ready`
als Antworttext. Der Diagnose-Endpunkt enthält keine persönlichen Daten
und funktioniert auch für noch nicht als App freigegebene Slugs, damit
reine DNS-/NGINX-Fehler unabhängig von Datenbank-Freigaben erkennbar sind.

- DNS/TCP/TLS-Fehler: Windows-DNS-Wildcard, Routing, Port 443 und
  Browser-Zertifikat der vorgeschalteten NGINX kontrollieren.
- NGINX `502`: Upstream-Verbindung von der vorgeschalteten NGINX zum
  Plenaro-Docker-Host einschließlich Host/TLS prüfen.
- `404`, `426` oder statt Prüfsignatur die Plenaro-HTML-Oberfläche:
  **beide** Proxy-Stufen müssen den Original-`Host` weiterleiten.
  Außerdem `X-Forwarded-Proto: https` setzen und im Plenaro-Container
  `PLENARO_FAVORITES_BASE_DOMAIN=apps.example.org` überprüfen.
- Korrekte Prüfsignatur, aber Bootstrap `401`: über die
  **Plenaro-Favoriten-Navigation** einen neuen einmaligen Link erzeugen;
  Links verfallen nach 45 Sekunden und sind nur einmal verwendbar.
  Niemals echte Bootstrap-Tokens in öffentliche Tickets/Logs kopieren.
- Korrekte Prüfsignatur, aber Bootstrap `403`: freigegebene
  App, aktiven Favoriten und persönliche Benutzerberechtigung prüfen.
- Bootstrap `302` auf den eingestellten Startpfad: Routing und
  Sitzungseinrichtung funktionieren; dann erst das eigentliche
  Upstream-Wiki/HTML/AJAX untersuchen.

**Besonderheit:** Die Einstellung `PLENARO_SERVER_NAME` ist eine
Liste mit Hauptdomain, Znuny-Domain und `*.apps.example.org`.
Das Wildcard-Zertifikat deckt nur eine Subdomain-Ebene ab. Falls
eine vorgeschaltete NGINX vorhanden ist, benötigt sie ebenfalls
einen passenden Wildcard-vHost, der auf **Plenaro** weiterleitet
und den `Host` (z. B. `wiki.apps.example.org`) beibehält.

React.StrictMode kann im Entwicklungsmodus Effekte doppelt ausführen.
Die neue Favoriten-Startlogik nutzt für denselben Favoriten/Verbindungsversuch
dieselbe Start-Promise und verhindert so konkurrierende Einmal-Tokens.
Das allein löst keine DNS-/NGINX-Fehlkonfiguration in Produktivumgebungen.

---

## Neue Funktion (PR #12)

Administratoren können unter Einstellungen → Favoriten freigegebene HTTPS-Anwendungen anlegen, auf Subdomain-Slugs zuordnen und allen oder ausgewählten Benutzern gestatten. Ein Benutzer kann daraus Favoriten erstellen, umbenennen, sortieren und im Plenaro-Reiter öffnen. Benutzername/Passwort sind optional und bleiben serverseitig verschlüsselt. Automatisches Login unterstützt nur übliche HTML-POST-Formulare; ansonsten ist eine interaktive Anmeldung erforderlich.

Erforderliche Konfiguration auf dem Host (Beispieldaten):

```dotenv
PLENARO_PUBLIC_ORIGIN=https://portal.example.org
PLENARO_ZNUNY_PROXY_ORIGIN=https://tickets.example.org
PLENARO_SERVER_NAME=portal.example.org tickets.example.org *.apps.example.org
PLENARO_FAVORITES_BASE_DOMAIN=apps.example.org
```

Zusätzlich Wildcard-DNS und ein vertrauenswürdiges Zertifikat für die App-Domains einrichten. Neue freigegebene Apps erfordern dann keine weitere NGINX-Änderung. Fremde Redirects, direkte Benutzer-URLs und Cross-Origin-Zugriffe bleiben gesperrt. SSO/2FA, WebSockets und komplexe Apps erfordern möglicherweise eigene Integrationen. Pro Benutzer wird zunächst genau ein Favorit pro freigegebener Anwendung zugelassen, damit zwei verschiedene Konten nicht denselben Browser-Cookie-Host überschreiben. Reale Host-Abnahme bleibt offen.

---

# Plenaro 2.0 – Wildcard-Hosts und zukünftige Favoriten

## Ziel und WPF-Referenz

In der klassischen Desktop-Version werden Webfavoriten über `Models/AppSettings.cs`
(`WebShortcutSettings`), `Views/Settings/FavoritesSettingsView.xaml`,
`ViewModels/SettingsViewModel.cs` und
`Services/WebShortcutBrowserSessionManager.cs` verwaltet.
Übernommen werden sollen Name, Ziel-URL, Aktiv-Schalter, Reihenfolge, getrennte
Benutzeranmeldungen und optionales Auto-Login. **Nicht** übernommen werden
`DisableWebSecurity` und `AllowInsecureContent`: diese Desktop-Optionen
dürfen browserseitige Sicherheitsgrenzen nicht abschalten.

Die bestehende Znuny-Proxy-Origin bleibt unverändert und wird nicht als
universeller Proxy für Benutzeradressen umfunktioniert.

## Wildcard-Infrastruktur (nur vorbereitete Host-Sperre, noch KEINE Favoriten-GUI)

Bereits vorhandene NGINX-Templates benutzen `PLENARO_SERVER_NAME` in
`server_name`. Deshalb kann die **lokale** `.env` ohne neue NGINX-Site-Datei
zum Beispiel diese **frei erfundenen** Werte enthalten:

```dotenv
PLENARO_HTTPS_ENABLED=true
PLENARO_PUBLIC_ORIGIN=https://portal.example.org
PLENARO_ZNUNY_PROXY_ORIGIN=https://tickets.example.org
PLENARO_SERVER_NAME=portal.example.org tickets.example.org *.apps.example.org
PLENARO_FAVORITES_BASE_DOMAIN=apps.example.org
```

Für `*.apps.example.org` muss ein Wildcard-DNS-A-/AAAA-Record auf **denselben**
Plenaro-Reverse-Proxy zeigen. Es wird ein vom Browser akzeptiertes Zertifikat
für `*.apps.example.org` benötigt. Es muss außerdem die Hauptanwendung und
die Znuny-Origin abdecken (oder die jeweilige vorgelagerte TLS-Terminierung
benutzt passende Zertifikate). Ein Standard-Wildcard-Zertifikat gilt nur für
**eine** zusätzliche DNS-Ebene.

Das neue `FavoriteHostGuard`-Middleware erkennt alle Hosts unter
`PLENARO_FAVORITES_BASE_DOMAIN`. Solange für einen solchen Host **keine**
verifizierte Anwendung existiert, beantwortet Plenaro die Anfrage bewusst
mit HTTP 404 und `Cache-Control: no-store`, **bevor** die Plenaro-UI,
Static Files, Identity-Cookies oder APIs angesprochen werden.
Die bereits funktionierende, ausdrücklich konfigurierte Znuny-Origin wird
zuvor separat abgefangen. IP-/Healthcheck-Zugriff und alle bisherigen Hosts
außerhalb der optionalen Domain werden nicht verändert.

**Aktueller Stand:** Die Wildcard-Host-Sperre ist der erste sichere Baustein.
Eine Adresse wie `wiki.apps.example.org` zeigt nach diesem Schritt noch
**keine Wiki-Seite**. Es wird noch kein frei wählbares internes Ziel
weitergeleitet.

## Danach zu implementieren (Phase 7, mit Security-Abnahme)

- [ ] Admin-Verzeichnis von zulässigen Web-Apps: unveränderliche Site-ID,
      eindeutiger Subdomain-Slug, HTTPS-Upstream-Origin und freigegebene
      Zugriffsgruppen/Benutzer. Hostname und Ziel dürfen nicht vom Browser
      ungeprüft bestimmt werden.
- [ ] Benutzerfavoriten in PostgreSQL mit Name, Sortierung, Aktivstatus,
      App-ID und optionalem Deep-Link **nur innerhalb dieser App**;
      pro Benutzer isoliertes CRUD und Tests.
- [ ] Pro Benutzer + App separate Proxy-Session mit serverseitigem
      Cookie-Jar, CSRF-geschütztem einmaligen Starttoken und
      host-exklusivem Secure/HttpOnly-Cookie; Logout, Sperrung und
      Rechteentzug widerrufen die Sitzung.
- [ ] Pro-App-Berechtigung vor jedem HTML-/Asset-/AJAX-/POST-Request
      prüfen; keine offenen Redirects, keine Port-/DNS-/IP-SSRF,
      kein Zugriff auf Loopback/Metadaten/Cloud-Netze, TLS/CA-Prüfung,
      Begrenzungen für Request-/Response-Größen und Rate-Limits.
- [ ] Anmeldemethoden abhängig von freigegebener Anwendung. Klassische
      HTTPS-Formulare können eine persönliche Vault-basierte Anmeldung
      bekommen; Entra/SAML, Windows Integrated, 2FA oder dynamische
      JavaScript-Loginformen müssen einzeln geprüft werden.
- [ ] Statische Ressourcen, JS, AJAX, Formulare, Downloads, WebSockets,
      absolute/relative Links und Content Security Policy gegen reale
      Testanwendungen prüfen. Nicht jede fremde Anwendung kann generisch
      und verlustfrei durch eine HTML-Umschreib-Bridge laufen.
- [ ] Favoriten als eigene Plenaro-Navigation und Einstellungen
      implementieren; DNS-/Zertifikats-/Proxy-Abnahme am Zielhost.

**Sicherheit:** NGINX-Wildcard ist nur die Eingangsadresse. Die
**serverseitige** App-Liste ist die entscheidende Erlaubnis. Benutzer
dürfen nicht per URL-Eingabe beliebige interne Server zum Proxy-Ziel
machen. Jede eingebettete App läuft auf einer anderen Browser-Origin,
niemals unter der Plenaro-Login-Origin; Plenaro-CSRF/Origin-Checks
müssen erhalten bleiben.

Bei Änderungen an der `.env` ist wie bisher ein Neustart mit
`docker compose up -d --build --remove-orphans` erforderlich. Wenn
später ein Administrator eine **neue freigegebene App** in Plenaro
anlegt, soll dafür weder eine Änderung der NGINX-Konfiguration noch
ein Neustart notwendig sein.


### Verbesserte CSS-/JS-Ressourcen (PR #21)

Seit PR #21 wird der gemeinsame Pfadhost auch bei unquoted HTML-src/href,
mehreren srcset-Kandidaten, CSS-url()-Pfaden und JS-Template-Strings unter
/<slug>/ umgeschrieben. Bekannte CSS/JS/JSON/SVG/Manifest/WASM-Endungen
werden korrigiert, wenn der Upstream sie irrtümlich als text/plain oder
application/octet-stream ausliefert; die nosniff-Sicherheitsregel bleibt
aktiv. Ein Aufruf von /wiki ohne Slash führt mit 307 nach /wiki/.

Bei weiterhin fehlenden Ressourcen in Entwicklerwerkzeugen (Network) prüfen:
angefragte URL, HTTP-Status, Content-Type, CSP/CORS-Meldung und eventuelle
Weiterleitung auf den originalen Upstream. Niemals Session-Cookies oder
Bootstrap-Token teilen. Ein gemeinsamer Pfadhost kann nicht beliebige
Drittanbieter-CDNs, WebSockets, Service Worker oder OAuth-Callbacks einer
fremden App automatisch kompatibel machen. Der serverseitige Cookie-Jar
isoliert keine beliebigen JavaScript-Anwendungen auf derselben Browser-Origin.
