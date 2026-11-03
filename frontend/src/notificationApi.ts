export type ReminderKind = 'lead' | 'start';

export type TaskReminder = {
  segmentId: string;
  taskId: string;
  taskTitle: string;
  startsAt: string;
  dueAt: string;
  kind: ReminderKind;
};

export type ReminderPreferences = {
  enabled: boolean;
  leadMinutes: number;
};

async function csrf(): Promise<string> {
  const response = await fetch('/api/v1/auth/csrf', { credentials: 'same-origin', cache: 'no-store' });
  if (!response.ok) throw new Error('Sicherheitstoken konnte nicht geladen werden.');
  return (await response.json() as { token: string }).token;
}

export async function listReminders(): Promise<TaskReminder[]> {
  const response = await fetch('/api/v1/notifications/', {
    credentials: 'same-origin', cache: 'no-store',
  });
  if (!response.ok) throw new Error('Benachrichtigungen konnten nicht geladen werden.');
  return await response.json() as TaskReminder[];
}

export async function readReminderPreferences(): Promise<ReminderPreferences> {
  const response = await fetch('/api/v1/notifications/settings', {
    credentials: 'same-origin', cache: 'no-store',
  });
  if (!response.ok) throw new Error('Reminder-Einstellungen konnten nicht geladen werden.');
  return await response.json() as ReminderPreferences;
}

export async function saveReminderPreferences(settings: ReminderPreferences): Promise<void> {
  const token = await csrf();
  const response = await fetch('/api/v1/notifications/settings', {
    method: 'PUT', credentials: 'same-origin',
    headers: { 'X-CSRF-TOKEN': token, 'Content-Type': 'application/json' },
    body: JSON.stringify(settings),
  });
  if (!response.ok) {
    const error = await response.json().catch(() => ({})) as { error?: string };
    throw new Error(error.error || 'Reminder-Einstellungen konnten nicht gespeichert werden.');
  }
}

export async function markReminderRead(item: TaskReminder): Promise<void> {
  const token = await csrf();
  const response = await fetch('/api/v1/notifications/read', {
    method: 'POST', credentials: 'same-origin',
    headers: { 'X-CSRF-TOKEN': token, 'Content-Type': 'application/json' },
    body: JSON.stringify({ segmentId: item.segmentId, startsAt: item.startsAt, kind: item.kind }),
  });
  if (!response.ok) throw new Error('Benachrichtigung konnte nicht quittiert werden.');
}

export function reminderKey(userId: string, item: TaskReminder): string {
  return [userId, item.segmentId, item.startsAt, item.kind].join(':');
}

export function shouldShowBrowserAlert(item: TaskReminder, now = Date.now()): boolean {
  const due = Date.parse(item.dueAt);
  // Never show stale "starts now" browser notifications after returning to a page.
  return Number.isFinite(due) && due <= now && now - due <= 120000;
}
