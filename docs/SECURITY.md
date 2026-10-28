# Sicherheitsgrundlagen

## Aktuell

- Web-Port standardmäßig auf `0.0.0.0:8080` gebunden (LAN-Zugriff auf Host-IP möglich). **Ausschließlich in vertrauenswürdigem LAN verwenden; keine Internet-Freigabe!** Mit `PLENARO_BIND_IP=127.0.0.1` kann der Zugang wieder lokal begrenzt werden.
- PostgreSQL nur im internen Docker-Netz; kein Port 5432 am Host.
- Passwort wird ausschließlich aus `.env` gelesen; `.env` ist gitignored.
- Container läuft als unprivilegierter Nutzer (`APP_UID`).
- Backend gibt bei DB-Ausfall oder veralteter/inkonsistenter Migrationshistorie HTTP 503 zurück, ohne das Connection-Secret an den Browser zu senden.
- Migrationen sind transaktional, per Advisory Lock gegen parallelen Start geschützt und durch SHA-256-Prüfsummen überwacht.
- Noch **keine** Identität und Autorisierung: es gibt nur eine öffentliche SPA-Vorschau mit leeren Modulen und Statusabfragen. **Kein öffentliches Deployment**, keine produktiven Daten.

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
