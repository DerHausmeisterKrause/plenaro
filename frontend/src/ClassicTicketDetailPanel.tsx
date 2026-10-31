import { useEffect,useState } from 'react';
import { ExternalLink, Save, Ticket, WifiOff } from 'lucide-react';
import { Link } from 'react-router-dom';
import type { CachedTicket } from './znunySyncApi';
import { changeZnunyTicketState,readTicketWorkspace,saveTicketWorkspace,
  type TicketWorkspace } from './znunySyncApi';
import { getZnunyServer } from './znunyApi';
import { ZnunyTicketTimerPanel } from './ZnunyTicketTimerPanel';
import { ZnunyTicketPlanPanel } from './ZnunyTicketPlanPanel';
import { ZnunyTicketConversationPanel } from './ZnunyTicketConversationPanel';
import { ZnunyTicketAssignmentPanel } from './ZnunyTicketAssignmentPanel';

function safeLink(value:string):string|null{
  try{
    const uri=new URL(value);
    if((uri.protocol==='http:'||uri.protocol==='https:')&&
      !uri.username&&!uri.password)return uri.href;
  }catch{ /* not a valid URL */ }
  return null;
}
function classicZnunyUrl(base:string,ticketId:string):string|null{
  try{
    const uri=new URL(base);
    if((uri.protocol!=='http:'&&uri.protocol!=='https:')||
      uri.hostname.toLowerCase()==='server'||uri.username||uri.password)return null;
    // Classic Znuny AgentTicketZoom navigation, not its GenericInterface endpoint.
    uri.search='?Action=AgentTicketZoom;TicketID='+encodeURIComponent(ticketId);
    uri.hash='';
    return uri.href;
  }catch{return null;}
}

/** WPF-inspired ticket detail. Local notes remain separate from remote
 * Znuny article and assignment operations; both are explicitly confirmed. */
export function ClassicTicketDetailPanel({ticket,onChanged}:{
  ticket:CachedTicket;onChanged?:()=>void;
}){
  const [fields,setFields]=useState<TicketWorkspace>({description:'',ticketUrl:''});
  const [znunyUrl,setZnunyUrl]=useState<string|null>(null);
  const [loading,setLoading]=useState(true);
  const [saving,setSaving]=useState(false);
  const [stateBusy,setStateBusy]=useState(false);
  const [dirty,setDirty]=useState(false);
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('');
  const [wikiOpen,setWikiOpen]=useState(false);

  useEffect(()=>{
    let active=true;
    setLoading(true);setError('');setNotice('');setDirty(false);
    setFields({description:'',ticketUrl:''});setZnunyUrl(null);
    void Promise.all([
      readTicketWorkspace(ticket.ticketId),
      getZnunyServer().catch(()=>null),
    ]).then(([data,server])=>{
      if(!active)return;
      setFields(data);
      setZnunyUrl(server?classicZnunyUrl(server.webUrl,ticket.ticketId):null);
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

  const update=(key:keyof TicketWorkspace,value:string)=>{
    setFields(prev=>({...prev,[key]:value}));setDirty(true);
  };
  const direct=safeLink(fields.ticketUrl)||znunyUrl;
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

      <ZnunyTicketConversationPanel ticketId={ticket.ticketId} closed={closed}/>

      <label htmlFor="classic-wpf-ticket-url">Ticket URL</label>
      <div className="classic-wpf-url-row">
        <input id="classic-wpf-ticket-url" type="url" maxLength={2048}
          value={fields.ticketUrl} disabled={loading}
          onChange={e=>update('ticketUrl',e.target.value)}
          placeholder={znunyUrl||'https://tickets.example.de/index.pl?...'}/>
        {direct?<a className="button button-secondary" href={direct}
          target="_blank" rel="noopener noreferrer">
          <ExternalLink size={14}/>Ticket öffnen</a>
          :<button type="button" className="button button-secondary" disabled
            title="Zuerst eine gültige Ticket-URL eintragen.">Ticket öffnen</button>}
      </div>

      <ZnunyTicketAssignmentPanel ticket={ticket} onChanged={onChanged}/>

      <div className="classic-wpf-actions">
        <button type="button" className="button button-primary"
          disabled={saving||loading} onClick={()=>void save()}>
          <Save size={14}/>{saving?'Speichere …':'Speichern'}{dirty?' *':''}</button>
        {!closed&&<button type="button" className="button classic-wpf-success"
          disabled={stateBusy||saving} onClick={()=>void changeState(true)}>
          {stateBusy?'Ändere Status …':'Erledigt'}</button>}
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
    <ZnunyTicketTimerPanel key={'timer:'+ticket.ticketId} ticketId={ticket.ticketId}/>
    {!closed&&<ZnunyTicketPlanPanel key={'segments:'+ticket.ticketId} ticketId={ticket.ticketId}
      onChanged={onChanged}/>}
  </div>;
}
