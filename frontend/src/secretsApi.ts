export type SecretScope = 'me' | 'global';

function pathFor(scope: SecretScope): string {
  return scope === 'me' ? '/api/v1/secrets/me' : '/api/v1/admin/secrets';
}

export async function listSecretNames(scope: SecretScope): Promise<string[]> {
  const result = await fetch(pathFor(scope) + '/', { credentials: 'same-origin', cache: 'no-store' });
  if (result.status === 503) throw new Error('Verschlüsselter Speicher noch nicht eingerichtet. Details stehen in der HTTPS-/Vault-Dokumentation.');
  if (!result.ok) throw new Error('Secret-Namen konnten nicht geladen werden.');
  return (await result.json() as { names: string[] }).names;
}

async function csrfToken(): Promise<string> {
  const response = await fetch('/api/v1/auth/csrf', { credentials: 'same-origin', cache: 'no-store' });
  if (!response.ok) throw new Error('Sicherheitstoken nicht verfügbar.');
  return (await response.json() as { token: string }).token;
}

async function changeSecret(scope: SecretScope, name: string, method: 'PUT'|'DELETE', value?: string) {
  const token = await csrfToken();
  const response = await fetch(pathFor(scope) + '/' + encodeURIComponent(name), {
    method, credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token },
    ...(method === 'PUT' ? { body: JSON.stringify({ value }) } : {}),
  });
  if (response.ok) return;
  if (response.status === 403) throw new Error('Hierfür fehlen Administratorrechte.');
  const data = await response.json().catch(() => ({})) as { error?: string; detail?: string };
  throw new Error(data.error || data.detail || 'Secret konnte nicht geändert werden.');
}

export const setSecret = (scope: SecretScope, name: string, value: string) =>
  changeSecret(scope, name, 'PUT', value);
export const deleteSecret = (scope: SecretScope, name: string) =>
  changeSecret(scope, name, 'DELETE');
