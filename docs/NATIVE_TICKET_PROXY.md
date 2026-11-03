# Native Ticketsystem-Webansicht (HTTPS-Origin-Proxy)

## Ziel

Plenaro zeigt eine **echte HTML-/JavaScript-Oberfläche** des angebundenen
Ticketsystems im regulären Browser. Es wird nicht länger für jeden
Mausklick ein Screenshot aus dem Docker-Chromium übertragen.

Der PC erreicht **nur Plenaro**, nicht die interne Adresse des Ticketsystems.
Plenaro leitet CSS, JS, HTML, AJAX, Formulare und Downloads über den
bestehenden HTTPS-Port 443 an die intern konfigurierte Webadresse weiter.

Jeder Plenaro-Benutzer bekommt eine eigene Upstream-Cookie-Jar.
Persönliche Zugangsdaten verbleiben im verschlüsselten Vault.
Die echten Ticketsystem-Cookies werden **niemals** an den PC ausgegeben.

## Voraussetzung: Zwei Web-Origins auf EINEM Server

Der Ticketsystem-Frame muss auf einer **anderen HTTPS-Origin** als das
Plenaro-Login laufen. Eine Einbettung unter demselben Hostnamen würde
Legacy-JavaScript und Authentifizierungs-Cookies unnötig vermischen.
Dafür sind zwei **gleichsiteige** DNS-Namen nötig, zum Beispiel:

- Plenaro: `https://portal.example.com`
- Eingebetteter Proxy: `https://tickets.example.com`

Beide DNS-Namen zeigen auf **denselben Plenaro-Server**; keine zweite IP,
kein zusätzlicher Port und keine DNS-Auflösung der internen Zieladresse
auf dem Client. Das TLS-Zertifikat am NGINX muss **beide** Namen
als SAN oder per passender Wildcard abdecken. Das Zertifikat muss auch
vom Benutzer-Browser als vertrauenswürdig eingestuft werden.
Bei vorgelagertem SSO-Portal/Reverse Proxy müssen **beide Hostnamen**
korrekt über denselben Plenaro-Dienst erreichbar sein.

In `/opt/plenaro/.env` ergänzen:

```dotenv
PLENARO_HTTPS_ENABLED=true
PLENARO_SERVER_NAME=portal.example.com tickets.example.com
PLENARO_PUBLIC_ORIGIN=https://portal.example.com
PLENARO_ZNUNY_PROXY_ORIGIN=https://tickets.example.com
# Bestehende öffentliche TLS-Zertifikate, die für beide Namen gültig sind:
PLENARO_TLS_CERT_PATH=/etc/ssl/plenaro/fullchain.pem
PLENARO_TLS_KEY_PATH=/etc/ssl/plenaro/privkey.pem
```

**Die Domains sind reine Platzhalter.** Niemals echte Firmen-Domains,
Hostnamen, IP-Adressen, interne Zertifikatspfade, Benutzernamen oder
Zugangsdaten in dieses Repository, ein Issue, einen PR oder CI-Logs
schreiben. Die intern erreichbare Ticketsystem-Adresse bleibt ausschließlich
in der Plenaro-Admin-Konfiguration. Der vertrauenswürdige CA-Pfad wird nur
in der lokalen `.env` hinterlegt.

Danach:

```bash
cd /opt/plenaro
git pull --ff-only
docker compose up -d --build --remove-orphans
docker compose ps
```

Ohne die beiden HTTPS-Origins startet **keine** Ticketsystem-Einbettung.
Plenaro zeigt dann eine konkrete Einrichtungsanleitung. Der vorherige
Chromium-Screenshot-Modus wurde vollständig aus Frontend, Backend und
Docker Compose entfernt; es gibt keinen automatischen oder manuellen
Rückfall auf eine unsichere Einbettung.

## Authentifizierung und Isolation

1. Der angemeldete Plenaro-Benutzer öffnet den Ticketsystem-Reiter.
2. Plenaro prüft Benutzerstatus, Agentenzuordnung und ggf. Ticketfreigabe.
3. Der Server meldet sich mit den persönlichen, verschlüsselt gespeicherten
   Zugangsdaten über das normale Agenten-Loginformular beim Ticketsystem an.
   Formulare mit zusätzlichen Hidden-Feldern/Challenge-Tokens werden beachtet.
4. Die Browser-Oberfläche erhält ein **45 Sekunden gültiges,
   einmalig verwendbares Bootstrap-Token**. Das Token wird in einen
   host-exklusiven, `Secure`/`HttpOnly`/`SameSite=Strict` Cookie
   für die Proxy-Origin umgewandelt.
5. Beim Abruf leitet Plenaro die Anfragen mit einer individuellen
   serverseitigen `CookieContainer`-Session weiter. Keine
   Ticketsystem-Cookies und kein Login-Passwort gelangen zum Browser.
6. Beim Plenaro-Logout wird die aktive Proxy-Sitzung widerrufen.
   Zusätzlich gelten Inaktivitäts- und absolute Ablaufzeiten.

Der Proxy leitet nur an die vom Administrator gespeicherte HTTPS-Origin
weiter. Die existente DNS/TCP-Sicherheitsprüfung verhindert
Loopback-/Metadata-SSRF, TLS- und Hostnamenprüfung bleiben aktiv.
Keine beliebigen Upstream-Redirects oder Cross-Origin-Formular-POSTs.

Die iframe-Sandbox erlaubt nur benötigte Browserfunktionen,
**keine** Top-Level-Navigation. Eine Content-Security-Policy begrenzt
Ressourcen und `frame-ancestors` auf die eingestellte Plenaro-Origin.
Der im Browser laufende Frame kann die Plenaro-Sitzung des anderen
Hostnamens nicht lesen.

## Bekannte Kompatibilitätsgrenzen

- Das klassische Znuny-/OTRS-Agenten-Loginformular wird unterstützt.
  Externe SAML/OIDC-Redirects oder individuelle JavaScript-only-SSO-Formulare
  erfordern eine separate Implementierung. In diesem Fall zeigt
  Plenaro eine Fehlermeldung; es gibt keinen Browser-Bildmodus mehr.
- HTML, JS, CSS, JSON und Redirects mit absoluten Links auf die interne
  Origin werden umgeschrieben. Anwendungen mit dynamisch zusammengesetzten
  Hostnamen, Service Workers, WebSockets oder ungewöhnlichen CSP-Regeln
  können Sonderanpassungen erfordern.
- Der Proxy liefert binäre Anhänge ohne Text-Umschreibung. In Firefox/Chrome
  kann eine iframe-Sandbox spezielle Popup-/Download-Verhalten begrenzen.
- Mehrere Benutzer erhalten getrennte Sessions und HTTP-Verbindungen.
  Es werden keine dauerhaften Browserprozesse pro Benutzer gestartet.
- Die Sitzungsliste ist zunächst pro Plenaro-Webcontainer gespeichert.
  Bei mehreren replizierten Webcontainern sind Sticky Sessions oder ein
  gemeinsamer geschützter Session-Store erforderlich.
- Bis echte HTML-/AJAX-/Login-End-to-End-Tests gegen ein erlaubtes
  Zielsystem abgeschlossen sind, gilt die Implementierung nicht als
  produktiv abgenommen.

## Prüfungen

```bash
docker compose config --quiet
docker compose ps
docker compose logs --tail=80 nginx web
```

- Die Plenaro-Webadresse muss auf die Plenaro-UI zeigen.
- Die zweite Domain muss **nicht** Plenaro zeigen, sondern die isolierte
  Ticketsystem-Proxy-Route (ohne Einmal-Token normalerweise HTTP 401).
- Login, Dashboard, Filter, Scrollen, Tickets, Anhänge, Antworten und
  Benutzerwechsel getrennt verifizieren.
- In den Entwicklerwerkzeugen dürfen keine Verbindungen zu internen
  Ticketsystem-Domains sichtbar sein.
- Zwischen zwei Benutzern dürfen keine Cookies oder Sessions geteilt werden.


## Fehlerdiagnose und alternative native Anmeldung

Die automatische Anmeldung erfolgt zunächst weiterhin serverseitig mit dem
persönlichen, verschlüsselten Zugang. Wird danach erneut eine Loginmaske
zurückgegeben oder verwendet das Zielsystem ein individuell gestaltetes
Loginformular, öffnet Plenaro die echte Loginseite **innerhalb der
isolierten Proxy-Origin**. Benutzer können sich dort regulär anmelden.
Dafür wird kein Chromium-Container und kein zusätzlicher Client-Zugang
zum internen Ticketsystem benötigt.

Verbindungs- und TLS-Fehler werden dagegen nicht durch ein unsicheres
Fallback umgangen. Die Weboberfläche zeigt dafür einen **neutralen
Fehlercode** an, z. B. `upstream-network`, `upstream-tls`,
`upstream-timeout`, `unsafe-redirect` oder `server-error`.
Vorhandene Zugangsdaten, interne Zieladressen, HTML-Antworten und
Fehler-Stacktraces werden nicht zurückgegeben oder protokolliert.

Wenn das Einloggen am Proxy funktioniert, aber das iframe nicht erscheint,
muss der **Benutzer-Browser zusätzlich die konfigurierte zweite
HTTPS-Origin erreichen**. Sie zeigt auf denselben Plenaro-Server, nicht
auf den eigentlichen Ticketsystem-Server. Der vorhandene einzelne
Plenaro-Hostname allein genügt bei dieser isolierten Architektur
nicht. Solange ein sicherer Ein-Hostname-Mechanismus nicht separat
entwickelt und getestet ist, wird keine gleich-origin Einbettung
zugelassen.

Für Diagnosen nur HTTP-Status und neutralen Fehlercode teilen, keine
Screenshots mit Mitarbeiterdaten, Cookies, Loginformularen, internen
Hostnamen oder Konfigurationsdateien veröffentlichen.
