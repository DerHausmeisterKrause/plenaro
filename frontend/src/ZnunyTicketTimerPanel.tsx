import {useEffect,useState,type FormEvent} from 'react';
import {Check,Clock3,Play,RefreshCw,Square,X} from 'lucide-react';
import {
  bookZnunyTime,readTicketTime,readZnunyBookingFields,readZnunyBookings,
  reconcileZnunyBooking,setTicketUnbookedSeconds,startTicketTime,stopTicketTime,
  type TicketTimeSummary,type ZnunyBooking,type ZnunyBookingFields,
  type ZnunyFieldOption,
} from './znunySyncApi';
import {secondsToClock} from './taskWorkApi';

export function parseTimerDuration(value:string):number|null {
  const match=/^(\d{1,3}):([0-5]\d):([0-5]\d)$/.exec(value.trim());
  if(!match)return null;
  const seconds=Number(match[1])*3600+Number(match[2])*60+Number(match[3]);
  return seconds<=864000?seconds:null;
}
export function roundedBookingMinutes(seconds:number):number {
  return seconds>0?Math.ceil(seconds/900)*15:0;
}
function pickerOptions(options:ZnunyFieldOption[],current:string):ZnunyFieldOption[] {
  return current&&!options.some(x=>x.key===current)
    ?[{key:current,displayText:current+' (aktueller Wert)'},...options]:options;
}

/** WPF: the displayed clock is the *unbooked* amount; click to correct
 * HH:MM:SS directly. Only the transfer to Znuny rounds up in 15m units. */
export function ZnunyTicketTimerPanel({ticketId,defaultDescription='Zeitbuchung'}:{
  ticketId:string|null;defaultDescription?:string;
}) {
  const [summary,setSummary]=useState<TicketTimeSummary|null>(null);
  const [tick,setTick]=useState(()=>Date.now());
  const [editing,setEditing]=useState(false);
  const [clockInput,setClockInput]=useState('00:00:00');
  const [fields,setFields]=useState<ZnunyBookingFields|null>(null);
  const [bookings,setBookings]=useState<ZnunyBooking[]>([]);
  const [cost,setCost]=useState('');
  const [order,setOrder]=useState('');
  const [costSearch,setCostSearch]=useState('');
  const [orderSearch,setOrderSearch]=useState('');
  const [description,setDescription]=useState(defaultDescription);
  const [bookingNote,setBookingNote]=useState('');
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  const [fieldsError,setFieldsError]=useState('');
  const [notice,setNotice]=useState('');

  useEffect(()=>{
    const interval=window.setInterval(()=>setTick(Date.now()),1000);
    return()=>window.clearInterval(interval);
  },[]);
  useEffect(()=>{
    let active=true;
    setSummary(null);setFields(null);setBookings([]);setCost('');setOrder('');
    setDescription(defaultDescription);setEditing(false);
    setNotice('');setError('');setFieldsError('');
    if(ticketId) {
      void readTicketTime(ticketId).then(data=>{if(active)setSummary(data);})
        .catch(e=>{if(active)setError(e instanceof Error?e.message:'Timer nicht verfügbar.');});
      void readZnunyBookings(ticketId).then(data=>{if(active)setBookings(data);})
        .catch(()=>{ /* A temporary history error must not hide the timer. */ });
      void readZnunyBookingFields(ticketId).then(data=>{
        if(!active)return;
        setFields(data);
        const cc=data.costCenter||data.costCenterOptions.find(x=>x.key==='00000')?.key||'';
        setCost(cc);setOrder(data.order);
      }).catch(e=>{if(active)setFieldsError(
        e instanceof Error?e.message:'Kostenstellen und Aufträge nicht verfügbar.');});
    }
    return()=>{active=false;};
  },[ticketId]);

  async function update(task:()=>Promise<TicketTimeSummary>,message:string){
    if(busy||!ticketId)return false;
    setBusy(true);setError('');setNotice('');
    try{
      const value=await task();
      setSummary(value);setTick(Date.now());setNotice(message);
      return true;
    }catch(e){
      setError(e instanceof Error?e.message:'Timer konnte nicht geändert werden.');
      return false;
    }finally{setBusy(false);}
  }
  async function correct(e:FormEvent){
    e.preventDefault();
    const target=parseTimerDuration(clockInput);
    if(target===null){setError('Dauer bitte als Stunden:Minuten:Sekunden (z. B. 01:15:30) angeben.');return;}
    if(!ticketId)return;
    const ok=await update(()=>setTicketUnbookedSeconds(ticketId,target),'Timerzeit gespeichert.');
    if(ok)setEditing(false);
  }
  async function refresh(){
    if(!ticketId)return;
    const [time,history]=await Promise.all([
      readTicketTime(ticketId),readZnunyBookings(ticketId)]);
    setSummary(time);setBookings(history);setTick(Date.now());
  }
  async function transfer(){
    if(!ticketId||busy||!summary||!fields)return;
    const rounded=roundedBookingMinutes(unbooked);
    if(rounded<=0)return;
    if(!window.confirm('Jetzt '+rounded+' Minuten in Znuny buchen? Die tatsächliche Zeit wird auf volle 15 Minuten aufgerundet.'))return;
    setBusy(true);setError('');setNotice('');
    try{
      // WPF stops a running timer before booking. The server reads its
      // authoritative stopped duration and performs the 15-minute ceiling.
      if(summary.runningSince)await stopTicketTime(ticketId);
      const result=await bookZnunyTime(ticketId,{
        costCenter:cost,order,shortDescription:description.trim()||'Zeitbuchung',
        note:bookingNote.trim()
      });
      setBookingNote('');
      await refresh();
      setNotice(result.bookedMinutes+' Minuten wurden von Znuny bestätigt ('+
        Math.ceil(result.sourceSeconds/60)+' Minuten erfasst).');
    }catch(e){
      setError(e instanceof Error?e.message:'Znuny-Buchung nicht bestätigt.');
      // If the remote ACK is uncertain, the journal remains pending.
      try{await refresh();}catch{/* Preserve the original remote error. */}
    }finally{setBusy(false);}
  }
  async function reconcile(id:string){
    if(!ticketId||busy)return;
    setBusy(true);setError('');setNotice('');
    try{
      const result=await reconcileZnunyBooking(ticketId,id);
      await refresh();
      setNotice(result.confirmed?'Buchung anhand der Znuny-Buchungs-ID bestätigt.':
        result.message||'Buchung bleibt ungeklärt. Nicht erneut übertragen.');
    }catch(e){setError(e instanceof Error?e.message:'Abgleich fehlgeschlagen.');}
    finally{setBusy(false);}
  }
  if(!ticketId)return null;
  const running=summary?.runningSince?
    Math.max(0,Math.floor((tick-Date.parse(summary.runningSince))/1000)):0;
  const unbooked=Math.max(0,(summary?.bookedSeconds??0)+running-
    (summary?.transferredSeconds??0));
  const preview=roundedBookingMinutes(unbooked);
  const pending=bookings.some(row=>row.status==='pending');
  const filtered=(options:ZnunyFieldOption[],text:string,current:string)=>
    pickerOptions(options,current).filter(item=>item.key===current||
      (item.key+' '+item.displayText).toLocaleLowerCase('de-DE')
        .includes(text.toLocaleLowerCase('de-DE')));
  return <section className="content-card classic-wpf-timer-card" aria-label="Timer / Buchung">
    <div className="classic-wpf-timer-main">
      <span className="eyebrow">Timer / Buchung</span>
      {editing?<form className="znuny-timer-edit" onSubmit={e=>void correct(e)}>
        <label htmlFor="znuny-timer-value">Zeit bearbeiten (HH:MM:SS)</label>
        <div className="znuny-timer-edit-controls">
          <input id="znuny-timer-value" autoFocus value={clockInput}
            aria-label="Timerzeit bearbeiten" maxLength={10}
            onChange={e=>setClockInput(e.target.value)}
            onKeyDown={e=>{if(e.key==='Escape')setEditing(false);}}/>
          <button type="submit" className="button button-primary" disabled={busy}
            aria-label="Timerzeit speichern"><Check size={16}/>Speichern</button>
          <button type="button" className="button button-secondary" disabled={busy}
            onClick={()=>setEditing(false)} aria-label="Timerzeit abbrechen">
            <X size={16}/>
          </button>
        </div>
      </form>:<button type="button" className="znuny-timer-value"
        disabled={busy||!summary||pending}
        title="Timer anklicken und die ungebuchte Zeit direkt bearbeiten"
        aria-label="Timerzeit bearbeiten"
        onClick={()=>{setClockInput(secondsToClock(unbooked));setEditing(true);setError('');}}>
        <Clock3 size={19}/><strong>{summary?secondsToClock(unbooked):'00:00:00'}</strong>
      </button>}
      <p className="classic-wpf-unbooked">Noch nicht in Znuny gebucht: {Math.ceil(unbooked/60)} Min.
        {summary&&summary.transferredSeconds>0?
          ' · Bereits übertragen: '+Math.ceil(summary.transferredSeconds/60)+' Min.':''}
      </p>
      <div className="classic-wpf-controls">
        <button type="button" className="button button-primary"
          disabled={busy||editing||!summary||!!summary.runningSince||pending}
          onClick={()=>void update(()=>startTicketTime(ticketId),'Timer gestartet.')}>
          <Play size={14}/>Start</button>
        <button type="button" className="button button-secondary"
          disabled={busy||editing||!summary?.runningSince}
          onClick={()=>void update(()=>stopTicketTime(ticketId),'Timer gestoppt.')}>
          <Square size={13}/>Stop</button>
      </div>
      {error&&<p role="alert" className="um-feedback failure">{error}</p>}
      {notice&&<p role="status" className="um-feedback success">{notice}</p>}
      <div className="classic-wpf-external-booking">
        <h4>Znuny-Zeitbuchung</h4>
        <p className="classic-wpf-help">Kostenstelle und Auftrag stammen aus den
          dynamischen Feldern des Tickets und den hinterlegten Znuny-Optionen.</p>
        {fieldsError&&<p role="alert" className="um-feedback failure">
          {fieldsError} <button type="button" className="button button-secondary"
            disabled={busy} onClick={()=>void readZnunyBookingFields(ticketId).then(data=>{
              setFields(data);setCost(data.costCenter);setOrder(data.order);setFieldsError('');
            }).catch(e=>setFieldsError(String(e)))}>
            <RefreshCw size={14}/>Erneut laden</button>
        </p>}
        <label>Kostenstelle
          <input aria-label="Kostenstelle suchen" placeholder="Kostenstelle suchen …"
            disabled={!fields||busy} value={costSearch}
            onChange={e=>setCostSearch(e.target.value)}/>
          <select aria-label="Kostenstelle" disabled={!fields||busy}
            value={cost} onChange={e=>setCost(e.target.value)}>
            <option value="">Keine Kostenstelle</option>
            {filtered(fields?.costCenterOptions??[],costSearch,cost).map(option=>
              <option value={option.key} key={option.key}>
                {option.displayText||option.key} ({option.key})
              </option>)}
          </select>
        </label>
        <label>Auftrag
          <input aria-label="Auftrag suchen" placeholder="Auftrag suchen …"
            disabled={!fields||busy} value={orderSearch}
            onChange={e=>setOrderSearch(e.target.value)}/>
          <select aria-label="Auftrag" disabled={!fields||busy}
            value={order} onChange={e=>setOrder(e.target.value)}>
            <option value="">Kein Auftrag</option>
            {filtered(fields?.orderOptions??[],orderSearch,order).map(option=>
              <option value={option.key} key={option.key}>
                {option.displayText||option.key} ({option.key})
              </option>)}
          </select>
        </label>
        <label>Kurzbeschreibung
          <input aria-label="Buchungsbeschreibung" maxLength={500}
            value={description} disabled={busy}
            onChange={e=>setDescription(e.target.value)}/>
        </label>
        <label>Notiz (optional)
          <textarea aria-label="Buchungsnotiz" maxLength={1000}
            rows={3} value={bookingNote} disabled={busy}
            onChange={e=>setBookingNote(e.target.value)}/>
        </label>
        <p className="classic-wpf-help">Erfasst: {Math.ceil(unbooked/60)} Min. ·
          <strong> an Znuny: {preview} Min.</strong> (immer auf volle 15 Min. aufgerundet)</p>
        <button type="button" className="button button-primary"
          disabled={busy||editing||!fields||!summary||
            pending||preview<=0}
          onClick={()=>void transfer()}>Im Ticketsystem buchen</button>
        {pending&&<p role="alert" className="um-feedback failure">
          Vorherige Buchung noch nicht bestätigt. Kein erneutes Senden,
          bis die Buchungs-ID abgeglichen ist.</p>}
      </div>
    </div>
    <div className="classic-wpf-booking-history">
      <h4>Übertragene Zeiten</h4>
      {bookings.length===0&&<p className="classic-wpf-help">
        Noch keine Znuny-Zeitbuchungen vorhanden.</p>}
      {bookings.map(row=><div key={row.id} className="znuny-booking-history-item">
        <strong>{row.status==='confirmed'?'✓ Gebucht':
          row.status==='rejected'?'✕ Von Znuny abgelehnt':'⏳ Unklar / Pending'}</strong>
        <span>{new Date(row.createdAt).toLocaleString('de-DE')}</span>
        <span>{Math.ceil(row.sourceSeconds/60)} Min. erfasst → {row.bookedMinutes} Min. gebucht</span>
        <span>{row.shortDescription}</span>
        {(row.costCenter||row.order)&&<small>Kostenstelle: {row.costCenter||'–'}
          {' · '}Auftrag: {row.order||'–'}</small>}
        {row.status==='rejected'&&<small role="status">
          Nicht übertragen. Die ungebuchte Zeit bleibt erhalten.
          {row.rejectionCode?' Znuny-Code: '+row.rejectionCode:''}
        </small>}
        {row.status==='pending'&&<button type="button" className="button button-secondary"
          disabled={busy} onClick={()=>void reconcile(row.id)}>Status in Znuny prüfen</button>}
      </div>)}
    </div>
  </section>;
}
