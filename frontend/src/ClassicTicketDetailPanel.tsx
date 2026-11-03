import { useEffect,useState } from 'react';
import { ExternalLink, Save, Ticket, WifiOff } from 'lucide-react';
import { Link } from 'react-router-dom';
import type { CachedTicket } from './znunySyncApi';
import { checkZnunyWriteReadiness,changeZnunyTicketState,readTicketWorkspace,saveTicketWorkspace,
  type TicketWorkspace } from './znunySyncApi';
import { ZnunyTicketTimerPanel } from './ZnunyTicketTimerPanel';
import { ZnunyTicketPlanPanel } from './ZnunyTicketPlanPanel';
import { ZnunyTicketConversationPanel } from './ZnunyTicketConversationPanel';
import { ZnunyTicketAssignmentPanel } from './ZnunyTicketAssignmentPanel';

/** WPF-inspired ticket detail. Local notes remain separate from remote
 * Znuny article and assignment operations; both are explicitly confirmed. */
export function ClassicTicketDetailPanel({ticket,onChanged}:{
  ticket:CachedTicket;onChanged?:()=>void;
}){
  const [fields,setFields]=useState<TicketWorkspace>({description:'',ticketUrl:''});
  const [loading,setLoading]=useState(true);
  const [saving,setSaving]=useState(false);
  const [stateBusy,setStateBusy]=useState(false);
  const [diagnosticsBusy,setDiagnosticsBusy]=useState(false);
  const [dirty,setDirty]=useState(false);
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('');
  const [wikiOpen,setWikiOpen]=useState(false);
  const [closeRequest,setCloseRequest]=useState(0);

  useEffect(()=>{
    let active=true;
    setLoading(true);setError('');setNotice('');setDirty(false);
    setFields({description:'',ticketUrl:''});
    void readTicketWorkspace(ticket.ticketId).then(data=>{
      if(!active)return;
      setFields(data);
      setLoading(false);
    }).catch(e=>{if(active){setError(e instanceof Error?e.message:
      'Task Details konnten nicht geladen werden.');setLoading(false);}});
    return()=>{active=false;};
  },[ticket.ticketId]);

  async function save(){
    if(saving||loading)return;
    setSaving(true);setError('');setNotice('');
    try{
      await saveTicketWorkspace(ticket.ticketId,fields);
      setDirty(false);setNotice('Lokale Task-Daten gespeichert.');
      onChanged?.();
    }catch(e){setError(e instanceof Error?e.message:'Speichern fehlgeschlagen.');}
    finally{setSaving(false);}
  }

  async function changeState(close:boolean){
    if(stateBusy||saving)return;
    if(!window.confirm(close?
      'Dieses Ticket wird direkt in Znuny geschlossen. Fortfahren?':
      'Dieses Ticket wird direkt in Znuny wieder geöffnet. Fortfahren?'))return;
    setStateBusy(true);setError('');setNotice('');
    try{
      const result=await changeZnunyTicketState(ticket.ticketId,close);
      if(!result.confirmed)throw new Error('Znuny hat die Statusänderung nicht bestätigt.');
      setNotice(close?'Ticket in Znuny geschlossen.':'Ticket in Znuny wieder geöffnet.');
      onChanged?.();
    }catch(e){setError(e instanceof Error?e.message:'Statusänderung fehlgeschlagen.');}
    finally{setStateBusy(false);}
  }

  async function checkWriteAccess(){
    if(diagnosticsBusy)return;
    setDiagnosticsBusy(true);setError('');setNotice('');
    try{
      const result=await checkZnunyWriteReadiness(ticket.ticketId);
      setNotice(result.message+' Konfigurierte Schreibroute: '+
        result.writeMethod+' '+result.writeRoute+'.');
    }catch(e){setError(e instanceof Error?e.message:
      'Persönliche Znuny-API-Diagnose fehlgeschlagen.');}
    finally{setDiagnosticsBusy(false);}
  }

  const update=(key:keyof TicketWorkspace,value:string)=>{
    setFields(prev=>({...prev,[key]:value}));setDirty(true);
  };
  const closed=ticket.stateType.toLowerCase()==='closed';

  return <div className="classic-wpf-ticket-detail" aria-label="Classic Ticket-Detailbereich">
    <section className="content-card classic-wpf-task-card" aria-label="Task Details">
      <span className="eyebrow">Task Details</span>
      <h2>{ticket.title||'Ticket #'+ticket.ticketNumber}</h2>
      <div className="classic-wpf-ticket-meta">
        <span><Ticket size={13}/>Ticket #{ticket.ticketNumber||ticket.ticketId}</span>
        <span>{ticket.state}</span><span>{ticket.queue}</span>
      </div>
      <label htmlFor="classic-wpf-description">Beschreibung</label>
      <textarea id="classic-wpf-description" className="classic-wpf-description"
        aria-label="Beschreibung" rows={10} maxLength={10000} disabled={loading}
        value={fields.description} onChange={e=>update('description',e.target.value)}
        placeholder="Deine lokale Task-Beschreibung / Notizen zu diesem Ticket …"/>
      <p className="classic-wpf-help">Persönliche Plenaro-Beschreibung.
        Der Originalinhalt des Znuny-Tickets bleibt unverändert.</p>

      <ZnunyTicketConversationPanel ticketId={ticket.ticketId} closed={closed}
        closeRequest={closeRequest} onClosed={()=>{
          setNotice('Antwort versendet und Ticket in Znuny geschlossen.');
          onChanged?.();
        }}/>

      <label htmlFor="classic-wpf-ticket-url">Ticket URL</label>
      <div className="classic-wpf-url-row">
        <input id="classic-wpf-ticket-url" type="url" maxLength={2048}
          value={fields.ticketUrl} disabled={loading}
          onChange={e=>update('ticketUrl',e.target.value)}
          placeholder="https://tickets.example.de/index.pl?..."/>
        <Link className="button button-secondary" to={'/ticket-system?ticket='+encodeURIComponent(ticket.ticketId)}>
          <ExternalLink size={14}/>Ticket öffnen
        </Link>
      </div>
      <p className="classic-wpf-help">Die gespeicherte Ticket-URL ist nur eine Referenz. 
        Znuny wird ausschließlich über den geschützten Plenaro-Webreiter geöffnet.</p>

      <ZnunyTicketAssignmentPanel ticket={ticket} onChanged={onChanged}/>

      <div className="classic-wpf-actions">
        <button type="button" className="button button-secondary"
          disabled={diagnosticsBusy||loading} onClick={()=>void checkWriteAccess()}>
          {diagnosticsBusy?'Prüfe Znuny-Zugang …':'Znuny-Schreibzugriff prüfen'}
        </button>
        <button type="button" className="button button-primary"
          disabled={saving||loading} onClick={()=>void save()}>
          <Save size={14}/>{saving?'Speichere …':'Speichern'}{dirty?' *':''}</button>
        {!closed&&<button type="button" className="button classic-wpf-success"
          disabled={stateBusy||saving}
          onClick={()=>setCloseRequest(v=>v+1)}>
          Erledigt</button>}
        {closed&&<button type="button" className="button button-secondary"
          disabled={stateBusy||saving} onClick={()=>void changeState(false)}>
          {stateBusy?'Ändere Status …':'Zurück zu Aktuell'}</button>}
      </div>
      {error&&<p role="alert" className="um-feedback failure">{error}</p>}
      {notice&&<p role="status" className="um-feedback success">{notice}</p>}
      {closed&&<p className="classic-wpf-help">Das Ticket ist in Znuny geschlossen.
        Die lokalen Notizen und Zeitdaten bleiben bearbeitbar.</p>}

      <details open={wikiOpen} onToggle={e=>
        setWikiOpen((e.currentTarget as HTMLDetailsElement).open)}
        className="classic-wpf-wiki">
        <summary>Passende Wiki-Einträge</summary>
        <div className="classic-wpf-optional-inner">
          <p className="classic-wpf-help">Im Desktop-Plenaro werden hier Wiki-Quellen
            nach Ticket-Stichwörtern durchsucht, mit Quelle, Relevanz und Link.
            Die Wiki-Suche ist in der Webversion noch nicht angebunden.</p>
          <Link to="/wiki" className="button button-secondary">
            <WifiOff size={14}/>Wissensmanagement öffnen</Link>
        </div>
      </details>
    </section>
    <ZnunyTicketTimerPanel key={'timer:'+ticket.ticketId} ticketId={ticket.ticketId}
      defaultDescription={fields.description.split(/\r?\n/)[0].trim()||ticket.title||'Zeitbuchung'}/>
    {!closed&&<ZnunyTicketPlanPanel key={'segments:'+ticket.ticketId} ticketId={ticket.ticketId}
      onChanged={onChanged}/>}
  </div>;
}
