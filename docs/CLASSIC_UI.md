# Web-Oberfläche nach Plenaro Classic – Referenzkonzept

Quellen: [Classic / project_tool](https://github.com/DerHausmeisterKrause/project_tool), vor allem `Views/TodayView.xaml`, `Views/WeekView.xaml`, `Views/TaskDetailPanel.xaml` und `Views/SettingsView.xaml`. Zusätzlich der explizite Nutzerwunsch: Ticketliste links, ausgewählter Datensatz rechts; neues React-Theme mit Hell-/Dunkelmodus erhalten.

## Navigation und Zuständigkeiten

| Bereich | Zugeordnete Funktionen | Zustand |
| --- | --- | --- |
| **Arbeitsplatz** (`/workspace`) | Heute-Agenda, Aktuelle/Neue/Erledigte Znuny-Tickets, lokale Aufgaben in einem Unterreiter, WPF-Ticketdetails rechts | Implementiert, Live-Abnahme offen |
| Wochenplanung | Lokaler Mo–Fr-Kalender, Segmente, UL/AM/BR/HO, Outlook optional | Implementiert, Graph-Live-Abnahme offen |
| Berichte | Monatsreports und CSV-Export | Implementiert |
| Wissensdatenbank und KI | Wissen, Dokumente, Assistent | Offen |
| Einstellungen | Allgemein / Aufgaben & Kalender / Microsoft 365 / Ticketsystem / Konto & Sicherheit / Administration | Implementiert |

Die separaten Navigationseinträge **Aufgaben** und **Ticketsystem** entfallen.
Bestehende Links `/today`, `/tasks` und `/tickets` werden nach `/workspace`
umgeleitet. Ticketdaten und lokale Aufgaben werden weiterhin separat
persistiert und durch die bestehende Benutzerisolation geschützt.

## Kalender ohne Microsoft

Der **lokale Wochenkalender ist immer verfügbar**. Er lädt die eigenen geplanten Tasks mit `plannedFor` und UL-/AM-/BR-Markierungen direkt aus PostgreSQL. 06–18 Uhr ist ein sichtbares Stundenraster als Grundlage für spätere Zeitsegmente. Aufgaben ohne Uhrzeit stehen in der Tageskopfzeile / den Tagesdetails, nicht fiktiv um 08:00. Alle Termine / Uhrzeiten werden in der Browser-Zeitzone dargestellt.

Ist ein Outlook-Konto verbunden, kommen Microsoft-Graph-Termine als **zusätzliche** Quelle dazu. Ohne Entra, bei ausgelaufener Zustimmung oder bei Graph-Ausfall bleibt das lokale Raster mitsamt lokaler Aufgaben und Marker benutzbar. Bestehende Graph-Leseberechtigung bleibt unverändert. Die Kalenderintegration ist noch nicht gegen einen echten Microsoft-Tenant abgenommen.

Die Tagesmarker können im Kalender über `UL / AM / BR bearbeiten` eingeblendet und danach aktualisiert werden. Individuelle Kontingente stehen unter **Einstellungen → Aufgaben & Kalender**.

## Ticketliste und Details

Beim klassischen TaskTool ist `TicketSystemView.xaml` derzeit ein eingebetteter WebView2-Browser. Der Nutzer wünscht für die neue Weboberfläche **ausdrücklich** eine native Liste/Details-Zweiteilung; dieses Bedienmuster hat deshalb Vorrang. Der Ticketcache, WPF-ähnliche Details und erste Live-Ticketaktionen sind implementiert. Ausstehend sind vollständige Znuny-Live-Abnahme, Remote-Zeitbuchungen und weitere Adapter. Neue Kandidaten-Tickets sind nur mit administrativ zugewiesener Agenten- und Kandidaten-Benutzer-ID sichtbar.

## Anzeige und Erhalt bestehender Funktionen

- Die neue UI nutzt ausschließlich vorhandene CSS-Variablen für Farben, Hintergründe, Kontraste und Rahmen: bestehender Hell-/Dunkelmodus funktioniert weiterhin.
- Bei schmalen Viewports wird die Liste über dem Detailbereich angeordnet; der Wochenkalender scrollt horizontal, statt Stunden/Spalten wegzulassen.
- Nur tatsächlich vorhandene APIs werden aufgerufen. Kein automatisches Anlegen von Demodaten, keine Änderung an Login, Rollen, Secret-Vault oder NGINX.
- Das aufgabenbezogene Planen von Zeitsegmenten und der SQLite-Import bleiben separate offene Aufgaben.
