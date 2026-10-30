# Microsoft Entra – Einrichtung über das Plenaro-Adminportal

Die Microsoft-Entra-Konfiguration wird **nicht mehr aus der .env** geladen. Plenaro verwaltet Tenant-ID, Client-ID und Client-Secret als **einen AES-256-GCM-verschlüsselten Datensatz im Secret-Vault**. Der lokale Login bleibt weiterhin uneingeschränkt verfügbar.

## Voraussetzungen

1. Plenaro muss mit `PLENARO_HTTPS_ENABLED=true` über vertrauenswürdiges HTTPS erreichbar sein. Der FQDN in den Redirect-URIs muss dem tatsächlichen Aufrufnamen entsprechen.
2. Der Secret-Vault muss einmalig über einen zufälligen 32-Byte-Masterkey im Host-Dateisystem eingerichtet sein, z. B.:

```bash
mkdir -p secrets
openssl rand -hex 32 > secrets/vault.key
chmod 600 secrets/vault.key
```

In die `.env` gehört **nur der Dateipfad** `PLENARO_VAULT_KEY_FILE=/run/plenaro-secrets/vault.key`, niemals der Schlüsselwert. Der Docker-Compose-Mount `./secrets:/run/plenaro-secrets:ro` liest die Schlüsseldatei nur beim Containerstart. Container-User-Berechtigungen prüfen.

3. In Microsoft Entra [App registrations](https://entra.microsoft.com/) eine **Single-Tenant-Webanwendung** anlegen. Als **Web Redirect URIs** beide URLs eintragen: `https://DEIN-FQDN/signin-entra` und `https://DEIN-FQDN/signin-graph`.
4. Ein Client-Secret erstellen. Die **Secret Value**, Tenant-ID und Client-ID notieren. Graph delegierte API-Berechtigungen **User.Read** und **Calendars.Read** hinzufügen. `offline_access` wird im Auth-Code-Flow als Scope angefordert. Zustimmung ist vom jeweiligen Nutzer oder abhängig von Tenant-Regeln vom Administrator nötig.

## Admin-Webkonfiguration

1. Als lokaler Plenaro-Admin anmelden.
2. **Einstellungen → Microsoft 365 → Microsoft Entra ID – Administration** öffnen.
3. Tenant-ID, Client-ID und Secret Value eingeben, speichern. Die Einstellungen werden **sofort** aktiv, ohne Compose-Änderung oder Containerneustart.
4. Client-Secret später rotieren: neues Secret im gleichen Formular eingeben. Bei unverändertem Secret Feld leer lassen. Das Secret wird nach dem Speichern nicht mehr ausgegeben.
5. Mit „Microsoft-SSO deaktivieren“ die gespeicherte Konfiguration entfernen; der lokale Login bleibt erhalten.

**Sicherheit:** Nur lokale Admins haben Zugriff auf die Konfigurationsendpunkte. Jede Mutation prüft CSRF und ist begrenzt. Die gesamte Entra-Konfiguration inklusive IDs ist verschlüsselt gespeichert. Der unabhängige Vault-Masterkey bleibt getrennt von PostgreSQL. Keine Tokens/Secrets in URL, Browser-Antwort oder Sitzungscookie. NGINX-HTTPS ist Pflicht.

## Benutzer und Kalender

Plenaro verknüpft Entra-Identitäten **nicht anhand der E-Mail-Adresse**. Jeder Benutzer meldet sich einmalig lokal an, öffnet **Microsoft 365** und bestätigt seine `tid:oid`-Kontoverknüpfung ausdrücklich. Nur so wird die richtige Plenaro-ID zugeordnet.

Wenn ein **bereits verknüpfter** Benutzer danach „Mit Microsoft anmelden“ nutzt, fordert der Code-Flow direkt `User.Read Calendars.Read offline_access` an. Nach Zustimmung wird sein Refresh-Token verschlüsselt im eigenen Benutzertresor abgelegt; er muss Outlook normalerweise **nicht mehr separat anbinden**. Beim ersten Kontoverknüpfen ist die ausdrückliche Bestätigung notwendig; falls noch keine Kalenderverbindung vorhanden ist, über **Wochenplanung → Outlook verbinden** freigeben oder beim nächsten Microsoft-Login automatisch verbinden lassen.

Der lokale Kalender zeigt eigene Aufgaben und UL/AM/BR/HO unabhängig von Microsoft. Fehlende Tenant-Zustimmung oder Microsoft-Fehler dürfen ihn nicht blockieren. Outlook wird zusätzlich über delegierten Zugriff auf **`/me/calendar/calendarView`** gelesen, nicht über Anwendungsberechtigungen für alle Postfächer.

## Abnahme

Der vorhandene GitHub-CI-Workflow testet Anmeldung, Vault nicht konfiguriert und Adminzugriff ohne echte Microsoft-Zugangsdaten. Ein vollständiger SSO- und Kalender-Roundtrip inklusive Einwilligung ist **ohne echten Entra-Tenant nicht verifiziert**. Nur ein Single-Tenant-Setup ist vorgesehen. Mit mehreren Docker-Webreplikas wäre zusätzliche Cache-/Konfigurationssynchronisation erforderlich.

Ein früheres `PLENARO_ENTRA_*`-Setup aus der `.env` wird **nicht automatisch übernommen**. Die Daten müssen einmalig im geschützten Adminportal neu eingegeben werden.
