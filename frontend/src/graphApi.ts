export type GraphStatus = {
  enabled: boolean;
  linked: boolean;
  vaultReady: boolean;
  connected: boolean;
};

export type OutlookEvent = {
  id: string;
  subject: string;
  start: string;
  end: string;
  isAllDay: boolean;
  showAs: string;
  location: string;
  webLink: string | null;
};

export type OutlookWeek = { events: OutlookEvent[]; hasMore: boolean };

export async function graphStatus(): Promise<GraphStatus> {
  const response = await fetch('/api/v1/graph/status', { credentials: 'same-origin', cache: 'no-store' });
  if (!response.ok) throw new Error('Outlook-Verbindungsstatus konnte nicht geladen werden.');
  return await response.json() as GraphStatus;
}

export async function readOutlookWeek(start: Date, end: Date): Promise<OutlookWeek> {
  const query = new URLSearchParams({ start: start.toISOString(), end: end.toISOString() });
  const response = await fetch('/api/v1/graph/calendar?' + query.toString(), {
    credentials: 'same-origin', cache: 'no-store',
  });
  if (response.status === 409) throw new Error('Deine Outlook-Berechtigung ist abgelaufen. Bitte erneut verbinden.');
  if (!response.ok) throw new Error('Outlook-Termine konnten nicht geladen werden.');
  return await response.json() as OutlookWeek;
}

export async function disconnectOutlook(): Promise<void> {
  const tokenResponse = await fetch('/api/v1/auth/csrf', { credentials: 'same-origin', cache: 'no-store' });
  if (!tokenResponse.ok) throw new Error('Sicherheitstoken konnte nicht geladen werden.');
  const { token } = await tokenResponse.json() as { token: string };
  const response = await fetch('/api/v1/graph/disconnect', {
    method: 'POST', credentials: 'same-origin',
    headers: { 'X-CSRF-TOKEN': token },
  });
  if (!response.ok) throw new Error('Outlook-Verbindung konnte nicht getrennt werden.');
}
