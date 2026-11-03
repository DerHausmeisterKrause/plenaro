export type PersonalPreferences = {
  theme: 'light' | 'dark';
  weekStart: 'inherit' | 'monday' | 'sunday';
};

export type WorkspacePreferences = {
  instanceName: string;
  supportEmail: string;
  defaultWeekStart: 'monday' | 'sunday';
};

export async function readPersonalPreferences(): Promise<PersonalPreferences> {
  const response = await fetch('/api/v1/settings/me', { credentials: 'same-origin', cache: 'no-store' });
  if (!response.ok) throw new Error('Persönliche Einstellungen konnten nicht geladen werden.');
  return await response.json() as PersonalPreferences;
}

export async function readWorkspacePreferences(): Promise<WorkspacePreferences> {
  const response = await fetch('/api/v1/settings/global', { credentials: 'same-origin', cache: 'no-store' });
  if (!response.ok) throw new Error('Instanzeinstellungen konnten nicht geladen werden.');
  return await response.json() as WorkspacePreferences;
}

async function update<T>(path: string, value: T): Promise<void> {
  const result = await fetch('/api/v1/auth/csrf', { credentials: 'same-origin', cache: 'no-store' });
  if (!result.ok) throw new Error('Sicherheitstoken konnte nicht geladen werden.');
  const { token } = await result.json() as { token: string };
  const response = await fetch(path, {
    method: 'PUT',
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token },
    body: JSON.stringify(value),
  });
  if (!response.ok) {
    if (response.status === 401) throw new Error('Sitzung abgelaufen. Bitte neu anmelden.');
    if (response.status === 403) throw new Error('Für diese Änderung fehlen Administratorrechte.');
    const details = await response.json().catch(() => ({})) as { error?: string };
    throw new Error(details.error || 'Einstellungen konnten nicht gespeichert werden.');
  }
}

export async function savePersonalPreferences(settings: PersonalPreferences): Promise<void> {
  await update('/api/v1/settings/me', settings);
}
export async function saveWorkspacePreferences(settings: WorkspacePreferences): Promise<void> {
  await update('/api/v1/settings/global', settings);
}
