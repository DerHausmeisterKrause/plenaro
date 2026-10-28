# Plenaro: HTTPS im LAN und verschlüsselter Secret-Vault

Diese Erweiterung ist **opt-in**, damit bestehende Docker-Installationen weiterhin ohne Unterbrechung starten.

## Voraussetzung

- Docker Compose **v2.24.4 oder neuer**, da `!override` für die vollständige Entfernung der unsicheren Portfreigabe benötigt wird.
- Auf dem Docker-Host muss Port **443/tcp** frei sein.
- Das Caddy-Edge-Netzwerk verwendet `172.29.213.0/24` und den **exakt vertrauenswürdigen** Proxy `172.29.213.10`. Bei Subnetzkonflikten Netzwerk und `Security__TrustedProxyIp` gemeinsam anpassen.
- Plenaro vor dem Update sichern (PostgreSQL-Dump); `docker compose down -v` niemals auf Datenbeständen ausführen.

## Schlüssel erzeugen und schützen (Linux/Debian)

```bash
mkdir -p secrets
openssl rand -hex 32 > secrets/vault.key
sudo chown root:1654 secrets/vault.key
sudo chmod 0640 secrets/vault.key
```

Der .NET-Container läuft standardmäßig mit UID 1654; die Gruppe 1654 erhält Leserechte. **Der Schlüssel ist unverzichtbar für die Entschlüsselung.** Separat vom Datenbankbackup geschützt sichern, niemals einchecken. Wenn der Schlüssel verloren geht, sind die verschlüsselten Integrationswerte unwiederbringlich verloren. Beim Hinzufügen weiterer App-Instanzen muss derselbe Schlüssel bereitstehen.

*Docker Desktop und nichtstandardmäßige User-Namespaces:* Zugriffsrechte des gemounteten Secrets prüfen; Linux UID/GID muss ggf. zur tatsächlichen Container-UID passen.

## Optionalen sicheren Stack starten

Beispiel mit LAN-IP `192.168.178.50` (durch **deine** Host-IP ersetzen):

```bash
# In .env setzen:
# PLENARO_HTTPS_HOST=192.168.178.50
docker compose -f compose.yaml -f compose.secure.yaml up -d --build --wait
```

Jetzt lauscht Caddy auf **HTTPS Port 443**. Die bisherige HTTP-Portfreigabe des Webcontainers wird durch `!override []` entfernt. Die ASP.NET-Core-Anwendung akzeptiert im sicheren Modus nur HTTPS-Proxynachrichten vom fest konfigurierten Caddy-IP-Endpunkt. Nur die lokale Container-Gesundheitsprüfung darf HTTP verwenden. Die Auth- und CSRF-Cookies sind `Secure`, `HttpOnly`, `SameSite=Strict`.

Der lokale Caddy stellt über `tls internal` ein Zertifikat aus seiner internen CA bereit. Das Zertifikat wird **nicht** automatisch in Browsern anderer Geräte vertraut.

### Interne CA auf eigenen Geräten vertrauen

Nach dem ersten Start:

```bash
docker compose -f compose.yaml -f compose.secure.yaml cp proxy:/data/caddy/pki/authorities/local/root.crt ./plenaro-lan-root.crt
```

Dieses **öffentliche Root-Zertifikat** nur auf den eigenen, verwalteten Endgeräten als vertrauenswürdige Stammzertifizierungsstelle installieren. Nicht die CA-Private-Keys kopieren, versenden oder ins Git-Repository laden. Für Tests darf `curl -k` verwendet werden, aber **niemals** Zertifikatsprüfung in produktiven Clients deaktivieren. Bei Browsern mit eigener CA-Verwaltung ggf. separat importieren.

Der HTTPS-Aufruf ist anschließend `https://192.168.178.50/` (bzw. der Name aus `PLENARO_HTTPS_HOST`). Verwende exakt den Hostnamen bzw. die IP aus dem Zertifikat.

Für öffentlich vertrauenswürdige Zertifikate und Domains ist später ein gesonderter ACME-/DNS-Aufbau erforderlich. **Keine Router-Portweiterleitung** für die derzeitige Entwicklungsinstanz.

### Vorhandene Reverse-Proxies (NGINX/Traefik)

Alternativ kann ein bestehender HTTPS-Reverse-Proxy genutzt werden. Dann **keinen zweiten Caddy starten**. Den Webport auf Loopback oder ein nur für den Proxy erreichbares Netzwerk beschränken, `Security__RequireHttps=true` und `Security__TrustedProxyIp` exakt auf die tatsächliche Proxy-IP setzen. Vertrauenswürdige Forwarded-Header `X-Forwarded-Proto` und `X-Forwarded-For` müssen vom Proxy kommen. Kein `KnownNetworks` für das gesamte LAN freigeben.

## Verschlüsselte Integration-Secrets

Die SQL-Migration `0007_encrypted_secrets.sql` legt `encrypted_secrets` an. AES-256-GCM verschlüsselt Werte mit zufälligem 12-Byte-Nonce und authentifiziert Scope, Benutzer und Namen per AAD. **Das 256-Bit-Master-Key-Material liegt ausschließlich in der Host-Datei `secrets/vault.key`**, nicht in PostgreSQL.

Unter **Einstellungen → Persönliche Zugangsdaten** kann jeder Nutzer eigene Schlüssel speichern/löschen. Unter **Globale Integrations-Zugangsdaten** kann nur ein Administrator Schlüssel hinterlegen. Der Browser sieht **nur die Namen**, niemals gespeicherten Klartext. Für die späteren Entra-/Znuny-Adapter existiert eine ausschließlich serverseitige Entschlüsselungsmethode, aber noch keine fertige Integration.

API: `GET/PUT/DELETE /api/v1/secrets/me` und `/api/v1/admin/secrets` (für globale Werte). `GET` gibt nur `names` zurück, Schreiboperationen verlangen CSRF.

### Notwendige Grenzen

- Ohne `compose.secure.yaml` startet die Webapp weiterhin, die Vault-Endpunkte geben **HTTP 503** zurück. Dadurch wird der bestehende Entwicklungsbetrieb nicht unterbrochen.
- Das Keyfile ist außerhalb des DB-Backups zu sichern; **Master-Key-Rotation** benötigt noch eine geplante Re-Encryption-Migration und ist nicht als Knopfdruck verfügbar.
- AES-GCM schützt vor reinem Datenbankabfluss. Administratoren des Docker-Hosts mit Zugriff auf Keyfile und Datenbank können die Geheimnisse prinzipiell entschlüsseln.
- Der bisherige ASP.NET-Core-DataProtection-Keyring für Sessions liegt weiterhin in PostgreSQL; seine Verschlüsselung-at-rest, Backup-Restore, Auditlogging und ausführliche Penetrationsprüfung stehen vor Produktivbetrieb aus.
- Integrationstokens nicht in gewöhnliche `system_settings` oder `user_preferences` schreiben.
