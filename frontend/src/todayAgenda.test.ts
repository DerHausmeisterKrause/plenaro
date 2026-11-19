import { describe, expect, it } from 'vitest';
import { agendaTimeLabel, buildTodayAgenda, localDayBounds, outlookAllDayOccursOnDay } from './todayAgenda';
import type { TicketAgendaSegment } from './znunySyncApi';
import type { OutlookEvent } from './graphApi';

const day = '2026-10-31';
const segment=(id:string,ticketId:string,start:string,end:string,
  stateType='open'):TicketAgendaSegment=>({
  id,ticketId,ticketNumber:ticketId,title:'Ticket '+ticketId,queue:'IT',
  ownerId:42,responsibleId:null,createdAt:null,changedAt:null,
  state:'open',stateType,startsAt:start,endsAt:end,note:'Lokaler Termin',participants:[]
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
      // 31.10. midnight in Germany: UTC+1 (winter time).
      event('whole','Ganztägig','2026-10-30T23:00:00Z','2026-10-31T23:00:00Z',true),
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

  it('zeigt einen ganztägigen Outlook-Feiertag nicht fälschlich am Folgetag',()=>{
    // Original bug: Graph returns a UTC-midnight all-day range.
    // At 2026-11-19T00:00:00Z it is 01:00 on 19.11. in Germany,
    // but Outlook's all-day END DATE is exclusive (event is only 18.11.).
    if(Intl.DateTimeFormat().resolvedOptions().timeZone!=='Europe/Berlin')return;
    const holiday=event('buss','Buß- und Bettag (§)',
      '2026-11-18T00:00:00Z','2026-11-19T00:00:00Z',true);
    expect(buildTodayAgenda('2026-11-18',[],[holiday]).map(e=>e.title))
      .toEqual(['Buß- und Bettag (§)']);
    expect(buildTodayAgenda('2026-11-19',[],[holiday])).toEqual([]);
    expect(outlookAllDayOccursOnDay(holiday,'2026-11-19')).toBe(false);
  });

  it('berücksichtigt Mitternacht in Berlin und die Sommerzeit korrekt',()=>{
    if(Intl.DateTimeFormat().resolvedOptions().timeZone!=='Europe/Berlin')return;
    // Correct Berlin-midnight UTC instants in winter.
    const winter=event('winter','Winterfeiertag',
      '2026-11-17T23:00:00Z','2026-11-18T23:00:00Z',true);
    expect(outlookAllDayOccursOnDay(winter,'2026-11-18')).toBe(true);
    expect(outlookAllDayOccursOnDay(winter,'2026-11-19')).toBe(false);
    // March DST change: a calendar day can be only 23 hours long.
    const spring=event('spring','Sommerzeit-Tag',
      '2026-03-28T23:00:00Z','2026-03-29T22:00:00Z',true);
    expect(outlookAllDayOccursOnDay(spring,'2026-03-29')).toBe(true);
    expect(outlookAllDayOccursOnDay(spring,'2026-03-30')).toBe(false);
  });

  it('zeigt mehrtägige Ganztagstermine an allen Tagen bis zum exklusiven Ende',()=>{
    if(Intl.DateTimeFormat().resolvedOptions().timeZone!=='Europe/Berlin')return;
    const multi=event('multi','Mehrere Tage',
      '2026-11-18T00:00:00Z','2026-11-21T00:00:00Z',true);
    for(const date of ['2026-11-18','2026-11-19','2026-11-20'])
      expect(outlookAllDayOccursOnDay(multi,date)).toBe(true);
    expect(outlookAllDayOccursOnDay(multi,'2026-11-17')).toBe(false);
    expect(outlookAllDayOccursOnDay(multi,'2026-11-21')).toBe(false);
  });

  it('belässt normale Termine mit überlappendem Folgekalendertag unverändert',()=>{
    if(Intl.DateTimeFormat().resolvedOptions().timeZone!=='Europe/Berlin')return;
    const timed=event('late','Später Termin',
      '2026-11-18T22:30:00Z','2026-11-18T23:30:00Z',false);
    expect(buildTodayAgenda('2026-11-18',[],[timed]).length).toBe(1);
    expect(buildTodayAgenda('2026-11-19',[],[timed]).length).toBe(1);
  });

  it('ignoriert defekte Ganztagsdaten und übernimmt WPF-Eintages-Fallback',()=>{
    if(Intl.DateTimeFormat().resolvedOptions().timeZone!=='Europe/Berlin')return;
    const broken=event('bad','Ungültig','nonsense','2026-11-19T00:00:00Z',true);
    expect(outlookAllDayOccursOnDay(broken,'2026-11-18')).toBe(false);
    const single=event('same','Ein Tag',
      '2026-11-18T00:00:00Z','2026-11-18T00:00:00Z',true);
    expect(outlookAllDayOccursOnDay(single,'2026-11-18')).toBe(true);
    expect(outlookAllDayOccursOnDay(single,'2026-11-19')).toBe(false);
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
