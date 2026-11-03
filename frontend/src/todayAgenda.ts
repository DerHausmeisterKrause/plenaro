import type { OutlookEvent } from './graphApi';
import type { TicketAgendaSegment } from './znunySyncApi';

export type TodayAgendaEntry = {
  key: string;
  kind: 'ticket' | 'outlook';
  ticketId: string | null;
  title: string;
  description: string;
  start: string;
  end: string;
  allDay: boolean;
  location: string;
  webLink: string | null;
};

export function localDayBounds(day: string): { start: Date; end: Date } {
  // Wall-clock calendar arithmetic: DST transitions have 23 or 25 hours.
  const [year, month, date] = day.split('-').map(Number);
  const start = new Date(year, month - 1, date);
  const end = new Date(year, month - 1, date + 1);
  return { start, end };
}

export function buildTodayAgenda(day: string, segments: TicketAgendaSegment[],
  outlook: OutlookEvent[]): TodayAgendaEntry[] {
  const { start, end } = localDayBounds(day);
  const first = start.getTime();
  const last = end.getTime();
  const overlapsDay = (from: string, until: string) => {
    const a = new Date(from).getTime();
    const b = new Date(until).getTime();
    return Number.isFinite(a) && Number.isFinite(b) && a < last && b > first && b > a;
  };
  const agenda: TodayAgendaEntry[] = [];
  const seenSegments = new Set<string>();
  for (const segment of segments) {
    if (seenSegments.has(segment.id) || !overlapsDay(segment.startsAt, segment.endsAt) ||
      segment.stateType.toLowerCase() === 'closed') continue;
    seenSegments.add(segment.id);
    agenda.push({
      key: 'ticket-segment:' + segment.id,
      kind: 'ticket', ticketId: segment.ticketId,
      title: segment.title || 'Ticket #' + segment.ticketNumber,
      description: segment.note,
      start: segment.startsAt, end: segment.endsAt,
      allDay: false, location: '', webLink: null,
    });
  }
  const seenEvents = new Set<string>();
  for (const event of outlook) {
    if (seenEvents.has(event.id) || !overlapsDay(event.start, event.end)) continue;
    seenEvents.add(event.id);
    let safeLink: string | null = null;
    if (event.webLink) {
      try {
        const parsed = new URL(event.webLink);
        if (parsed.protocol === 'https:' || parsed.protocol === 'http:')
          safeLink = parsed.href;
      } catch { /* Never turn a malformed Outlook URL into a link. */ }
    }
    agenda.push({
      key: 'outlook:' + event.id, kind: 'outlook', ticketId: null,
      title: event.subject || 'Outlook-Termin', description: '',
      start: event.start, end: event.end,
      allDay: event.isAllDay, location: event.location, webLink: safeLink,
    });
  }
  return agenda.sort((a,b) =>
    Number(b.allDay) - Number(a.allDay)
    || new Date(a.start).getTime()-new Date(b.start).getTime()
    || (a.kind === 'ticket' ? 0 : 1)-(b.kind === 'ticket' ? 0 : 1)
    || a.title.localeCompare(b.title,'de',{sensitivity:'base'})
    || a.key.localeCompare(b.key));
}

export function agendaTimeLabel(item: TodayAgendaEntry): string {
  if (item.allDay) return 'Ganztägig';
  const fmt = new Intl.DateTimeFormat('de-DE',{hour:'2-digit',minute:'2-digit'});
  return fmt.format(new Date(item.start))+' – '+fmt.format(new Date(item.end));
}
