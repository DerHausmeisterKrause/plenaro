import type { TaskSegment } from './taskWorkApi';

export type RescheduleInput = {
  startsAt: string;
  endsAt: string;
  expectedStartsAt: string;
  expectedEndsAt: string;
};

// The calendar has half-hour drop targets between 06:00 and 17:30.
// Preserve the original duration exactly; never mutate participants/notes.
export function calendarMove(segment: TaskSegment, day: string, hour: number, minute: number): RescheduleInput {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(day) ||
      !Number.isInteger(hour) || hour < 6 || hour > 17 ||
      (minute !== 0 && minute !== 30))
    throw new Error('Ungültiges Zeitraster: nur Mo–Fr, 06:00–17:30 in 30-Minuten-Schritten.');

  const start = new Date(segment.startsAt).getTime();
  const end = new Date(segment.endsAt).getTime();
  const next = new Date(`${day}T${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}:00`);
  const duration = end - start;
  if (!Number.isFinite(start) || !Number.isFinite(end) || duration <= 0 ||
      duration > 24 * 60 * 60 * 1000 ||
      Number.isNaN(next.getTime()) || next.getFullYear() < 2000 || next.getFullYear() > 2099 ||
      next.getFullYear() !== Number(day.slice(0, 4)) ||
      next.getMonth() + 1 !== Number(day.slice(5, 7)) ||
      next.getDate() !== Number(day.slice(8, 10)) ||
      (next.getDay() === 0 || next.getDay() === 6))
    throw new Error('Segment kann nicht in diesen Kalenderslot verschoben werden.');

  return {
    startsAt: next.toISOString(),
    endsAt: new Date(next.getTime() + duration).toISOString(),
    expectedStartsAt: segment.startsAt,
    expectedEndsAt: segment.endsAt,
  };
}

export function halfHourFromPosition(clientY: number, top: number, height: number): 0 | 30 {
  return height > 0 && clientY - top >= height / 2 ? 30 : 0;
}

export function intervalsOverlap(aStart: string, aEnd: string, bStart: string, bEnd: string): boolean {
  const a = Date.parse(aStart), z = Date.parse(aEnd);
  const b = Date.parse(bStart), y = Date.parse(bEnd);
  return [a, z, b, y].every(Number.isFinite) && a < z && b < y && a < y && b < z;
}

export function conflictingLocalSegments(
  segments: TaskSegment[], start: string, end: string, exceptId?: string,
): TaskSegment[] {
  return segments.filter(s =>
    s.id !== exceptId && intervalsOverlap(start, end, s.startsAt, s.endsAt));
}
