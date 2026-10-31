import { describe, expect, it } from 'vitest';
import { agendaTimeLabel, buildTodayAgenda, localDayBounds } from './todayAgenda';
import type { WorkTask } from './tasksApi';
import type { TaskSegment } from './taskWorkApi';
import type { OutlookEvent } from './graphApi';

const day = '2026-10-31';
const task = (id: string, title: string, plannedFor: string | null, status: WorkTask['status']='planned'): WorkTask => ({
  id,title,plannedFor,status,description:'',
  priority:3,tags:[],createdAt:'2026-10-01T00:00:00Z',
  updatedAt:'2026-10-15T00:00:00Z',completedAt:null,
});
const segment = (id:string,taskId:string,start:string,end:string):TaskSegment => ({
  id,taskId,taskTitle:taskId,startsAt:start,endsAt:end,note:'',participants:[],
});
const event = (id:string,subject:string,start:string,end:string,isAllDay=false):OutlookEvent => ({
  id,subject,start,end,isAllDay,showAs:'busy',location:'Büro',webLink:'https://outlook.office.com/calendar/item',
});

describe('Heute-Tagesagenda wie Plenaro Classic',()=>{
  it('zeigt nur heute anstehende Aufgaben und Zeitsegmente, nicht alle aktiven/überfälligen',()=>{
    const tasks=[
      task('a','Heute ohne Uhrzeit',day),
      task('b','Nur aktuell',null,'active'),
      task('c','Überfällig','2026-10-20'),
      task('d','Morgen','2026-11-01'),
      task('e','Heute mit Termin','2026-10-20'),
      task('f','Erledigt',day,'completed'),
    ];
    const segments=[
      segment('s1','e','2026-10-31T08:00:00Z','2026-10-31T09:00:00Z'),
      segment('s2','d','2026-11-01T08:00:00Z','2026-11-01T09:00:00Z'),
      segment('s3','f','2026-10-31T08:00:00Z','2026-10-31T09:00:00Z'),
    ];
    const agenda=buildTodayAgenda(day,tasks,segments,[]);
    expect(agenda.map(x=>x.title)).toEqual(['Heute ohne Uhrzeit','Heute mit Termin']);
    expect(agenda[0].allDay).toBe(true);
    expect(agenda[1].allDay).toBe(false);
  });

  it('sortiert ganztägige Elemente zuerst, dann Zeitsegmente und Outlook-Termine chronologisch',()=>{
    const agenda=buildTodayAgenda(day,[
      task('one','Lokale Aufgabe',day),
      task('two','Team-Aufgabe',null),
    ],[segment('s','two','2026-10-31T09:00:00Z','2026-10-31T09:30:00Z')],[
      event('later','Spätere Besprechung','2026-10-31T14:00:00Z','2026-10-31T15:00:00Z'),
      event('earlier','Frühe Besprechung','2026-10-31T07:00:00Z','2026-10-31T08:00:00Z'),
      event('whole','Ganztägige Veranstaltung','2026-10-30T22:00:00Z','2026-10-31T22:00:00Z',true),
      event('tomorrow','Erst morgen','2026-10-31T23:00:00Z','2026-11-01T00:00:00Z'),
    ]);
    // On Oct 31, Europe/Berlin is UTC+1. 23:00Z belongs to Nov 1.
    expect(agenda.map(x=>x.title)).toEqual([
      'Lokale Aufgabe','Ganztägige Veranstaltung','Frühe Besprechung',
      'Team-Aufgabe','Spätere Besprechung',
    ]);
    expect(agendaTimeLabel(agenda[0])).toBe('Heute geplant');
    expect(agendaTimeLabel(agenda[1])).toBe('Ganztägig');
    expect(agendaTimeLabel(agenda[2])).toMatch(/08:00.*09:00/);
  });

  it('zeigt mehrere Zeitfenster derselben Aufgabe einzeln statt Duplikaten der Aufgabenliste',()=>{
    const agenda=buildTodayAgenda(day,[task('a','Kundenticket',day)],[
      segment('1','a','2026-10-31T07:00:00Z','2026-10-31T07:30:00Z'),
      segment('2','a','2026-10-31T10:00:00Z','2026-10-31T10:30:00Z'),
    ],[]);
    expect(agenda.map(x=>x.key)).toEqual(['segment:1','segment:2']);
    expect(agenda.every(x=>x.taskId==='a')).toBe(true);
  });

  it('erfasst über Mitternacht laufende Termine und blendet morgige aus',()=>{
    const result=buildTodayAgenda(day,[],[],[
      event('a','Von gestern','2026-10-30T22:45:00Z','2026-10-31T01:00:00Z'),
      event('b','Morgen','2026-10-31T23:00:00Z','2026-11-01T00:00:00Z'),
    ]);
    expect(result.map(x=>x.title)).toEqual(['Von gestern']);
  });

  it('verwendet kalendarische Tagesgrenzen auch an der Zeitumstellung',()=>{
    const spring=localDayBounds('2026-03-29');
    const autumn=localDayBounds('2026-10-25');
    expect((spring.end.getTime()-spring.start.getTime())/3600000).toBe(23);
    expect((autumn.end.getTime()-autumn.start.getTime())/3600000).toBe(25);
  });
});
