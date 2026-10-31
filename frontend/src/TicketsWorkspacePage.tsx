import { useCallback, useEffect, useState } from 'react';
import { Inbox, RefreshCw, Search, Ticket } from 'lucide-react';
import { Link, useSearchParams } from 'react-router-dom';
import { ClassicTicketDetailPanel } from './ClassicTicketDetailPanel';
import {
  getCachedTickets,getTicketAccess,type CachedTicket,type TicketAccess,
} from './znunySyncApi';

/** Only centrally cached, server-authorized tickets are displayed.
 * There are no browser calls to Znuny and no invented demo tickets. */
export function TicketsWorkspacePage() {
  const [tickets,setTickets]=useState<CachedTicket[]>([]);
  const [selectedId,setSelectedId]=useState<string|null>(null);
  const [access,setAccess]=useState<TicketAccess|null>(null);
  const [view,setView]=useState<'mine'|'new'|'completed'>('mine');
  const [params]=useSearchParams();
  const [search,setSearch]=useState('');
  const [page,setPage]=useState(0);
  const [loading,setLoading]=useState(true);
  const [error,setError]=useState('');
  const refresh=useCallback(async()=>{
    setLoading(true);setError('');
    try {
      const [nextAccess,nextTickets]=await Promise.all([
        getTicketAccess(),getCachedTickets(view,search,page)]);
      setAccess(nextAccess);
      setTickets(nextTickets);
      setSelectedId(old=>nextTickets.some(ticket=>ticket.ticketId===old)?old:
        nextTickets.find(ticket=>ticket.ticketId===params.get('ticket'))?.ticketId??
        nextTickets[0]?.ticketId??null);
    } catch(e) {setError(e instanceof Error?e.message:'Ticketliste nicht verfügbar.');}
    finally{setLoading(false);}
  },[view,search,page,params]);
  useEffect(()=>{
    void refresh();
    const interval=window.setInterval(()=>{if(!document.hidden)void refresh();},60_000);
    return()=>window.clearInterval(interval);
  },[refresh]);
  const selected=tickets.find(x=>x.ticketId===selectedId);
  return <div className="page classic-workspace">
    <div className="page-heading">
      <span className="eyebrow">PLENARO / TICKETSYSTEM</span>
      <h1>Ticketsystem</h1>
      <p>Zentral synchronisierte Znuny-Tickets – eine Ticketdatenbank für alle Benutzer,
        mit persönlicher Berechtigung und ohne separate Synchronisation pro Benutzer.</p>
    </div>
    <div className="classic-split">
      <section className="classic-list-pane" aria-label="Ticketliste">
        <div className="classic-list-header">
          <div><strong>{view==='mine'?'Meine aktuellen Tickets':view==='new'?'Neue Aufgaben':'Erledigte Aufgaben'}</strong>
            <span>{access?.approved?`${tickets.length} Tickets im Cache`:
              'Agentenzuordnung noch nicht freigegeben'}</span></div>
          <button className="button button-secondary" type="button"
            disabled={loading} onClick={()=>void refresh()} aria-label="Ticketliste aktualisieren">
            <RefreshCw size={17}/></button>
        </div>
        <div className="classic-ticket-views" role="group" aria-label="Ticketansicht">
          <button type="button" className={view==='mine'?'active':''}
            aria-pressed={view==='mine'} onClick={()=>{setPage(0);setView('mine');}}>Meine Tickets</button>
          <button type="button" className={view==='new'?'active':''}
            aria-pressed={view==='new'} onClick={()=>{setPage(0);setView('new');}}>Neue Aufgaben</button>
          <button type="button" className={view==='completed'?'active':''}
            aria-pressed={view==='completed'} onClick={()=>{setPage(0);setView('completed');}}>Erledigte Aufgaben</button>
        </div>
        <label className="classic-search">
          <Search size={16}/>
          <input aria-label="Tickets suchen" placeholder="Ticketnummer, Betreff …"
            maxLength={100} value={search} onChange={e=>{setPage(0);setSearch(e.target.value);}}/>
        </label>
        <div className="classic-list-scroll">
          {error&&<p role="alert" className="um-feedback failure">{error}</p>}
          {loading&&<p role="status">Ticketcache wird geladen …</p>}
          {!loading&&tickets.length===0&&<div className="classic-empty-list">
            <Inbox size={30}/>
            <h2>Noch keine Tickets geladen</h2>
            <p>{!access?.approved
              ? 'Ein Administrator muss deine Znuny-Agenten-ID freigeben.'
              : !access.syncEnabled
              ? 'Der zentrale Znuny-Sync ist noch nicht aktiviert.'
              : 'Noch keine passenden Tickets im zentralen Cache.'}</p>
          </div>}
          {!loading&&tickets.map(ticket=>
            <button key={ticket.ticketId} type="button"
              className={`classic-list-item${selectedId===ticket.ticketId?' active':''}`}
              aria-current={selectedId===ticket.ticketId?'true':undefined}
              onClick={()=>setSelectedId(ticket.ticketId)}>
              <strong>{ticket.title||'Ticket '+ticket.ticketNumber}</strong>
              <small>#{ticket.ticketNumber||ticket.ticketId} · {ticket.queue}</small>
              <span className="tasks-state">{ticket.state}</span>
            </button>)}
          {!loading&&(page>0||tickets.length===100)&&<nav className="znuny-ticket-pagination" aria-label="Ticketseiten">
            <button type="button" className="button button-secondary" disabled={page===0}
              onClick={()=>setPage(p=>Math.max(0,p-1))}>Vorherige Seite</button>
            <span>Seite {page+1}</span>
            <button type="button" className="button button-secondary" disabled={tickets.length<100||page>=1000}
              onClick={()=>setPage(p=>p+1)}>Nächste Seite</button>
          </nav>}
        </div>
      </section>
      <section className="classic-detail-pane" aria-label="Ticketdetails">
        {!selected?<div className="classic-detail-empty">
          <Ticket size={42}/>
          <h2>Ticket auswählen</h2>
          <p>Die zentrale Synchronisation speichert zunächst nur die notwendigen
            Ticketmetadaten. Ticketaktionen und Artikelverläufe folgen separat.</p>
          <Link to="/settings" className="button button-secondary">Integrationseinstellungen öffnen</Link>
        </div>:
        <ClassicTicketDetailPanel key={selected.ticketId} ticket={selected}
          onChanged={()=>void refresh()}/>}
      </section>
    </div>
  </div>;
}
