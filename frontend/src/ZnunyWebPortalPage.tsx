import {useEffect,useState} from 'react';
import {Link,useSearchParams} from 'react-router-dom';
import {RefreshCw,ShieldCheck,Ticket,WifiOff} from 'lucide-react';

type NativeStatus={enabled:boolean;mode?:string;reason?:string};

/**
 * Native, browser-rendered HTML application isolated on its own HTTPS origin.
 * No Selenium, remote desktop, screenshots or WebDriver code executes here.
 * Authentication and upstream cookies stay in Plenaro's per-user proxy.
 */
export function ZnunyWebPortalPage(){
  const [params]=useSearchParams();
  const ticket=params.get('ticket');
  const validTicket=ticket===null||/^\d{1,40}$/.test(ticket);
  const [status,setStatus]=useState<NativeStatus|null>(null);
  const [url,setUrl]=useState('');
  const [error,setError]=useState('');
  const [loading,setLoading]=useState(true);
  const [revision,setRevision]=useState(0);

  // Check configuration before attempting login. Without a separate HTTPS
  // origin fail closed: never run legacy HTML with Plenaro's own privileges.
  useEffect(()=>{
    let active=true;
    setStatus(null);
    setError('');
    setUrl('');
    setLoading(true);
    void fetch('/api/v1/znuny/native/status',{
      credentials:'same-origin',cache:'no-store'
    }).then(async r=>{
      if(!r.ok)throw new Error('Proxy-Konfiguration konnte nicht geprüft werden.');
      return await r.json() as NativeStatus;
    }).then(data=>{if(active)setStatus(data);})
      .catch(e=>{if(active){
        setStatus({enabled:false});
        setError(e instanceof Error?e.message:'Proxy-Konfiguration nicht erreichbar.');
        setLoading(false);
      }});
    return()=>{active=false;};
  },[]);

  useEffect(()=>{
    if(!status?.enabled)return;
    let active=true;
    setLoading(true);setUrl('');setError('');
    void(async()=>{
      try{
        if(!validTicket)throw new Error('Ungültige Ticket-ID.');
        const tokenResponse=await fetch('/api/v1/auth/csrf',{
          credentials:'same-origin',cache:'no-store'});
        if(!tokenResponse.ok)throw new Error('Sicherheitstoken nicht verfügbar.');
        const token=await tokenResponse.json() as {token:string};
        const response=await fetch('/api/v1/znuny/native/start',{
          method:'POST',credentials:'same-origin',cache:'no-store',
          headers:{'X-CSRF-TOKEN':token.token,'Content-Type':'application/json'},
          body:JSON.stringify({ticketId:ticket,reset:revision>0})
        });
        const body=await response.json().catch(()=>({})) as {url?:string,error?:string};
        if(!response.ok||!body.url)
          throw new Error(body.error||'Native Ticketsystem-Anmeldung fehlgeschlagen.');
        const external=new URL(body.url);
        if(external.protocol!=='https:'||external.origin===window.location.origin)
          throw new Error('Ungültige Ticketsystem-Proxy-Adresse.');
        if(active)setUrl(external.toString());
      }catch(e){
        if(active){
          setError(e instanceof Error?e.message:'Native Ticketsystem-Ansicht nicht erreichbar.');
          setLoading(false);
        }
      }
    })();
    return()=>{active=false;};
  },[status,validTicket,ticket,revision]);

  const configured=status?.enabled===true;
  return <div className="page znuny-web-portal">
    <div className="znuny-web-portal-toolbar content-card">
      <div>
        <span className="eyebrow"><Ticket size={14}/>Plenaro / Ticketsystem</span>
        <h1>Ticketsystem-Weboberfläche</h1>
        <p>Direkte HTML-Browseransicht über den geschützten Plenaro-Proxy.</p>
      </div>
      <div className="znuny-browser-toolbar">
        <button type="button" className="button button-secondary"
          disabled={!configured||loading}
          title="Ticketsystem-Sitzung neu verbinden"
          onClick={()=>setRevision(n=>n+1)}>
          <RefreshCw size={15}/>Neu verbinden
        </button>
      </div>
    </div>
    {!status&&<div className="znuny-native-loading" role="status">
      Ticketsystem-Verbindung wird geprüft …
    </div>}
    {status&&!configured&&<div className="content-card znuny-native-setup"
      role="alert">
      <WifiOff size={22}/>
      <h2>Native Ticketsystem-Einbettung noch nicht eingerichtet</h2>
      <p>{error||status.reason||
        'Für eine sichere HTML-Einbettung ist ein eigener HTTPS-Hostname erforderlich.'}</p>
      <p>In der <strong>lokalen</strong> Serverdatei <code>.env</code> müssen
        die beiden HTTPS-Origins konfiguriert sein. Beide DNS-Namen
        benötigen ein gültiges Zertifikat und müssen auf denselben
        Plenaro-Server (Port 443) zeigen:</p>
      <pre>{'PLENARO_HTTPS_ENABLED=true\nPLENARO_PUBLIC_ORIGIN=https://portal.example.com\nPLENARO_ZNUNY_PROXY_ORIGIN=https://tickets.example.com\nPLENARO_SERVER_NAME=portal.example.com tickets.example.com'}</pre>
      <p>Danach <code>docker compose up -d --build --remove-orphans</code> ausführen.
        Keine echten Firmenadressen oder Zugangsdaten ins GitHub-Repository schreiben.</p>
      <Link className="button button-secondary" to="/settings">
        Ticketsystem-Einstellungen
      </Link>
    </div>}
    {configured&&error&&<div className="content-card znuny-portal-error" role="alert">
      <WifiOff size={20}/><strong>Ticketsystem-Verbindung fehlgeschlagen</strong>
      <p>{error}</p>
      <button type="button" className="button button-secondary"
        onClick={()=>setRevision(n=>n+1)}>Erneut versuchen</button>
      <Link className="button button-secondary" to="/settings">
        Ticketsystem-Einstellungen
      </Link>
    </div>}
    {configured&&!error&&validTicket&&<div
      className="znuny-portal-frame-wrap znuny-native-frame-wrap">
      <div className="znuny-portal-security-caption">
        <ShieldCheck size={14}/>
        Die Webseite läuft direkt im Browser; die persönliche
        Ticketsystem-Sitzung wird vom Plenaro-Server verwaltet.
      </div>
      {loading&&<div className="znuny-native-loading" role="status">
        Ticketsystem wird verbunden …
      </div>}
      {url&&<iframe key={url}
        className="znuny-native-frame"
        title="Ticketsystem – isolierte, native Weboberfläche"
        src={url}
        sandbox="allow-same-origin allow-scripts allow-forms allow-popups allow-modals allow-downloads"
        referrerPolicy="no-referrer"
        onLoad={()=>setLoading(false)}
      />}
    </div>}
  </div>;
}
