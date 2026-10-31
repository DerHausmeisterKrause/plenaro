import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AppContent } from './App';
import { localDate } from './tasksApi';
import type { TaskSegment } from './taskWorkApi';
import type { TaskReminder } from './notificationApi';

let calendarSegments: TaskSegment[] = [];
let reminderItems: TaskReminder[] = [];


beforeEach(() => {
  calendarSegments = [];
  reminderItems = [];
  vi.stubGlobal('fetch', vi.fn(async (path: string) => {
    if (path === '/api/v1/classic-import/batches') return {
      ok: true, status: 200, json: async () => [],
    };
    if (path === '/api/v1/notifications/') return {
      ok: true, status: 200, json: async () => reminderItems,
    };
    if (path === '/api/v1/notifications/settings') return {
      ok: true, status: 200, json: async () => ({ enabled: true, leadMinutes: 2 }),
    };
    if (path === '/api/v1/znuny/server') return {
      ok: true, status: 200, json: async () => ({
        webUrl: 'https://SERVER/index.pl',
        apiUrl: 'https://SERVER/nph-genericinterface.pl/Webservice/GenericTicketConnectorREST',
        ticketSearchRoute: '/Ticket', ticketSearchMethod: 'GET',
        ticketSearchAuthMode: 'Session', ticketGetRouteTemplate: '/Ticket/{TicketID}',
        ticketGetMethod: 'GET', ticketGetAuthMode: 'Session',
        ticketUpdateRoute: '/Ticket/{TicketID}/Update',
        agentListRoute: '/Ticket/Agent/List',
        ticketCreateRoute: '/TicketCreate', ticketCreateMethod: 'POST',
        ticketCreateQueue: '', ticketCreateState: 'open', ticketCreatePriority: '3 normal',
        ticketCreateType: '', ticketCreateCustomerUser: '', ticketReplyTemplate: '',
        dynamicFieldOptionsRoute: '/Ticket/DynamicField/{FieldName}/Options',
        costCenterFieldName: 'KostenstelleID', orderFieldName: 'AuftragsID',
        costCenterOptions: '', orderOptions: '',
        syncIntervalMinutes: 15, candidateSyncIntervalMinutes: 5,
        searchLimit: 100, articleLimit: 20,
        onlyOpenTickets: true, showClosedTickets: false,
      }),
    };
    if (path === '/api/v1/znuny/me') return {
      ok: true, status: 200, json: async () => ({
        username: '', agentId: 0, candidateUserId: 1,
        candidateKeywords: '', candidateExcludeKeywords: '',
        includeOwner: true, includeResponsible: true,
        notifyOnNewAssignedTickets: true, notifyOnTicketChanges: true,
        showAllCandidatePoolTickets: false,
        passwordConfigured: false, vaultReady: true,
      }),
    };
    if (path === '/api/v1/settings/me') return {
      ok: true, status: 200, json: async () => ({ theme: 'light', weekStart: 'inherit' }),
    };
    if (path === '/api/v1/settings/global') return {
      ok: true, status: 200, json: async () => ({ instanceName: 'Plenaro', supportEmail: '', defaultWeekStart: 'monday' }),
    };
    if (path === '/api/v1/admin/entra/') return {
      ok: true, status: 200, json: async () => ({
        enabled: false, tenantId: '', clientId: '', secretConfigured: false,
        vaultReady: false, httpsRequired: false,
      }),
    };
    if (path === '/api/v1/day-markers/budget') return { ok: true, status: 200, json: async () => ({ annualUlDays: null, monthlyBrDays: null }) };
    if (path.startsWith('/api/v1/day-markers/summary?')) return { ok: true, status: 200, json: async () => ({
      year: 2026, month: 10, annualUlDays: null, usedUlDays: 0,
      remainingUlDays: null, ulExhausted: false, ulExceeded: false,
      monthlyBrDays: null, usedBrDays: 0,
      remainingBrDays: null, brExhausted: false, brExceeded: false,
    }) };
    if (path.startsWith('/api/v1/day-markers/?')) return { ok: true, status: 200, json: async () => [] };
    if (path.startsWith('/api/v1/reports/month?')) return {
      ok: true, status: 200, json: async () => ({
        month: '2026-10', timeZone: 'Europe/Berlin', generatedAt: '2026-10-30T21:00:00Z',
        timerSeconds: 3600, adjustmentMinutes: 15, totalSeconds: 4500,
        sessionCount: 1, taskCount: 1,
        tasks: [{ taskId: '00000000-0000-0000-0000-000000000003', title: 'Zeittest',
          status: 'active', timerSeconds: 3600, adjustmentMinutes: 15,
          totalSeconds: 4500, sessionCount: 1 }],
      }),
    };
    if (path.startsWith('/api/v1/segments/?')) return {
      ok: true, status: 200, json: async () => calendarSegments,
    };
    if (path.endsWith('/schedule')) return { ok: true, status: 204 };
    if (path.startsWith('/api/v1/tasks/')) return {
      ok: true, status: 200, json: async () => ([]),
    };
    if (path === '/api/v1/graph/status') return {
      ok: true, status: 200, json: async () => ({
        enabled: false, linked: false, vaultReady: false, connected: false,
      }),
    };
    if (path === '/api/v1/auth/entra/status') return {
      ok: true, status: 200, json: async () => ({ enabled: false, linked: false }),
    };
    if (path === '/api/v1/auth/csrf') return {
      ok: true, status: 200, json: async () => ({ token: 'test-token' }),
    };
    if (path === '/api/v1/setup/status') return {
      ok: true, status: 200, json: async () => ({ required: false }),
    };
    if (path === '/api/v1/auth/me') return {
      ok: true, status: 200, json: async () => ({
        id: '00000000-0000-0000-0000-000000000001',
        email: 'test@example.invalid', name: 'Test', roles: ['Admin'],
      }),
    };
    return { ok: true, status: 200 };
  }));
  window.localStorage.clear();
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

function renderAt(path: string) {
  return render(<MemoryRouter initialEntries={[path]}><AppContent /></MemoryRouter>);
}

describe('Plenaro web shell', () => {
  it('zeigt die Übersicht und prüft beide Health-Endpunkte', async () => {
    renderAt('/');
    expect(await screen.findByRole('heading', { name: /Willkommen bei Plenaro/i })).toBeTruthy();
    await waitFor(() => expect(vi.mocked(fetch).mock.calls.length).toBeGreaterThanOrEqual(4));
    expect(vi.mocked(fetch)).toHaveBeenCalledWith('/health/live', expect.any(Object));
    expect(vi.mocked(fetch)).toHaveBeenCalledWith('/health/ready', expect.any(Object));
  });

  it('zeigt das klassische Ticketlayout ohne vorgetäuschte Tickets', async () => {
    renderAt('/tickets');
    expect(await screen.findByRole('heading', { name: 'Ticketsystem' })).toBeTruthy();
    expect(screen.getByRole('region', { name: 'Ticketliste' })).toBeTruthy();
    expect(screen.getByRole('region', { name: 'Ticketdetails' })).toBeTruthy();
    expect(screen.getByText('Noch keine Tickets geladen')).toBeTruthy();
  });

  it('bietet den Dunkelmodus mit umschaltbarem Zustand', async () => {
    renderAt('/settings');
    const themeButton = await screen.findByRole('button', { name: /Dunkelmodus aktivieren/i });
    fireEvent.click(themeButton);
    await waitFor(() => expect(document.documentElement.dataset.theme).toBe('dark'));
  });

  it('zeigt die Benachrichtigungsglocke und ein leeres persönliches Postfach', async () => {
    renderAt('/');
    const button = await screen.findByRole('button', { name: /Benachrichtigungen öffnen, 0 ungelesen/ });
    fireEvent.click(button);
    expect(await screen.findByRole('region', { name: 'Benachrichtigungen' })).toBeTruthy();
    expect(screen.getByText('Keine ungelesenen Erinnerungen.')).toBeTruthy();
  });

  it('öffnet die direkt verlinkte Aufgabe aus einer ungelesenen Erinnerung', async () => {
    reminderItems = [{
      segmentId: 'a0bb1e29-6574-4970-8a91-40dfd2a15ed7',
      taskId: '2f145e76-1853-4b88-a1be-6fbc0bdc42b7',
      taskTitle: 'Aufgabe aus Reminder',
      startsAt: '2026-10-30T10:30:00Z',
      dueAt: '2026-10-30T10:28:00Z', kind: 'lead',
    }];
    renderAt('/');
    const button = await screen.findByRole('button', { name: /Benachrichtigungen öffnen, 1 ungelesen/ });
    fireEvent.click(button);
    expect(screen.getByText('Aufgabe aus Reminder')).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Aufgabe öffnen' }).getAttribute('href'))
      .toBe('/tasks?task=2f145e76-1853-4b88-a1be-6fbc0bdc42b7');
  });

  it('bietet persönliche Reminder-Einstellungen mit Standardvorlauf', async () => {
    renderAt('/settings');
    fireEvent.click(await screen.findByRole('button', { name: 'Aufgaben & Kalender' }));
    expect(await screen.findByRole('heading', { name: 'Benachrichtigungen und Reminder' })).toBeTruthy();
    expect((screen.getByLabelText('Vorwarnung in Minuten') as HTMLInputElement).value).toBe('2');
  });

  it('bietet einen persönlichen Datenimport mit Vorschau und ohne direkte SQLite-Übertragung', async () => {
    renderAt('/settings');
    fireEvent.click(await screen.findByRole('button', { name: 'Datenimport' }));
    expect(await screen.findByRole('heading', { name: 'Desktop-Daten importieren' })).toBeTruthy();
    expect(screen.getAllByText(/tools\/export_classic.py/).length).toBeGreaterThan(0);
    expect(screen.getByText(/Noch kein Import ausgeführt/)).toBeTruthy();
  });

  it('zeigt die echten Classic-API-Standardwerte unter Ticketsystem', async () => {
    renderAt('/settings');
    fireEvent.click(await screen.findByRole('button', { name: 'Ticketsystem' }));
    expect(await screen.findByRole('heading', { name: 'GenericTicketConnectorREST' })).toBeTruthy();
    expect((screen.getByLabelText('API-Basisadresse') as HTMLInputElement).value)
      .toBe('https://SERVER/nph-genericinterface.pl/Webservice/GenericTicketConnectorREST');
    expect((screen.getByLabelText('Znuny-Benutzername') as HTMLInputElement).value).toBe('');
    expect((screen.getByLabelText('Eigene Agenten-ID') as HTMLInputElement).value).toBe('0');
    expect(screen.getByRole('button', { name: 'Verbindung prüfen' })).toHaveProperty('disabled', true);
    fireEvent.click(screen.getByText('Classic-Routen, Sync-Zeiten und weitere API-Standardwerte'));
    expect((screen.getByLabelText('TicketSearch-Route') as HTMLInputElement).value).toBe('/Ticket');
    expect((screen.getByLabelText('Maximaler TicketSearch-Umfang') as HTMLInputElement).value).toBe('100');
    expect((screen.getByLabelText('Kandidaten-Abfrage (Minuten)') as HTMLInputElement).value).toBe('5');
  });

  it('zeigt getrennte Formulare für persönliche und globale Einstellungen', async () => {
    renderAt('/settings');
    expect(await screen.findByRole('heading', { name: 'Persönliche Einstellungen' })).toBeTruthy();
    expect(await screen.findByRole('heading', { name: 'Instanzeinstellungen' })).toBeTruthy();
    expect(screen.getByLabelText('Darstellung')).toBeTruthy();
    expect(screen.getByLabelText('Instanzname')).toBeTruthy();
  });

  it('sortiert Kontingente, Microsoft und Verwaltung in passende Einstellungskategorien', async () => {
    renderAt('/settings');
    fireEvent.click(await screen.findByRole('button', { name: 'Aufgaben & Kalender' }));
    expect(await screen.findByRole('heading', { name: 'UL- und BR-Kontingente' })).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Microsoft 365' }));
    expect(screen.getByRole('heading', { name: 'Outlook-Kalender' })).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Administration' }));
    expect(await screen.findByRole('heading', { name: 'Benutzerverwaltung' })).toBeTruthy();
  });

  it('versteckt Microsoft SSO wenn Entra nicht eingerichtet ist', async () => {
    vi.stubGlobal('fetch', vi.fn(async (path: string) =>
      path === '/api/v1/auth/me' ? { ok: false, status: 401 } :
      path === '/api/v1/setup/status' ? { ok: true, status: 200, json: async () => ({ required: false }) } :
      path === '/api/v1/auth/entra/status' ? { ok: true, status: 200, json: async () => ({ enabled: false, linked: false }) } :
      { ok: true, status: 200 },
    ));
    renderAt('/login');
    expect(await screen.findByRole('heading', { name: 'Willkommen zurück.' })).toBeTruthy();
    expect(screen.queryByText('Mit Microsoft anmelden')).toBeNull();
  });

  it('zeigt den echten lokalen Wochenkalender ohne Microsoft Entra', async () => {
    renderAt('/week');
    expect(await screen.findByRole('heading', { name: 'Meine Woche' })).toBeTruthy();
    expect(screen.getByRole('region', { name: 'Wochenkalender' })).toBeTruthy();
    expect(screen.getByText('Ohne Microsoft nutzbar')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Vorherige Woche' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Nächste Woche' })).toBeTruthy();
    expect(screen.queryByText('Microsoft Graph ist noch nicht eingerichtet')).toBeNull();
  });

  it('verschiebt ein Segment mit Drag-and-drop und erhält Notizen durch den gezielten PATCH', async () => {
    const today = new Date();
    const monday = new Date(today.getFullYear(), today.getMonth(), today.getDate());
    monday.setDate(monday.getDate() - ((monday.getDay() + 6) % 7));
    const tuesday = new Date(monday);
    tuesday.setDate(tuesday.getDate() + 1);
    const start = new Date(monday);
    start.setHours(9, 0, 0, 0);
    const end = new Date(start.getTime() + 45 * 60 * 1000);
    calendarSegments = [{
      id: '1179cbb8-5b66-4c2c-940f-44ea05f4fa91',
      taskId: 'd10e1e7c-a55d-42a0-a0df-061fcf157810',
      taskTitle: 'Testaufgabe', startsAt: start.toISOString(), endsAt: end.toISOString(),
      note: 'Bestehende Notiz', participants: ['kunde@example.invalid'],
    }];
    renderAt('/week');
    const segment = await screen.findByRole('button', { name: /Zeitsegment Testaufgabe/ });
    const transfer = { setData: vi.fn(), effectAllowed: 'copy', dropEffect: 'none' };
    fireEvent.dragStart(segment, { dataTransfer: transfer });
    expect(transfer.setData).toHaveBeenCalledWith('text/plain', calendarSegments[0].id);
    const target = screen.getByTestId(`calendar-slot-${localDate(tuesday)}-10`);
    fireEvent.dragOver(target, { dataTransfer: transfer, clientY: 0 });
    fireEvent.drop(target, { dataTransfer: transfer, clientY: 0 });
    await waitFor(() => expect(vi.mocked(fetch)).toHaveBeenCalledWith(
      `/api/v1/tasks/${calendarSegments[0].taskId}/segments/${calendarSegments[0].id}/schedule`,
      expect.objectContaining({
        method: 'PATCH',
        body: expect.stringContaining('"expectedStartsAt"'),
      }),
    ));
    const patch = vi.mocked(fetch).mock.calls.find(([url]) => String(url).endsWith('/schedule'));
    const payload = JSON.parse((patch?.[1] as RequestInit).body as string) as {
      startsAt: string; endsAt: string; expectedStartsAt: string;
    };
    expect(new Date(payload.startsAt).getHours()).toBe(10);
    expect(new Date(payload.endsAt).getTime() - new Date(payload.startsAt).getTime()).toBe(45 * 60000);
    expect(payload.expectedStartsAt).toBe(start.toISOString());
    expect((patch?.[1] as RequestInit).headers).toMatchObject({ 'X-CSRF-TOKEN': 'test-token' });
  });

  it('erlaubt Segmentverschiebung per Tastatur bedienbarem Formular', async () => {
    const monday = new Date();
    monday.setDate(monday.getDate() - ((monday.getDay() + 6) % 7));
    monday.setHours(9, 0, 0, 0);
    calendarSegments = [{
      id: '1179cbb8-5b66-4c2c-940f-44ea05f4fa91',
      taskId: 'd10e1e7c-a55d-42a0-a0df-061fcf157810',
      taskTitle: 'Testaufgabe', startsAt: monday.toISOString(),
      endsAt: new Date(monday.getTime() + 30 * 60000).toISOString(),
      note: '', participants: [],
    }];
    renderAt('/week');
    fireEvent.click(await screen.findByRole('button', { name: /Zeitsegment Testaufgabe/ }));
    const form = screen.getByRole('form', { name: 'Zeitsegment verschieben' });
    fireEvent.change(within(form).getByLabelText('Neue Startzeit'), { target: { value: '10:30' } });
    fireEvent.click(within(form).getByRole('button', { name: 'Zeitsegment verschieben' }));
    await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([url, options]) =>
      String(url).endsWith('/schedule') && (options as RequestInit).method === 'PATCH',
    )).toBe(true));
  });

  it('zeigt Aufgaben links und Details rechts statt Einzelkarten', async () => {
    renderAt('/tasks');
    expect(await screen.findByRole('heading', { name: 'Aufgaben' })).toBeTruthy();
    expect(screen.getByRole('region', { name: 'Aufgabenliste' })).toBeTruthy();
    expect(screen.getByRole('region', { name: 'Aufgabendetails' })).toBeTruthy();
    expect(await screen.findByRole('heading', { name: 'Keine Aufgaben gefunden' })).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Neue Aufgabe' }));
    expect(await screen.findByLabelText('Titel')).toBeTruthy();
  });

  it('zeigt Heute mit Classic-Schnellerfassung, Reitern und festem Detailbereich', async () => {
    renderAt('/today');
    expect(await screen.findByRole('heading', { name: 'Arbeitstag heute' })).toBeTruthy();
    expect(screen.getByRole('region', { name: 'Arbeitsbereich Heute' })).toBeTruthy();
    expect(screen.getByRole('complementary', { name: 'Aufgabedetails und Planung' })).toBeTruthy();
    expect(screen.getByRole('tab', { name: 'Aktuelle Aufgaben' })).toBeTruthy();
    expect(screen.getByLabelText('Neue Aufgabe')).toBeTruthy();
    expect(screen.getByRole('region', { name: 'Aufgabenbezogener Timer' })).toBeTruthy();
    expect(screen.getByRole('region', { name: 'Aufgabensegmente' })).toBeTruthy();
  });

  it('zeigt Normal, UL, AM, HO und BR als Tagesmarker ohne Microsoft', async () => {
    renderAt('/today');
    expect(await screen.findByRole('heading', { name: 'Arbeitstag heute' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'UL' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'AM' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'HO' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'BR' })).toBeTruthy();
  });

  it('zeigt echte Monatsaufgabenzeiten und bietet CSV-Export an', async () => {
    renderAt('/reports');
    expect(await screen.findByRole('heading', { name: 'Monatsberichte' })).toBeTruthy();
    expect(await screen.findByText('Zeittest')).toBeTruthy();
    expect(screen.getByRole('button', { name: /CSV exportieren/ })).toBeTruthy();
    expect(screen.getByLabelText('Monat auswählen')).toBeTruthy();
  });

  it('zeigt unbekannte URLs als Fehlerseite', async () => {
    renderAt('/nicht-vorhanden');
    expect(await screen.findByRole('heading', { name: 'Seite nicht gefunden.' })).toBeTruthy();
  });

  it('zeigt am Login nur das Loginformular und keine Navigation', async () => {
    vi.stubGlobal('fetch', vi.fn(async (path: string) =>
      path === '/api/v1/auth/me' ? { ok: false, status: 401 } :
      path === '/api/v1/setup/status' ? { ok: true, status: 200, json: async () => ({ required: false }) } :
      { ok: true, status: 200 },
    ));
    renderAt('/login');
    expect(await screen.findByRole('heading', { name: 'Willkommen zurück.' })).toBeTruthy();
    expect(screen.getByLabelText('E-Mail-Adresse')).toBeTruthy();
    expect(screen.queryByRole('navigation')).toBeNull();
    expect(screen.queryByText('Deine Arbeitsbereiche')).toBeNull();
  });

  it('öffnet vor dem ersten Benutzer automatisch den Einrichtungsassistenten', async () => {
    vi.stubGlobal('fetch', vi.fn(async (path: string) =>
      path === '/api/v1/setup/status' ? { ok: true, status: 200, json: async () => ({ required: true }) } :
      path === '/api/v1/auth/me' ? { ok: false, status: 401 } :
      { ok: true, status: 200 },
    ));
    renderAt('/login');
    expect(await screen.findByRole('heading', { name: 'Willkommen bei Plenaro.' })).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: /Einrichtung starten/i }));
    expect(screen.getByRole('heading', { name: 'Administrator erstellen' })).toBeTruthy();
    expect(screen.queryByRole('navigation')).toBeNull();
  });

  it('sperrt private Seiten ohne gültige Sitzung', async () => {
    vi.stubGlobal('fetch', vi.fn(async (path: string) =>
      path === '/api/v1/auth/me' ? { ok: false, status: 401 } :
      path === '/api/v1/setup/status' ? { ok: true, status: 200, json: async () => ({ required: false }) } :
      { ok: true, status: 200 },
    ));
    renderAt('/tasks');
    expect(await screen.findByRole('heading', { name: 'Willkommen zurück.' })).toBeTruthy();
  });
});
