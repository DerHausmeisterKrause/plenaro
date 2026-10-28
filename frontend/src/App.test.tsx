import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AppContent } from './App';

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn(async () => ({ ok: true })));
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
    expect(screen.getByRole('heading', { name: /Willkommen bei Plenaro/i })).toBeTruthy();
    await waitFor(() => expect(vi.mocked(fetch)).toHaveBeenCalledTimes(2));
    expect(vi.mocked(fetch)).toHaveBeenCalledWith('/health/live', expect.any(Object));
    expect(vi.mocked(fetch)).toHaveBeenCalledWith('/health/ready', expect.any(Object));
  });

  it('navigiert in einen Modul-Platzhalter und meldet keine erfundenen Daten', () => {
    renderAt('/tickets');
    expect(screen.getByRole('heading', { name: /Dieser Bereich wird aufgebaut/i })).toBeTruthy();
    expect(screen.getByText(/Geplant für Phase 6/)).toBeTruthy();
  });

  it('bietet den Dunkelmodus mit umschaltbarem Zustand', () => {
    renderAt('/settings');
    const themeButton = screen.getByRole('button', { name: /Design wechseln/i });
    fireEvent.click(themeButton);
    expect(document.documentElement.dataset.theme).toBe('dark');
  });

  it('zeigt unbekannte URLs als Fehlerseite', () => {
    renderAt('/nicht-vorhanden');
    expect(screen.getByRole('heading', { name: 'Seite nicht gefunden.' })).toBeTruthy();
  });

  it('verspricht keine bereits verfügbare Authentifizierung', () => {
    renderAt('/login');
    expect(screen.getByRole('heading', { name: 'Anmeldung folgt.' })).toBeTruthy();
  });
});
