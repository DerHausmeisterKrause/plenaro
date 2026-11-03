import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Pencil, Save, Trash2, X } from 'lucide-react';
import { localDate } from './tasksApi';
import {graphStatus,readOutlookWeek,type OutlookEvent} from './graphApi';
import {availabilityOverlaps,buildAvailabilitySlots} from './znunyAvailability';
import {
  deleteTicketSegment,getTicketSegments,planTicketSegment,updateTicketSegment,
  readTicketDayAvailability,type TicketSegment,type BusyInterval,
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
  const [localBusy,setLocalBusy]=useState<BusyInterval[]|null>(null);
  const [outlook,setOutlook]=useState<OutlookEvent[]|null>(null);
  const [availabilityStatus,setAvailabilityStatus]=useState('Verfügbarkeit wird geladen …');
  const [revision,setRevision]=useState(0);

  async function reload(id:string){
    setSegments(await getTicketSegments(id));
    setRevision(n=>n+1);
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

  useEffect(()=>{
    const timer=window.setInterval(()=>{
      if(!document.hidden)setRevision(n=>n+1);
    },60_000);
    return()=>window.clearInterval(timer);
  },[]);
  useEffect(()=>{
    let active=true;
    setLocalBusy(null);setOutlook(null);
    setAvailabilityStatus('Verfügbarkeit wird geladen …');
    if(!ticketId)return()=>{active=false;};
    const from=new Date(day+'T00:00:00');
    const to=new Date(from);
    to.setDate(to.getDate()+1);
    void readTicketDayAvailability(ticketId,day).then(data=>{
      if(!active)return;
      if(data.hasMore){
        setLocalBusy(null);
        setAvailabilityStatus('Zu viele lokale Segmente: keine vollständige Auslastungsanzeige.');
      }else{
        setLocalBusy(data.intervals);
        setAvailabilityStatus('Plenaro-Segmente geladen. Outlook wird geprüft …');
      }
    }).catch(e=>{if(active){
      setAvailabilityStatus(e instanceof Error?e.message:'Lokale Verfügbarkeit unbekannt.');
    }});
    void graphStatus().then(async status=>{
      if(!active)return;
      if(!status.connected){
        setAvailabilityStatus(old=>old.includes('Outlook wird geprüft')
          ?'Nur lokale Plenaro-Segmente – Outlook nicht verbunden.':'Outlook nicht verbunden; lokale Verfügbarkeit wird separat geladen.');
        return;
      }
      try{
        const result=await readOutlookWeek(from,to);
        if(!active)return;
        if(result.hasMore){
          setOutlook(null);
          setAvailabilityStatus('Outlook liefert weitere Seiten. Keine vollständige Kalenderauslastung möglich.');
        }else{
          setOutlook(result.events);
          setAvailabilityStatus('Plenaro-Segmente und eigener Outlook-Kalender geladen.');
        }
      }catch{
        if(active)setAvailabilityStatus('Outlook derzeit nicht verfügbar. Nur lokale Segmente berücksichtigt.');
      }
    }).catch(()=>{if(active)setAvailabilityStatus('Outlook-Verbindung nicht prüfbar. Nur lokale Segmente berücksichtigt.');});
    return()=>{active=false;};
  },[ticketId,day,revision]);

  const bar=useMemo(()=>buildAvailabilitySlots(day,localBusy,outlook,editing??undefined),
    [day,localBusy,outlook,editing]);
  const newStart=new Date(day+'T'+start+':00');
  const newEnd=new Date(day+'T'+end+':00');
  const validDates=Number.isFinite(newStart.getTime())&&Number.isFinite(newEnd.getTime());
  const fromIso=validDates?newStart.toISOString():'';
  const toIso=validDates?newEnd.toISOString():'';
  const localConflict=validDates&&localBusy!==null&&availabilityOverlaps(
    fromIso,toIso,localBusy.filter(item=>item.id!==editing));
  const outlookConflict=validDates&&outlook!==null&&availabilityOverlaps(
    fromIso,toIso,
    outlook.filter(item=>!['free','workingelsewhere'].includes(item.showAs.toLowerCase()))
      .map(item=>({startsAt:item.start,endsAt:item.end})));
  const selectedConflict=localConflict||outlookConflict;

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
    if(selectedConflict){
      setError(localConflict?'Der Zeitraum überschneidet sich mit einem eigenen Plenaro-Segment.':
        'Der Zeitraum ist laut Outlook bereits belegt.');return;
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
        aria-label="Auslastung zwischen 06:00 und 18:00 Uhr: grün lokal frei, rot belegt, grau unbekannt">
        {bar.map((slot,index)=><span key={index}
          className={slot.state+(slot.time>=start&&slot.time<end?' selected':'')}
          title={slot.time+' – '+slot.state+' ('+slot.source+')'}/>)}
      </div>
      <div className="znuny-availability-legend">
        <span><i className="free"/>Frei</span>
        <span><i className="busy"/>Belegt</span>
        <span><i className="unknown"/>Unbekannt</span>
      </div>
      <p className="classic-wpf-help" role="status">{availabilityStatus}
        {' '}Grün bedeutet nur: in den geladenen Quellen nicht belegt.
        Teilnehmer-Kalender werden ohne eigene Berechtigung nicht geprüft.
      </p>
      {selectedConflict&&<p className="classic-wpf-conflict" role="alert">
        {localConflict?'Zeitüberschneidung mit einem deiner Plenaro-Segmente.':
          'Der gewählte Zeitraum überschneidet sich mit einem Outlook-Termin.'}
      </p>}
      <form className="classic-wpf-segment-form" onSubmit={e=>void save(e)}>
        <div className="classic-wpf-segment-datetime">
          <label><span className="sr-only">Segmentdatum</span>
            <input type="date" value={day} required min="2000-01-01" max="2099-12-31"
              onChange={e=>setDay(e.target.value)}/></label>
          <label><span className="sr-only">Startzeit</span>
            <select aria-label="Startzeit" value={start} onChange={e=>{
              const after=slots.indexOf(end);
              setStart(e.target.value);
              if(after<=slots.indexOf(e.target.value)){
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
        <button className="button button-primary" type="submit"
          disabled={busy||selectedConflict||localBusy===null||!validDates||newEnd<=newStart}>
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
