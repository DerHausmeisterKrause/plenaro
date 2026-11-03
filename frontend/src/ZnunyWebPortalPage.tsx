import {useCallback,useEffect,useRef,useState} from 'react';
import type {MouseEvent as ReactMouseEvent,KeyboardEvent as ReactKeyboardEvent} from 'react';
import {Link,useSearchParams} from 'react-router-dom';
import {ArrowLeft,ArrowRight,ClipboardPaste,RefreshCw,ShieldCheck,Ticket,WifiOff} from 'lucide-react';
import {getZnunyPersonal,getZnunyServer} from './znunyApi';

const api='/api/v1/znuny/browser';
type RemoteInput={
  kind:'click'|'scroll'|'key'|'text'|'back'|'forward'|'refresh';
  x?:number;y?:number;deltaY?:number;key?:string;text?:string;
  ctrl?:boolean;shift?:boolean;alt?:boolean;
};

/**
 * The visible Znuny GUI is rendered by Chromium INSIDE Docker.
 * The local browser receives PNG frames and sends mouse/keyboard commands
 * to authenticated Plenaro endpoints. No Znuny HTML/JS/cookies/credentials
 * execute or exist on the user's computer and no extra public port is needed.
 */
export function ZnunyWebPortalPage(){
  const [params]=useSearchParams();
  const ticket=params.get('ticket');
  const validTicket=ticket===null||/^\d{1,40}$/.test(ticket);
  const [ready,setReady]=useState(false);
  const [loading,setLoading]=useState(true);
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('Starte die interne Chromium-Webansicht …');
  const [frame,setFrame]=useState<string|null>(null);
  const [pasteText,setPasteText]=useState('');
  const [revision,setRevision]=useState(0);
  const csrfRef=useRef('');
  const frameRef=useRef<HTMLImageElement|null>(null);
  const viewRef=useRef<HTMLDivElement|null>(null);
  const actionQueue=useRef<Promise<void>>(Promise.resolve());
  const loadingFrame=useRef(false);
  const lastFrameAt=useRef(0);
  const alive=useRef(true);

  const fetchFrame=useCallback(async()=>{
    if(!alive.current||loadingFrame.current||document.visibilityState==='hidden'||
      Date.now()-lastFrameAt.current<500)return;
    loadingFrame.current=true;
    lastFrameAt.current=Date.now();
    try{
      const response=await fetch(api+'/frame?ts='+Date.now(),{
        credentials:'same-origin',cache:'no-store'
      });
      if(!response.ok){
        const data=await response.json().catch(()=>({})) as {error?:string};
        throw new Error(data.error||'Webansicht nicht erreichbar (HTTP '+response.status+').');
      }
      const blob=await response.blob();
      if(!alive.current)return;
      if(!blob.type.startsWith('image/'))throw new Error('Ungültige Webansicht-Antwort.');
      setFrame(URL.createObjectURL(blob));
      setError('');
    }catch(e){
      if(alive.current)setError(e instanceof Error?e.message:'Webansicht nicht erreichbar.');
    }finally{loadingFrame.current=false;}
  },[]);

  useEffect(()=>{
    alive.current=true;setReady(false);setLoading(true);setFrame(null);setError('');
    setNotice('Starte die interne Chromium-Webansicht …');
    let active=true;
    void(async()=>{
      try{
        if(!validTicket)throw new Error('Ungültige Ticket-ID.');
        const [personal,server]=await Promise.all([getZnunyPersonal(),getZnunyServer()]);
        if(!active)return;
        if(!personal.vaultReady||!personal.passwordConfigured||
          !personal.username.trim()||!personal.agentId)
          throw new Error('Bitte unter Einstellungen die persönlichen Znuny-Zugangsdaten speichern und die Agenten-ID vom Administrator zuweisen lassen.');
        if(new URL(server.webUrl).hostname.toLowerCase()==='server')
          throw new Error('Die Znuny-Webadresse ist noch nicht konfiguriert.');
        const csrf=await fetch('/api/v1/auth/csrf',{credentials:'same-origin'})
          .then(async r=>r.ok?await r.json() as {token:string}:Promise.reject(new Error('Sicherheitstoken fehlt.')));
        if(!active)return;
        csrfRef.current=csrf.token;
        const response=await fetch(api+'/start',{
          method:'POST',credentials:'same-origin',
          headers:{'X-CSRF-TOKEN':csrf.token,'Content-Type':'application/json'},
          body:JSON.stringify({ticketId:ticket,reset:revision>0})
        });
        if(!response.ok){
          const details=await response.json().catch(()=>({})) as {error?:string};
          throw new Error(details.error||'Interner Browserstart fehlgeschlagen (HTTP '+response.status+').');
        }
        if(!active)return;
        setReady(true);setNotice('Znuny läuft serverseitig. Klicke in die Ansicht, um Maus und Tastatur zu verwenden.');
        await fetchFrame();
      }catch(e){
        if(active)setError(e instanceof Error?e.message:'Die Znuny-Webansicht konnte nicht gestartet werden.');
      }finally{if(active)setLoading(false);}
    })();
    return()=>{active=false;alive.current=false;};
  },[ticket,revision,validTicket,fetchFrame]);

  useEffect(()=>{return()=>{if(frame)URL.revokeObjectURL(frame);};},[frame]);

  useEffect(()=>{
    if(!ready)return;
    const timer=window.setInterval(()=>void fetchFrame(),700);
    return()=>window.clearInterval(timer);
  },[ready,fetchFrame]);

  const send=useCallback((input:RemoteInput)=>{
    if(!ready)return;
    actionQueue.current=actionQueue.current.then(async()=>{
      if(!alive.current)return;
      const response=await fetch(api+'/input',{
        method:'POST',credentials:'same-origin',
        headers:{'X-CSRF-TOKEN':csrfRef.current,'Content-Type':'application/json'},
        body:JSON.stringify(input)
      });
      if(!response.ok){
        const details=await response.json().catch(()=>({})) as {error?:string};
        throw new Error(details.error||'Browseraktion fehlgeschlagen (HTTP '+response.status+').');
      }
      // Do not block the keyboard command queue on screenshot latency.
      // Frames are separately refreshed/polled with only one request in flight.
      void fetchFrame();
    }).catch(e=>{if(alive.current)setError(e instanceof Error?e.message:'Browseraktion fehlgeschlagen.');});
  },[ready,fetchFrame]);

  function position(e:ReactMouseEvent<HTMLImageElement>){
    const image=frameRef.current;
    if(!image)return {x:0,y:0};
    const rect=image.getBoundingClientRect();
    return {
      x:Math.max(0,Math.min(image.naturalWidth||1280,
        Math.round((e.clientX-rect.left)/rect.width*(image.naturalWidth||1280)))),
      y:Math.max(0,Math.min(image.naturalHeight||800,
        Math.round((e.clientY-rect.top)/rect.height*(image.naturalHeight||800))))
    };
  }
  function keyDown(e:ReactKeyboardEvent<HTMLDivElement>){
    if(e.target!==viewRef.current)return;
    if((e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==='v')return;
    if(e.key.length===1||[
      'Tab','Enter','Backspace','Delete','Escape','ArrowUp','ArrowDown',
      'ArrowLeft','ArrowRight','Home','End','PageUp','PageDown'
    ].includes(e.key)){
      e.preventDefault();
      send({kind:'key',key:e.key,ctrl:e.ctrlKey,alt:e.altKey,shift:e.shiftKey});
    }
  }
  // React's delegated wheel listener can be passive. Register one native,
  // non-passive listener so scrolling moves Znuny, not the Plenaro page.
  useEffect(()=>{
    const image=frameRef.current;
    if(!image||!ready)return;
    const onWheel=(event:WheelEvent)=>{
      event.preventDefault();
      const rect=image.getBoundingClientRect();
      const x=Math.max(0,Math.min(image.naturalWidth||1280,
        Math.round((event.clientX-rect.left)/rect.width*(image.naturalWidth||1280))));
      const y=Math.max(0,Math.min(image.naturalHeight||800,
        Math.round((event.clientY-rect.top)/rect.height*(image.naturalHeight||800))));
      send({kind:'scroll',x,y,
        deltaY:Math.max(-1200,Math.min(1200,Math.round(event.deltaY)))});
    };
    image.addEventListener('wheel',onWheel,{passive:false});
    return()=>image.removeEventListener('wheel',onWheel);
  },[ready,frame,send]);

  const reload=()=>setRevision(n=>n+1);

  return <div className="page znuny-web-portal">
    <div className="znuny-web-portal-toolbar content-card">
      <div>
        <span className="eyebrow"><Ticket size={14}/>Plenaro / Ticketsystem</span>
        <h1>Znuny-Weboberfläche</h1>
        <p>Wie im Desktop-Client: eine interaktive Browseransicht – jetzt vollständig im Plenaro-Docker ausgeführt.</p>
      </div>
      <div className="znuny-browser-toolbar">
        <button type="button" className="button button-secondary" disabled={!ready}
          title="Zurück" onClick={()=>send({kind:'back'})}><ArrowLeft size={15}/></button>
        <button type="button" className="button button-secondary" disabled={!ready}
          title="Vorwärts" onClick={()=>send({kind:'forward'})}><ArrowRight size={15}/></button>
        <button type="button" className="button button-secondary"
          title="Browser neu starten" onClick={reload}><RefreshCw size={15}/>Neu laden</button>
      </div>
    </div>
    {!validTicket&&<p role="alert" className="um-feedback failure">
      Ungültige Ticket-ID. <Link to="/ticket-system">Startseite öffnen</Link>
    </p>}
    {error&&<div className="content-card znuny-portal-error" role="alert">
      <WifiOff size={20}/><strong>Znuny-Browser</strong><p>{error}</p>
      <button type="button" className="button button-secondary" onClick={reload}>
        <RefreshCw size={14}/>Erneut versuchen
      </button>
      <Link className="button button-secondary" to="/settings">Znuny-Einstellungen</Link>
    </div>}
    {validTicket&&<div className="znuny-portal-frame-wrap">
      <div className="znuny-portal-security-caption">
        <ShieldCheck size={14}/>
        {notice} Der PC benötigt weder Zugriff auf Znuny noch einen zusätzlichen Port.
      </div>
      {loading&&!frame&&<div className="znuny-browser-loading" role="status">
        Interne Chromium-Webansicht wird gestartet …
      </div>}
      {frame&&<div className="znuny-browser-surface"
        tabIndex={0} ref={viewRef} onKeyDown={keyDown}
        onPaste={e=>{e.preventDefault();send({kind:'text',text:e.clipboardData.getData('text')});}}
        aria-label="Interaktive Znuny-Webansicht">
        <img ref={frameRef} src={frame} alt="Znuny-Browserbild (serverseitig gerendert)"
          draggable={false} onClick={e=>{
            send({kind:'click',...position(e)});
            viewRef.current?.focus();
          }}
          />
      </div>}
      {ready&&<div className="znuny-browser-paste">
        <label htmlFor="znuny-browser-paste-text">Text in das ausgewählte Znuny-Feld einfügen</label>
        <textarea id="znuny-browser-paste-text" rows={2} value={pasteText}
          onChange={e=>setPasteText(e.target.value)}
          placeholder="Optional: längeren Antworttext hier einfügen"/>
        <button className="button button-secondary" type="button"
          disabled={!pasteText} onClick={()=>{
            send({kind:'text',text:pasteText.slice(0,4000)});
            setPasteText('');
            viewRef.current?.focus();
          }}><ClipboardPaste size={14}/>Einfügen</button>
      </div>}
    </div>}
  </div>;
}
