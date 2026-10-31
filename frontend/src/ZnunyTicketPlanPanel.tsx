import { useEffect, useState, type FormEvent } from 'react';
import { CalendarClock, Trash2 } from 'lucide-react';
import {
  deleteTicketSegment,getTicketSegments,planTicketSegment,type TicketSegment,
} from './znunySyncApi';

function defaultTime(addMinutes: number) {
  const date = new Date(Date.now() + addMinutes * 60_000);
  // datetime-local expects the *browser's local wall clock*, not UTC.
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 16);
}

/** User-scoped local plan: NO Znuny API writes, no remote ticket bookings. */
export function ZnunyTicketPlanPanel({ticketId,onChanged}:{
  ticketId:string|null;onChanged?:()=>void;
}) {
  const [segments,setSegments]=useState<TicketSegment[]>([]);
  const [start,setStart]=useState(()=>defaultTime(30));
  const [end,setEnd]=useState(()=>defaultTime(60));
  const [note,setNote]=useState('');
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('');

  async function reload(id:string) {
    setSegments(await getTicketSegments(id));
  }
  useEffect(()=>{
    let active=true;
    setSegments([]);setError('');setNotice('');
    if (ticketId) void getTicketSegments(ticketId)
      .then(items=>{if(active)setSegments(items);})
      .catch(e=>{if(active)setError(e instanceof Error?e.message:'Planung nicht verfügbar.');});
    return ()=>{active=false;};
  },[ticketId]);

  async function add(e:FormEvent) {
    e.preventDefault();
    if(!ticketId||busy)return;
    const begins=new Date(start),ends=new Date(end);
    if(!Number.isFinite(begins.getTime())||!Number.isFinite(ends.getTime())||
      ends<=begins||ends.getTime()-begins.getTime()>7*24*3600_000){
      setError('Bitte einen gültigen Zeitraum bis maximal sieben Tage wählen.');return;
    }
    setBusy(true);setError('');setNotice('');
    try {
      await planTicketSegment(ticketId,{startsAt:begins.toISOString(),
        endsAt:ends.toISOString(),note});
      await reload(ticketId);
      setNote('');setNotice('Ticket-Zeitsegment geplant.');
      onChanged?.();
    } catch(e){setError(e instanceof Error?e.message:'Segment konnte nicht gespeichert werden.');}
    finally{setBusy(false);}
  }
  async function remove(segmentId:string) {
    if(!ticketId||busy)return;
    setBusy(true);setError('');setNotice('');
    try{
      await deleteTicketSegment(ticketId,segmentId);
      await reload(ticketId);
      setNotice('Segment entfernt.');onChanged?.();
    }catch(e){setError(e instanceof Error?e.message:'Löschen fehlgeschlagen.');}
    finally{setBusy(false);}
  }
  if(!ticketId)return null;
  const clock=(time:string)=>new Date(time).toLocaleString('de-DE',{
    dateStyle:'short',timeStyle:'short'
  });
  return <section className="content-card um-panel znuny-ticket-plan" aria-label="Ticket-Zeitplanung">
    <div className="um-section-title"><CalendarClock size={18}/><h3>Ticket-Zeitplanung</h3></div>
    <p className="um-hint">Zeitsegmente für dieses Znuny-Ticket lokal planen.
      Die Planung wird nur für dich gespeichert und erscheint am jeweiligen Tag unter „Heute“.
      Keine automatische Zeitbuchung im Ticketsystem.</p>
    {error&&<p role="alert" className="um-feedback failure">{error}</p>}
    {notice&&<p role="status" className="um-feedback success">{notice}</p>}
    <form className="um-form" onSubmit={e=>void add(e)}>
      <label htmlFor="ticket-plan-start">Beginn</label>
      <input id="ticket-plan-start" type="datetime-local" required
        value={start} onChange={e=>setStart(e.target.value)}/>
      <label htmlFor="ticket-plan-end">Ende</label>
      <input id="ticket-plan-end" type="datetime-local" required
        value={end} onChange={e=>setEnd(e.target.value)}/>
      <label htmlFor="ticket-plan-note">Notiz (optional)</label>
      <input id="ticket-plan-note" maxLength={2000}
        value={note} onChange={e=>setNote(e.target.value)}/>
      <button type="submit" className="button button-primary" disabled={busy}>
        <CalendarClock size={16}/>Segment planen
      </button>
    </form>
    {segments.length>0&&<div className="znuny-ticket-segment-list">
      <h4>Meine geplanten Segmente</h4>
      {segments.map(segment=><div className="znuny-ticket-segment" key={segment.id}>
        <span>{clock(segment.startsAt)} – {clock(segment.endsAt)}
          {segment.note&&<small>{segment.note}</small>}</span>
        <button type="button" className="button button-secondary"
          aria-label="Segment entfernen" disabled={busy}
          onClick={()=>void remove(segment.id)}><Trash2 size={15}/></button>
      </div>)}
    </div>}
  </section>;
}
