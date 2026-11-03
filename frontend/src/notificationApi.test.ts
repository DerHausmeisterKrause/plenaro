import { describe, expect, it } from 'vitest';
import { reminderKey, shouldShowBrowserAlert, type TaskReminder } from './notificationApi';

const notice: TaskReminder = {
  segmentId: '80bbbf05-5573-4bb2-a32e-30c09d4c5d72',
  taskId: 'ac2b27cb-ea06-4278-ab3b-0557adc42aae',
  taskTitle: 'Aufgabe', startsAt: '2026-10-30T11:00:00Z',
  dueAt: '2026-10-30T10:58:00Z', kind: 'lead',
};

describe('Web-Reminder', () => {
  it('bindet quittierte Hinweise an Benutzer, Segment, Start und Art', () => {
    expect(reminderKey('user-one', notice)).not.toBe(reminderKey('user-two', notice));
    expect(reminderKey('user-one', notice)).not.toBe(reminderKey('user-one', {
      ...notice, kind: 'start',
    }));
    expect(reminderKey('user-one', notice)).not.toBe(reminderKey('user-one', {
      ...notice, startsAt: '2026-10-30T12:00:00Z',
    }));
  });

  it('zeigt frische Alerts, aber keine historischen oder zukünftigen Hinweise', () => {
    const now = Date.parse(notice.dueAt) + 45000;
    expect(shouldShowBrowserAlert(notice, now)).toBe(true);
    expect(shouldShowBrowserAlert(notice, now - 60000)).toBe(false);
    expect(shouldShowBrowserAlert(notice, now + 121000)).toBe(false);
  });
});
