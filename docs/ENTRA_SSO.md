# Microsoft Entra ID – optionaler SSO-Login (Phase 4)

Plenaro unterstützt neben dem **unverändert vorhandenen lokalen Login** einen Microsoft-Entra-ID-Login als **optionale, tenantgebundene** Anmeldung. Microsoft-Konten werden nicht automatisch zu Plenaro-Benutzern; die lokale Admin-/User-Rolle bleibt maßgeblich.

## Voraussetzung: HTTPS

- Plenaro bereits unter einem **vertrauenswürdigen** HTTPS-Namen erreichbar, z. B. `https://plenaro.example.de`.
- `PLENARO_HTTPS_ENABLED=true` mit passenden NGINX-Zertifikaten/Schlüsseln. Über HTTP wird die Microsoft-SSO-Konfiguration aus Sicherheitsgründen **nicht gestartet**.
- Für die Microsoft-Rückleitung muss dein FQDN von den Clients erreichbar sein. Die Redirect-URI muss exakt auf diesen Namen zeigen. Bei IP-basierten oder selbstsignierten Zertifikaten gelten die Einschränkungen der Microsoft-Plattform und des Browsers – für SSO wird ein gültiger FQDN empfohlen.

## 1. Microsoft Entra App Registration

In [Microsoft Entra Admin Center](https://entra.microsoft.com/) unter **App registrations → New registration**:

1. Anzeigename: `Plenaro`
2. Kontotyp: **Accounts in this organizational directory only (Single tenant)**
3. Redirect-URI, Typ **Web**: `https://plenaro.example.de/signin-entra`
4. **Application (client) ID** und **Directory (tenant) ID** notieren.
5. Unter *Certificates & secrets* ein **Client Secret** anlegen. Den **Value** sofort sicher kopieren; nicht die Secret-ID.
6. Für diese erste Stufe braucht Plenaro **nur Anmeldung** (`openid`, `profile`, `email`). **Keine** `Calendars.Read`, `Mail.Read` oder `offline_access` benötigt. Graph wird später separat implementiert und braucht explizite Rechte/Einwilligung.

Der vertrauliche Authorization-Code-Flow mit PKCE, signierter ID-Token-Prüfung, State/Nonce und tenantgebundener Issuerprüfung läuft im ASP.NET-Backend. `oid` und `tid` identifizieren das Microsoft-Konto dauerhaft – E-Mail ist **kein** Verknüpfungskriterium. Keine Graph- oder Microsoft-Tokens werden in das Plenaro-Session-Cookie geschrieben.

## 2. Client Secret sicher als Datei hinterlegen

Im Repository-Verzeichnis auf Debian:

```bash
mkdir -p secrets
sudo install -m 0640 -o root -g 1654 /dev/null secrets/entra-client-secret
sudo nano secrets/entra-client-secret
```

In die Datei **nur den Secret-Value** einfügen, ohne Anführungszeichen oder `KEY=`. Sie liegt in `./secrets`, ist von Git ausgeschlossen und wird read-only nach `/run/plenaro-secrets` im Webcontainer eingebunden. Nicht in `.env`, Compose, PRs oder GitHub Secrets kopieren.

Auf Docker-Hosts mit anderer Container-UID/GID die Lesbarkeit separat prüfen. Bei Secret-Rotation die Datei aktualisieren und den Webcontainer neu erstellen, da die OIDC-Konfiguration beim Start geladen wird.

## 3. In .env aktivieren

```dotenv
PLENARO_HTTPS_ENABLED=true
PLENARO_SERVER_NAME=plenaro.example.de
PLENARO_ENTRA_ENABLED=true
PLENARO_ENTRA_TENANT_ID=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
PLENARO_ENTRA_CLIENT_ID=yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy
PLENARO_ENTRA_CLIENT_SECRET_FILE=/run/plenaro-secrets/entra-client-secret
```

Tenant-/Client-ID durch deine echten GUIDs ersetzen. Der Client Secret **Value** bleibt in der Datei, nicht in `.env`.

```bash
docker compose up -d --force-recreate --wait
docker compose logs --tail=50 web nginx
```

Ohne vollständige oder gültige Entra-Konfiguration startet der Webcontainer bei aktiviertem Entra bewusst **nicht**. Bei `PLENARO_ENTRA_ENABLED=false` (Standard) sind keine Microsoft-Zugangsdaten nötig und der gewohnte lokale Login bleibt unverändert.

## 4. Microsoft-Konto mit lokalem Benutzer sicher verbinden

1. In Plenaro **lokal** mit deinem vorhandenen Konto anmelden.
2. **Einstellungen → Microsoft Entra ID → Microsoft-Konto verknüpfen**.
3. In Microsoft mit dem gewünschten Unternehmenskonto anmelden.
4. Zurück in den Plenaro-Einstellungen **Verknüpfung bestätigen** (geschützte POST-Anfrage mit CSRF-Token).
5. Abmelden. Danach erscheint beim Login auch **Mit Microsoft anmelden**.
6. Du kannst dich weiterhin mit E-Mail und lokalem Passwort anmelden. Der Entra-Login übernimmt **nur** die bereits vorhandenen lokalen Plenaro-Rollen.

**Wichtig:** Ein gleichlautender E-Mail-Name reicht nicht! Ohne explizite Verbindung im angemeldeten lokalen Konto wird ein Microsoft-Login abgewiesen. Ein Microsoft-Konto kann nur einem lokalen Plenaro-Konto zugewiesen werden.

Im Einstellungsbereich kann eine Verbindung wieder entfernt werden, sofern der Benutzer ein lokales Passwort hat. Beim Trennen werden frühere lokale Sessions widerrufen; die eigene aktuelle Sitzung wird erneuert.

## Fehler und Abnahme

- **Microsoft-Button fehlt:** `PLENARO_ENTRA_ENABLED=false` oder unvollständige Konfiguration.
- **Microsoft-Konto noch nicht verbunden:** lokal einloggen und unter Einstellungen verknüpfen.
- **AADSTS50011:** Redirect-URI stimmt nicht exakt mit `https://DEIN-FQDN/signin-entra` überein.
- **Ungültiges Zertifikat:** TLS-Zertifikat auf dem Client prüfen; keine Zertifikatsprüfung abschalten.
- **OIDC-Failure:** Tenant-/Client-ID, Secret-Wert, gültige Uhrzeit, Netzwerkzugang zu `login.microsoftonline.com` sowie `docker compose logs web` prüfen.

Der CI-Workflow testet ohne Microsoft-Zugangsdaten weiterhin den **deaktivierten Zustand** und den lokalen Login. **Ein vollständiger SSO-/Verknüpfungs-Test mit echtem Entra-Tenant ist noch offen und muss auf dem Zielhost abgenommen werden.**

**Noch nicht implementiert:** Microsoft Graph Kalender/Outlook, Tokenrefresh/-speicherung für Graph, dynamische Tenant-Konfiguration im Admin-UI und mehrere Tenants. Es werden keine Kalenderberechtigungen angefordert.
