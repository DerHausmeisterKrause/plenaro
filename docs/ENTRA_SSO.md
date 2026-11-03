# Microsoft Entra – Einrichtung über das Plenaro-Adminportal

Die Microsoft-Entra-Konfiguration wird **nicht mehr aus der .env** geladen. Plenaro verwaltet Tenant-ID, Client-ID und Client-Secret als **einen AES-256-GCM-verschlüsselten Datensatz im Secret-Vault**. Der lokale Login bleibt weiterhin uneingeschränkt verfügbar.

## Voraussetzungen

1. Plenaro muss mit `PLENARO_HTTPS_ENABLED=true` über vertrauenswürdiges HTTPS erreichbar sein. Der FQDN in den Redirect-URIs muss dem tatsächlichen Aufrufnamen entsprechen.
2. Der Secret-Vault muss einmalig über einen zufälligen 32-Byte-Masterkey im Host-Dateisystem eingerichtet sein, z. B.:

```bash
mkdir -p secrets
if [ ! -e secrets/vault.key ]; then
  openssl rand -hex 32 | sudo tee secrets/vault.key >/dev/null
fi
sudo chown root:1654 secrets/vault.key
sudo chmod 0640 secrets/vault.key
```

**Bestehenden Vault-Masterkey niemals überschreiben oder neu erzeugen!** Sonst werden bereits verschlüsselt gespeicherte Daten unlesbar. Sichere ihn getrennt von der Datenbank.\n\nIn die `.env` gehört **nur der Dateipfad** `PLENARO_VAULT_KEY_FILE=/run/plenaro-secrets/vault.key`, niemals der Schlüsselwert. Der Docker-Compose-Mount `./secrets:/run/plenaro-secrets:ro` liest die Schlüsseldatei nur beim Containerstart. Der .NET-Container startet standardmäßig als nicht privilegierter Benutzer mit UID/GID 1654. Die Gruppe 1654 darf den Schlüssel lesen, andere Benutzer nicht. Bei abweichender Container-UID/-GID die Zugriffsrechte entsprechend anpassen.

3. In Microsoft Entra [App registrations](https://entra.microsoft.com/) eine **Single-Tenant-Webanwendung** anlegen. Als **Web Redirect URIs** beide URLs eintragen: `https://DEIN-FQDN/signin-entra` und `https://DEIN-FQDN/signin-graph`.
4. Ein Client-Secret erstellen. Die **Secret Value**, Tenant-ID und Client-ID notieren. Graph delegierte API-Berechtigungen **User.Read** und **Calendars.Read** hinzufügen. `offline_access` wird im Auth-Code-Flow als Scope angefordert. Zustimmung ist vom jeweiligen Nutzer oder abhängig von Tenant-Regeln vom Administrator nötig.

## Admin-Webkonfiguration

1. Als lokaler Plenaro-Admin anmelden.
2. **Einstellungen → Microsoft 365 → Microsoft Entra ID – Administration** öffnen.
3. Tenant-ID, Client-ID und Secret Value eingeben, speichern. Die Einstellungen werden **sofort** aktiv, ohne Compose-Änderung oder Containerneustart.
4. Client-Secret später rotieren: neues Secret im gleichen Formular eingeben. Bei unverändertem Secret Feld leer lassen. Das Secret wird nach dem Speichern nicht mehr ausgegeben.
5. Mit „Microsoft-SSO deaktivieren“ die gespeicherte Konfiguration entfernen; der lokale Login bleibt erhalten.

**Sicherheit:** Nur lokale Admins haben Zugriff auf die Konfigurationsendpunkte. Jede Mutation prüft CSRF und ist begrenzt. Die gesamte Entra-Konfiguration inklusive IDs ist verschlüsselt gespeichert. Der unabhängige Vault-Masterkey bleibt getrennt von PostgreSQL. Keine Tokens/Secrets in URL, Browser-Antwort oder Sitzungscookie. NGINX-HTTPS ist Pflicht.

## Benutzer und Kalender (automatische Kontoanlage)

Bei der ersten erfolgreichen Microsoft-Anmeldung legt Plenaro für die validierte
Single-Tenant-Identität `tid:oid` automatisch **ein eigenes lokales Benutzerkonto**
mit Rolle `User` an. Der Benutzer erscheint in
**Einstellungen → Administration → Benutzerverwaltung**. Keine E-Mail-Zuordnung,
keine bestehende lokale Identität wird stillschweigend übernommen. Bereits früher
explizit verknüpfte Entra-Logins funktionieren weiter.

Nur ein Administrator darf die **Znuny-Agenten-ID** und die
**Kandidaten-Benutzer-ID** vergeben. Neue Entra-Benutzer erhalten beide IDs
zunächst nicht. Ohne Agenten-ID gibt es keine Ticketfreigabe; ohne zusätzliche
Kandidaten-ID keinen Zugang zum Kandidatenpool. Passwortlose SSO-Konten
benötigen kein Plenaro-Startpasswort.

Wenn der Microsoft-Tenant die delegierte Zustimmung für
`User.Read Calendars.Read offline_access` erteilt, wird der Refresh-Token
beim Login AES-256-GCM-verschlüsselt im Benutzertresor abgelegt.
Der Outlook-Kalender wird nur für das angemeldete Konto gelesen.
Bei fehlender Zustimmung bleibt die lokale Kalenderplanung nutzbar.

## Abnahme

Der vorhandene GitHub-CI-Workflow testet Anmeldung, Vault nicht konfiguriert und Adminzugriff ohne echte Microsoft-Zugangsdaten. Ein vollständiger SSO- und Kalender-Roundtrip inklusive Einwilligung ist **ohne echten Entra-Tenant nicht verifiziert**. Nur ein Single-Tenant-Setup ist vorgesehen. Mit mehreren Docker-Webreplikas wäre zusätzliche Cache-/Konfigurationssynchronisation erforderlich.

Ein früheres `PLENARO_ENTRA_*`-Setup aus der `.env` wird **nicht automatisch übernommen**. Die Daten müssen einmalig im geschützten Adminportal neu eingegeben werden.
