import { useEffect, useState } from 'react';
import { CalendarDays, Clock3, ExternalLink, MapPin, RefreshCw, Ticket } from 'lucide-react';
import { Link } from 'react-router-dom';
import { localDate } from './tasksApi';
import { getMonthMarkers, saveDayMarker, type DayMarker, type DayType } from './dayMarkersApi';
import { graphStatus, readOutlookWeek, type OutlookEvent } from './graphApi';
import { ClassicTicketDetailPanel } from './ClassicTicketDetailPanel';
import {
  getCachedTickets, getTicketAccess, getTicketAgenda,
  type CachedTicket, type TicketAccess, type TicketAgendaSegment,
} from './znunySyncApi';
import { agendaTimeLabel, buildTodayAgenda, localDayBounds } from './todayAgenda';

type Tab = 'today' | 'active' | 'new' | 'completed';
const labels: {key:Tab;label:string}[] = [
  {key:'today',label:'Heute'},
  {key:'active',label:'Aktuelle Aufgaben'},
  {key:'new',label:'Neue Aufgaben'},
  {key:'completed',label:'Erledigte Aufgaben'},
];

export function ClassicTodayPage() {
  const [day,setDay]=useState(()=>localDate());
  const [tab,setTab]=useState<Tab>('today');
  const [tickets,setTickets]=useState<CachedTicket[]>([]);
  const [ticketAgenda,setTicketAgenda]=useState<TicketAgendaSegment[]>([]);
  const [outlook,setOutlook]=useState<OutlookEvent[]>([]);
  const [access,setAccess]=useState<TicketAccess|null>(null);
  const [marker,setMarker]=useState<DayMarker|null>(null);
  const [selectedTicketId,setSelectedTicketId]=useState<string|null>(null);
  const [selectedAgendaKey,setSelectedAgendaKey]=useState<string|null>(null);
  const [page,setPage]=useState(0);
  const [loading,setLoading]=useState(true);
  const [listLoading,setListLoading]=useState(false);
  const [busy,setBusy]=useState(false);
  const [outlookConnected,setOutlookConnected]=useState(false);
  const [calendarError,setCalendarError]=useState('');
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('');
  const [refresh,setRefresh]=useState(0);

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
    if(tab==='today')return;
    let active=true;
    setListLoading(true);setError('');
    const view=tab==='active'?'mine':tab;
    void getCachedTickets(view,'',page)
      .then(rows=>{
        if(!active)return;
        setTickets(rows);
        setSelectedTicketId(previous=>rows.some(t=>t.ticketId===previous)?previous:null);
        setListLoading(false);
      })
      .catch(e=>{if(active){setError(e instanceof Error?e.message:'Tickets nicht verfügbar.');setListLoading(false);}});
    return ()=>{active=false;};
  },[tab,page,refresh]);

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
    <div className="plenaro-today-columns">
      <section className="plenaro-today-left" aria-label="Arbeitsbereich Heute">
        <div className="content-card plenaro-today-quick">
          <p>Znuny-Tickets werden zentral synchronisiert. Eigene lokale Aufgaben
            kannst du weiterhin im separaten Aufgabenbereich anlegen.</p>
          <Link to="/tasks" className="button button-secondary">Lokale Aufgaben öffnen</Link>
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
          <div className="plenaro-tabs" role="tablist" aria-label="Aufgabenansichten">
            {labels.map(item=><button role="tab" type="button" key={item.key}
              className={tab===item.key?'active':''} aria-selected={tab===item.key}
              onClick={()=>changeTab(item.key)}>{item.label}</button>)}
          </div>
          <div className="plenaro-list-title">
            <h2>{labels.find(item=>item.key===tab)?.label}:</h2>
            <button type="button" className="classic-text-action" aria-label="Ansicht aktualisieren"
              onClick={()=>setRefresh(x=>x+1)}><RefreshCw size={16}/>Aktualisieren</button>
          </div>
          {tab==='today'&&outlookConnected&&calendarError&&
            <p role="status" className="um-hint">{calendarError}</p>}
          {!access?.approved&&!loading&&<p className="um-hint">
            Für Znuny-Tickets muss ein Administrator deine Agenten-ID hinterlegen.
            Outlook-Termine bleiben trotzdem sichtbar.</p>}
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
            <p>{tab==='new'?'Keine unzugewiesenen Tickets im zentralen Cache.':
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
    </div>
  </div>;
}
