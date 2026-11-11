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
