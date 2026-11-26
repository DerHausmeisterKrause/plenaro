## 26.11.2026 – Phase 7/7: Listen-Scrollposition, Inline-Tagesmarker und weiße Jetzt-Linie

- [x] Desktop-Referenz `DerHausmeisterKrause/project_tool/Views/WeekView.xaml` geprüft: `NowIndicatorCanvas` zeigt eine 2-px-weiße Linie über dem Zeitraster und einen weißen Marker auf dem aktuellen Tag; `DayMarkerSegment` bietet direkte Buttons für Normal/UL/AM sowie HO/BR. `TodayView.xaml` verwendet ScrollViewer für die Liste.
- [x] Znuny-Ticketlisten in `ClassicTodayPage` bleiben während regelmäßiger oder manueller Aktualisierungen im DOM; statt Ausblenden der bestehenden Liste zeigt Plenaro eine kleine „Aktualisiere …“-Meldung. Gespeicherter `scrollTop` wird nach neuen Daten wiederhergestellt, bei bewusstem Tabwechsel zurückgesetzt.
- [x] Lokale Aufgaben in `ClassicTasksPage` behalten ihren Scrolloffset auch nach Aktualisierung, Speichern und Statuswechsel. Der Wochenkalender bewahrt `scrollTop` und `scrollLeft` bei Daten-Neuladen.
- [x] In der Wochenplanung den separaten, großflächigen aufklappbaren Tagesmarkereditor samt zusätzlichem Aktualisieren-Button entfernt. Rechts im Tagesdetail stehen direkte, statusanzeigende Buttons für Normal/UL/AM und HO/BR; Änderungen werden für den ausgewählten Tag sofort gespeichert und ohne Reload im Kalenderkopf angezeigt.
- [x] Bestehende HO-/BR-Sicherheitsabläufe beibehalten: neue HO-/BR-Anträge erfordern explizite Bestätigung; vor Speicherung erfolgt Preflight für Outlook-/Mail-Voraussetzungen; neue HO-Anträge prüfen/erstellen den ganztägigen Outlook-Termin und lösen die Mail aus; BR ohne HO versendet nur E-Mail. Bestehende HO-Termine lassen sich weiterhin nachtragen/prüfen. Keine doppelten Sendungen bei vorhandenen akzeptierten Mailstatus.
- [x] WPF-ähnliche weiße 2-px-Jetzt-Linie mit Tag-Punkt durch das 06–18-Uhr-Zeitraster integriert; nur in der angezeigten aktuellen Arbeitswoche sichtbar, Position nach lokaler Browserzeit, Aktualisierung ohne Kalender-Reload alle 30 Sekunden, ohne Einfluss auf Drag-and-drop.
- [x] Frontend-Regressionstests für Inline-Marker, HO-Bestätigung und API-Aufrufreihenfolge, Scrollpositionen von Ticketliste/lokalen Aufgaben/Wochenkalender sowie reine Jetzt-Linien-Zeitberechnung.
- [ ] Vollständige GitHub-CI prüfen, PR mergen und Dokumentationsstatus abhaken.
- [ ] Live-Sichtprüfung: Scrollposition bei tatsächlichem automatischen Sync, neue Marker im rechten Tagespanel und weiße Jetzt-Linie auf der produktiven Wochenansicht.
- [x] Keine Datenbankmigration, keine neuen Container, Ports, DNS-Namen oder Browserdienste.

### Deployment nach grüner CI/Merge

```bash
cd /opt/plenaro
git switch main
git pull --ff-only origin main
docker compose up -d --build --no-deps web
docker compose restart nginx
docker compose ps
```

---

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

Die Tagesmarker werden nach Auswahl eines Kalendertags **direkt rechts im Tagesdetail** per Normal/UL/AM/HO/BR-Schaltflächen gesetzt und sofort gespeichert. Neue HO-/BR-Anträge benötigen weiterhin eine Bestätigung sowie die bestehenden Outlook-/Mail-Sicherheitsprüfungen. Individuelle Kontingente stehen unter **Einstellungen → Aufgaben & Kalender**. Die aktuelle Uhrzeit ist im 06–18-Uhr-Kalender als weiße Linie mit Tagespunkt sichtbar – wie im alten WPF-Plenaro.

## Ticketliste und Details

Beim klassischen TaskTool ist `TicketSystemView.xaml` derzeit ein eingebetteter WebView2-Browser. Der Nutzer wünscht für die neue Weboberfläche **ausdrücklich** eine native Liste/Details-Zweiteilung; dieses Bedienmuster hat deshalb Vorrang. Der Ticketcache, WPF-ähnliche Details und erste Live-Ticketaktionen sind implementiert. Ausstehend sind vollständige Znuny-Live-Abnahme, Remote-Zeitbuchungen und weitere Adapter. Neue Kandidaten-Tickets sind nur mit administrativ zugewiesener Agenten- und Kandidaten-Benutzer-ID sichtbar.

## Anzeige und Erhalt bestehender Funktionen

- Die neue UI nutzt ausschließlich vorhandene CSS-Variablen für Farben, Hintergründe, Kontraste und Rahmen: bestehender Hell-/Dunkelmodus funktioniert weiterhin.
- Bei schmalen Viewports wird die Liste über dem Detailbereich angeordnet; der Wochenkalender scrollt horizontal, statt Stunden/Spalten wegzulassen.
- Nur tatsächlich vorhandene APIs werden aufgerufen. Kein automatisches Anlegen von Demodaten, keine Änderung an Login, Rollen, Secret-Vault oder NGINX.
- Das aufgabenbezogene Planen von Zeitsegmenten und der SQLite-Import bleiben separate offene Aufgaben.
