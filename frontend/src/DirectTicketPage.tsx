import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ClassicTicketDetailPanel } from './ClassicTicketDetailPanel';
import { getTicketSummary, type CachedTicket } from './znunySyncApi';

export function DirectTicketPage() {
  const {ticketId=''}=useParams();
  return <TicketDetail key={ticketId} ticketId={ticketId}/>;
}

function TicketDetail({ticketId}:{ticketId:string}) {
  const [ticket,setTicket]=useState<CachedTicket|null>(null);
  const [error,setError]=useState('');
  const [refresh,setRefresh]=useState(0);
  useEffect(()=>{
    let active=true;
    void getTicketSummary(ticketId).then(value=>{
      if(active){setTicket(value);setError('');}
    }).catch(e=>{
      if(active){setTicket(null);setError(e instanceof Error?e.message:'Ticket nicht verfügbar.');}
    });
    return()=>{active=false;};
  },[ticketId,refresh]);
  return <div className="page plenaro-today">
    <Link to="/workspace" className="classic-text-action">Zurück zum Arbeitsplatz</Link>
    {error?<p role="alert" className="um-feedback failure">{error}</p>:
      ticket?<ClassicTicketDetailPanel key={ticketId} ticket={ticket}
        onChanged={()=>setRefresh(x=>x+1)}/>:
        <p role="status">Ticket wird geladen …</p>}
  </div>;
}
