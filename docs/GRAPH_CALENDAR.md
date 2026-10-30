# Phase 4: Outlook-Kalender lesen (Microsoft Graph)

Dieser Meilenstein ergänzt den bereits implementierten, optionalen Microsoft-Entra-Login um **delegierten Lesezugriff** auf den eigenen Outlook-Kalender. Die UI erscheint unter **Woche**.

## Architektur und Berechtigungen

- Single-Tenant-Entra-App aus [ENTRA_SSO.md](ENTRA_SSO.md), HTTPS aktiv, Microsoft-Identität **explizit** mit vorhandenem lokalem Plenaro-Account verknüpft.
- **Zusätzliche Web-Redirect-URI in der App Registration:** `https://DEIN-FQDN/signin-graph` (die bestehende `/signin-entra`-URI bleibt erhalten).
- Delegated Permissions für Microsoft Graph: **`User.Read`** (Prüfung der gebundenen Identität) und **`Calendars.Read`** (Kalendereinträge lesen). **`offline_access`** erlaubt, nach ausdrücklicher Einwilligung einen Refresh-Token zu empfangen. Je nach Tenant-/Consent-Richtlinie kann Admin-Einwilligung erforderlich sein.
- Der Kalenderverbindungs-Flow verwendet getrenntes ASP.NET-Core OAuth mit Authorization Code, PKCE und geschütztem State/Korrelation. Der Benutzer muss schon lokal angemeldet sein.
- Microsoft Graph `GET /me?$select=id` prüft anhand eines frisch ausgestellten **Graph-Access-Tokens**, ob die Microsoft-Objekt-ID mit der vorher explizit verknüpften Entra-`tid:oid`-Identität übereinstimmt. E-Mail-/UPN-Abgleich wird nicht verwendet.
- Refresh-Token wird **AES-256-GCM verschlüsselt** im bisherigen `SecretVault` mit Scope `user`, Besitzer-ID aus der lokalen Sitzung und Namen `graph.refresh` gespeichert. Weder Access- noch Refresh-Token werden an Browser, Cookies oder API-Antworten ausgegeben.
- Vor jeder Kalenderabfrage fordert das Backend mit dem verschlüsselten Refresh-Token einen neuen Access-Token von Microsoft an. Einen rotierten Refresh-Token ersetzt es ebenfalls verschlüsselt. In dieser ersten Version gibt es noch keinen Token-Cache, um die Implementierung klein zu halten.
- Die Outlook-Anzeige verwendet ausschließlich `GET /v1.0/me/calendar/calendarView` auf einem fest konfigurierten Microsoft-Graph-Host. Keine frei wählbaren URLs, fremden Mailboxen oder fremden Benutzer-IDs.
- Nur die nächsten/gewählten **7 Tage** werden angezeigt; die API begrenzt den Zeitraum auf **maximal 14 Tage** und die Ergebnisse auf **100 Termine je Anfrage**. Serientermine werden über `calendarView` aufgelöst. Bei mehr als 100 Einträgen weist die UI auf die unvollständige Anzeige hin.
- Rückgabedaten sind auf ID, Betreff, Beginn, Ende, Ganztägig-Kennzeichen, Belegtstatus, Ort und Outlook-Link beschränkt, HTTP `Cache-Control: no-store`. Keine gesamten Nachrichtentexte/Bodies oder Teilnehmerlisten.

## Einrichtung, sobald ein Microsoft-Tenant zur Verfügung steht

1. NGINX über vertrauenswürdiges HTTPS mit passendem FQDN aufrufen; Entra wie in [ENTRA_SSO.md](ENTRA_SSO.md) konfigurieren.
2. Den Masterkey für `SecretVault` vorbereiten und `PLENARO_VAULT_KEY_FILE=/run/plenaro-secrets/vault.key` in der `.env` setzen.
3. In der Entra App Registration die zweite Web-Redirect-URI `https://DEIN-FQDN/signin-graph` ergänzen.
4. Microsoft Graph Delegated Permissions `User.Read`, `Calendars.Read` hinzufügen. Der Zustimmungsdialog fordert auch `offline_access` an; etwaige Tenant-Admin-Freigabe beachten.
5. Container aktualisieren: `docker compose up -d --build --wait`.
6. Lokal mit Plenaro anmelden, unter **Einstellungen** die Microsoft-Identität verknüpfen.
7. Unter **Woche → Outlook verbinden** den delegierten Kalenderzugriff erlauben. Plenaro validiert, dass dieselbe Microsoft-Identität verwendet wurde.
8. Termine lesen. Für den Widerruf **Woche → Trennen**; bei Entfernung der Entra-Kontoverknüpfung löscht Plenaro ebenfalls den gespeicherten Graph-Refresh-Token.

## Endpunkte

- `GET /api/v1/graph/status`: angemeldeter Nutzer, Status `enabled`, `linked`, `vaultReady`, `connected`. Keine Tokenwerte.
- `GET /api/v1/graph/connect/start`: OAuth-Challenge, nur für angemeldete/Entra-verknüpfte Benutzer mit konfiguriertem Vault.
- `GET /signin-graph`: ausschließlich OAuth-Middleware-Callback.
- `GET /api/v1/graph/calendar?start=<UTC>&end=<UTC>`: bis 14 Tage des eigenen Kalenders; keine fremde Benutzer-ID. Bei fehlender/abgelaufener Zustimmung 409; bei Microsoft-Fehlern 502.
- `POST /api/v1/graph/disconnect`: eigenes Refresh-Token löschen; CSRF erforderlich.

## Grenzen & Abnahme

Der bestehende **einzige CI-Workflow** prüft den deaktivierten Zustand, Berechtigungsgrenzen und weiterhin Login, Datenbank und TLS. **Ein tatsächlicher Microsoft-OAuth-Roundtrip und Kalenderabruf können ohne App-Registration nicht getestet werden.** Der Code ist die Grundlage, nicht die bestätigte Funktion auf einem echten Tenant.

Noch offen: persönlicher Token-Cache, OAuth-Fehler-/Tenant-Integrationstests, Pagination über mehr als 100 Termine, Mehrkalender-Auswahl, Schreiboperationen für Meetings/Teams, Focus Time, Rate-Limit-/Retry-Policy und Admin-gestützte Tenant-Registrierung in der Weboberfläche.

## Kalender bei Entra-Anmeldung automatisch freigeben

Die neue Admin-Konfiguration verwendet ausschließlich den verschlüsselten Plenaro-Vault. Beim Microsoft-Login eines bereits verknüpften Plenaro-Benutzers werden `User.Read`, `Calendars.Read` und `offline_access` zusätzlich angefordert. Wenn der Tenant die delegierte Zustimmung erlaubt und Microsoft einen Refresh-Token ausstellt, speichert das Backend diesen sofort verschlüsselt unter `user/graph.refresh`. Kein Microsoft-Token wird ins Browser-Cookie geschrieben. Für neue Kontoverknüpfungen bleibt der Einwilligungs-/Verknüpfungsschritt notwendig; Benutzer können andernfalls einmalig über den separaten Kalender-Connect nachziehen. Die lokale 5-Tage-Ansicht läuft stets unabhängig.
