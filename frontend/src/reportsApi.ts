export type ReportRow = {
  taskId: string;
  title: string;
  status: 'planned' | 'active' | 'completed' | 'cancelled';
  timerSeconds: number;
  adjustmentMinutes: number;
  totalSeconds: number;
  sessionCount: number;
};

export type AccountedTicketRow = {
  ticketNumber:string;
  title:string;
  remoteSeconds:number;
  localOnlySeconds:number;
  totalSeconds:number;
  remoteCount:number;
  localOnlyCount:number;
};

export type MonthReport = {
  month: string;
  timeZone: string;
  generatedAt: string;
  timerSeconds: number;
  adjustmentMinutes: number;
  totalSeconds: number;
  sessionCount: number;
  taskCount: number;
  tasks: ReportRow[];
  accountedSeconds: number;
  remoteSeconds: number;
  localFallbackSeconds: number;
  remoteEntries: number;
  localFallbackEntries: number;
  accountingStatus: 'ok' | 'not-connected' | 'not-configured' | 'unavailable';
  accountingMessage: string;
  accountedTickets: AccountedTicketRow[];
};

function reportUrl(month: string, csv = false): string {
  return '/api/v1/reports/month' + (csv ? '.csv' : '') + '?' +
    new URLSearchParams({ month }).toString();
}

export async function readMonthReport(month: string, signal?: AbortSignal): Promise<MonthReport> {
  const result = await fetch(reportUrl(month), {
    credentials: 'same-origin', cache: 'no-store', signal,
  });
  if (!result.ok) throw new Error(result.status === 401
    ? 'Deine Sitzung ist abgelaufen. Bitte erneut anmelden.'
    : (await result.json().catch(()=>({})) as {detail?:string;error?:string}).error ||
      'Der Monatsbericht konnte nicht geladen werden.');
  return await result.json() as MonthReport;
}

export async function downloadMonthCsv(month: string): Promise<void> {
  const response = await fetch(reportUrl(month, true), {
    credentials: 'same-origin', cache: 'no-store',
  });
  if (!response.ok) throw new Error('Der CSV-Export ist fehlgeschlagen.');
  const file = await response.blob();
  const url = URL.createObjectURL(file);
  try {
    const link = document.createElement('a');
    link.href = url;
    link.download = `plenaro-zeitreport-${month}.csv`;
    document.body.appendChild(link);
    link.click();
    link.remove();
  } finally {
    URL.revokeObjectURL(url);
  }
}
