import { describe, expect, it } from 'vitest';
import { agendaTimeLabel, buildTodayAgenda, localDayBounds } from './todayAgenda';
import type { TicketAgendaSegment } from './znunySyncApi';
import type { OutlookEvent } from './graphApi';

const day = '2026-10-31';
const segment=(id:string,ticketId:string,start:string,end:string,
  stateType='open'):TicketAgendaSegment=>({
  id,ticketId,ticketNumber:ticketId,title:'Ticket '+ticketId,queue:'IT',
  ownerId:42,responsibleId:null,createdAt:null,changedAt:null,
  state:'open',stateType,startsAt:start,endsAt:end,note:'Lokaler Termin'
});
const event=(id:string,subject:string,start:string,end:string,isAllDay=false):OutlookEvent=>({
  id,subject,start,end,isAllDay,showAs:'busy',location:'Büro',
  webLink:'https://outlook.office.com/calendar/item'
});

describe('Heute: ausschließlich Znuny-Ticketsegmente und Outlook-Termine',()=>{
  it('ignoriert andere Tage, geschlossene Tickets und doppelte Segmentdatensätze',()=>{
    const agenda=buildTodayAgenda(day,[
      segment('one','100','2026-10-31T08:00:00Z','2026-10-31T09:00:00Z'),
      segment('one','100','2026-10-31T08:00:00Z','2026-10-31T09:00:00Z'),
      segment('tomorrow','101','2026-11-01T08:00:00Z','2026-11-01T09:00:00Z'),
      segment('closed','102','2026-10-31T08:00:00Z','2026-10-31T09:00:00Z','closed'),
    ],[]);
    expect(agenda.map(a=>a.key)).toEqual(['ticket-segment:one']);
    expect(agenda[0].ticketId).toBe('100');
  });

  it('zeigt mehrere heute geplante Segmente des gleichen Tickets einzeln',()=>{
    const agenda=buildTodayAgenda(day,[
      segment('a','100','2026-10-31T07:00:00Z','2026-10-31T08:00:00Z'),
      segment('b','100','2026-10-31T10:00:00Z','2026-10-31T10:30:00Z'),
    ],[]);
    expect(agenda.map(a=>a.key)).toEqual(['ticket-segment:a','ticket-segment:b']);
  });

  it('sortiert ganztägige Outlook-Termine zuerst, dann Ticketsegmente und Termine chronologisch',()=>{
    const agenda=buildTodayAgenda(day,[
      segment('ticket','201','2026-10-31T08:00:00Z','2026-10-31T09:00:00Z'),
    ],[
      event('later','Später Termin','2026-10-31T10:00:00Z','2026-10-31T11:00:00Z'),
      event('early','Früher Termin','2026-10-31T06:00:00Z','2026-10-31T07:00:00Z'),
      event('whole','Ganztägig','2026-10-30T22:00:00Z','2026-10-31T22:00:00Z',true),
    ]);
    expect(agenda.map(a=>a.title)).toEqual([
      'Ganztägig','Früher Termin','Ticket 201','Später Termin',
    ]);
    expect(agendaTimeLabel(agenda[0])).toBe('Ganztägig');
    expect(agendaTimeLabel(agenda[2])).toMatch(/\d\d:\d\d.*\d\d:\d\d/);
  });

  it('erfasst Zeitfenster über Mitternacht und verwirft unsichere Outlook-Links',()=>{
    const entries=buildTodayAgenda(day,[
      segment('late','999','2026-10-30T22:00:00Z','2026-10-31T01:00:00Z'),
    ],[ {...event('unsafe','Termin','2026-10-31T08:00:00Z',
      '2026-10-31T09:00:00Z'),webLink:'javascript:alert(1)'} ]);
    expect(entries.length).toBe(2);
    expect(entries.find(e=>e.kind==='outlook')?.webLink).toBeNull();
  });

  it('berechnet den lokalen Tageswechsel bei Sommer- und Winterzeit richtig',()=>{
    const spring=localDayBounds('2026-03-29');
    const autumn=localDayBounds('2026-10-25');
    const h=(s:Date,e:Date)=>(e.getTime()-s.getTime())/3600000;
    if(Intl.DateTimeFormat().resolvedOptions().timeZone==='Europe/Berlin'){
      expect(h(spring.start,spring.end)).toBe(23);
      expect(h(autumn.start,autumn.end)).toBe(25);
    }
  });
});
