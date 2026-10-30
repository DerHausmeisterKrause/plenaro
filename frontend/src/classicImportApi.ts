export type ClassicImport = {
  formatVersion: number;
  sourceId: string;
  schemaVersion: number;
  tasks: unknown[];
  segments: unknown[];
  timeEntries: unknown[];
  dayMarkers: unknown[];
  settings?: { reminderLeadMinutes: number | null };
  warnings?: string[];
};
export type ImportPreview = {
  taskCount: number; segmentCount: number; timeCount: number; markerCount: number;
  newTasks: number; newSegments: number; newTimeEntries: number; newMarkers: number;
  skippedExistingOrDuplicateMarkers: number;
  reminderLeadMinutes: number | null;
  warnings: string[];
  note: string;
};
export type ImportBatch = {
  id: string; sourceId: string; createdAt: string;
  rowCount: number; importedLeadMinutes: number | null;
};
export type ImportResult = { batchId: string; inserted: number; skipped: number;
  reminderLeadMinutes: number | null };

async function withCsrf(path: string, method: 'POST' | 'DELETE', body?: ClassicImport) {
  const tokenReply = await fetch('/api/v1/auth/csrf', { credentials: 'same-origin', cache: 'no-store' });
  if (!tokenReply.ok) throw new Error('Sicherheitstoken konnte nicht geladen werden.');
  const { token } = await tokenReply.json() as { token: string };
  const response = await fetch(path, {
    method, credentials: 'same-origin',
    headers: { 'X-CSRF-TOKEN': token, ...(body ? { 'Content-Type': 'application/json' } : {}) },
    ...(body ? { body: JSON.stringify(body) } : {}),
  });
  if (!response.ok) {
    const message = await response.json().catch(() => ({})) as { error?: string };
    throw new Error(message.error || 'Import konnte nicht verarbeitet werden.');
  }
  if (response.status === 204) return null;
  return await response.json() as unknown;
}

export async function previewClassicImport(document: ClassicImport): Promise<ImportPreview> {
  return await withCsrf('/api/v1/classic-import/preview', 'POST', document) as ImportPreview;
}
export async function commitClassicImport(document: ClassicImport): Promise<ImportResult> {
  return await withCsrf('/api/v1/classic-import/commit', 'POST', document) as ImportResult;
}
export async function listClassicImports(): Promise<ImportBatch[]> {
  const response = await fetch('/api/v1/classic-import/batches', { credentials: 'same-origin', cache: 'no-store' });
  if (!response.ok) throw new Error('Importverlauf konnte nicht geladen werden.');
  return await response.json() as ImportBatch[];
}
export async function undoClassicImport(id: string): Promise<void> {
  await withCsrf('/api/v1/classic-import/batches/' + encodeURIComponent(id), 'DELETE');
}
export function parseClassicFile(content: string): ClassicImport {
  let data: unknown;
  try { data = JSON.parse(content); }
  catch { throw new Error('Ungültige JSON-Datei. Verwende den Classic-Konverter.'); }
  if (!data || typeof data !== 'object') throw new Error('Ungültige Importdatei.');
  const doc = data as Partial<ClassicImport>;
  if (doc.formatVersion !== 1 || !doc.sourceId ||
      !Array.isArray(doc.tasks) || !Array.isArray(doc.segments) ||
      !Array.isArray(doc.timeEntries) || !Array.isArray(doc.dayMarkers))
    throw new Error('Unbekanntes Exportformat. Verwende tools/export_classic.py.');
  return doc as ClassicImport;
}
