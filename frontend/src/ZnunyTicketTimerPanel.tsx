import { useEffect,useState, type FormEvent } from 'react';
import { Clock3, Play, Square } from 'lucide-react';
import {
  adjustTicketTime,readTicketTime,startTicketTime,stopTicketTime,
  type TicketTimeSummary,
} from './znunySyncApi';
import { secondsToClock } from './taskWorkApi';

export function ZnunyTicketTimerPanel({ticketId}: {ticketId:string|null}) {
  const [summary,setSummary]=useState<TicketTimeSummary|null>(null);
  const [tick,setTick]=useState(()=>Date.now());
  const [manual,setManual]=useState('15');
  const [note,setNote]=useState('');
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('');
  useEffect(()=>{
    const interval=window.setInterval(()=>setTick(Date.now()),1000);
    return()=>window.clearInterval(interval);
  },[]);
  useEffect(()=>{
    let active=true;
    setSummary(null);setNotice('');setError('');
    if(ticketId)void readTicketTime(ticketId)
      .then(s=>{if(active)setSummary(s);})
      .catch(e=>{if(active)setError(e instanceof Error?e.message:'Timer nicht verfügbar.');});
    return()=>{active=false;};
  },[ticketId]);

  async function run(task:()=>Promise<TicketTimeSummary>,success:string){
    if(busy)return;
    setBusy(true);setError('');setNotice('');
    try{
      const next=await task();setSummary(next);setTick(Date.now());setNotice(success);
    }catch(e){setError(e instanceof Error?e.message:'Timer konnte nicht geändert werden.');}
    finally{setBusy(false);}
  }
  async function correct(minutes:number) {
    if(!ticketId||!Number.isInteger(minutes)||minutes===0||Math.abs(minutes)>1440)return;
    await run(()=>adjustTicketTime(ticketId,minutes,note),'Zeitkorrektur gespeichert.');
    setNote('');
  }
  if(!ticketId)return null;
  const running=summary?.runningSince
    ?Math.max(0,Math.floor((tick-Date.parse(summary.runningSince))/1000)):0;
  const totalSeconds=(summary?.bookedSeconds??0)+running;
  return <section className="content-card classic-wpf-timer-card" aria-label="Timer / Buchung">
    <div className="classic-wpf-timer-main">
      <span className="eyebrow">Timer / Buchung</span>
      <div className="classic-wpf-timer-time">
        <Clock3 size={19}/><strong>{summary?secondsToClock(totalSeconds):'00:00:00'}</strong>
      </div>
      <p className="classic-wpf-unbooked">Noch nicht in Znuny gebucht: {Math.floor(totalSeconds/60)} Min.</p>
      <div className="classic-wpf-controls">
        <button type="button" className="button button-primary"
          disabled={busy||!summary||!!summary.runningSince}
          onClick={()=>void run(()=>startTicketTime(ticketId),'Timer gestartet.')}>
          <Play size={14}/>Start</button>
        <button type="button" className="button button-secondary"
          disabled={busy||!summary?.runningSince}
          onClick={()=>void run(()=>stopTicketTime(ticketId),'Timer gestoppt.')}>
          <Square size={13}/>Stop</button>
      </div>
      <div className="classic-wpf-preset-grid">
        {[15,30,60,-15,-30,-60].map(amount=><button type="button" key={amount}
          className={'plenaro-time-chip'+(amount<0?' negative':'')}
          disabled={busy||!summary} onClick={()=>void correct(amount)}>
          {amount>0?'+':''}{Math.abs(amount)===60?
            (amount>0?'1h':'-1h'):(amount>0?amount:String(amount))}
        </button>)}
      </div>
      <form className="classic-wpf-timer-manual" onSubmit={(e:FormEvent)=>{
        e.preventDefault();void correct(Number(manual));
      }}>
        <label htmlFor="classic-ticket-manual">Korrektur (Minuten ±)</label>
        <input type="number" id="classic-ticket-manual" min="-1440" max="1440"
          step="1" required value={manual} onChange={e=>setManual(e.target.value)}/>
        <label htmlFor="classic-ticket-time-note">Notiz zur Buchung (optional)</label>
        <input id="classic-ticket-time-note" maxLength={500} value={note}
          onChange={e=>setNote(e.target.value)}/>
        <button type="submit" className="button button-secondary" disabled={busy||!summary}>
          Zeit korrigieren</button>
      </form>
      {error&&<p role="alert" className="um-feedback failure">{error}</p>}
      {notice&&<p role="status" className="um-feedback success">{notice}</p>}
      <div className="classic-wpf-external-booking">
        <h4>Znuny-Zeitbuchung</h4>
        <label>Kostenstelle
          <select disabled><option>Dynamic Fields noch nicht angebunden</option></select>
        </label>
        <label>Auftrag
          <select disabled><option>Dynamic Fields noch nicht angebunden</option></select>
        </label>
        <button type="button" className="button button-primary" disabled
          title="Die Znuny-Schreibschnittstelle für echte Zeitbuchungen fehlt noch.">
          Im Ticketsystem buchen</button>
        <p className="classic-wpf-help">Die Zeit wird bis dahin nur in Plenaro gespeichert.
          Kein Eintrag wird an Znuny übertragen.</p>
      </div>
    </div>
    <div className="classic-wpf-booking-history">
      <h4>Übertragene Zeiten</h4>
      <p className="classic-wpf-help">Noch keine Übertragungen. Die direkte
        Znuny-Zeitbuchung und Buchungshistorie folgen mit dem Schreibconnector.</p>
      <p className="classic-wpf-help">Lokal erfasst: {Math.floor(totalSeconds/60)} Min.
        {summary? ` · ${summary.sessionCount} abgeschlossene Timer-Sitzungen` : ''}</p>
    </div>
  </section>;
}
