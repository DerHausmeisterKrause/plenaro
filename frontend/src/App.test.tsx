import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AppContent } from './App';

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn(async (path: string) => {
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
    if (path.startsWith('/api/v1/segments/?')) return { ok: true, status: 200, json: async () => [] };
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
