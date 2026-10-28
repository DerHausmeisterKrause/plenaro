export type DayType = 'Normal' | 'UL' | 'AM';
export type DayMarker = { day: string; dayType: DayType; isBr: boolean };
export type BudgetSettings = { annualUlDays: number | null; monthlyBrDays: number | null };
export type BudgetSummary = {
  year: number; month: number; annualUlDays: number | null; usedUlDays: number;
  remainingUlDays: number | null; ulExhausted: boolean; ulExceeded: boolean;
  monthlyBrDays: number | null; usedBrDays: number;
  remainingBrDays: number | null; brExhausted: boolean; brExceeded: boolean;
};

async function get<T>(url: string): Promise<T> {
  const response = await fetch(url, { credentials: 'same-origin', cache: 'no-store' });
  if (!response.ok) throw new Error('Tagesmarker konnten nicht geladen werden.');
  return await response.json() as T;
}

async function save(url: string, body: unknown): Promise<void> {
  const token = await get<{ token: string }>('/api/v1/auth/csrf');
  const response = await fetch(url, {
    method: 'PUT', credentials: 'same-origin',
    headers: { 'X-CSRF-TOKEN': token.token, 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (response.ok) return;
  const json = await response.json().catch(() => ({})) as { error?: string };
  throw new Error(json.error || 'Tagesmarker konnten nicht gespeichert werden.');
}

export function monthParams(year: number, month: number) {
  return new URLSearchParams({ year: String(year), month: String(month) }).toString();
}

export const getMarkerBudget = () =>
  get<BudgetSettings>('/api/v1/day-markers/budget');
export const saveMarkerBudget = (budget: BudgetSettings) =>
  save('/api/v1/day-markers/budget', budget);
export const getMarkerSummary = (year: number, month: number) =>
  get<BudgetSummary>('/api/v1/day-markers/summary?' + monthParams(year, month));
export const getMonthMarkers = (year: number, month: number) =>
  get<DayMarker[]>('/api/v1/day-markers/?' + monthParams(year, month));
export const saveDayMarker = (day: string, dayType: DayType, isBr: boolean) =>
  save('/api/v1/day-markers/' + encodeURIComponent(day), { dayType, isBr });
