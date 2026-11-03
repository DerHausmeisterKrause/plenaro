export type TaskStatus = 'planned' | 'active' | 'completed' | 'cancelled';

export type WorkTask = {
  id: string;
  title: string;
  description: string;
  status: TaskStatus;
  priority: number | null;
  tags: string[];
  plannedFor: string | null;
  createdAt: string;
  updatedAt: string;
  completedAt: string | null;
};

export type TaskInput = {
  title: string;
  description: string;
  status: TaskStatus;
  priority: number | null;
  tags: string[];
  plannedFor: string | null;
};

async function csrf(): Promise<string> {
  const result = await fetch('/api/v1/auth/csrf', { credentials: 'same-origin', cache: 'no-store' });
  if (!result.ok) throw new Error('Sicherheitstoken konnte nicht geladen werden.');
  return (await result.json() as { token: string }).token;
}

async function mutation(path: string, method: string, data?: object): Promise<void> {
  const token = await csrf();
  const response = await fetch(path, {
    method, credentials: 'same-origin',
    headers: { 'X-CSRF-TOKEN': token, ...(data ? { 'Content-Type': 'application/json' } : {}) },
    ...(data ? { body: JSON.stringify(data) } : {}),
  });
  if (response.ok) return;
  const error = await response.json().catch(() => ({})) as { error?: string };
  throw new Error(error.error || (response.status === 401 ? 'Bitte erneut anmelden.' : 'Die Aufgabe konnte nicht gespeichert werden.'));
}

async function getTasks(url: string): Promise<WorkTask[]> {
  const response = await fetch(url, { credentials: 'same-origin', cache: 'no-store' });
  if (!response.ok) throw new Error('Aufgaben konnten nicht geladen werden.');
  return await response.json() as WorkTask[];
}

export const listTasks = (query = '', status = '') => {
  const params = new URLSearchParams();
  if (query.trim()) params.set('q', query.trim());
  if (status) params.set('status', status);
  return getTasks('/api/v1/tasks/' + (params.size ? '?' + params.toString() : ''));
};
export const todayTasks = (date: string) =>
  getTasks('/api/v1/tasks/today?date=' + encodeURIComponent(date));
export const createTask = (value: TaskInput) => mutation('/api/v1/tasks/', 'POST', value);
export const updateTask = (id: string, value: TaskInput) =>
  mutation('/api/v1/tasks/' + encodeURIComponent(id), 'PUT', value);
export const changeTaskStatus = (id: string, status: TaskStatus) =>
  mutation('/api/v1/tasks/' + encodeURIComponent(id) + '/status', 'POST', { status });
export const deleteTask = (id: string) =>
  mutation('/api/v1/tasks/' + encodeURIComponent(id), 'DELETE');

export function localDate(date = new Date()): string {
  const pad = (value: number) => String(value).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

export const taskStatusLabel: Record<TaskStatus, string> = {
  planned: 'Geplant', active: 'In Arbeit', completed: 'Erledigt', cancelled: 'Abgebrochen',
};
