import type { OutlookEvent } from './graphApi';
import type { TaskSegment } from './taskWorkApi';
import type { WorkTask } from './tasksApi';

export type TodayAgendaEntry = {
  key: string;
  kind: 'task' | 'outlook';
  taskId: string | null;
  title: string;
  description: string;
  start: string | null;
  end: string | null;
  allDay: boolean;
  location: string;
  webLink: string | null;
};

export function localDayBounds(day: string): { start: Date; end: Date } {
  // Calendar arithmetic, not +24h: Berlin has 23h/25h DST days.
  const [year, month, date] = day.split('-').map(Number);
  const start = new Date(year, month - 1, date);
  const end = new Date(year, month - 1, date + 1);
  return { start, end };
}

export function buildTodayAgenda(
  day: string, tasks: WorkTask[], segments: TaskSegment[], outlook: OutlookEvent[],
): TodayAgendaEntry[] {
  const { start, end } = localDayBounds(day);
  const first = start.getTime();
  const last = end.getTime();
  const overlapsDay = (from: string, until: string) => {
    const a = new Date(from).getTime();
    const b = new Date(until).getTime();
    return Number.isFinite(a) && Number.isFinite(b) && a < last && b > first && b > a;
  };
  const eligible = new Map(tasks
    .filter(task => task.status === 'active' || task.status === 'planned')
    .map(task => [task.id, task]));
  const agenda: TodayAgendaEntry[] = [];
  const scheduledIds = new Set<string>();

  for (const segment of segments) {
    const task = eligible.get(segment.taskId);
    if (!task || !overlapsDay(segment.startsAt, segment.endsAt)) continue;
    scheduledIds.add(task.id);
    agenda.push({
      key: 'segment:' + segment.id,
      kind: 'task', taskId: task.id, title: task.title,
      description: segment.note || task.description,
      start: segment.startsAt, end: segment.endsAt,
      allDay: false, location: '', webLink: null,
    });
  }

  // A task explicitly planned for today without a timed segment is an undated
  // day task. Keep it visible rather than silently dropping quick-added tasks.
  for (const task of eligible.values()) {
    if (task.plannedFor !== day || scheduledIds.has(task.id)) continue;
    agenda.push({
      key: 'daytask:' + task.id, kind: 'task', taskId: task.id,
      title: task.title, description: task.description,
      start: null, end: null, allDay: true, location: '', webLink: null,
    });
  }

  const seenEvents = new Set<string>();
  for (const event of outlook) {
    if (seenEvents.has(event.id) || !overlapsDay(event.start, event.end)) continue;
    seenEvents.add(event.id);
    const url = event.webLink;
    let safeLink: string | null = null;
    if (url) {
      try {
        const parsed = new URL(url);
        if (parsed.protocol === 'https:' || parsed.protocol === 'http:')
          safeLink = parsed.href;
      } catch { /* Invalid Outlook URL must not become an action */ }
    }
    agenda.push({
      key: 'outlook:' + event.id, kind: 'outlook', taskId: null,
      title: event.subject || 'Outlook-Termin', description: '',
      start: event.start, end: event.end,
      allDay: event.isAllDay, location: event.location, webLink: safeLink,
    });
  }
  return agenda.sort((a, b) =>
    Number(b.allDay) - Number(a.allDay)
    || (a.start ? new Date(a.start).getTime() : first) -
       (b.start ? new Date(b.start).getTime() : first)
    || (a.kind === 'task' ? 0 : 1) - (b.kind === 'task' ? 0 : 1)
    || a.title.localeCompare(b.title, 'de', { sensitivity: 'base' })
    || a.key.localeCompare(b.key));
}

export function agendaTimeLabel(item: TodayAgendaEntry): string {
  if (item.allDay) return item.kind === 'outlook' ? 'Ganztägig' : 'Heute geplant';
  if (!item.start || !item.end) return '';
  const fmt = new Intl.DateTimeFormat('de-DE', { hour: '2-digit', minute: '2-digit' });
  return fmt.format(new Date(item.start)) + ' – ' + fmt.format(new Date(item.end));
}
