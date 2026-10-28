import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AppContent } from './App';

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn(async (path: string) => {
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
    await waitFor(() => expect(vi.mocked(fetch)).toHaveBeenCalledTimes(3));
    expect(vi.mocked(fetch)).toHaveBeenCalledWith('/health/live', expect.any(Object));
    expect(vi.mocked(fetch)).toHaveBeenCalledWith('/health/ready', expect.any(Object));
  });

  it('navigiert in einen Modul-Platzhalter und meldet keine erfundenen Daten', async () => {
    renderAt('/tickets');
    expect(await screen.findByRole('heading', { name: /Dieser Bereich wird aufgebaut/i })).toBeTruthy();
    expect(screen.getByText(/Geplant für Phase 6/)).toBeTruthy();
  });

  it('bietet den Dunkelmodus mit umschaltbarem Zustand', async () => {
    renderAt('/settings');
    const themeButton = await screen.findByRole('button', { name: /Design wechseln/i });
    fireEvent.click(themeButton);
    expect(document.documentElement.dataset.theme).toBe('dark');
  });

  it('zeigt unbekannte URLs als Fehlerseite', () => {
    renderAt('/nicht-vorhanden');
    expect(screen.getByRole('heading', { name: 'Seite nicht gefunden.' })).toBeTruthy();
  });

  it('zeigt die Login-Maske', async () => {
    renderAt('/login');
    expect(screen.getByRole('heading', { name: 'Willkommen zurück.' })).toBeTruthy();
    expect(await screen.findByLabelText('E-Mail-Adresse')).toBeTruthy();
  });

  it('sperrt private Seiten ohne gültige Sitzung', async () => {
    vi.stubGlobal('fetch', vi.fn(async (path: string) =>
      path === '/api/v1/auth/me' ? { ok: false, status: 401 } : { ok: true, status: 200 },
    ));
    renderAt('/tasks');
    expect(await screen.findByRole('heading', { name: 'Willkommen zurück.' })).toBeTruthy();
  });
});
