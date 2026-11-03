import {useEffect,useState} from 'react';
import {RefreshCw,UserRoundCheck} from 'lucide-react';
import type {CachedTicket} from './znunySyncApi';
import {
  assignZnunyToMe,readZnunyAgents,updateZnunyAssignment,
  type ZnunyAgent,type ZnunyUpdateConfirmation,
} from './znunySyncApi';

export function ZnunyTicketAssignmentPanel({ticket,onChanged}:{
  ticket:CachedTicket;onChanged?:()=>void;
}){
  const [agents,setAgents]=useState<ZnunyAgent[]>([]);
  const [loaded,setLoaded]=useState(false);
  const [owner,setOwner]=useState<number|null>(ticket.ownerId);
  const [responsible,setResponsible]=useState<number|null>(ticket.responsibleId);
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('');
  const closed=ticket.stateType.toLowerCase()==='closed';
  useEffect(()=>{
    setOwner(ticket.ownerId);setResponsible(ticket.responsibleId);
    setError('');setNotice('');
  },[ticket.ticketId,ticket.ownerId,ticket.responsibleId]);

  useEffect(()=>{
    let live=true;
    setLoaded(false);setAgents([]);
    const load=async()=>{
      try{
        const names=await readZnunyAgents();
        if(live){setAgents(names);setLoaded(true);setError('');}
      }catch(e){
        if(live){setLoaded(false);
          setError(e instanceof Error?e.message:'Agentennamen nicht verfügbar.');}
      }
    };
    void load();
    const timer=window.setInterval(()=>{
      if(!document.hidden)void load();
    },5*60_000);
    return()=>{live=false;window.clearInterval(timer);};
  },[ticket.ticketId]);
  async function refresh(){
    setBusy(true);setError('');
    try{
      setAgents(await readZnunyAgents());setLoaded(true);
    }catch(e){
      setError(e instanceof Error?e.message:'Agentenliste nicht verfügbar.');
    }finally{setBusy(false);}
  }
  async function run(action:()=>Promise<ZnunyUpdateConfirmation>,success:string){
    if(busy)return;
    setBusy(true);setError('');setNotice('');
    try{
      const confirmation=await action();
      if(!confirmation.confirmed)throw new Error('Znuny hat die Änderung nicht bestätigt.');
      setNotice(success+(confirmation.cacheUpdated===false
        ?' Ticketcache wird beim nächsten Sync aktualisiert.':''));
      onChanged?.();
    }catch(e){setError(e instanceof Error?e.message:'Zuweisung fehlgeschlagen.');}
    finally{setBusy(false);}
  }
  const options=(value:number|null)=>{
    const list=[...agents];
    if(value!==null&&!list.some(a=>a.id===value))
      list.unshift({id:value,name:'Agent-ID '+value});
    return list;
  };
  const nameOf=(id:number|null)=>id===null||id===0?'Nicht vergeben':
    agents.find(agent=>agent.id===id)?.name??'Agent-ID '+id;
  const changed=owner!==ticket.ownerId||responsible!==ticket.responsibleId;
  return <section className="classic-wpf-assignment" aria-label="Zuweisung">
    <h3>Zuweisung</h3>
    <div className="znuny-assignment-current" aria-label="Aktuelle Ticketzuweisung">
      <div><span>Besitzer</span><strong>{nameOf(ticket.ownerId)}</strong></div>
      <div><span>Verantwortlicher</span><strong>{nameOf(ticket.responsibleId)}</strong></div>
    </div>
    <div className="classic-wpf-assignment-fields">
      <label>Besitzer
        <select aria-label="Ticket Besitzer" value={owner??''}
          disabled={!loaded||closed||busy} onChange={e=>setOwner(
            e.target.value===''?null:Number(e.target.value))}>
          <option value="" disabled={ticket.ownerId!==null}>Nicht vergeben</option>
          {options(owner).map(a=><option key={a.id} value={a.id}>{a.name} (#{a.id})</option>)}
        </select>
      </label>
      <label>Verantwortlicher
        <select aria-label="Ticket Verantwortlicher" value={responsible??''}
          disabled={!loaded||closed||busy} onChange={e=>setResponsible(
            e.target.value===''?null:Number(e.target.value))}>
          <option value="" disabled={ticket.responsibleId!==null}>Nicht vergeben</option>
          {options(responsible).map(a=><option key={a.id} value={a.id}>{a.name} (#{a.id})</option>)}
        </select>
      </label>
    </div>
    <div className="classic-wpf-actions">
      <button type="button" className="button button-secondary" disabled={busy}
        onClick={()=>void refresh()}><RefreshCw size={14}/>Jetzt aktualisieren</button>
      <button type="button" className="button button-secondary"
        disabled={busy||!loaded||!changed||closed||(!owner&&!responsible)}
        onClick={()=>void run(()=>updateZnunyAssignment(ticket.ticketId,
          owner===ticket.ownerId?null:owner,
          responsible===ticket.responsibleId?null:responsible),
        'Zuweisung von Znuny bestätigt.')}>Änderungen übernehmen</button>
      {!closed&&<button type="button" className="button button-secondary"
        disabled={busy} onClick={()=>void run(()=>assignZnunyToMe(ticket.ticketId),
          'Ticket wurde dir in Znuny zugewiesen.')}>
        <UserRoundCheck size={14}/>Mir zuweisen</button>}
    </div>
    <p className="classic-wpf-help">
      Agentennamen werden beim Öffnen automatisch geladen und alle fünf Minuten
      aktualisiert. Änderungen erfordern deine persönlichen Znuny-Zugangsdaten
      und werden direkt in Znuny ausgeführt.</p>
    {error&&<p role="alert" className="um-feedback failure">{error}</p>}
    {notice&&<p role="status" className="um-feedback success">{notice}</p>}
  </section>;
}
