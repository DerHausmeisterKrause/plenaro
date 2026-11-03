export type TaskTimeSummary = {
  bookedSeconds: number;
  runningSeconds: number;
  runningSince: string | null;
  adjustedMinutes: number;
  sessionCount: number;
};

export type TaskSegment = {
  id: string;
  taskId: string;
  taskTitle: string;
  startsAt: string;
  endsAt: string;
  note: string;
  participants: string[];
};

export type SegmentInput = {
  startsAt: string;
  endsAt: string;
  note: string;
  participants: string[];
};

export type SegmentConflict = {
  id: string;
  taskId: string;
  taskTitle: string;
  startsAt: string;
  endsAt: string;
};
export type SegmentAvailability = {
  available: boolean;
  conflicts: SegmentConflict[];
};


async function csrfToken(): Promise<string> {
  const response = await fetch('/api/v1/auth/csrf', { credentials: 'same-origin', cache: 'no-store' });
  if (!response.ok) throw new Error('Sicherheitstoken konnte nicht geladen werden.');
  return (await response.json() as { token: string }).token;
}

async function get<T>(url: string): Promise<T> {
  const response = await fetch(url, { credentials: 'same-origin', cache: 'no-store' });
  if (!response.ok) {
    if (response.status === 404) throw new Error('Die Aufgabe ist nicht mehr verfügbar.');
    throw new Error('Aufgabenzeiten konnten nicht geladen werden.');
  }
  return await response.json() as T;
}

async function modify<T>(url: string, method: 'POST' | 'PUT' | 'PATCH' | 'DELETE',
  body?: object): Promise<T> {
  const token = await csrfToken();
  const response = await fetch(url, {
    method, credentials: 'same-origin',
    headers: { 'X-CSRF-TOKEN': token, ...(body ? { 'Content-Type': 'application/json' } : {}) },
    ...(body ? { body: JSON.stringify(body) } : {}),
  });
  if (!response.ok) {
    const json = await response.json().catch(() => ({})) as { error?: string };
    throw new Error(json.error || (response.status === 409
      ? 'Der Zeitraum ist bereits belegt. Bitte einen freien Zeitpunkt wählen.'
      : 'Änderung konnte nicht gespeichert werden.'));
  }
  if (response.status === 204) return undefined as T;
  return await response.json() as T;
}

function taskPath(id: string): string {
  return '/api/v1/tasks/' + encodeURIComponent(id);
}

export const readTaskTime = (id: string) =>
  get<TaskTimeSummary>(taskPath(id) + '/time');
export const startTaskTimer = (id: string) =>
  modify<TaskTimeSummary>(taskPath(id) + '/time/start', 'POST');
export const stopTaskTimer = (id: string) =>
  modify<TaskTimeSummary>(taskPath(id) + '/time/stop', 'POST');
export const adjustTaskTime = (id: string, deltaMinutes: number, note = '') =>
  modify<TaskTimeSummary>(taskPath(id) + '/time/adjust', 'POST', { deltaMinutes, note });

export const listTaskSegments = (id: string) =>
  get<TaskSegment[]>(taskPath(id) + '/segments');
export const listWeekSegments = (start: string, end: string) =>
  get<TaskSegment[]>('/api/v1/segments/?' +
    new URLSearchParams({ start, end }).toString());
export const readSegmentAvailability = (startsAt: string, endsAt: string, excludeId?: string) =>
  get<SegmentAvailability>('/api/v1/segments/availability?' +
    new URLSearchParams({
      start: startsAt, end: endsAt, ...(excludeId ? { excludeId } : {}),
    }).toString());
export const createTaskSegment = (id: string, input: SegmentInput) =>
  modify<{ id: string }>(taskPath(id) + '/segments', 'POST', input);
export const updateTaskSegment = (id: string, segmentId: string, input: SegmentInput) =>
  modify<void>(taskPath(id) + '/segments/' + encodeURIComponent(segmentId), 'PUT', input);
export const rescheduleTaskSegment = (id: string, segmentId: string, input: {
  startsAt: string; endsAt: string; expectedStartsAt: string; expectedEndsAt: string;
}) => modify<void>(taskPath(id) + '/segments/' + encodeURIComponent(segmentId) + '/schedule', 'PATCH', input);
export const deleteTaskSegment = (id: string, segmentId: string) =>
  modify<void>(taskPath(id) + '/segments/' + encodeURIComponent(segmentId), 'DELETE');

export function secondsToClock(seconds: number): string {
  const safe = Math.max(0, Math.floor(seconds));
  const hours = Math.floor(safe / 3600);
  const minutes = Math.floor((safe % 3600) / 60);
  const rest = safe % 60;
  return [hours, minutes, rest].map(n => String(n).padStart(2, '0')).join(':');
}
