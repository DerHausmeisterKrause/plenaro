export type EntraStatus = { enabled: boolean; linked: boolean };

export async function entraStatus(): Promise<EntraStatus> {
  const response = await fetch('/api/v1/auth/entra/status', {
    credentials: 'same-origin', cache: 'no-store',
  });
  if (!response.ok) throw new Error('Entra-Status nicht erreichbar.');
  return await response.json() as EntraStatus;
}

async function csrfToken(): Promise<string> {
  const response = await fetch('/api/v1/auth/csrf', {
    credentials: 'same-origin', cache: 'no-store',
  });
  if (!response.ok) throw new Error('Sicherheitstoken nicht verfügbar.');
  const json = await response.json() as { token: string };
  return json.token;
}

async function update(path: string): Promise<void> {
  const token = await csrfToken();
  const response = await fetch(path, {
    method: 'POST',
    credentials: 'same-origin',
    headers: { 'X-CSRF-TOKEN': token },
  });
  if (response.ok) return;
  const body = await response.json().catch(() => ({})) as { error?: string };
  throw new Error(body.error || 'Microsoft-Kontoverknüpfung fehlgeschlagen.');
}

export const confirmEntraLink = () => update('/api/v1/auth/entra/link/confirm');
export const unlinkEntra = () => update('/api/v1/auth/entra/unlink');

export type PendingEntraIdentity = { account: string; tenantId: string };

export async function pendingEntraIdentity(): Promise<PendingEntraIdentity> {
  const result = await fetch('/api/v1/auth/entra/link/pending', {
    credentials: 'same-origin', cache: 'no-store',
  });
  if (!result.ok) throw new Error('Microsoft-Verknüpfung abgelaufen. Bitte erneut beginnen.');
  return await result.json() as PendingEntraIdentity;
}
