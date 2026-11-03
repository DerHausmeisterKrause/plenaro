import {describe,expect,it} from 'vitest';
import {availabilityOverlaps,buildAvailabilitySlots} from './znunyAvailability';
import type {BusyInterval} from './znunySyncApi';
import type {OutlookEvent} from './graphApi';
const iso=(time:string)=>new Date('2026-11-02T'+time+':00').toISOString();
const booked:BusyInterval[]=[
  {id:'local-1',startsAt:iso('09:00'),endsAt:iso('09:30')}
];
const outlook:OutlookEvent[]=[
  {id:'meeting',subject:'Termin',start:iso('10:00'),end:iso('10:45'),
    isAllDay:false,showAs:'busy',location:'',webLink:null},
  {id:'free',subject:'Nur Info',start:iso('11:00'),end:iso('11:30'),
    isAllDay:false,showAs:'free',location:'',webLink:null}
];

describe('WPF-Planungsbalken 06–18 Uhr',()=>{
  it('shows 48 quarter-hour slots with green free and red local+Outlook busy',()=>{
    const slots=buildAvailabilitySlots('2026-11-02',booked,outlook);
    expect(slots).toHaveLength(48);
    expect(slots[0]).toEqual({time:'06:00',state:'free',source:'Plenaro'});
    expect(slots[12]).toMatchObject({time:'09:00',state:'busy',source:'Plenaro'});
    expect(slots[13].state).toBe('busy');
    expect(slots[14].state).toBe('free');
    expect(slots[16]).toMatchObject({time:'10:00',state:'busy',source:'Outlook'});
    expect(slots[19].state).toBe('free');
    expect(slots[20].state).toBe('free');
    expect(slots[47].time).toBe('17:45');
  });
  it('excludes own segment during editing, without concealing other conflicts',()=>{
    const result=buildAvailabilitySlots('2026-11-02',booked,outlook,'local-1');
    expect(result[12].state).toBe('free');
    expect(result[16].state).toBe('busy');
  });
  it('indicates unknown availability without own local data instead of guessing free',()=>{
    const result=buildAvailabilitySlots('2026-11-02',null,null);
    expect(result.every(slot=>slot.state==='unknown')).toBe(true);
    expect(buildAvailabilitySlots('2026-11-02',[],null)[12].state).toBe('free');
  });
  it('checks half-open overlap: touching endpoints are not conflicts',()=>{
    expect(availabilityOverlaps(iso('09:30'),iso('10:00'),booked)).toBe(false);
    expect(availabilityOverlaps(iso('09:15'),iso('09:45'),booked)).toBe(true);
  });
});
