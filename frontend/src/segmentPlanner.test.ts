import { describe, expect, it } from 'vitest';
import { calendarMove, conflictingLocalSegments, halfHourFromPosition, intervalsOverlap } from './segmentPlanner';
import type { TaskSegment } from './taskWorkApi';

const segment: TaskSegment = {
  id: 'e5cc8f3e-2c20-419a-9236-9db66d96a9ff',
  taskId: '44c7ad94-bc54-44be-bd42-66fc7787e154',
  taskTitle: 'Planung',
  startsAt: '2026-10-30T08:15:00Z',
  endsAt: '2026-10-30T09:45:00Z',
  note: 'Bestehende Beschreibung',
  participants: ['team@example.invalid'],
};

describe('Kalender-Verschiebeplanung', () => {
  it('erhält die exakte ursprüngliche Dauer und erwartet den bekannten alten Zeitraum', () => {
    const next = calendarMove(segment, '2026-10-29', 10, 30);
    const starts = new Date(next.startsAt);
    expect(starts.getHours()).toBe(10);
    expect(starts.getMinutes()).toBe(30);
    expect(new Date(next.endsAt).getTime() - starts.getTime()).toBe(90 * 60 * 1000);
    expect(next.expectedStartsAt).toBe(segment.startsAt);
    expect(next.expectedEndsAt).toBe(segment.endsAt);
    expect(Object.keys(next).sort()).toEqual(['endsAt', 'expectedEndsAt', 'expectedStartsAt', 'startsAt']);
  });

  it('akzeptiert Wochentagswechsel über einen Sommerzeitwechsel ohne Daueränderung', () => {
    const source = { ...segment, startsAt: '2026-03-27T08:00:00Z', endsAt: '2026-03-27T09:30:00Z' };
    const next = calendarMove(source, '2026-03-30', 9, 0);
    expect(new Date(next.endsAt).getTime() - new Date(next.startsAt).getTime()).toBe(90 * 60 * 1000);
  });

  it('verhindert Wochenend-, Stunden- und Datumsfehler', () => {
    expect(() => calendarMove(segment, '2026-10-31', 9, 0)).toThrow();
    expect(() => calendarMove(segment, '2026-10-30', 18, 0)).toThrow();
    expect(() => calendarMove(segment, '2026-10-30', 9, 15)).toThrow();
    expect(() => calendarMove(segment, '2026-02-30', 9, 0)).toThrow();
  });

  it('erkennt echte Überschneidungen, aber keine direkt angrenzenden Zeiträume', () => {
    expect(intervalsOverlap('2026-10-30T09:00:00Z', '2026-10-30T09:30:00Z',
      '2026-10-30T09:30:00Z', '2026-10-30T10:00:00Z')).toBe(false);
    expect(intervalsOverlap('2026-10-30T09:00:00Z', '2026-10-30T09:30:00Z',
      '2026-10-30T09:29:00Z', '2026-10-30T10:00:00Z')).toBe(true);
    expect(conflictingLocalSegments([segment], segment.startsAt, segment.endsAt)).toHaveLength(1);
    expect(conflictingLocalSegments([segment], segment.startsAt, segment.endsAt, segment.id)).toHaveLength(0);
  });

  it('bestimmt die halbe Stunde relativ zur Drop-Fläche', () => {
    expect(halfHourFromPosition(24, 10, 60)).toBe(0);
    expect(halfHourFromPosition(40, 10, 60)).toBe(30);
    expect(halfHourFromPosition(42, 10, 0)).toBe(0);
  });
});
