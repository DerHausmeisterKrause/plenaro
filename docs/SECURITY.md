# Sicherheitsgrundlagen

## Aktuell

- NGINX ist standardmäßig auf `0.0.0.0:80` (und reserviert 443) gebunden; ASP.NET Port 8080 ist **nicht** am Host veröffentlicht. **Ausschließlich in vertrauenswürdigem LAN verwenden; keine Internet-Freigabe!** Mit `PLENARO_BIND_IP=127.0.0.1` kann der Zugang wieder lokal begrenzt werden.
- PostgreSQL nur im internen Docker-Netz; kein Port 5432 am Host.
- Passwort wird ausschließlich aus `.env` gelesen; `.env` ist gitignored.
- Container läuft als unprivilegierter Nutzer (`APP_UID`).
- Backend gibt bei DB-Ausfall oder veralteter/inkonsistenter Migrationshistorie HTTP 503 zurück, ohne das Connection-Secret an den Browser zu senden.
- Migrationen sind transaktional, per Advisory Lock gegen parallelen Start geschützt und durch SHA-256-Prüfsummen überwacht.
- Lokale ASP.NET Identity, Login, Admin-Bootstrap und RBAC-Grundlage sind implementiert. Fachliche Aufgaben-/Ticket-API-Rechte und vollständiger Security-Audit stehen noch aus.

**Netzwerk:** Host-Firewall auf vertrauenswürdiges LAN begrenzen. Auf keinen Fall Router-Portfreigabe, UPnP/öffentliche Reverse-Proxies oder Public-IP-Bindung vor Phase 3.

## Muss vor Produktivbetrieb erledigt sein

- Erststart-Admin, starke Passwort-Hashes, Session-Cookies (Secure/HttpOnly/SameSite), CSRF- und Rate-Limits.
- RBAC und Datenzugriffsschutz auf API- und Datenbankebene.
- Entra OIDC-Validierung (Issuer, Audience, Nonce, State und PKCE); Graph-Tokens verschlüsselt, minimale Scopes.
- TLS / Reverse Proxy, überprüfte Forwarded Headers, sicherer Cookie-/CORS-Betrieb.
- Versionierte SQL-Basismigrationen sind umgesetzt. Automatisierte **Backups und Restore-Tests**, Ausfall- und Updatekonzept sind weiterhin offen.
- Secret-Rotation, Protokollierung ohne Tokens, Security-Tests, Dependency-Audits.
- DSGVO-Rollen, Export/Löschung, Aufbewahrung, Löschkonzept bei Mehrbenutzerbetrieb.


## Erste lokale Identity-Authentifizierung (Phase 3)

- ASP.NET Identity mit gehashten Passwörtern (mindestens 12 Zeichen, Groß/Klein, Ziffer, Sonderzeichen).
- Nach fünf fehlgeschlagenen Passwortversuchen 15 Minuten Kontosperre, zusätzlich Login-IP-Rate-Limit.
- Cookie: HttpOnly, SameSite=Strict, acht Stunden Sliding Expiration. Secure bei HTTPS.
- Login/Logout erfordern Antiforgery-Token im HTTP-Header `X-CSRF-TOKEN` plus CSRF-Cookie.
- Adminrolle wird bei `/api/v1/admin/ping` serverseitig überprüft; ohne Login liefern private APIs 401.
- Erstinstallation ausschließlich per interaktivem Container-CLI (Advisory Lock, Datenbanktransaktion), kein anonymer Netzwerk-Setup-Endpunkt.
- DataProtection-Schlüssel liegen in PostgreSQL, Sessions überleben normale Webcontainer-Neustarts.

**Weiterhin kritisch:** Die Host-IP nutzt standardmäßig HTTP. Dabei sind Login-Daten unverschlüsselt. Echte Konten erst über HTTPS; keine Internetfreigabe. Der DB-Keyring ist noch nicht gesondert verschlüsselt, und eine umfassende Prüfung aller zukünftigen Daten-APIs steht aus.


## Browser-Ersteinrichtung – Phase 3

- `GET /api/v1/setup/status` liefert ausschließlich, ob die Datenbank noch **keinen Benutzer** enthält.
- Bei leerer Benutzer-Datenbank wird automatisch die separate Setup-UI gezeigt. Ein Admin-Setup über das Netzwerk erfordert zusätzlich einen 256-Bit-Einmalcode.
- Den Code erzeugt ausschließlich eine Person mit Docker-Hostzugriff: `docker compose exec web dotnet Plenaro.Api.dll --setup-token`. Die Datenbank speichert nur SHA-256, der Code ist 15 Minuten gültig; erneute Ausgabe widerruft den alten Code.
- `POST /api/v1/setup/complete` validiert CSRF und wird rate-limitiert; Prüfung und Identity-Erstellung laufen unter PostgreSQL-Transaktion und Advisory Lock. Nach Erfolg wird der Code gelöscht. Existiert bereits irgendein Benutzer, verweigert der Server das Setup.
- Bei vorhandenen Benutzern erscheint nur die separate Login-Maske. **Benutzerverwaltung, Recovery bei verlorener Adminrolle und HTTPS-Härtung bleiben offen.**

**Keine öffentliche Freigabe der HTTP-Instanz**; Einrichtung und Passwortanmeldung zunächst nur in einem kontrollierten Testnetz oder über HTTPS.


## Benutzerverwaltung – Phase 3

- Admin-Benutzer-API: \`/api/v1/admin/users\`; benötigt serverseitig Rolle \`Admin\` und für Mutationen CSRF-Tokens.
- Neue Nutzer erhalten sichere Identity-Passworthashes und eine lokale Rolle (Admin oder User).
- Dauerhafte Sperre ist getrennt von automatischer zeitweiser Konto-Lockout-Sperre.
- Änderungen an Rolle oder Sperre widerrufen bestehende Sitzungen durch Rotation des Security Stamps; dieser wird bei jedem Request geprüft.
- Administratoren dürfen ihr eigenes Konto weder sperren noch herabstufen. Parallel ausgeführte Änderungen sind mittels PostgreSQL Advisory Lock serialisiert; der letzte aktive Admin wird geschützt.
- Es existiert keine Benutzer-Löschfunktion, um spätere Aufgaben-Besitzerbezüge nicht unbemerkt zu verlieren.
- Offen: zwingende Passwortänderung bei erster Anmeldung, Passwort-Recovery bei komplett verlorenem Admin-Zugriff, Auditlogs und verpflichtendes HTTPS.


## Phase 3 – Einstellungen ohne Secrets

- Persönliche `GET/PUT /api/v1/settings/me` sind an die durch ASP.NET Identity authentifizierte Nutzer-ID gebunden. Beliebige Nutzer-IDs aus HTTP-Parametern werden **nicht** angenommen.
- Globale Einstellungen: Lesen nur mit Anmeldung; Ändern mit `AdminOnly`, geprüft am Backend. Mutationen benötigen CSRF-Schutz.
- JSON-Payloads sind auf wenige typisierte Felder und begrenzte Werte beschränkt; **keine sensiblen Secrets** in `system_settings` oder `user_preferences` speichern.
- Kontentrennung wird im Docker-Smoke-Test mit zwei echten Konten geprüft: normale Benutzer sehen ihre eigenen Einstellungen; globale Änderungen liefern 403. Persistenz wird nach Webcontainer-Neustart geprüft.
- Weiter offen: verschlüsselte Secrets für Entra/Graph/Ticketsysteme, umfassende Autorisierung der zukünftigen Datenendpunkte, DSGVO-Konzept und verpflichtender TLS-Reverse-Proxy.

## NGINX-Standardproxy und verschlüsselte Integration-Secrets (Phase 3)

- NGINX startet immer per `compose.yaml`. Die `.env` aktiviert bei Bedarf HTTPS auf 443 mit eigenen Zertifikat-/Schlüsselpfaden. HTTP auf 80 leitet im HTTPS-Modus per 301 weiter; der interne Port 8080 wird nie am Host veröffentlicht.
- Forwarded-Proto/For wird ausschließlich von der explizit hinterlegten Proxy-IP akzeptiert; andere HTTP-Aufrufe als die interne Loopback-Gesundheitsprüfung werden im sicheren Modus mit 426 abgelehnt.
- AES-256-GCM-Secret-Vault mit 256-Bit-Masterkey in einer Host-Datei **außerhalb von PostgreSQL**. Nonce und Auth-Tag werden pro Secret gespeichert, AAD bindet Scope, Benutzer und Namen.
- Secret-Listen zeigen nur Namen; gespeichert wird ausschließlich Ciphertext. User-Secrets sind durch die authentifizierte User-ID isoliert, globale Mutationen benötigen Adminrechte und CSRF.
- Master-Key muss separat gesichert und bewusst rotiert werden. Bei verlorenem Schlüssel sind Geheimnisse nicht wiederherstellbar.
- Bei intern signierten Zertifikaten muss die ausstellende CA auf den Clients als vertrauenswürdig installiert werden; Zertifikatsprüfung niemals deaktivieren. Bei FQDN ist ein passender DNS-Eintrag erforderlich.
- **Restarbeiten:** Schlüsselrotation, DataProtection-Keyring-at-rest, Security-Audit, Backup/Restore sowie TLS-Abnahme auf dem tatsächlichen Host.
- Betriebsanleitung: [HTTPS_AND_VAULT.md](HTTPS_AND_VAULT.md).

**Proxy-IP:** Nur die feste NGINX-IP `172.29.213.10` darf Forwarded Headers liefern. In HTTP- und HTTPS-Betrieb wird damit die echte Client-IP für Rate-Limits ermittelt. Sicherheitskonzept und Anleitung: [HTTPS_AND_VAULT.md](HTTPS_AND_VAULT.md).
