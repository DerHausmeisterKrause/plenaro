# Zentrale Wiki-Konfiguration mit persönlichem Auto-Login (Phase 7)

## Gewünschtes Betriebsmodell
- **Administrator:** Hinterlegt genau eine HTTPS-Wiki-Serveradresse und eine Wiki-Startseite für die gesamte Plenaro-Instanz. Der existierende Favoriten-App-Eintrag `slug=wiki` wird zentral aktualisiert und explizit für alle freigegeben. Andere Favoriten-Anwendungen bleiben unverändert; kein weiteres DNS oder NGINX nötig.
- **Jeder Benutzer:** Hinterlegt ausschließlich den **eigenen** Wiki-Benutzernamen und das **eigene** Passwort in Einstellungen → Wiki. Das Passwort bleibt als AES-256-GCM Secret unter `scope=user` im vorhandenen Vault gespeichert und wird nie zurückgegeben. Ohne Zugangsdaten wird kein Wiki-iframe gestartet.
- **Wissensdatenbank:** Startet nur nach durch den Wiki-Upstream **bestätigter** individueller Server-zu-Server-Anmeldung. Pro Benutzer verwendet der Favoriten-Proxy einen getrennten HttpClient/CookieContainer. Wird die Anmeldung nicht bestätigt, erscheint eine Fehlermeldung statt eines scheinbar erfolgreichen Login-Frames.

## Confluence Data Center
Die zuvor analysierte Browseraufzeichnung zeigte den Login-Endpunkt
`POST /rest/tsv/1.0/authenticate` mit JSON-Feldern
`username`, `password`, `rememberMe`, `targetUrl`, `captchaId`
und eine erfolgreiche Antwort HTTP 200 `{"next":"/"}`.
Die Wiki-Startfunktion ruft zuerst `/login.action` auf und sendet dann
genau dieses JSON über den sicheren, administrativ freigegebenen HTTPS-Upstream.
Eine erfolgreiche JSON-`next`-Antwort mit gleichem Upstream-Origin gilt als
bestätigter Login; 401, Captcha, ein fremdes Redirect-Ziel oder abweichende
Antwortformatierung **gelten nicht als erfolgreiche Anmeldung**.
MFA, Captcha und fremde SSO/IdP werden nicht umgangen.

## APIs und Zugriffsgrenzen
- `GET/PUT /api/v1/wiki/admin/`: AdminOnly, CSRF für PUT. Upsert in bestehendes
  `favorite_apps`-Schema mit Slug `wiki`, `available_to_all=true`; Startpfad
  als `system_settings.wiki.browser`. Bei Änderungen bestehende Proxy-Sitzungen
  widerrufen; kein Datenbankschema-Update nötig.
- `GET/PUT/DELETE /api/v1/wiki/settings`: ausschließlich persönliche
  Wiki-Credentials. Serverseitig wird die User-ID aus ASP.NET Identity ermittelt;
  ein benutzereigener Favorit für die zentrale Wiki-App wird automatisch angelegt
  bzw. aktualisiert (immer `auto_login=true`); Passwort ist optional nur beim
  Ändern bereits gespeicherter Credentials, bei Erstanlage Pflicht.
  PUT/DELETE erfordern CSRF. Es gibt keine nutzerseitige URL-Freigabe.
- `POST /api/v1/wiki/start`: Identity + CSRF + Same-Origin-Prüfung und
  Start-Rate-Limit. Lädt nur die **eigene** Wiki-Konfiguration, erzwingt die
  bestätigte private Confluence-Anmeldung und liefert danach eine einmalige
  Favoriten-Proxy-Bootstrap-URL. Das generische Favoriten-Verhalten bleibt
  unverändert; die Wiki-Startfunktion hat absichtlich keinen stillen Fallback
  zum manuellen Login.

## Bereitstellung
1. GitHub `main` aktualisieren und Docker-Dienste Web + NGINX neu bauen.
2. Administrator: **Einstellungen → Wiki** → Name, Wiki-HTTPS-Origin,
   Startpfad speichern. Der bereits vorhandene Wiki-App-Eintrag wird
   übernommen (nicht neu angelegt), bisherige Benutzerpasswörter bleiben.
3. Jeder Benutzer: im selben Einstellungsbereich eigenen Wiki-Benutzernamen
   und das persönliche Passwort einmalig speichern.
4. **Wissensdatenbank** öffnen: persönlicher Autologin vor Einbettung.
   Bei falschem Passwort gibt es eine klare Fehlermeldung.
5. Browser-Live-Test mit zwei getrennten Konten und ggf. Anmeldung/Logout.

## Grenzen
Diese Änderung ersetzt **nicht** die alten WPF-Wiki-REST-Suchprovider,
Space-Key-Suche oder Wiki-/KI-Indizierung. Ein unbekanntes Confluence-Plugin
oder eine andere Wiki-Anmeldetechnik benötigt einen gesondert geprüften
Adapter; sie darf nicht per unbestätigtem Auto-Login als erfolgreich gelten.

---

# Wissensdatenbank / Wiki-Browser (Phase 7, PR #31)

## WPF-Referenz
- `DerHausmeisterKrause/project_tool/Views/WikiBrowserView.xaml`: einfache Wiki-Seite mit Statuszeile und eingebettetem WebView2.
- `ViewModels/WikiBrowserViewModel.cs`: auswählbare aktivierte Quellen; zuerst gespeicherte Standardquelle, dann erste Quelle. `GetHomeUrl` bevorzugt `BrowserHomeUrl` vor der Basisadresse.
- `Views/Settings/WikiSettingsView.xaml`: Quellen, Name, Base URL, Anmeldeoptionen, eigene Startseite und Standard-Wiki; weitere Suchindex- und KI-Funktionen sind getrennte Features.

## Umsetzung Web
- Navigation `/wiki`: echter Wissensdatenbank-Reiter mit Quellenauswahl, Name, Benutzer-Sitzung und HTML-iframe im vorhandenen persönlichen Favoriten-Proxy. Ein bereits funktionierender Favorit mit Slug `wiki` wird übernommen; es gibt keinen zusätzlichen Remote-Fetch oder doppelte Anmeldedatenverwaltung.
- Einstellungen → `Wiki`: bestehenden persönlichen Wiki-Favoriten als Standard speichern, oder aus einer **bereits von einem Admin freigegebenen** Anwendung einen Wiki-Favoriten mit Name, Startpfad, persönlichem Benutzernamen und optionalem Passwort/Vault anlegen. Optionaler HTML-Autologin; JavaScript/SSO erfordern u. U. die normale manuelle Anmeldung.
- Administratoren können einen neuen HTTPS-Wiki-Server freigeben: exakt eine Proxy-Origin und optionaler Startpfad, kein unbeschränktes Proxyziel. Zur Bearbeitung vorhandener Rechte und Favoriten nach Einstellungen → Favoriten. Bereits freigegebene Proxy-Origins sind aus Sicherheitsgründen nicht still editierbar.
- `GET/PUT /api/v1/wiki/settings`: Speichern des persönlichen Standard-Wiki-Favoriten in `user_preferences.preferences.wikiFavoriteId`; Session-Identität, CSRF, Eigentümerbindung, App-`enabled` und freigegebene App-ACL serverseitig geprüft. Keine neue Migration. Die generische persönliche Einstellungs-API bewahrt das Wiki-Feld beim Speichern anderer Präferenzen mit JSONB-Merge.
- Jede HTML-iframe-URL wird als HTTPS Bootstrap-Pfad für die **genau ausgewählte App** geprüft. Die Sitzung stammt nur aus `POST /api/v1/favorites/{id}/start` und hat den vorhandenen Einmal-Token. Keine freie Wiki-URL in `iframe src`, keine fremden Cookies oder secret-bearing URLs in diesem Repo.

## Bestehende Wiki-Instanz einrichten
1. Zielhost aus aktuellem `main` deployen.
2. Unter **Einstellungen → Wiki → Standard-Wiki** den bereits funktionierenden persönlichen Wiki-Favoriten auswählen und speichern.
3. Den Menüpunkt **Wissensdatenbank** öffnen und die persönliche Confluence-Anmeldung verwenden. Kein neues DNS, kein neuer äußerer NGINX-vHost und keine Änderung an der bestehenden Favoritenkonfiguration nötig.
4. Falls bisher keine Freigabe besteht: Admin trägt zunächst Wiki-URL ein; danach legt der Benutzer die persönliche Quelle an und wählt sie aus.
5. Test: Seite nach Reload und Ab-/Anmeldung wieder öffnbar; Test mit zwei Benutzern und unterschiedlicher Favoritenfreigabe.

## Noch offen (keine vorgetäuschte WPF-Parität)
- Confluence Data Center / Cloud REST-Provider und XWiki, CQL/Space-Key-Suchscope und Indexaktualisierung.
- Ticket-bezogene Wiki-Treffer, Dokumenten-/Attachment-Index, lokale KI-Suche und semantisches Retrieval.
- Echter Browser-E2E-Test gegen die Wiki-Installation (Login, JS/CSS, Links/Unterseiten, zweiter Benutzer). Die CI benutzt ausschließlich Testwerte und Demo-Domains.
