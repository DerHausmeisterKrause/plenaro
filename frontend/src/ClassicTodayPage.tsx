import { useEffect, useState } from 'react';
import { CalendarDays, Clock3, ExternalLink, MapPin, RefreshCw, Search, SlidersHorizontal, Ticket } from 'lucide-react';
import { useLocation } from 'react-router-dom';
import { ClassicTasksPage } from './ClassicTasksPage';
import { localDate } from './tasksApi';
import { getMonthMarkers, saveDayMarker, type DayMarker, type DayType } from './dayMarkersApi';
import { graphStatus, readOutlookWeek, type OutlookEvent } from './graphApi';
import { ClassicTicketDetailPanel } from './ClassicTicketDetailPanel';
import {
  getCachedTickets, getTicketAccess, getTicketAgenda, setShowAllCandidates,
  type CachedTicket, type TicketAccess, type TicketAgendaSegment, type TicketListFilters,
} from './znunySyncApi';
import { agendaTimeLabel, buildTodayAgenda, localDayBounds } from './todayAgenda';

type Tab = 'today' | 'active' | 'new' | 'completed' | 'local';
const labels: {key:Tab;label:string}[] = [
  {key:'today',label:'Heute'},
  {key:'active',label:'Aktuelle Aufgaben'},
  {key:'new',label:'Neue Aufgaben'},
  {key:'completed',label:'Erledigte Aufgaben'},
  {key:'local',label:'Lokale Aufgaben'},
];

export function ClassicTodayPage() {
  const location=useLocation();
  const [day,setDay]=useState(()=>localDate());
  const [tab,setTab]=useState<Tab>(()=>new URLSearchParams(location.search).has('task')?'local':'today');
  const [tickets,setTickets]=useState<CachedTicket[]>([]);
  const [ticketAgenda,setTicketAgenda]=useState<TicketAgendaSegment[]>([]);
  const [outlook,setOutlook]=useState<OutlookEvent[]>([]);
  const [access,setAccess]=useState<TicketAccess|null>(null);
  const [marker,setMarker]=useState<DayMarker|null>(null);
  const [selectedTicketId,setSelectedTicketId]=useState<string|null>(null);
  const [selectedAgendaKey,setSelectedAgendaKey]=useState<string|null>(null);
  const [page,setPage]=useState(0);
  const [ticketSearch,setTicketSearch]=useState('');
  const [searchDebounced,setSearchDebounced]=useState('');
  const [filters,setFilters]=useState<TicketListFilters>({
    query:'',queue:'',state:'',sort:'changed',direction:'desc'
  });
  const [filtersOpen,setFiltersOpen]=useState(false);
  const [toggleBusy,setToggleBusy]=useState(false);
  const [loading,setLoading]=useState(true);
  const [listLoading,setListLoading]=useState(false);
  const [busy,setBusy]=useState(false);
  const [outlookConnected,setOutlookConnected]=useState(false);
  const [calendarError,setCalendarError]=useState('');
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('');
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
    const [yy,mm]=day.slice(0,7).split('-').map(Number);
    void Promise.all([getTicketAgenda(day),getMonthMarkers(yy,mm),getTicketAccess()])
      .then(([segments,days,currentAccess])=>{
        if(!active)return;
        setTicketAgenda(segments);
        setMarker(days.find(item=>item.day===day)??null);
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
  const selectedAppointment=tab==='today'
    ?agenda.find(a=>a.kind==='outlook'&&a.key===selectedAgendaKey)
    :undefined;
  const selectedTicket=tab==='today'
    ?(agenda.some(a=>a.kind==='ticket'&&a.key===selectedAgendaKey&&a.ticketId===selectedTicketId)
      ?ticketAgenda.find(t=>t.ticketId===selectedTicketId):undefined)
    :tickets.find(t=>t.ticketId===selectedTicketId);

  function changeTab(next:Tab) {
    setTab(next);setPage(0);setSelectedTicketId(null);setSelectedAgendaKey(null);
    setTicketSearch('');setSearchDebounced('');
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
  async function setMarkerValue(type:DayType,isBr:boolean,isHo:boolean) {
    if(busy)return;
    setBusy(true);setError('');setNotice('');
    try{
      await saveDayMarker(day,type,isBr,isHo);
      setRefresh(x=>x+1);setNotice('Tagesmarker gespeichert.');
    }catch(e){setError(e instanceof Error?e.message:'Tagesmarker konnte nicht gespeichert werden.');}
    finally{setBusy(false);}
  }
  const today=new Date(day+'T12:00:00');

  return <div className="page plenaro-today">
    {error&&<p role="alert" className="um-feedback failure">{error}</p>}
    {notice&&<p role="status" className="um-feedback success">{notice}</p>}
    <div className="plenaro-tabs content-card" role="tablist" aria-label="Arbeitsplatz-Ansichten">
      {labels.map(item=><button role="tab" type="button" key={item.key}
        className={tab===item.key?'active':''} aria-selected={tab===item.key}
        onClick={()=>changeTab(item.key)}>{item.label}</button>)}
    </div>
    {tab==='local'?<ClassicTasksPage/>:<div className="plenaro-today-columns">
      <section className="plenaro-today-left" aria-label="Arbeitsplatz-Ticketbereich">
        <div className="content-card plenaro-today-quick">
          <p>Hier bearbeitest du deine Znuny-Tickets und Termine.
            Lokale Aufgaben findest du direkt im Reiter „Lokale Aufgaben“.</p>
        </div>
        <div className="content-card plenaro-workday">
          <div>
            <h2>Arbeitstag heute</h2>
            <p>{new Intl.DateTimeFormat('de-DE',{dateStyle:'full'}).format(today)}</p>
            <p className="um-hint">Tagesmarker für deine persönliche Planung</p>
          </div>
          <div className="plenaro-marker-actions" aria-label="Tagesart einstellen">
            {(['Normal','UL','AM'] as DayType[]).map(type=><button key={type}
              type="button" disabled={busy}
              className={`plenaro-marker-pill${(marker?.dayType??'Normal')===type?' active':''}`}
              aria-pressed={(marker?.dayType??'Normal')===type}
              onClick={()=>void setMarkerValue(type,marker?.isBr??false,marker?.isHo??false)}>
              {type}</button>)}
            <button type="button" disabled={busy}
              className={`plenaro-marker-pill${marker?.isHo?' active':''}`}
              aria-pressed={marker?.isHo??false}
              onClick={()=>void setMarkerValue(marker?.dayType??'Normal',
                marker?.isBr??false,!marker?.isHo)}>HO</button>
            <button type="button" disabled={busy}
              className={`plenaro-marker-pill${marker?.isBr?' active':''}`}
              aria-pressed={marker?.isBr??false}
              onClick={()=>void setMarkerValue(marker?.dayType??'Normal',
                !marker?.isBr,marker?.isHo??false)}>BR</button>
          </div>
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
            <label className="classic-search">
              <Search size={16}/>
              <input aria-label="Tickets suchen" value={ticketSearch} maxLength={100}
                placeholder="Volltextsuche: Ticket, Betreff, E-Mails, Notizen …"
                onChange={e=>{setPage(0);setTicketSearch(e.target.value);}}/>
            </label>
            {(tab==='active'||tab==='new')&&<>
              <div className="znuny-list-filter-toggle">
                <button className="button button-secondary" type="button"
                  aria-expanded={filtersOpen} onClick={()=>setFiltersOpen(v=>!v)}>
                  <SlidersHorizontal size={14}/>Filter und Sortierung
                </button>
              </div>
              {filtersOpen&&<div className="znuny-list-filter-fields">
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
          {(tab==='today'?loading:listLoading)?<p role="status" className="um-hint">
            {tab==='today'?'Lade Tagesagenda …':'Lade Tickets …'}</p>:
          tab==='today'?(agenda.length===0
            ?<p className="plenaro-empty-list">Heute keine Outlook-Termine oder geplanten Ticketsegmente.</p>
            :<div className="plenaro-rows" aria-label="Tagesagenda">
              {agenda.map(item=><button key={item.key} type="button"
                className={`plenaro-task-row plenaro-agenda-row${selectedAgendaKey===item.key?' selected':''}`}
                onClick={()=>{setSelectedAgendaKey(item.key);setSelectedTicketId(item.ticketId);}}>
                <span className="plenaro-agenda-time">
                  {item.allDay?<CalendarDays size={15}/>:<Clock3 size={15}/>}
                  {agendaTimeLabel(item)}
                </span>
                <span className="plenaro-agenda-content">
                  <strong>{item.title}</strong>
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
          <div className="plenaro-rows">{tickets.map(ticket=><button
            key={ticket.ticketId} type="button"
            className={`plenaro-task-row${selectedTicketId===ticket.ticketId?' selected':''}`}
            onClick={()=>setSelectedTicketId(ticket.ticketId)}>
            <span><strong>{ticket.title}</strong>
              <small>#{ticket.ticketNumber||ticket.ticketId} · {ticket.queue}</small></span>
            <span className="tasks-state">{ticket.state}</span>
          </button>)}</div>}
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
      <aside className="plenaro-today-right" aria-label="Ticketdetails und Planung">
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
