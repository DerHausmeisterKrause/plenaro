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
