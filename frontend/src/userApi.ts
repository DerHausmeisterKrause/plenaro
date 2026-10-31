export type ManagedUser = {
  id: string;
  email: string;
  displayName: string;
  role: 'Admin' | 'User';
  disabled: boolean;
  agentId: number | null;
};

async function csrf(): Promise<string> {
  const r = await fetch('/api/v1/auth/csrf', { credentials: 'same-origin', cache: 'no-store' });
  if (!r.ok) throw Error('Sicherheitstoken konnte nicht geladen werden.');
  const json = await r.json() as { token: string };
  return json.token;
}

async function request<T>(path: string, method: string, payload: object): Promise<T> {
  const token = await csrf();
  const r = await fetch(path, {
    method,
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token },
    body: JSON.stringify(payload),
  });
  if (!r.ok) {
    if (r.status === 401) throw Error('Sitzung abgelaufen – bitte erneut anmelden.');
    if (r.status === 403) throw Error('Du hast keine Administratorrechte.');
    const body = await r.json().catch(() => ({})) as { error?: string };
    throw Error(body.error || 'Die Änderung konnte nicht gespeichert werden.');
  }
  if (r.status === 204) return undefined as T;
  return await r.json() as T;
}

export async function listUsers(): Promise<ManagedUser[]> {
  const r = await fetch('/api/v1/admin/users/', { credentials: 'same-origin', cache: 'no-store' });
  if (r.status === 403) throw Error('Zugriff verweigert.');
  if (!r.ok) throw Error('Benutzer konnten nicht geladen werden.');
  return await r.json() as ManagedUser[];
}

export async function addUser(email: string, displayName: string, password: string,
  role: 'Admin' | 'User', agentId: number | null): Promise<void> {
  await request('/api/v1/admin/users/', 'POST',
    { email, displayName, password, role, agentId });
}

export async function saveUser(id: string, displayName: string, role: 'Admin' | 'User',
  disabled: boolean, agentId: number | null): Promise<void> {
  await request(`/api/v1/admin/users/${id}`, 'PUT',
    { displayName, role, disabled, agentId });
}

export async function resetUserPassword(id: string, password: string): Promise<void> {
  await request(`/api/v1/admin/users/${id}/reset-password`, 'POST', { password });
}

export async function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  await request('/api/v1/auth/change-password', 'POST', { currentPassword, newPassword });
}
