# Plenaro: NGINX, HTTP/HTTPS und Secret-Vault

Seit dieser Änderung wird **NGINX automatisch mit der normalen `compose.yaml` gestartet**. Caddy und `compose.secure.yaml` werden nicht mehr benötigt. ASP.NET Core/Plenaro bleibt ausschließlich in einem Docker-Netz erreichbar (kein öffentlicher 8080-Port).

## Betriebsarten

| Wert in `.env` | Aufruf | Verhalten |
| --- | --- | --- |
| `PLENARO_HTTPS_ENABLED=false` | `http://HOST-IP/` | NGINX liefert Plenaro über Port 80 |
| `PLENARO_HTTPS_ENABLED=true` | `https://HOST-IP/` | NGINX liefert über 443; HTTP auf 80 leitet mit 301 zu HTTPS weiter |
| Optional: `PLENARO_SERVER_NAME=plenaro.example.de` | `http(s)://plenaro.example.de/` | Funktioniert, **wenn** DNS/Hosts-Datei auf die Host-IP zeigt; bei HTTPS muss das Zertifikat zum Namen passen |

Es werden keine Ports in der URL benötigt. Die Compose-Datei reserviert auf dem Docker-Host standardmäßig **Port 80 und Port 443**, auch wenn der HTTPS-Listener bei `false` nicht aktiv ist. Wenn dort bereits NGINX, Apache, Traefik oder Caddy läuft, müssen die Portkonflikte zuerst behoben werden.

## Update der bisherigen Docker-Installation

**Vorher PostgreSQL sichern.** Der folgende Wechsel entfernt **keine** persistenten Datenbank-Volumes:

```bash
# Im Plenaro-Verzeichnis: alte Container (auch ggf. Caddy) beenden.
docker compose down --remove-orphans
git pull --ff-only
docker compose up -d --build --wait --remove-orphans
docker compose ps
curl -f http://127.0.0.1/health/ready
```

**Wichtig:** Niemals `docker compose down -v` ausführen. Bei einem zuvor über `compose.secure.yaml` gestarteten Caddy-Stack den alten Stack zunächst gezielt mit `docker compose -f compose.yaml -f compose.secure.yaml down` beenden, **bevor** dessen Datei durch `git pull` entfernt wird.

Die bisherige `.env` bleibt bestehen. Die alten Variablen `PLENARO_HTTP_PORT` und `PLENARO_HTTPS_HOST` werden nicht mehr verwendet. Falls du sie eingetragen hast, kannst du sie entfernen.

## HTTP-Modus (Standard)

```dotenv
PLENARO_BIND_IP=0.0.0.0
PLENARO_SERVER_NAME=_
PLENARO_PUBLIC_HTTP_PORT=80
PLENARO_PUBLIC_HTTPS_PORT=443
PLENARO_HTTPS_ENABLED=false
```

Danach: `http://DEINE-SERVER-IP/`. `PLENARO_SERVER_NAME=_` bedeutet, dass NGINX als Default-Host sowohl IP-basierte als auch passend zugestellte Host-Anfragen bedient.

**HTTP überträgt Passwörter unverschlüsselt!** Nur für kurzfristige Entwicklung in einem vollständig vertrauenswürdigen LAN verwenden; keine Router-Portweiterleitung.

## HTTPS mit eigenem Zertifikat

Du brauchst einen gültigen Zertifikats-/Schlüssel-Dateisatz auf dem **Docker-Host**. Dieser kann aus deiner internen PKI, aus Let's Encrypt oder einem anderen vertrauenswürdigen Aussteller stammen. Der Zertifikatsname muss mit dem verwendeten FQDN beziehungsweise der IP übereinstimmen (Subject Alternative Name).

Beispiel-`.env`:

```dotenv
PLENARO_HTTPS_ENABLED=true
PLENARO_SERVER_NAME=plenaro.example.de
PLENARO_TLS_CERT_PATH=/etc/ssl/plenaro/fullchain.pem
PLENARO_TLS_KEY_PATH=/etc/ssl/plenaro/privkey.pem
```

Wenn du stattdessen die IP verwendest, setze z. B. `PLENARO_SERVER_NAME=192.168.178.50` **und verwende ein Zertifikat mit IP-SAN für genau diese Adresse**. Für selbst signierte/interne Zertifikate muss der Client der ausstellenden CA vertrauen.

`PLENARO_TLS_CERT_PATH` und `PLENARO_TLS_KEY_PATH` sind absolute **Host-Dateipfade** und werden nur lesbar nach NGINX gemountet. Die Zertifikatsdatei sollte die vollständige Zertifikatskette enthalten. Der private Schlüssel bleibt auf dem Host und darf **nicht** ins Git-Repository eingecheckt werden. Bei Let's Encrypt müssen die Quellpfade vom Docker-Daemon auflösbar sein; erneuerte Zertifikate erfordern `docker compose restart nginx`, damit NGINX sie erneut einliest.

```bash
docker compose config
docker compose up -d --build --wait
curl -I http://DEINE-SERVER-IP/
# Erwartet: 301 Location: https://DEINE-SERVER-IP/
```

Danach im Browser `https://DEINE-SERVER-IP/` bzw. `https://plenaro.example.de/` aufrufen. Für HTTPS werden die Session-/Antiforgery-Cookies als `Secure` ausgestellt; ASP.NET akzeptiert Proxy-Header ausschließlich von der fest konfigurierten NGINX-Container-IP. NGINX leitet die ursprüngliche Client-IP für Login-Rate-Limits weiter.

Ist HTTPS aktiv, fehlen aber gültige, lesbare Zertifikatsdateien, startet NGINX **absichtlich nicht**. Den Status mit `docker compose logs nginx` prüfen. Während HTTPS deaktiviert ist, werden die Zertifikatsdateien nicht benötigt.

**DNS bei FQDN:** NGINX erstellt keine DNS-Einträge. Einen A-/AAAA-Eintrag oder einen lokalen DNS-/Hosts-Eintrag auf die Server-IP setzen.

**Sicherheitsgrenzen:** HTTPS im LAN ist damit technisch vorhanden. Vor einer Internetfreigabe bleiben Sicherheits-/Rechte-Audits, Absicherung des Serverzugangs, Logging, Backups und Recovery erforderlich.

## Verschlüsselte Integration-Secrets

Der bisherige AES-256-GCM-Vault bleibt unverändert. Für seine Aktivierung auf dem Docker-Host:

```bash
mkdir -p secrets
openssl rand -hex 32 > secrets/vault.key
sudo chown root:1654 secrets/vault.key
sudo chmod 0640 secrets/vault.key
```

Zusätzlich in der `.env`:

```dotenv
PLENARO_VAULT_KEY_FILE=/run/plenaro-secrets/vault.key
```

Die Datei liegt auf dem Host unter `./secrets/vault.key` und wird nur lesbar nach `/run/plenaro-secrets/vault.key` im Webcontainer gemountet. Der Vault funktioniert unabhängig vom HTTPS-Schalter technisch in beiden Modi, aber **Secrets niemals über unverschlüsseltes HTTP eingeben**.

In **Einstellungen** können Administratoren globale und Benutzer eigene Secrets ablegen. Gespeicherte Werte verlassen die Datenbank nicht als Klartext über die Web-API; nur Secret-Namen werden angezeigt. Der Schlüssel muss sicher und **getrennt vom Datenbankbackup** aufbewahrt werden. Ohne den Schlüssel sind die Werte nicht entschlüsselbar. Schlüsselrotation und vollständige Recovery bleiben eigene offene Aufgaben.

## Diagnose

```bash
docker compose ps
docker compose logs --tail=100 nginx web
docker compose exec -T web dotnet Plenaro.Api.dll --healthcheck
curl -f http://127.0.0.1/health/ready  # HTTP-Modus
```

**Keinen zweiten Reverse-Proxy parallel auf denselben Ports starten.** Für eine bestehende NGINX-/Traefik-Infrastruktur kann Plenaro auch hinter dem bereits betriebenen Proxy integriert werden; das wäre eine separate Konfiguration ohne den eingebauten NGINX-Hostport.



## Znuny-Webansicht wie WebView2, vollständig auf dem Plenaro-Server

Plenaro verwendet jetzt einen **echten internen Chromium-Browser** im Docker-
Service `znuny-browser` statt eines HTML-iframe-Proxys. Dieser Browser öffnet
die administrativ festgelegte Znuny-Webadresse und meldet sich ausschließlich
mit den persönlichen, im Plenaro-Vault hinterlegten Zugangsdaten an.

**Client-Datenfluss:**

1. Browser des Mitarbeiters → bestehende Plenaro-Webadresse (Port 80/443)
2. Plenaro-Backend → interner Selenium/WebDriver-Container (Docker-Service
   `znuny-browser:4444`; **niemals auf dem Docker-Host veröffentlicht**)
3. Chromium im Docker-Container → internes Znuny/OTRS über HTTPS
4. Plenaro liefert ausschließlich gerenderte PNG-Bilder an den Mitarbeiter;
   Klicks, Tasten und Scrollen werden über authentifizierte API-Aufrufe
   serverseitig an Chromium weitergeleitet

Der Client erhält weder Znuny-Cookies, Passwörter, Browser-HTML/JavaScript
noch WebDriver-Session-IDs. Jede Plenaro-Benutzer-ID hat eine eigene
WebDriver-Sitzung; eine inaktive Sitzung läuft nach spätestens 12 Minuten aus.
Maximal vier gleichzeitige Chromium-Sitzungen werden zugelassen. Öffentliche
WebDriver- oder VNC-Ports sind nicht freigeschaltet. Formularaktionen
werden von Chromium direkt im privaten Netzwerk ausgeführt; ein iframe-Sandbox-
Ursprungsproblem existiert dadurch nicht mehr.

Der zweite Port 8444 sowie `PLENARO_ZNUNY_PORTAL_ORIGIN` und
`PLENARO_ZNUNY_MAIN_ORIGIN` aus PR #51 sind **entfallen** und können aus
der vorhandenen `.env` gelöscht werden.

### Start / Update

```bash
cd /opt/plenaro
git pull --ff-only
docker compose up -d --build
docker compose ps
docker compose logs --tail=50 znuny-browser
```

Beim ersten Start wird das offizielle Selenium/Chromium-Image
`selenium/standalone-chromium:4.51.0-20261010` geladen; es ist groß
(ungefähr 1 GB komprimierter Download). Für jeden aktiven nativen Browser
ist zusätzlicher RAM erforderlich. Der Docker-Host selbst muss Znuny
erreichen und dessen DNS-Namen auflösen können; der Mitarbeiter-PC nicht.

**Wichtig:** Die interne Unternehmens-CA muss bei Bedarf auch im
Chromium-Container vertraut sein. Wenn die Webseite im Server-Browser eine
Zertifikatswarnung zeigt, das Zertifikat bzw. die CA korrekt installieren,
statt die TLS-Prüfung pauschal abzuschalten.

### Prüfung

- Browser-Entwicklertools: Netzwerkziele ausschließlich Plenaro
  (`/api/v1/znuny/browser/start`, `/frame`, `/input`).
- `docker compose ps`: `znuny-browser` läuft, enthält aber **keine
  veröffentlichten Ports**.
- Interner Browser: tatsächliche Znuny-Loginseite oder Ticketübersicht
  sichtbar, Navigation und Formulare reagieren.
- Falls Anmeldung fehlschlägt: persönliche Zugangsdaten in Plenaro prüfen;
  die Znuny-GenericInterface-Anmeldung ist eine **separate** Verbindung
  und kann andere Berechtigungen benötigen.
- Ticketantworten/Zeiten aus der regulären Plenaro-Ticketansicht nutzen
  weiterhin die serverseitige Znuny-API und müssen separat anhand eines
  Testtickets verifiziert werden.


## Optionale interne CA für Ticketsystem-API und nativen HTML-Proxy

Der Plenaro-Webcontainer nutzt den Linux-Truststore, ergänzt um eine
optionale interne Unternehmens-CA. Für die ausgehende HTTPS-Verbindung
vom Backend zum Ticketsystem gilt **dieselbe** CA sowohl für API- als
auch für die native HTML-Proxy-Kommunikation.

Nur in der **lokalen** `.env` auf dem Docker-Host setzen:

```dotenv
PLENARO_TRUSTED_CA_FILE=/usr/local/share/ca-certificates/company-root-ca.crt
```

Dies ist ein neutraler Beispielpfad zu einer PEM-kodierten Root- oder
Intermediate-CA, **kein** privater Schlüssel und **kein** Hostzertifikat.
Der Mount ist read-only unter `/run/plenaro-trust/ca.crt`.
Beim Start kombiniert das Backend die internen und öffentlichen
Vertrauensanker in `/tmp/plenaro-ca-bundle.pem` und setzt
`SSL_CERT_FILE` für seinen eigenen Prozess. Die Zertifikatsprüfung und
die Prüfung des HTTPS-Hostnamens bleiben aktiv.

Ohne die Variable bleibt der öffentliche CA-Trust des Images bestehen.
Es gibt keinen zusätzlichen Chromium-/Selenium-Container und keinen
Chromium-Zertifikatsspeicher mehr.

### Hostprüfung

```bash
cd /opt/plenaro
docker compose config --quiet
docker compose up -d --build --remove-orphans
docker compose ps
docker compose exec web sh -c \
  'test -s /tmp/plenaro-ca-bundle.pem && echo "Zusatz-CA-Bundle aktiv"'
```

Der letzte Befehl ist nur erfolgreich, wenn eine zusätzliche CA
konfiguriert wurde. Falls das Zertifikat ungültig oder der Hostname
falsch ist, muss die Zertifikatskette des Ticketsystems korrigiert
werden: Die TLS-Prüfung wird **nicht** deaktiviert.

Das Zertifikat des öffentlichen Plenaro-HTTPS-Endpunkts ist davon
unabhängig. Für die native Webeinbettung müssen der Haupt- und
Proxy-Hostname **beide** im NGINX-Zertifikat enthalten sein. Siehe
[Native Ticketsystem-Webansicht](NATIVE_TICKET_PROXY.md).
