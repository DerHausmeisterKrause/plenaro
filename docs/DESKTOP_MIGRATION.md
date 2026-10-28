# Funktionsinventar aus project_tool (Desktop / WPF)

Quelle: [DerHausmeisterKrause/project_tool](https://github.com/DerHausmeisterKrause/project_tool) (WPF/.NET 8, SQLite, Outlook COM). Dieses Dokument ist eine **erste Bestandsaufnahme anhand des Repositorys**, noch kein vollständiges fachliches Pflichtenheft.

| Bestehender Bereich | Quellcode-Beispiele | Browser-Neuaufbau |
|---|---|---|
| Aufgaben | `TaskService`, `TaskItem`, `TodayView` | PostgreSQL und Aufgaben-API + Heute-Seite |
| Wochenplanung / Segmente | `WeekView`, `TaskSegment`, Availability-Bar | Browser-Kalender und Segmentplanung |
| Zeitbuchung / Arbeitstag | `TimeLog`, `WorkDayService` | persistente Zeitbuchungen je Benutzer |
| Reports | `ReportsView`, `ReportModels` | Monatsberichte und Exporte |
| Homeoffice | `HomeOfficeService` | Webworkflow / konfigurierbarer Versand |
| Tickets | `TicketSystemService`, `TicketSystemView` | Znuny/OTRS-Adapter als Standard |
| Ticketsuche / Kandidaten | `ZnunyCandidateTicket`, Sync Policies | Benutzerspezifische Suchregeln, Ausschlusswörter |
| Ticketaktionen | `TicketArticleItem`, Reply-Templates | Zuweisen, Antworten, Zeiten buchen, schließen |
| Outlook / Kalender | `OutlookInteropService`, `OutlookCalendarService` | Microsoft Graph (kein COM) |
| Teilen / Import | `PlenaroShare*` | Benutzer- und Rechtekonzept, Migration/Import |
| Wiki und Wissenssuche | `WikiSearchService`, `WikiProviders` | Web-Wiki + konfigurierbare Quellen |
| KI | `AiService`, `AiKnowledge*`, Chat | serverseitige Provider, optionale lokale Modelle |
| Favoriten / Weblinks | `WebShortcut*` | Webfavoriten |
| Einstellungen | `SettingsService`, `AppSettings` | Nutzer-/Admin-Konfiguration |
| Benachrichtigungen | `NotificationService`, `ReminderWindow` | Web-Notifications, später optionale Push |
| Updateverfahren | `UpdateService`, Releaseworkflow | Versionierte Docker-Images/Migrations |
| Desktop-Fenster | `DynamicIslandWindow`, XAML | Mobile/responsive Browser-Panels statt OS-Fenster |
| Lokale Datenbank | `DatabaseService` (SQLite-Schema v32) | PostgreSQL-Schema + nachvollziehbarer Migrationsimport |

**Keine vollständige Funktionsparität behaupten, bevor jede Funktion getestet und migriert wurde.** Fachliche Akzeptanztests werden mit Desktop 2.1.x verglichen. Insbesondere Benutzer-Isolation, Ticketsystem-API-Zugang und Outlook-Berechtigungen sind architektonisch neu.
