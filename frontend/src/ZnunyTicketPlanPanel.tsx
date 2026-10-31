import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Pencil, Save, Trash2, X } from 'lucide-react';
import { localDate } from './tasksApi';
import {
  deleteTicketSegment,getTicketSegments,planTicketSegment,updateTicketSegment,
  type TicketSegment,
} from './znunySyncApi';

const slots=Array.from({length:49},(_,index)=>{
  const h=6+Math.floor(index/4),m=(index%4)*15;
  return String(h).padStart(2,'0')+':'+String(m).padStart(2,'0');
});
const clock=(value:string)=>{
  const date=new Date(value);
  return [date.getHours(),date.getMinutes()].map(n=>String(n).padStart(2,'0')).join(':');
};
const parseDate=(value:string)=>new Date(value+'T00:00:00');
const duration=(a:string,b:string)=>Math.round(
  (new Date(b).getTime()-new Date(a).getTime())/60000
);
const fmtDay=(day:string)=>new Intl.DateTimeFormat('de-DE',
  {weekday:'long',day:'2-digit',month:'2-digit',year:'numeric'}).format(parseDate(day));

/** Desktop TaskDetailPanel.xaml: editable segment form, availability strip,
 *  existing segment editors, attendee text. No Outlook write is implied. */
export function ZnunyTicketPlanPanel({ticketId,onChanged}:{
  ticketId:string|null;onChanged?:()=>void;
}) {
  const [segments,setSegments]=useState<TicketSegment[]>([]);
  const [editing,setEditing]=useState<string|null>(null);
  const [day,setDay]=useState(()=>localDate());
  const [start,setStart]=useState('09:00');
  const [end,setEnd]=useState('09:30');
  const [note,setNote]=useState('');
  const [participants,setParticipants]=useState('');
  const [busy,setBusy]=useState(false);
  const [loading,setLoading]=useState(false);
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('');

  async function reload(id:string){
    setSegments(await getTicketSegments(id));
  }
  useEffect(()=>{
    let active=true;
    setSegments([]);setEditing(null);setError('');setNotice('');
    if(ticketId){
      setLoading(true);
      void getTicketSegments(ticketId)
        .then(rows=>{if(active){setSegments(rows);setLoading(false);}})
        .catch(e=>{if(active){
          setError(e instanceof Error?e.message:'Segmente nicht verfügbar.');setLoading(false);
        }});
    }
    return()=>{active=false;};
  },[ticketId]);

  const dayEntries=useMemo(()=>segments.filter(s=>
    new Date(s.startsAt)<new Date(day+'T23:59:59') &&
    new Date(s.endsAt)>parseDate(day)),[segments,day]);

  function reset(){
    setEditing(null);setNote('');setParticipants('');
    setDay(localDate());setStart('09:00');setEnd('09:30');
  }
  function beginEdit(item:TicketSegment){
    setEditing(item.id);setDay(localDate(new Date(item.startsAt)));
    setStart(clock(item.startsAt));setEnd(clock(item.endsAt));
    setNote(item.note);setParticipants(item.participants.join(', '));
    setError('');setNotice('');
  }

  async function save(e:FormEvent){
    e.preventDefault();
    if(!ticketId||busy)return;
    const begins=new Date(day+'T'+start+':00');
    const ends=new Date(day+'T'+end+':00');
    const emails=[...new Set(participants.split(/[,;\n]/)
      .map(x=>x.trim()).filter(Boolean))];
    if(!Number.isFinite(begins.getTime())||!Number.isFinite(ends.getTime())||
      ends<=begins){
      setError('Startzeit muss vor Endzeit liegen.');return;
    }
    if(emails.length>20||emails.some(x=>x.length>254||!/^\S+@\S+\.\S+$/.test(x))){
      setError('Bitte maximal 20 gültige Teilnehmer-E-Mail-Adressen eingeben.');return;
    }
    setBusy(true);setError('');setNotice('');
    try{
      const data={startsAt:begins.toISOString(),endsAt:ends.toISOString(),
        note:note.trim(),participants:emails};
      if(editing)await updateTicketSegment(ticketId,editing,data);
      else await planTicketSegment(ticketId,data);
      await reload(ticketId);reset();setNotice('Segment gespeichert.');
      onChanged?.();
    }catch(e){setError(e instanceof Error?e.message:'Segment konnte nicht gespeichert werden.');}
    finally{setBusy(false);}
  }
  async function remove(item:TicketSegment){
    if(!ticketId||busy||!window.confirm('Zeitsegment endgültig löschen?'))return;
    setBusy(true);setError('');setNotice('');
    try{
      await deleteTicketSegment(ticketId,item.id);
      await reload(ticketId);
      reset();setNotice('Segment gelöscht.');onChanged?.();
    }catch(e){setError(e instanceof Error?e.message:'Löschen fehlgeschlagen.');}
    finally{setBusy(false);}
  }
  if(!ticketId)return null;
  const displayTime=(value:string)=>new Date(value).toLocaleString('de-DE',
    {dateStyle:'short',timeStyle:'short'});
  return <section className="content-card classic-wpf-segment-card" aria-label="Planung (Segmente)">
    <h3>Planung (Segmente)</h3>
    <div className="classic-wpf-segment-box">
      <div className="classic-wpf-segment-title">
        <strong>{editing?'Segment bearbeiten':'Neues Segment'}</strong>
        {editing&&<button type="button" className="button button-secondary"
          onClick={reset} aria-label="Bearbeitung abbrechen"><X size={14}/></button>}
      </div>
      <strong className="classic-wpf-availability-heading">Verfügbarkeit · {fmtDay(day)}</strong>
      <div className="classic-wpf-time-labels">
        {Array.from({length:13},(_,i)=><span key={i}>{String(i+6).padStart(2,'0')}:00</span>)}
      </div>
      <div className="classic-wpf-availability" role="img"
        aria-label="Zeitplan, hell markierte Bereiche zeigen geplante Segmente dieses Tickets">
        {slots.slice(0,48).map(slot=>{
          const current=new Date(day+'T'+slot+':00').getTime();
          const taken=dayEntries.some(item=>item.id!==editing &&
            new Date(item.startsAt).getTime()<=current &&
            new Date(item.endsAt).getTime()>current);
          return <span key={slot} className={taken?'occupied':''}/>;
        })}
      </div>
      <p className="classic-wpf-help">Lokale Segmente dieses Tickets. Outlook- und
        Teilnehmer-Verfügbarkeit derzeit nicht verfügbar.</p>
      <form className="classic-wpf-segment-form" onSubmit={e=>void save(e)}>
        <div className="classic-wpf-segment-datetime">
          <label><span className="sr-only">Segmentdatum</span>
            <input type="date" value={day} required min="2000-01-01" max="2099-12-31"
              onChange={e=>setDay(e.target.value)}/></label>
          <label><span className="sr-only">Startzeit</span>
            <select aria-label="Startzeit" value={start} onChange={e=>{
              const before=slots.indexOf(start),after=slots.indexOf(end);
              setStart(e.target.value);
              if(after===before+2||after<=slots.indexOf(e.target.value)){
                setEnd(slots[Math.min(slots.length-1,slots.indexOf(e.target.value)+2)]);
              }
            }}>{slots.slice(0,-1).map(t=><option key={t}>{t}</option>)}</select></label>
          <label><span className="sr-only">Endzeit</span>
            <select aria-label="Endzeit" value={end} onChange={e=>setEnd(e.target.value)}>
              {slots.slice(1).map(t=><option key={t}>{t}</option>)}
            </select></label>
        </div>
        <label htmlFor="wpf-segment-note">Kurzbeschreibung</label>
        <input id="wpf-segment-note" maxLength={2000}
          value={note} onChange={e=>setNote(e.target.value)}/>
        <label htmlFor="wpf-segment-attendees">Teilnehmer (E-Mail)</label>
        <input id="wpf-segment-attendees" value={participants}
          onChange={e=>setParticipants(e.target.value)}/>
        <p className="classic-wpf-help">Mehrere Adressen mit Komma oder Semikolon trennen.</p>
        {error&&<p role="alert" className="um-feedback failure">{error}</p>}
        {notice&&<p role="status" className="um-feedback success">{notice}</p>}
        <button className="button button-primary" type="submit" disabled={busy}>
          <Save size={14}/>{editing?'Änderungen speichern':'Segment speichern'}
        </button>
      </form>
    </div>
    {loading&&<p role="status">Segmente laden …</p>}
    <div className="classic-wpf-segment-list">
      {segments.map((item,index)=><div className="classic-wpf-segment-box"
        key={item.id}>
        <strong>Segment {index+1}</strong>
        <p>{displayTime(item.startsAt)} – {displayTime(item.endsAt)}
          <span className="classic-wpf-help"> · Dauer: {duration(item.startsAt,item.endsAt)} min</span></p>
        {item.note&&<p>{item.note}</p>}
        {item.participants.length>0&&<p className="classic-wpf-help">
          Teilnehmer: {item.participants.join(', ')}</p>}
        <div className="classic-wpf-actions">
          <button className="button button-secondary" type="button" disabled={busy}
            onClick={()=>beginEdit(item)}><Pencil size={14}/>Bearbeiten</button>
          <button className="button button-secondary" type="button" disabled={busy}
            onClick={()=>void remove(item)}><Trash2 size={14}/>Löschen</button>
        </div>
      </div>)}
    </div>
  </section>;
}
