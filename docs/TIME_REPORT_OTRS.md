# Zeitreport: Plenaro + Znuny/OTRS

## Ursache des bisherigen Fehlers
`GET /api/v1/reports/month` summierte ursprünglich ausschließlich `time_entries`
und `task_time_adjustments` für **lokale** Plenaro-Aufgaben. Die Tabellen
`znuny_ticket_time_bookings` und die tatsächlichen Zeitbuchungen in Znuny
wurden nicht abgefragt. Damit war der Bericht kein OTRS-Zeitreport.

Das WPF-Referenzprojekt `DerHausmeisterKrause/project_tool` verwendet in
`ReportsViewModel` dagegen `TaskService.GetSuccessfulBookingStatistics`,
monatliche Nachbuchungen und getrennte Tagesmarker.

## Implementierung
- Ausgewählter Monat wird in Europe/Berlin in UTC-Grenzen umgerechnet.
- `ZnunyReader.ReadAccountedTimeAsync` liest die offizielle
  `TimeAccountingGet`-Operation des separaten Znuny-REST-Webservices
  `TimeAccountingREST`. Es werden **alle vom jeweiligen angemeldeten Agent
  in OTRS geschriebenen Zeitbuchungen** des Monats gelesen, unabhängig von
  aktuellem Ticketbesitzer oder Status.
- Authentifizierung nutzt ausschließlich den **persönlichen Znuny-Agent-
  Benutzernamen und sein im Plenaro-Vault gespeichertes Passwort**. Vor jedem
  Abruf ist eine administrativ freigegebene Agenten-ID erforderlich. Keine
  Abfrage anderer Benutzer und keine Weitergabe von Zugangsdaten an neue Hosts.
- Der remote-Zeitwert `TimeUnit` ist **Minuten**, auch Kommazahlen und
  negative Korrekturen sind möglich; pro Buchung in Sekunden umgerechnet.
- Lokale Einträge aus `znuny_ticket_time_bookings` zählen nur mit Status
  `confirmed`. `pending` und `rejected` sind keine nachgewiesenen
  Buchungen. `booked_minutes` ist maßgeblich, nicht `source_seconds`.
- Mit dem API-Ergebnis werden bestätigte lokale Einträge als Multimenge anhand
  Ticketnummer + Zeitwert abgeglichen. Jeder passende entfernte Eintrag
  deckt höchstens **einen** lokalen Eintrag ab. Zusätzliche lokale bestätigte
  Buchungen werden im Report als `Nur Plenaro` separat ergänzt.
  Damit werden gebuchte Plenaro-Zeiten, die bereits in OTRS stehen, nicht
  nochmals addiert. Ohne stabile Remote-Entry-ID lässt sich die genaue
  Herkunft zweier zufällig gleicher Beträge nicht beweisen; die Summe der
  vollständigen Remote-Liste bleibt jedoch maßgeblich.
- Der bisherige lokale Aufgabenreport bleibt getrennt verfügbar:
  Aufgaben-Timer dürfen nicht pauschal zu den OTRS-Buchungen addiert werden,
  da sie dieselbe geleistete Arbeit abbilden können.
- JSON und CSV zeigen `accountingStatus` und eindeutigen Warnhinweis, wenn
  die OTRS-Schnittstelle fehlt oder Fehler liefert. In diesem Fall sind nur
  lokal bestätigte Buchungen verfügbar, **kein vollständiger OTRS-Report**.
- Kein Schreibzugriff auf OTRS, keine neue Datenbankmigration, keine
  automatischen Zeitbuchungen und keine sensiblen Zeitdaten in Logs.

## Unbedingt in Znuny konfigurieren

Die offizielle Znuny-Dokumentation:
https://doc.znuny.org/znuny/admin/webservices/provider/index.html#time-accounting-operation

1. In **Znuny → Admin → Web Services** den mit Znuny ausgelieferten
   TimeAccounting-Webservice als REST-Provider konfigurieren. Standardname:
   `TimeAccountingREST`; Operation: `TimeAccountingGet`,
   REST-Route: `POST /TimeAccountingGet`.
2. Unter **Admin → Gruppen** für die berechtigten Benutzer/Agenten
   `timeaccounting_webservice` **rw** freigeben. Dies ist eine Vorgabe
   dieser Znuny-Operation. Keine generellen Admin- oder DB-Rechte erteilen.
3. In **Plenaro → Einstellungen → Ticketsystem** die bisherige
   GenericTicketConnectorREST-HTTPS-Adresse beibehalten; das neue Feld
   `TimeAccounting-Webservice (Name)` lautet `TimeAccountingREST`
   (oder der tatsächliche im Znuny-Webservice-Editor konfigurierte Name).
   Beide API-Aufrufe verwenden die schon freigegebene HTTPS-Origin.
4. Jeder Benutzer trägt den eigenen Znuny-Agent-Login und das eigene Passwort
   unter **Meine Znuny-Verbindung** ein und muss als Agent mit einer von
   Plenaro-Admins genehmigten Agenten-ID verknüpft sein.
5. Unter **Zeitreport** einen Monat auswählen. Nur wenn
   `OTRS-Abgleich vollständig` angezeigt wird, wurden die Remote-Buchungen
   tatsächlich mit dem lokalen Journal zusammengeführt.
6. Bei Fehlern prüfen: URL/REST-Route, Webservice-Operation, Gruppe
   `timeaccounting_webservice`, persönliche Anmeldung und Znuny-Fehlerlogs.
   Die Webservice-Zeitzone `OTRSTimeZone` sollte mit `Europe/Berlin`
   übereinstimmen (ansonsten können Buchungen an Monatsgrenzen abweichen).

## Live-Abnahme / Grenzen
- [ ] Ein Test-Agent bucht 30 Minuten **direkt in OTRS**; Report +30 Minuten.
- [ ] Derselbe Agent überträgt 15 Minuten aus Plenaro nach OTRS;
      im Report werden **15, nicht 30 Minuten** ergänzt.
- [ ] Zwei identische 15-Minuten-Buchungen an einem Ticket bleiben zwei
      eigenständige Zeitwerte.
- [ ] Negativer OTRS-Nachtrag wird korrekt abgezogen.
- [ ] Monatswechsel / Zeitzone / zweiter Agent / geschlossene Tickets testen.
- [ ] Fehlender Webservice führt zu deutlicher Warnung, nicht zu 0 als
      vermeintlich vollständigem Ergebnis.
- [ ] Die WPF-Option **monatliche pauschale Nachbuchungen**, die nicht
      auf einzelnen Aufgaben liegen, muss separat in der Web-Version
      nachgezogen werden; lokale Aufgaben-Korrekturen sind vorhanden.
- [ ] Erweiterte lokale Znuny-Timer-Summen vor Übertragung sind noch nicht
      Bestandteil der gebuchten OTRS-Gesamtsumme (bewusst keine Doppelzählung).
