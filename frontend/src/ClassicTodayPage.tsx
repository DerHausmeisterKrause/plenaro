import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { CalendarDays, Clock3, ExternalLink, MapPin, Pin, RefreshCw, Search, SlidersHorizontal, Star, Ticket } from 'lucide-react';
import { useLocation } from 'react-router-dom';
import { ClassicTasksPage } from './ClassicTasksPage';
import { localDate } from './tasksApi';
import { graphStatus, readOutlookWeek, type OutlookEvent } from './graphApi';
import { ClassicTicketDetailPanel } from './ClassicTicketDetailPanel';
import {
  getCachedTickets, getTicketAccess, getTicketAgenda, setShowAllCandidates,
  setZnunyTicketPinned,
  type CachedTicket, type TicketAccess, type TicketAgendaSegment, type TicketListFilters,
} from './znunySyncApi';
import { agendaTimeLabel, buildTodayAgenda, localDayBounds } from './todayAgenda';
import { isoWeekNumber } from './isoWeek';

type Tab = 'today' | 'active' | 'new' | 'completed' | 'local';
const labels: {key:Tab;label:string}[] = [
  {key:'today',label:'Heute'},
  {key:'active',label:'Aktuelle Aufgaben'},
  {key:'new',label:'Neue Aufgaben'},
  {key:'completed',label:'Erledigte Aufgaben'},
  {key:'local',label:'Lokale Aufgaben'},
];

export function ClassicTodayPage({userId}:{userId:string}) {
  const location=useLocation();
  const [day,setDay]=useState(()=>localDate());
  const [tab,setTab]=useState<Tab>(()=>new URLSearchParams(location.search).has('task')?'local':'today');
  const [tickets,setTickets]=useState<CachedTicket[]>([]);
  const [ticketAgenda,setTicketAgenda]=useState<TicketAgendaSegment[]>([]);
  const [outlook,setOutlook]=useState<OutlookEvent[]>([]);
  const [access,setAccess]=useState<TicketAccess|null>(null);
  const [selectedTicketId,setSelectedTicketId]=useState<string|null>(null);
  // One browser-local layout preference per signed-in user, not per ticket.
  const viewKey='plenaro-ticket-details-expanded:'+userId;
  const [detailsExpanded,setDetailsExpanded]=useState(()=>{
    try{return window.localStorage.getItem(viewKey)==='true';}
    catch{return false;}
  });
  function toggleDetailsExpanded(){
    const next=!detailsExpanded;
    setDetailsExpanded(next);
    try{window.localStorage.setItem(viewKey,String(next));}
    catch{/* The toggle still works for this mounted page. */}
  }
  const rowsRef=useRef<HTMLDivElement|null>(null);
  const rowsScrollTop=useRef(0);
  const [selectedAgendaKey,setSelectedAgendaKey]=useState<string|null>(null);
  const [page,setPage]=useState(0);
  const [ticketSearch,setTicketSearch]=useState('');
  const [searchDebounced,setSearchDebounced]=useState('');
  const [filters,setFilters]=useState<TicketListFilters>({
    query:'',queue:'',state:'',sort:'changed',direction:'desc'
  });
  const [filtersOpen,setFiltersOpen]=useState(false);
  const [toggleBusy,setToggleBusy]=useState(false);
  const [pinBusy,setPinBusy]=useState<string|null>(null);
  const [loading,setLoading]=useState(true);
  const [listLoading,setListLoading]=useState(false);
  const [outlookConnected,setOutlookConnected]=useState(false);
  const [calendarError,setCalendarError]=useState('');
  const [error,setError]=useState('');
  const [refresh,setRefresh]=useState(0);

  useEffect(()=>{
    if(new URLSearchParams(location.search).has('task'))setTab('local');
  },[location.search]);

  useEffect(()=>{
    const handle=window.setInterval(()=>setDay(old=>{
      const date=localDate();return date===old?old:date;
    }),60_000);
    return ()=>window.clearInterval(handle);
  },[]);

  useEffect(()=>{
    let active=true;
    setLoading(true);setError('');
    void Promise.all([getTicketAgenda(day),getTicketAccess()])
      .then(([segments,currentAccess])=>{
        if(!active)return;
        setTicketAgenda(segments);
        setAccess(currentAccess);setLoading(false);
      })
      .catch(e=>{if(active){setError(e instanceof Error?e.message:'Heute nicht verfügbar.');setLoading(false);}});
    return ()=>{active=false;};
  },[day,refresh]);

  useEffect(()=>{
    const timer=window.setTimeout(()=>setSearchDebounced(ticketSearch),300);
    return()=>window.clearTimeout(timer);
  },[ticketSearch]);
  useEffect(()=>{
    if(!['active','new','completed'].includes(tab))return;
    const timer=window.setInterval(()=>{
      if(!document.hidden)setRefresh(x=>x+1);
    },60_000);
    return()=>window.clearInterval(timer);
  },[tab]);
  useEffect(()=>{
    if(tab==='today'||tab==='local')return;
    let active=true;
    setListLoading(true);setError('');
    const view=tab==='active'?'mine':tab;
    void getCachedTickets(view,searchDebounced,page,filters)
      .then(rows=>{
        if(!active)return;
        setTickets(rows);
        setSelectedTicketId(previous=>rows.some(t=>t.ticketId===previous)?previous:null);
        setListLoading(false);
      })
      .catch(e=>{if(active){setError(e instanceof Error?e.message:'Tickets nicht verfügbar.');setListLoading(false);}});
    return ()=>{active=false;};
  },[tab,page,refresh,searchDebounced,filters]);

  useEffect(()=>{
    let active=true;
    setOutlook([]);setCalendarError('');setOutlookConnected(false);
    const {start,end}=localDayBounds(day);
    void graphStatus().then(async status=>{
      if(!active||!status.connected)return;
      setOutlookConnected(true);
      const result=await readOutlookWeek(start,end);
      if(active){
        setOutlook(result.events);
        if(result.hasMore)setCalendarError('Outlook zeigt möglicherweise nicht alle Termine.');
      }
    }).catch(e=>{
      if(active)setCalendarError(e instanceof Error?e.message:'Outlook nicht erreichbar.');
    });
    return ()=>{active=false;};
  },[day]);

  const agenda=buildTodayAgenda(day,ticketAgenda,outlook);

  // Refetches may reorder rows or briefly display a loading status. Restore
  // the exact visible scroll offset instead of jumping to the first ticket.
  useLayoutEffect(()=>{
    if(rowsRef.current)rowsRef.current.scrollTop=rowsScrollTop.current;
  },[tickets,agenda,tab,loading,listLoading]);
  const selectedAppointment=tab==='today'
    ?agenda.find(a=>a.kind==='outlook'&&a.key===selectedAgendaKey)
    :undefined;
  const selectedTicket=tab==='today'
    ?(agenda.some(a=>a.kind==='ticket'&&a.key===selectedAgendaKey&&a.ticketId===selectedTicketId)
      ?ticketAgenda.find(t=>t.ticketId===selectedTicketId):undefined)
    :tickets.find(t=>t.ticketId===selectedTicketId);

  function changeTab(next:Tab) {
    setTab(next);setPage(0);setSelectedTicketId(null);setSelectedAgendaKey(null);
    rowsScrollTop.current=0;
    if(rowsRef.current)rowsRef.current.scrollTop=0;
    setTicketSearch('');setSearchDebounced('');
  }
  async function togglePin(ticket:CachedTicket){
    if(pinBusy)return;
    setPinBusy(ticket.ticketId);setError('');
    try{
      await setZnunyTicketPinned(ticket.ticketId,!ticket.isPinned);
      // Server handles pinned-first sort before pagination. Reload instead of
      // locally reordering this page and possibly hiding pins on other pages.
      setPage(0);setRefresh(v=>v+1);
    }catch(e){setError(e instanceof Error?e.message:'Ticket konnte nicht angepinnt werden.');}
    finally{setPinBusy(null);}
  }

  function updateFilter<K extends keyof TicketListFilters>(
    key:K,value:TicketListFilters[K]){
    setPage(0);setFilters(old=>({...old,[key]:value}));
  }
  async function toggleShowAll(){
    if(!access?.approved||toggleBusy)return;
    setToggleBusy(true);setError('');
    try{
      const next=!access.showAllCandidatePoolTickets;
      await setShowAllCandidates(next);
      setAccess(old=>old?{...old,showAllCandidatePoolTickets:next}:old);
      setPage(0);setRefresh(x=>x+1);
    }catch(e){setError(e instanceof Error?e.message:'Kandidatenfilter konnte nicht gespeichert werden.');}
    finally{setToggleBusy(false);}
  }
  const today=new Date(day+'T12:00:00');

  return <div className="page plenaro-today">
    {error&&<p role="alert" className="um-feedback failure">{error}</p>}
    <div className="plenaro-tabs content-card" role="tablist" aria-label="Arbeitsplatz-Ansichten">
      {labels.map(item=><button role="tab" type="button" key={item.key}
        className={tab===item.key?'active':''} aria-selected={tab===item.key}
        onClick={()=>changeTab(item.key)}>{item.label}</button>)}
    </div>
    {tab==='local'?<ClassicTasksPage/>:<div className={
      'plenaro-today-columns'+(selectedTicket&&detailsExpanded?' is-ticket-expanded':'')
    }>
      <section className="plenaro-today-left" aria-label="Arbeitsplatz-Ticketbereich">
        <div className="content-card plenaro-workday">
          <h2>Arbeitstag heute</h2>
          <p className="plenaro-workday-date">
            {new Intl.DateTimeFormat('de-DE',{dateStyle:'full'}).format(today)}
            {' · KW '+isoWeekNumber(today)}
          </p>
        </div>
        <div className="content-card plenaro-today-list">
          <div className="plenaro-list-title">
            <h2>{labels.find(item=>item.key===tab)?.label}:</h2>
            <button type="button" className="classic-text-action" aria-label="Ansicht aktualisieren"
              onClick={()=>setRefresh(x=>x+1)}><RefreshCw size={16}/>Aktualisieren</button>
          </div>
          {tab==='new'&&<div className="znuny-candidate-controls">
            <button type="button" className={'button '+(
              access?.showAllCandidatePoolTickets?'button-primary':'button-secondary')}
              aria-pressed={!!access?.showAllCandidatePoolTickets}
              disabled={!access?.approved||toggleBusy}
              onClick={()=>void toggleShowAll()}>Alle neuen Aufgaben</button>
            <span className="classic-wpf-help">
              {access?.showAllCandidatePoolTickets?'Alle offenen Pool-Tickets':
                access?.hasCandidateKeywords?'Nur Treffer der Such- und Ausschlusskriterien':
                'Keine Suchbegriffe hinterlegt – gefilterte Ansicht ist leer'}
            </span>
          </div>}
          {(tab==='active'||tab==='new'||tab==='completed')&&<>
            <div className="znuny-list-toolbar">
              <label className="classic-search">
                <Search size={16}/>
                <input aria-label="Tickets suchen" value={ticketSearch} maxLength={100}
                  placeholder="Volltextsuche: Ticket, Betreff, E-Mails, Notizen …"
                  onChange={e=>{setPage(0);setTicketSearch(e.target.value);}}/>
              </label>
              <button className="button button-secondary znuny-filter-button" type="button"
                aria-expanded={filtersOpen} aria-controls="workspace-ticket-filters"
                onClick={()=>setFiltersOpen(v=>!v)}>
                <SlidersHorizontal size={14}/>Filter und Sortierung
              </button>
            </div>
            {filtersOpen&&<div id="workspace-ticket-filters" className="znuny-list-filter-fields">
              <label>Sortieren nach
                <select aria-label="Tickets sortieren nach" value={filters.sort}
                  onChange={e=>updateFilter('sort',e.target.value as TicketListFilters['sort'])}>
                  <option value="changed">Zuletzt geändert</option>
                  <option value="created">Erstellt</option>
                  <option value="title">Titel</option>
                  <option value="number">Ticketnummer</option>
                </select>
              </label>
              <label>Richtung
                <select aria-label="Sortierrichtung" value={filters.direction}
                  onChange={e=>updateFilter('direction',e.target.value as TicketListFilters['direction'])}>
                  <option value="desc">Absteigend</option>
                  <option value="asc">Aufsteigend</option>
                </select>
              </label>
              <button type="button" className="button button-secondary"
                onClick={()=>{setPage(0);setFilters({
                  query:'',queue:'',state:'',sort:'changed',direction:'desc'
                });}}>Filter zurücksetzen</button>
            </div>}
          </>}
          {tab==='today'&&outlookConnected&&calendarError&&
            <p role="status" className="um-hint">{calendarError}</p>}
          {access?.approved&&!access.syncEnabled&&<p className="um-hint" role="status">
            Die zentrale Znuny-Synchronisation ist deaktiviert. Es werden nur bereits
            zwischengespeicherte Tickets angezeigt. Ein Administrator muss den Sync aktivieren.</p>}
          {!access?.approved&&!loading&&<p className="um-hint">
            Für Znuny-Tickets muss der Administrator deine Agenten-ID hinterlegen.
            Outlook-Termine bleiben trotzdem sichtbar.</p>}
          {tab==='new'&&access?.approved&&access.candidateUserId==null&&
            <p className="um-hint" role="status">Für die Anzeige neuer Ticketkandidaten
              muss ein Administrator deine Kandidaten-Benutzer-ID hinterlegen.</p>}
          {(tab==='today'?loading:listLoading)&&<p role="status" className="um-hint">
            {tab==='today'?'Aktualisiere Tagesagenda …':'Aktualisiere Tickets …'}</p>}
          {tab==='today'?(agenda.length===0
            ?<p className="plenaro-empty-list">Heute keine Outlook-Termine oder geplanten Ticketsegmente.</p>
            :<div className="plenaro-rows" aria-label="Tagesagenda"
              ref={rowsRef}
              onScroll={event=>{rowsScrollTop.current=event.currentTarget.scrollTop;}}>
              {agenda.map(item=><button key={item.key} type="button"
                className={`plenaro-task-row plenaro-agenda-row${selectedAgendaKey===item.key?' selected':''}`}
                onClick={()=>{setSelectedAgendaKey(item.key);setSelectedTicketId(item.ticketId);}}>
                <span className="plenaro-agenda-time">
                  {item.allDay?<CalendarDays size={15}/>:<Clock3 size={15}/>}
                  {agendaTimeLabel(item)}
                </span>
                <span className="plenaro-agenda-content">
                  <strong>{item.kind==='ticket'&&ticketAgenda.some(t=>
                    t.ticketId===item.ticketId&&!!t.unreadArticleCount)&&
                    <Star role="img" aria-label="Neue ungelesene Ticket-Nachricht"
                      className="ticket-unread-star" size={17}/>}{item.title}</strong>
                  {item.description&&<small>{item.description}</small>}
                  {item.location&&<small><MapPin size={12}/>{item.location}</small>}
                </span>
                <span className="plenaro-agenda-kind">{item.kind==='outlook'?'Outlook':'Znuny-Ticket'}</span>
              </button>)}
            </div>):
          tickets.length===0?<div className="plenaro-empty-list">
            <Ticket size={20}/>
            <strong>Keine passenden Znuny-Tickets</strong>
            <p>{tab==='new'?'Keine Tickets passend zum Pool- und Suchfilter gefunden.':
              'In dieser Ansicht sind keine Tickets vorhanden.'}</p>
          </div>:
          <div className="plenaro-rows" ref={rowsRef}
            onScroll={event=>{rowsScrollTop.current=event.currentTarget.scrollTop;}}>
            {tickets.map(ticket=>
            <div key={ticket.ticketId} className={`ticket-pin-row${ticket.isPinned&&tab==='active'?' is-pinned':''}`}>
              <button type="button"
                className={`plenaro-task-row${selectedTicketId===ticket.ticketId?' selected':''}`}
                onClick={()=>setSelectedTicketId(ticket.ticketId)}>
                <span><strong>{!!ticket.unreadArticleCount&&
                  <Star role="img" aria-label="Neue ungelesene Ticket-Nachricht"
                    className="ticket-unread-star" size={17}/>} {ticket.title}</strong>
                  <small>#{ticket.ticketNumber||ticket.ticketId} · {ticket.queue}</small></span>
                <span className="tasks-state">{ticket.state}</span>
              </button>
              {tab==='active'&&<button type="button"
                className={`ticket-pin-toggle${ticket.isPinned?' is-pinned':''}`}
                aria-label={`Ticket #${ticket.ticketNumber||ticket.ticketId} ${ticket.isPinned?'Pin lösen':'anpinnen'}`}
                title={ticket.isPinned?'Von oben lösen':'Oben anpinnen'}
                aria-pressed={!!ticket.isPinned} disabled={!!pinBusy}
                onClick={()=>void togglePin(ticket)}>
                <Pin size={16} fill={ticket.isPinned?'currentColor':'none'}/>
              </button>}
            </div>
          )}</div>}
          {tab!=='today'&&!listLoading&&(page>0||tickets.length===100)&&
            <div className="znuny-ticket-pagination">
              <button type="button" className="button button-secondary" disabled={page===0}
                onClick={()=>{setPage(p=>Math.max(p-1,0));setSelectedTicketId(null);}}>Zurück</button>
              <span>Seite {page+1}</span>
              <button type="button" className="button button-secondary"
                disabled={tickets.length<100||page>=1000}
                onClick={()=>{setPage(p=>p+1);setSelectedTicketId(null);}}>Weiter</button>
            </div>}
        </div>
      </section>
      <aside id="plenaro-ticket-details" className="plenaro-today-right"
        aria-label="Ticketdetails und Planung">
        {selectedAppointment?<section className="content-card plenaro-today-detail"
          aria-label="Outlook-Termindetails">
          <span className="eyebrow">TASK DETAILS / OUTLOOK</span>
          <div className="plenaro-agenda-appointment">
            <h2>{selectedAppointment.title}</h2>
            <p><Clock3 size={15}/>{agendaTimeLabel(selectedAppointment)}</p>
            {selectedAppointment.location&&<p><MapPin size={15}/>{selectedAppointment.location}</p>}
            {selectedAppointment.webLink&&<a className="button button-secondary"
              href={selectedAppointment.webLink} target="_blank" rel="noopener noreferrer">
              <ExternalLink size={14}/>In Outlook öffnen</a>}
            <p className="um-hint">Outlook-Termin, nur lesbar.</p>
          </div>
        </section>:selectedTicket?<ClassicTicketDetailPanel
          key={selectedTicket.ticketId} ticket={selectedTicket}
          detailsExpanded={detailsExpanded}
          onToggleDetailsExpanded={toggleDetailsExpanded}
          onChanged={()=>setRefresh(x=>x+1)}/>:
          <section className="content-card plenaro-today-detail" aria-label="Task Details">
            <span className="eyebrow">TASK DETAILS</span>
            <div className="classic-detail-empty">
              <h2>{tab==='today'?'Tagespunkt auswählen':'Ticket auswählen'}</h2>
              <p>Wähle links einen Termin oder ein Ticket, um die Details zu öffnen.</p>
            </div>
          </section>}
      </aside>
    </div>}
  </div>;
}
