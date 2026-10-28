export type CurrentUser = {
  id: string;
  email: string;
  name: string;
  roles: string[];
};

export async function currentUser(): Promise<CurrentUser | null> {
  const result = await fetch('/api/v1/auth/me', { credentials: 'same-origin', cache: 'no-store' });
  if (result.status === 401) return null;
  if (!result.ok) throw new Error('Sitzung konnte nicht geprüft werden.');
  return await result.json() as CurrentUser;
}

async function csrfToken(): Promise<string> {
  const result = await fetch('/api/v1/auth/csrf', { credentials: 'same-origin', cache: 'no-store' });
  if (!result.ok) throw new Error('Sicherheitstoken nicht verfügbar.');
  const data = await result.json() as { token: string };
  if (!data.token) throw new Error('Sicherheitstoken fehlt.');
  return data.token;
}

export async function login(email: string, password: string): Promise<CurrentUser> {
  const token = await csrfToken();
  const response = await fetch('/api/v1/auth/login', {
    method: 'POST',
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token },
    body: JSON.stringify({ email, password }),
  });
  if (response.status === 401) throw new Error('E-Mail oder Passwort nicht korrekt.');
  if (response.status === 429) throw new Error('Zu viele Anmeldeversuche. Bitte später erneut versuchen.');
  if (!response.ok) throw new Error('Anmeldung momentan nicht möglich.');
  const user = await currentUser();
  if (!user) throw new Error('Sitzung konnte nach Anmeldung nicht bestätigt werden.');
  return user;
}

export async function logout(): Promise<void> {
  const token = await csrfToken();
  const response = await fetch('/api/v1/auth/logout', {
    method: 'POST',
    credentials: 'same-origin',
    headers: { 'X-CSRF-TOKEN': token },
  });
  if (!response.ok) throw new Error('Abmeldung fehlgeschlagen.');
}

export async function setupRequired(): Promise<boolean> {
  const response = await fetch('/api/v1/setup/status', { credentials: 'same-origin', cache: 'no-store' });
  if (!response.ok) throw new Error('Einrichtungsstatus nicht erreichbar.');
  return (await response.json() as { required: boolean }).required;
}

export async function completeSetup(code: string, displayName: string, email: string, password: string): Promise<void> {
  const token = await csrfToken();
  const response = await fetch('/api/v1/setup/complete', {
    method: 'POST',
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token },
    body: JSON.stringify({ code, displayName, email, password }),
  });
  if (response.ok) return;
  if (response.status === 429) throw new Error('Zu viele Versuche. Bitte später erneut versuchen.');
  if (response.status === 409) throw new Error('Plenaro wurde bereits eingerichtet.');
  const data = await response.json().catch(() => ({})) as { error?: string };
  throw new Error(data.error || 'Die Einrichtung ist fehlgeschlagen.');
}
