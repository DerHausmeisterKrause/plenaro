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
