import type {OutlookEvent} from './graphApi';
import type {BusyInterval} from './znunySyncApi';

export type AvailabilitySlot = {
  time:string;state:'free'|'busy'|'unknown';
  source:'Plenaro'|'Outlook'|'Plenaro & Outlook'|'unbekannt';
};
const overlaps=(a:number,z:number,b:number,y:number)=>a<y&&b<z;
const minutes=(d:Date)=>d.getHours()*60+d.getMinutes();
export const availabilityOverlaps=(start:string,end:string,intervals:ReadonlyArray<{
  startsAt:string;endsAt:string;
}>)=>{
  const a=Date.parse(start),z=Date.parse(end);
  return intervals.some(x=>overlaps(a,z,Date.parse(x.startsAt),Date.parse(x.endsAt)));
};
export function buildAvailabilitySlots(day:string,local:BusyInterval[]|null,
  outlook:OutlookEvent[]|null,excludeId?:string):AvailabilitySlot[] {
  const localKnown=local!==null;
  return Array.from({length:48},(_,index)=>{
    const start=new Date(day+'T06:00:00');
    start.setMinutes(start.getMinutes()+index*15);
    const finish=new Date(start.getTime()+15*60_000);
    const a=start.getTime(),z=finish.getTime();
    const localBusy=(local??[]).some(item=>item.id!==excludeId&&
      overlaps(a,z,Date.parse(item.startsAt),Date.parse(item.endsAt)));
    const graphBusy=(outlook??[]).some(item=>{
      if(/^(free|workingElsewhere)$/i.test(item.showAs))return false;
      return overlaps(a,z,Date.parse(item.start),Date.parse(item.end));
    });
    const state=localBusy||graphBusy?'busy':localKnown?'free':'unknown';
    const source=localBusy&&graphBusy?'Plenaro & Outlook':localBusy?'Plenaro':
      graphBusy?'Outlook':localKnown?'Plenaro':'unbekannt';
    return {time:[String(Math.floor(minutes(start)/60)).padStart(2,'0'),
      String(minutes(start)%60).padStart(2,'0')].join(':'),state,source};
  });
}
