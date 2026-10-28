# Web-Oberfläche nach Plenaro Classic – Referenzkonzept

Quellen: [Classic / project_tool](https://github.com/DerHausmeisterKrause/project_tool), vor allem `Views/TodayView.xaml`, `Views/WeekView.xaml`, `Views/TaskDetailPanel.xaml` und `Views/SettingsView.xaml`. Zusätzlich der explizite Nutzerwunsch: Ticketliste links, ausgewählter Datensatz rechts; neues React-Theme mit Hell-/Dunkelmodus erhalten.

## Navigation und Zuständigkeiten

| Bereich | Zugeordnete Funktionen | Zustand |
| --- | --- | --- |
| Heute | Tagesaufgaben und UL/AM/BR; später Tagesagenda und Schnellaktionen | Bestehender Arbeitsbereich |
| Wochenplanung | **Lokaler** 7-Tage-Kalender mit Stundenraster 06–18 Uhr, geplante Aufgaben, lokale UL/AM/BR-Marker, Details pro Tag | Implementiert |
| Aufgaben | Kompakte Liste links, ausgewählte Aufgabe rechts, Schnellhinzufügen, Filter, Bearbeiten und Statusaktionen | Implementiert |
| Ticketsystem | Liste links, Ticketdetails rechts ohne Seitenwechsel; echter Znuny/OTRS-Datenadapter | **Nur Layout vorbereitet** |
| Berichte | Monatsreports und Export | Noch offen |
| Wissensdatenbank und KI | Wissen, Dokumente, Assistent | Noch offen |
| Einstellungen | Horizontale Kategorien Allgemein / Aufgaben & Kalender / Microsoft 365 / Ticketsystem / Konto & Sicherheit / Administration | Implementiert |

## Kalender ohne Microsoft

Der **lokale Wochenkalender ist immer verfügbar**. Er lädt die eigenen geplanten Tasks mit `plannedFor` und UL-/AM-/BR-Markierungen direkt aus PostgreSQL. 06–18 Uhr ist ein sichtbares Stundenraster als Grundlage für spätere Zeitsegmente. Aufgaben ohne Uhrzeit stehen in der Tageskopfzeile / den Tagesdetails, nicht fiktiv um 08:00. Alle Termine / Uhrzeiten werden in der Browser-Zeitzone dargestellt.

Ist ein Outlook-Konto verbunden, kommen Microsoft-Graph-Termine als **zusätzliche** Quelle dazu. Ohne Entra, bei ausgelaufener Zustimmung oder bei Graph-Ausfall bleibt das lokale Raster mitsamt lokaler Aufgaben und Marker benutzbar. Bestehende Graph-Leseberechtigung bleibt unverändert. Die Kalenderintegration ist noch nicht gegen einen echten Microsoft-Tenant abgenommen.

Die Tagesmarker können im Kalender über `UL / AM / BR bearbeiten` eingeblendet und danach aktualisiert werden. Individuelle Kontingente stehen unter **Einstellungen → Aufgaben & Kalender**.

## Ticketliste und Details

Beim klassischen TaskTool ist `TicketSystemView.xaml` derzeit ein eingebetteter WebView2-Browser. Der Nutzer wünscht für die neue Weboberfläche **ausdrücklich** eine native Liste/Details-Zweiteilung; dieses Bedienmuster hat deshalb Vorrang. Die Ticketliste ist bis zur Phase-6-API-Anbindung bewusst leer und stellt keine Demo-Tickets dar. Dann sollen Such-/Filter-/Ticketnummern- und Statusfunktionen, Konversation, Zuordnung und Aktionen rechts ergänzt werden, ohne die Grundstruktur zu ändern.

## Anzeige und Erhalt bestehender Funktionen

- Die neue UI nutzt ausschließlich vorhandene CSS-Variablen für Farben, Hintergründe, Kontraste und Rahmen: bestehender Hell-/Dunkelmodus funktioniert weiterhin.
- Bei schmalen Viewports wird die Liste über dem Detailbereich angeordnet; der Wochenkalender scrollt horizontal, statt Stunden/Spalten wegzulassen.
- Nur tatsächlich vorhandene APIs werden aufgerufen. Kein automatisches Anlegen von Demodaten, keine Änderung an Login, Rollen, Secret-Vault oder NGINX.
- Das aufgabenbezogene Planen von Zeitsegmenten und der SQLite-Import bleiben separate offene Aufgaben.
