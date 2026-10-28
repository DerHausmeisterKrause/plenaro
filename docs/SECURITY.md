# Sicherheitsgrundlagen

## Aktuell

- Web nur an localhost gebunden: `127.0.0.1:8080`.
- PostgreSQL nur im internen Docker-Netz; kein Port 5432 am Host.
- Passwort wird ausschließlich aus `.env` gelesen; `.env` ist gitignored.
- Container läuft als unprivilegierter Nutzer (`APP_UID`).
- Backend gibt bei DB-Ausfall HTTP 503 zurück, ohne das Connection-Secret an den Browser zu senden.
- Noch **keine** Identität und Autorisierung: die Statusseite ist absichtlich die einzige Oberfläche. **Kein öffentliches Deployment**.

## Muss vor Produktivbetrieb erledigt sein

- Erststart-Admin, starke Passwort-Hashes, Session-Cookies (Secure/HttpOnly/SameSite), CSRF- und Rate-Limits.
- RBAC und Datenzugriffsschutz auf API- und Datenbankebene.
- Entra OIDC-Validierung (Issuer, Audience, Nonce, State und PKCE); Graph-Tokens verschlüsselt, minimale Scopes.
- TLS / Reverse Proxy, überprüfte Forwarded Headers, sicherer Cookie-/CORS-Betrieb.
- Datenbankmigrationen, automatisierte Backups **und** Restore-Tests, Ausfall- und Updatekonzept.
- Secret-Rotation, Protokollierung ohne Tokens, Security-Tests, Dependency-Audits.
- DSGVO-Rollen, Export/Löschung, Aufbewahrung, Löschkonzept bei Mehrbenutzerbetrieb.
