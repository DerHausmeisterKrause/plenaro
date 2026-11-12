# BR-Beantragung mit bestätigter E-Mail (Phase 5/7)

## Desktop-Referenz und gewünschter Unterschied

Der Vorgänger [project_tool](https://github.com/DerHausmeisterKrause/project_tool)
implementiert die Bestätigung **für HO**, nicht BR:
- `ViewModels/WeekViewModel.Core.cs::ToggleHomeOfficeAsync` fragt:
  „Wollen Sie am DD.MM.YYYY Homeoffice einreichen?“
- `Services/HomeOfficeService.cs::SubmitAsync` prüft zwei persönliche
  Empfänger, legt einen Outlook-Homeoffice-Termin an und versendet anschließend
  eine E-Mail per installiertem Outlook.
- `Services/OutlookInteropService.cs::SendHomeOfficeMail` verwendet den
  Betreff „Homeoffice am DD.MM.YYYY“ und sendet das MailItem per Outlook-COM.
- `BR` ist in der alten wie neuen Version ein **separater Tagesmarker**
  mit monatlichem BR-Kontingent; HO bleibt unabhängig.

**Expliziter Web-Auftrag:** Für das erstmalige Setzen des **BR**-Markers
wird jetzt vor dem Speichern gefragt:
„Willst du wirklich für den DD.MM.YYYY Bereitschaft beantragen?“
Bei „Nein“ wird weder BR gespeichert noch versendet.
Bei „Ja“ wird zuerst der BR-Marker lokal gespeichert, danach **ein**
E-Mail-Versuch über das persönliche Microsoft-365-Postfach gestartet.
Der Nutzer hat ausdrücklich den Text „Homeoffice“ angegeben, obwohl
der Auslöser BR (Bereitschaft) heißt: Wortlaut hier bewusst beibehalten
und separat klären, nicht stillschweigend ändern.

## Persönliche Empfänger

Einstellungen → **Aufgaben & Kalender** → **BR-Beantragung per E-Mail**:
`E-Mail-Empfänger 1` und optional `E-Mail-Empfänger 2`.
Beide werden jeweils **pro authentifiziertem Benutzer** in
`user_preferences.preferences.brMailRecipient1/brMailRecipient2`
gespeichert. Die anderen Präferenzen bleiben durch JSONB-Merge erhalten.
Der Server akzeptiert nur einzelne gültige E-Mail-Adressen, keine Header,
Steuerzeichen oder durch Semikolon getrennten Listen. Gleiche Adressen
werden beim Versenden dedupliziert.

## Mailtext

Betreff: `Homeoffice am DD.MM.YYYY`

```text
Servus,

ich mache am DD.MM.YYYY Homeoffice.

mfg.
<Vor- und Nachname des angemeldeten Benutzers>
```

Der Absender ist das eigene **delegierte Microsoft-365-Postfach** des
angemeldeten Benutzers. Der Anzeigename kommt ausschließlich aus der
serverseitig ermittelten Plenaro-Identität, nicht aus dem POST-Body.

## Microsoft Graph Berechtigung

Im Web kann Outlook-COM nicht laufen. Versand erfolgt einmalig über
`POST https://graph.microsoft.com/v1.0/me/sendMail` mit
**delegierter** `Mail.Send`-Berechtigung. Das existierende Graph-OAuth-
Verfahren fordert `Mail.Send` **erst beim expliziten Verbinden von
Microsoft 365** zusätzlich an; normaler Entra-Login und bestehende
`Calendars.Read`-Abfragen werden nicht automatisch zu Mail-Send
hochgestuft. Der Nutzer muss daher ggf. nach dem Update unter
Einstellungen → Aufgaben & Kalender → BR-Beantragung über
„Outlook verbinden / Mail.Send freigeben“ Microsoft neu verbinden.
Microsoft-Tenant-Administratoren müssen bei restriktiven Tenants
ggf. ihre Genehmigung erteilen.

Microsoft Graph liefert HTTP **202 Accepted** nach Annahme der
Mailanfrage; dies ist keine garantierte Zustellung.
Es gibt keine SMTP-Accounts oder App-only `Mail.Send` in Plenaro.

## Kein automatischer Doppelversand

Die Migration `0028_br_mail_requests.sql` legt den einzigartigen Schlüssel
`(user_id,day)` an. Vor einem Versand prüft der Server:
- angemeldeter, aktiver Benutzer und gültiges CSRF-Token
- BR-Marker für **genau diesen Benutzer und Tag** ist bereits gespeichert
- mindestens ein gültiger, persönlicher E-Mail-Empfänger
- persönlicher Microsoft-Graph-Refresh-Token und `Mail.Send`-Einwilligung
- atomare erstmalige Reservierung des Schlüssels, bevor Microsoft
  kontaktiert wird

Die Zustände `processing`, `accepted`, `unknown` verhindern
gleichzeitige, wiederholte und unkontrollierte Versuche. Ein zweiter
Versuch nach HTTP 202 oder unbekanntem Versandstatus wird serverseitig
abgewiesen. Ein sicher abgewiesener HTTP 403 und Vorab-Consentfehler
können nach Benutzerkorrektur erneut manuell ausgelöst werden.
Bei Timeout/Netzfehlern nach Absenden wird **nicht** wiederholt;
zuerst „Gesendete Elemente“ des eigenen Postfachs prüfen.

Das Markieren eines bestehenden BR-Tags, eines HO-Tags, von UL/AM
oder das reine Öffnen des Kalenders **sendet keine Mail**.
Nur die Bestätigung beim erstmaligen Setzen des BR-Markers oder der
separat sichtbare manuelle „Bereitschaftsantrag per E-Mail senden“-Knopf
für noch nicht eingereichte BR-Tage starten den Versand.

## Live-Abnahme

- [ ] Eigene Empfänger speichern und nach Neuanmeldung wiederfinden
- [ ] Microsoft `Mail.Send` vom betroffenen Benutzer freigeben
- [ ] BR-Tag auswählen, Nein: weder DB-Marker noch Mail
- [ ] BR-Tag auswählen, Ja: Marker gespeichert, Graph antwortet 202,
      passende Mail unter „Gesendete Elemente“ sichtbar
- [ ] Erneutes Speichern desselben Tages sendet nicht erneut
- [ ] Zweiter Benutzer: eigene Empfänger, eigene Mailbox, eigene Marker
- [ ] 401/403/Timeout/Unklarheit: keine Doppelmail; sichere Fehlermeldung
- [ ] Fachliche Begriffsklärung: BR-Antrag vs. Homeoffice-Mailtext

## 12.11.2026 – Korrektur Homeoffice-Mail: HO/BR, Ja/Nein und Microsoft-Verbindung (PR #34)

- [x] Ursache der fehlenden Mail beim HO-Marker gefunden: bisher hat nur BR die Mail ausgelöst; WPF verwendete ausdrücklich HO.
- [x] Auch beim erstmaligen **HO**-Marker (BR bleibt kompatibel) wird nach „Willst du wirklich am TT.MM.JJJJ Homeoffice beantragen?“ gefragt.
- [x] Eigener responsiver Dialog mit **„Ja, beantragen“** und **„Nein“**, statt nicht lokalisierbarem `window.confirm`.
- [x] Bei Nein keine Änderung und kein Mailversuch.
- [x] Bei Ja Vorabprüfung von persönlichen Empfängern und delegiertem Microsoft-365-Token inklusive `Mail.Send`; wenn ungültig, **kein** neuer HO-/BR-Marker und Fehlermeldung statt scheinbar erfolgreicher Beantragung.
- [x] Bei freigegebenem Konto Marker speichern und danach genau einen Mailversuch per Graph; vorhandener Doppelversandschutz bleibt erhalten.
- [x] In Benutzer-Einstellungen die Microsoft-Verbindung anzeigen. Der Button zum erstmaligen Verbinden erscheint nur bei fehlendem gespeicherten Graph-Token, nicht pauschal bei jedem Benutzer.
- [x] Ergänzte Frontendtests für HO, BR, Ja/Nein, Vorabprüfung und M365-Status.
- [ ] Auf dem Zielhost prüfen, ob delegiertes `Mail.Send` tatsächlich erteilt wurde und ein gültiges persönliches Token vorhanden ist; Graph 202 und E-Mail im Ordner „Gesendete Elemente“ prüfen.
- [ ] Zeitfenster, Ausfallszenarien und zwei parallele Benutzer real testen; keine unkontrollierten Mehrfachsendungen.

- [x] [PR #34](https://github.com/DerHausmeisterKrause/plenaro/pull/34) nach `main` gemergt; [GitHub-CI #43199978295](https://github.com/DerHausmeisterKrause/plenaro/actions/runs/43199978295) vollständig erfolgreich (Backend, Frontend, Tests, Docker-Login). Der echte Microsoft-Graph-Mailversand am Zielhost ist noch offen.
