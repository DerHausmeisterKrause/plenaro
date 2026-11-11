import {useEffect,useRef,useState} from 'react';
import {BookOpenText,RefreshCw,Settings2,ShieldCheck} from 'lucide-react';
import {Link} from 'react-router-dom';
import {readWikiSettings,startWiki,validWikiBootstrap,type WikiSettings} from './wikiApi';

/** One admin-configured Wiki with each user's private verified login. */
export function WikiPage(){
  const [wiki,setWiki]=useState<WikiSettings|null>(null);
  const [url,setUrl]=useState('');
  const [revision,setRevision]=useState(0);
  const [loading,setLoading]=useState(true);
  const [starting,setStarting]=useState(false);
  const [error,setError]=useState('');
  const startup=useRef<{key:string;promise:Promise<string>}|null>(null);

  useEffect(()=>{
    let active=true;
    void readWikiSettings().then(settings=>{
      if(active){setWiki(settings);setLoading(false);}
    }).catch(reason=>{
      if(active){setLoading(false);
        setError(reason instanceof Error?reason.message:'Wiki konnte nicht geladen werden.');}
    });
    return()=>{active=false;};
  },[]);

  useEffect(()=>{
    if(!wiki?.ready||!wiki.favoriteId){setUrl('');return;}
    let active=true;
    setStarting(true);setUrl('');setError('');
    const key=wiki.favoriteId+':'+revision;
    // React StrictMode mounts effects twice. Starting a second time would
    // revoke the first single-use bootstrap token and its private cookie jar.
    if(startup.current?.key!==key){
      const promise=(async()=>{
        const result=await startWiki();
        if(!validWikiBootstrap(result.url))
          throw new Error('Ungültige Wiki-Proxy-Adresse.');
        return result.url;
      })();
      startup.current={key,promise};
    }
    void startup.current.promise.then(result=>{
      if(active)setUrl(result);
    }).catch(reason=>{
      if(active){setStarting(false);setError(
        reason instanceof Error?reason.message:'Wiki-Autologin fehlgeschlagen.');}
    });
    return()=>{active=false;};
  },[wiki?.ready,wiki?.favoriteId,revision]);

  return <div className="page znuny-web-portal wiki-browser-page">
    <div className="znuny-web-portal-toolbar content-card wiki-browser-toolbar">
      <div>
        <span className="eyebrow"><BookOpenText size={14}/>PLENARO / WISSENSDATENBANK</span>
        <h1>Wissensdatenbank</h1>
        <p>{wiki?.configured
          ? wiki.name+' · persönliche Wiki-Anmeldung'
          : 'Zentral bereitgestelltes Firmenwiki'}</p>
      </div>
      <div className="favorites-toolbar">
        <button type="button" className="button button-secondary"
          disabled={!wiki?.ready||starting}
          onClick={()=>setRevision(value=>value+1)}>
          <RefreshCw size={16}/>Wiki neu verbinden</button>
        <Link to="/settings?section=wiki" className="button button-secondary">
          <Settings2 size={16}/>Wiki-Zugangsdaten</Link>
      </div>
    </div>
    {loading&&<div className="znuny-native-loading" role="status">
      Wiki-Verbindung wird geprüft …</div>}
    {!loading&&!wiki?.configured&&<div className="content-card znuny-native-setup">
      <h2>Wiki noch nicht konfiguriert</h2>
      <p>Ein Administrator muss unter Einstellungen → Wiki die Wiki-Adresse
        für alle Plenaro-Benutzer einrichten.</p>
      <Link to="/settings?section=wiki" className="button button-primary">
        Wiki-Einstellungen öffnen</Link>
    </div>}
    {!loading&&wiki?.configured&&!wiki.ready&&<div className="content-card znuny-native-setup">
      <h2>Persönliche Wiki-Anmeldung einrichten</h2>
      <p>Der Wiki-Server wurde vom Administrator bereitgestellt.
        Hinterlege deinen eigenen Wiki-Benutzernamen und dein Passwort.
        Erst danach wird die Wissensdatenbank automatisch geöffnet.</p>
      <Link to="/settings?section=wiki" className="button button-primary">
        Zugangsdaten hinterlegen</Link>
    </div>}
    {!loading&&error&&<div className="content-card znuny-portal-error" role="alert">
      <strong>Wiki-Verbindung nicht hergestellt</strong>
      <p>{error}</p>
      <p>Bitte deine Wiki-Zugangsdaten unter Einstellungen → Wiki prüfen.</p>
    </div>}
    {wiki?.ready&&<div className="znuny-portal-frame-wrap znuny-native-frame-wrap">
      <div className="znuny-portal-security-caption">
        <ShieldCheck size={14}/>Eigene, automatisch angemeldete Wiki-Sitzung;
        keine Zugangsdaten im Browser-Frame.
      </div>
      {starting&&!url&&<div className="znuny-native-loading" role="status">
        Wiki-Anmeldung wird geprüft …</div>}
      {url&&<iframe title={'Wissensdatenbank – '+wiki.name}
        key={url} src={url} className="znuny-native-frame"
        sandbox="allow-same-origin allow-scripts allow-forms allow-popups allow-modals allow-downloads"
        referrerPolicy="no-referrer" onLoad={()=>setStarting(false)}/>}
    </div>}
  </div>;
}
