import {useCallback,useEffect,useRef,useState} from 'react';
import type {MouseEvent as ReactMouseEvent,KeyboardEvent as ReactKeyboardEvent} from 'react';
import {Link,useSearchParams} from 'react-router-dom';
import {ArrowLeft,ArrowRight,RefreshCw,ShieldCheck,Ticket,WifiOff} from 'lucide-react';
import {getZnunyPersonal,getZnunyServer} from './znunyApi';

const api='/api/v1/znuny/browser';
type RemoteInput={
  kind:'click'|'scroll'|'key'|'text'|'back'|'forward'|'refresh';
  x?:number;y?:number;deltaY?:number;key?:string;text?:string;
  ctrl?:boolean;shift?:boolean;alt?:boolean;
};

/**
 * The visible Znuny GUI is rendered by Chromium INSIDE Docker.
 * The local browser receives streamed JPEG frames and sends mouse/keyboard commands
 * to authenticated Plenaro endpoints. No Znuny HTML/JS/cookies/credentials
 * execute or exist on the user's computer and no extra public port is needed.
 */
function LegacyZnunyWebPortalPage(){
  const [params]=useSearchParams();
  const ticket=params.get('ticket');
  const validTicket=ticket===null||/^\d{1,40}$/.test(ticket);
  const [ready,setReady]=useState(false);
  const [loading,setLoading]=useState(true);
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('Starte die interne Chromium-Webansicht …');
  const [frame,setFrame]=useState<string|null>(null);
  const [live,setLive]=useState(false);
  const liveRef=useRef(false);
  const lastViewport=useRef('');
  const [revision,setRevision]=useState(0);
  const csrfRef=useRef('');
  const frameRef=useRef<HTMLImageElement|null>(null);
  const viewRef=useRef<HTMLDivElement|null>(null);
  const actionQueue=useRef<Promise<void>>(Promise.resolve());
  const streamRef=useRef<EventSource|null>(null);
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
    liveRef.current=false;setLive(false);lastViewport.current='';
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
    return()=>{active=false;alive.current=false;streamRef.current?.close();};
  },[ticket,revision,validTicket,fetchFrame]);

  useEffect(()=>{return()=>{if(frame?.startsWith('blob:'))URL.revokeObjectURL(frame);};},[frame]);

  // Native Chromium screencast (one long-lived HTTP response). No websocket,
  // browser credentials or third-party connection is exposed to the client.
  // If the Selenium CDP stream is unavailable, keep the secure polling fallback.
  useEffect(()=>{
    if(!ready)return;
    let active=true;
    // Older clients and test environments can use the polling fallback.
    if(typeof EventSource==='undefined')return;
    const source=new EventSource(api+'/stream');
    streamRef.current=source;
    source.addEventListener('frame',(event)=>{
      if(!active)return;
      const image=(event as MessageEvent).data as string;
      if(!image||image.length>9000000)return;
      liveRef.current=true;
      setLive(true);
      setFrame('data:image/jpeg;base64,'+image);
    });
    source.addEventListener('unavailable',()=>{
      if(!active)return;
      liveRef.current=false;setLive(false);
      setNotice('Browseransicht im kompatiblen Bildmodus.');
      source.close();
    });
    source.onerror=()=>{
      if(!active)return;
      liveRef.current=false;setLive(false);
      setNotice('Browseransicht im kompatiblen Bildmodus.');
      source.close();
    };
    return()=>{active=false;source.close();if(streamRef.current===source)streamRef.current=null;
      liveRef.current=false;};
  },[ready]);

  // Match Chromium's *real* viewport to the content area. Previously a fixed
  // 1280x800 capture was constrained to 1280px while the panel was wider.
  useEffect(()=>{
    if(!ready||!viewRef.current)return;
    const surface=viewRef.current;
    let timer:number|undefined;
    let active=true;
    const apply=()=>{
      if(!active||!csrfRef.current)return;
      const rect=surface.getBoundingClientRect();
      const width=Math.max(480,Math.min(2400,Math.round(rect.width)));
      const height=Math.max(320,Math.min(1600,Math.round(rect.height)));
      const current=width+'x'+height;
      if(lastViewport.current===current)return;
      lastViewport.current=current;
      void fetch(api+'/viewport',{
        method:'POST',credentials:'same-origin',
        headers:{'X-CSRF-TOKEN':csrfRef.current,'Content-Type':'application/json'},
        body:JSON.stringify({width,height})
      }).catch(()=>{lastViewport.current='';});
    };
    const schedule=()=>{if(timer!==undefined)window.clearTimeout(timer);
      timer=window.setTimeout(apply,180);};
    if(typeof ResizeObserver==='undefined'){schedule();return()=>{active=false;
      if(timer!==undefined)window.clearTimeout(timer);};}
    const observer=new ResizeObserver(schedule);
    observer.observe(surface);
    schedule();
    return()=>{active=false;observer.disconnect();if(timer!==undefined)window.clearTimeout(timer);};
  },[ready]);

  useEffect(()=>{
    if(!ready||live)return;
    const timer=window.setInterval(()=>void fetchFrame(),850);
    return()=>window.clearInterval(timer);
  },[ready,live,fetchFrame]);

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
      if(!liveRef.current)void fetchFrame();
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
  },[ready,send]);

  const reload=()=>setRevision(n=>n+1);

  return <div className="page znuny-web-portal">
    <div className="znuny-web-portal-toolbar content-card">
      <div>
        <span className="eyebrow"><Ticket size={14}/>Plenaro / Ticketsystem</span>
        <h1>Znuny-Weboberfläche</h1>
        <p>Interaktive Browseransicht über den Plenaro-Server.</p>
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
      {ready&&<div className="znuny-browser-surface"
        tabIndex={0} ref={viewRef} onKeyDown={keyDown}
        onPaste={e=>{e.preventDefault();send({kind:'text',text:e.clipboardData.getData('text')});}}
        aria-label="Interaktive Znuny-Webansicht">
        <img ref={frameRef} src={frame??undefined}
          alt="Znuny-Browserbild (serverseitig gerendert)"
          style={{visibility:frame?'visible':'hidden'}}
          draggable={false} onClick={e=>{
            send({kind:'click',...position(e)});
            viewRef.current?.focus();
          }}
          />
      </div>}
    </div>}
  </div>;
}

type NativeStatus={enabled:boolean;mode?:string;reason?:string};

/** Prefer real HTML/JS in an isolated, server-proxied HTTPS origin.
 * A legacy Chromium screenshot mode remains available for nonconfigured
 * single-host installations and as a manual emergency fallback.
 */
export function ZnunyWebPortalPage(){
  const [status,setStatus]=useState<NativeStatus|null>(null);
  const [legacy,setLegacy]=useState(false);
  const [problem,setProblem]=useState('');
  useEffect(()=>{
    let active=true;
    void fetch('/api/v1/znuny/native/status',{credentials:'same-origin',
      cache:'no-store'})
      .then(async response=>{
        if(!response.ok)throw new Error('Native Anzeige nicht erreichbar.');
        return await response.json() as NativeStatus;
      })
      .then(value=>{if(active)setStatus(value);})
      .catch(()=>{if(active){setStatus({enabled:false});
        setProblem('Native Ansicht nicht erreichbar; kompatible Ansicht aktiv.');}});
    return()=>{active=false;};
  },[]);
  if(!status)return <div className="page znuny-web-portal">
    <div className="znuny-browser-loading" role="status">
      Ticketsystem-Ansicht wird vorbereitet …
    </div>
  </div>;
  if(!status.enabled||legacy)return <>
    {problem&&<p role="status" className="um-hint">{problem}</p>}
    <LegacyZnunyWebPortalPage/>
  </>;
  return <NativeZnunyWebPortalPage onLegacy={()=>setLegacy(true)}/>;
}

function NativeZnunyWebPortalPage({onLegacy}:{onLegacy:()=>void}){
  const [params]=useSearchParams();
  const ticket=params.get('ticket');
  const validTicket=ticket===null||/^\d{1,40}$/.test(ticket);
  const [url,setUrl]=useState('');
  const [error,setError]=useState('');
  const [loading,setLoading]=useState(true);
  const [revision,setRevision]=useState(0);

  useEffect(()=>{
    let active=true;
    setLoading(true);setUrl('');setError('');
    void(async()=>{
      try{
        if(!validTicket)throw new Error('Ungültige Ticket-ID.');
        const tokenResponse=await fetch('/api/v1/auth/csrf',{
          credentials:'same-origin',cache:'no-store'});
        if(!tokenResponse.ok)throw new Error('Sicherheitstoken nicht verfügbar.');
        const token=await tokenResponse.json() as {token:string};
        const start=await fetch('/api/v1/znuny/native/start',{
          method:'POST',credentials:'same-origin',cache:'no-store',
          headers:{'X-CSRF-TOKEN':token.token,'Content-Type':'application/json'},
          body:JSON.stringify({ticketId:ticket,reset:revision>0})
        });
        const body=await start.json().catch(()=>({})) as {url?:string,error?:string};
        if(!start.ok||!body.url)throw new Error(body.error||
          'Ticketsystem-Anmeldung konnte nicht aufgebaut werden.');
        const external=new URL(body.url);
        if(external.protocol!=='https:'||external.origin===location.origin)
          throw new Error('Ungültige Proxy-Origin.');
        if(active)setUrl(external.toString());
      }catch(e){
        if(active){setError(e instanceof Error?e.message:
          'Native Ticketsystem-Ansicht nicht erreichbar.');setLoading(false);}
      }
    })();
    return()=>{active=false;};
  },[ticket,revision,validTicket]);

  return <div className="page znuny-web-portal">
    <div className="znuny-web-portal-toolbar content-card">
      <div>
        <span className="eyebrow"><Ticket size={14}/>Plenaro / Ticketsystem</span>
        <h1>Ticketsystem-Weboberfläche</h1>
        <p>Originale HTML-Oberfläche – flüssig im Browser, Verbindung über Plenaro.</p>
      </div>
      <div className="znuny-browser-toolbar">
        <button type="button" className="button button-secondary"
          onClick={()=>setRevision(n=>n+1)} title="Ticketsystem neu verbinden">
          <RefreshCw size={15}/>Neu verbinden
        </button>
        <button type="button" className="button button-secondary"
          onClick={onLegacy} title="Serverseitigen Bildmodus verwenden">
          Kompatibler Modus
        </button>
      </div>
    </div>
    {error&&<div className="content-card znuny-portal-error" role="alert">
      <WifiOff size={20}/><strong>Native Ticketsystem-Verbindung</strong>
      <p>{error}</p>
      <button type="button" className="button button-secondary"
        onClick={()=>setRevision(n=>n+1)}>Erneut versuchen</button>
      <button type="button" className="button button-secondary"
        onClick={onLegacy}>Kompatiblen Modus öffnen</button>
      <Link className="button button-secondary" to="/settings">
        Ticketsystem-Einstellungen
      </Link>
    </div>}
    <div className="znuny-portal-frame-wrap znuny-native-frame-wrap">
      <div className="znuny-portal-security-caption">
        <ShieldCheck size={14}/>
        Echte Browseransicht. Persönliche Ticketsystem-Sitzung läuft isoliert
        über den Plenaro-Server.
      </div>
      {loading&&<div className="znuny-native-loading" role="status">
        Ticketsystem wird verbunden …
      </div>}
      {url&&<iframe
        className="znuny-native-frame"
        title="Ticketsystem – isolierte, native Weboberfläche"
        src={url}
        sandbox="allow-same-origin allow-scripts allow-forms allow-popups allow-modals allow-downloads"
        referrerPolicy="no-referrer"
        onLoad={()=>setLoading(false)}
      />}
    </div>
  </div>;
}
