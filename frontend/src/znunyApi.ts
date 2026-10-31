export type ZnunyServerSettings = {
  webUrl: string;
  apiUrl: string;
  ticketSearchRoute: string;
  ticketSearchMethod: 'GET' | 'POST';
  ticketSearchAuthMode: 'Session';
  ticketGetRouteTemplate: string;
  ticketGetMethod: 'GET' | 'POST';
  ticketGetAuthMode: 'Session';
  ticketUpdateRoute: string;
  agentListRoute: string;
  ticketCreateRoute: string;
  ticketCreateMethod: 'GET' | 'POST';
  ticketCreateQueue: string;
  ticketCreateState: string;
  ticketCreatePriority: string;
  ticketCreateType: string;
  ticketCreateCustomerUser: string;
  ticketReplyTemplate: string;
  dynamicFieldOptionsRoute: string;
  costCenterFieldName: string;
  orderFieldName: string;
  costCenterOptions: string;
  orderOptions: string;
  syncIntervalMinutes: number;
  candidateSyncIntervalMinutes: number;
  searchLimit: number;
  articleLimit: number;
  onlyOpenTickets: boolean;
  showClosedTickets: boolean;
};
export type ZnunyPersonalSettings = {
  username: string;
  agentId: number;
  candidateUserId: number;
  candidateKeywords: string;
  candidateExcludeKeywords: string;
  includeOwner: boolean;
  includeResponsible: boolean;
  notifyOnNewAssignedTickets: boolean;
  notifyOnTicketChanges: boolean;
  showAllCandidatePoolTickets: boolean;
};
export type ZnunyPersonalView = ZnunyPersonalSettings & {
  passwordConfigured: boolean;
  vaultReady: boolean;
};
export type ZnunyConnectionResult = { success: boolean; status: string };

async function jsonRequest<T>(url: string): Promise<T> {
  const result = await fetch(url, { credentials: 'same-origin', cache: 'no-store' });
  if (!result.ok) throw new Error('Znuny-Einstellungen konnten nicht geladen werden.');
  return await result.json() as T;
}
async function mutate(url: string, method: 'PUT' | 'POST' | 'DELETE', body?: unknown): Promise<Response> {
  const csrf = await jsonRequest<{ token: string }>('/api/v1/auth/csrf');
  const response = await fetch(url, {
    method, credentials: 'same-origin',
    headers: { 'X-CSRF-TOKEN': csrf.token, ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
  if (!response.ok) {
    if (response.status === 403) throw new Error('Nur Administratoren können die Server-API ändern.');
    const detail = await response.json().catch(() => ({})) as { error?: string };
    throw new Error(detail.error || ('Znuny-Operation fehlgeschlagen (HTTP ' + response.status + ').'));
  }
  return response;
}

export const getZnunyServer = () => jsonRequest<ZnunyServerSettings>('/api/v1/znuny/server');
export const getZnunyPersonal = () => jsonRequest<ZnunyPersonalView>('/api/v1/znuny/me');
export const saveZnunyServer = async (settings: ZnunyServerSettings) => {
  await mutate('/api/v1/admin/znuny/server', 'PUT', settings);
};
export const resetZnunyServer = async () => {
  await mutate('/api/v1/admin/znuny/server','DELETE');
};
export const saveZnunyPersonal = async (settings: ZnunyPersonalSettings) => {
  await mutate('/api/v1/znuny/me', 'PUT', settings);
};
export const saveZnunyPassword = async (password: string) => {
  await mutate('/api/v1/znuny/password', 'PUT', { password });
};
export const deleteZnunyPassword = async () => {
  await mutate('/api/v1/znuny/password', 'DELETE');
};
export const testZnunyConnection = async (): Promise<ZnunyConnectionResult> => {
  const result = await mutate('/api/v1/znuny/connection-test','POST');
  return await result.json() as ZnunyConnectionResult;
};

export function znunyConnectionMessage(status: string): string {
  const messages: Record<string,string> = {
    connected: 'Znuny-Session wurde erfolgreich erstellt.',
    'auth-rejected': 'Znuny hat die Anmeldung abgelehnt.',
    'auth-or-protocol-error': 'Znuny lieferte keine SessionID. Zugangsdaten oder GenericInterface-Webservice prüfen.',
    'invalid-response': 'Die Antwort des Ticketservers hat kein gültiges Session-Format.',
    'remote-error': 'Znuny meldet einen Server- oder Webservicefehler.',
    timeout: 'Verbindung zum Znuny-Server hat zu lange gedauert.',
    unavailable: 'Der Znuny-Server ist nicht erreichbar oder das TLS-Zertifikat ist ungültig.',
  };
  return messages[status] || 'Znuny-Verbindung konnte nicht hergestellt werden.';
}
