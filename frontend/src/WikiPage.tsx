import {useEffect,useRef,useState} from 'react';
import {BookOpenText,RefreshCw,Settings2,ShieldCheck} from 'lucide-react';
import {Link} from 'react-router-dom';
import {loadFavorites,startFavorite,type Favorite} from './favoritesApi';
import {readWikiSelection,saveWikiSelection,validWikiBootstrap} from './wikiApi';

/** Classic WikiBrowserView: source selection + isolated embedded browser. */
export function WikiPage(){
  const [sources,setSources]=useState<Favorite[]>([]);
  const [selected,setSelected]=useState('');
  const [frameUrl,setFrameUrl]=useState('');
  const [revision,setRevision]=useState(0);
  const [loading,setLoading]=useState(true);
  const [frameLoading,setFrameLoading]=useState(false);
  const [error,setError]=useState('');
  const startup=useRef<{key:string;promise:Promise<string>}|null>(null);

  useEffect(()=>{
    let active=true;
    void Promise.all([readWikiSelection(),loadFavorites()]).then(([pref,items])=>{
      if(!active)return;
      const enabled=items.filter(f=>f.enabled);
      const configured=enabled.find(f=>f.id===pref.favoriteId);
      // The classic desktop browser defaults to the first enabled Wiki.
      // Never silently open an unrelated favorite as a knowledge source.
      const fallback=enabled.find(f=>f.slug==='wiki');
      setSources(enabled);
      setSelected(configured?.id??fallback?.id??'');
      setLoading(false);
    }).catch(reason=>{
      if(active){setError(reason instanceof Error?reason.message:'Wiki konnte nicht geladen werden.');
        setLoading(false);}
    });
    return()=>{active=false;};
  },[]);

  const source=sources.find(f=>f.id===selected);
  useEffect(()=>{
    if(!source){setFrameUrl('');return;}
    let active=true;
    const key=source.id+':'+revision;
    setFrameUrl('');setFrameLoading(true);setError('');
    // React StrictMode repeats effects. Bootstrap tokens are single-use,
    // and a second start would revoke the first user's proxy session.
    if(startup.current?.key!==key){
      const promise=(async()=>{
        const result=await startFavorite(source.id);
        if(!validWikiBootstrap(result.url,source.slug))
          throw new Error('Ungültige Wiki-Proxy-Adresse.');
        return result.url;
      })();
      startup.current={key,promise};
    }
    void startup.current.promise.then(url=>{
      if(active)setFrameUrl(url);
    }).catch(reason=>{
      if(active){setFrameLoading(false);
        setError(reason instanceof Error?reason.message:'Wiki-Verbindung fehlgeschlagen.');}
    });
    return()=>{active=false;};
  },[source?.id,revision]);

  async function chooseSource(id:string){
    if(id===selected)return;
    try{
      await saveWikiSelection(id||null);
      setSelected(id);
      setRevision(0);
      startup.current=null;
    }catch(reason){setError(reason instanceof Error?reason.message:'Quelle konnte nicht gespeichert werden.');}
  }

  return <div className="page znuny-web-portal wiki-browser-page">
    <div className="znuny-web-portal-toolbar content-card wiki-browser-toolbar">
      <div>
        <span className="eyebrow"><BookOpenText size={14}/>PLENARO / WISSENSDATENBANK</span>
        <h1>Wissensdatenbank</h1>
        <p>Wiki-Browser mit persönlicher Anmeldung und geschützter Sitzung.</p>
      </div>
      <div className="favorites-toolbar">
        {sources.length>0&&<label className="wiki-source-picker">Wiki-Quelle
          <select aria-label="Wiki-Quelle" value={selected}
            onChange={e=>void chooseSource(e.target.value)}>
            <option value="">Bitte auswählen</option>
            {sources.map(item=><option key={item.id} value={item.id}>{item.name}</option>)}
          </select>
        </label>}
        <button type="button" className="button button-secondary"
          disabled={!source||frameLoading} onClick={()=>setRevision(v=>v+1)}>
          <RefreshCw size={16}/>Startseite / Neu verbinden</button>
        <Link to="/settings?section=wiki" className="button button-secondary">
          <Settings2 size={16}/>Wiki-Einstellungen</Link>
      </div>
    </div>
    {loading&&<div role="status" className="znuny-native-loading">Wiki-Quellen werden geladen …</div>}
    {!loading&&error&&<div className="content-card znuny-portal-error" role="alert">
      <strong>Wissensdatenbank</strong><p>{error}</p>
    </div>}
    {!loading&&!source&&<div className="content-card znuny-native-setup">
      <h2>Noch keine Wiki-Quelle ausgewählt</h2>
      <p>Verknüpfe unter Einstellungen eine freigegebene Wiki-Webseite.
        Ein bereits funktionierender Wiki-Favorit kann direkt übernommen werden.</p>
      <Link to="/settings?section=wiki" className="button button-primary">
        <Settings2 size={16}/>Wiki einrichten</Link>
    </div>}
    {source&&<div className="znuny-portal-frame-wrap znuny-native-frame-wrap">
      <div className="znuny-portal-security-caption">
        <ShieldCheck size={14}/>Die Wiki-Seite läuft über den persönlichen Plenaro-Favoriten-Proxy.
      </div>
      {frameLoading&&!frameUrl&&<div className="znuny-native-loading" role="status">
        {source.name} wird verbunden …</div>}
      {frameUrl&&<iframe title={'Wissensdatenbank – '+source.name}
        key={frameUrl} src={frameUrl} className="znuny-native-frame"
        sandbox="allow-same-origin allow-scripts allow-forms allow-popups allow-modals allow-downloads"
        referrerPolicy="no-referrer" onLoad={()=>setFrameLoading(false)}/>}
    </div>}
  </div>;
}
