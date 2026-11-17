import {useCallback,useEffect,useRef,useState,type FormEvent} from 'react';
import {Link,useParams} from 'react-router-dom';
import {ArrowDown,ArrowUp,ExternalLink,Plus,RefreshCw,Save,ShieldCheck,
  Star,Trash2} from 'lucide-react';
import {
  loadFavorites,createPersonalFavorite,updatePersonalFavorite,
  removePersonalFavorite,sortPersonalFavorites,startFavorite,
  type Favorite,type PersonalSiteInput
} from './favoritesApi';

const blank=():PersonalSiteInput=>({
  name:'',url:'https://',username:'',autoLogin:false,
  enabled:true,sortOrder:0,password:'',clearPassword:false
});
function ErrorMessage({message}:{message:string}){
  return message?<p role="alert" className="um-feedback failure">{message}</p>:null;
}

/** Every user maintains unrestricted personal bookmark names and order.
 * Proxy starts remain server-side, strictly scoped and HTTPS only.
 * The centrally configured Knowledge Base is NOT a bookmark.
 */
export function FavoriteSettingsPanel({onChanged}:{onChanged:()=>void}){
  const [favorites,setFavorites]=useState<Favorite[]>([]);
  const [selected,setSelected]=useState('');
  const [form,setForm]=useState<PersonalSiteInput>(blank());
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  const [info,setInfo]=useState('');
  const [loaded,setLoaded]=useState(false);
  const reload=useCallback(async()=>{
    const entries=await loadFavorites();
    setFavorites(entries);setLoaded(true);
  },[]);
  useEffect(()=>{void reload().catch(e=>
    setError(e instanceof Error?e.message:'Favoriten konnten nicht geladen werden.'));},[reload]);
  const current=favorites.find(x=>x.id===selected);
  function choose(item:Favorite){
    setSelected(item.id);
    setForm({name:item.name,url:item.url??'https://',username:item.username,
      autoLogin:item.autoLogin,enabled:item.enabled,sortOrder:item.sortOrder,
      password:'',clearPassword:false});
    setError('');setInfo('');
  }
  function fresh(){
    setSelected('');setForm(blank());setError('');setInfo('');
  }
  async function save(e:FormEvent){
    e.preventDefault();if(busy)return;
    setBusy(true);setError('');setInfo('');
    try{
      const payload={...form,name:form.name.trim(),url:form.url.trim(),
        username:form.username.trim(),
        sortOrder:current?form.sortOrder:favorites.length};
      const id=current?current.id:(await createPersonalFavorite(payload)).id;
      if(current)await updatePersonalFavorite(current.id,payload);
      await reload();
      setSelected(id);setForm({...payload,password:'',clearPassword:false});
      setInfo('Deine Webseite wurde gespeichert.');onChanged();
    }catch(reason){
      setError(reason instanceof Error?reason.message:'Favorit konnte nicht gespeichert werden.');
    }finally{setBusy(false);}
  }
  async function erase(){
    if(!current||busy)return;
    if(!window.confirm('Favorit „'+current.name+'“ entfernen?'))return;
    setBusy(true);setError('');setInfo('');
    try{
      await removePersonalFavorite(current.id);
      fresh();await reload();onChanged();setInfo('Favorit entfernt.');
    }catch(reason){
      setError(reason instanceof Error?reason.message:'Entfernen fehlgeschlagen.');
    }finally{setBusy(false);}
  }
  async function move(direction:-1|1){
    if(!current||busy)return;
    const items=[...favorites].sort((a,b)=>a.sortOrder-b.sortOrder);
    const index=items.findIndex(item=>item.id===current.id);
    const other=items[index+direction];
    if(!other)return;
    const swap=items[index];
    items[index]=other;items[index+direction]=swap;
    setBusy(true);setError('');
    try{
      await sortPersonalFavorites(items.map(item=>item.id));
      await reload();
      setForm(v=>({...v,sortOrder:index+direction}));
      onChanged();
    }catch(reason){
      setError(reason instanceof Error?reason.message:'Sortierung konnte nicht gespeichert werden.');
    }finally{setBusy(false);}
  }
  return <section className="content-card favorites-panel">
    <div className="um-section-title"><Star size={20}/><h2>Meine Webseiten</h2></div>
    <p>Lege deine eigenen HTTPS-Webseiten an, bearbeite sie und bestimme
      die Reihenfolge. Eine Freigabe des Administrators für einzelne
      Webseiten ist nicht erforderlich. Die Wissensdatenbank wird separat
      eingerichtet und erscheint nicht in dieser Liste.</p>
    <div className="favorites-toolbar">
      <button type="button" className="button button-secondary"
        onClick={fresh} disabled={busy}><Plus size={15}/>Webseite hinzufügen</button>
      <button type="button" className="button button-secondary"
        disabled={!current||busy} onClick={()=>void move(-1)}
        aria-label="Favorit nach oben"><ArrowUp size={15}/></button>
      <button type="button" className="button button-secondary"
        disabled={!current||busy} onClick={()=>void move(1)}
        aria-label="Favorit nach unten"><ArrowDown size={15}/></button>
    </div>
    <div className="favorites-editor-grid">
      <div className="favorites-list" aria-label="Meine Favoriten">
        {favorites.map(item=><button type="button" key={item.id}
          className={'favorites-list-item'+(item.id===selected?' selected':'')}
          onClick={()=>choose(item)}>
          <Star size={15}/><span>{item.name}</span>
        </button>)}
        {loaded&&!favorites.length&&<p>Noch keine Webseiten vorhanden.</p>}
      </div>
      <form className="um-form favorites-form" onSubmit={e=>void save(e)}>
        <label htmlFor="fav-name">Name der Webseite</label>
        <input id="fav-name" value={form.name} required minLength={2} maxLength={120}
          onChange={e=>setForm({...form,name:e.target.value})}/>
        <label htmlFor="fav-url">Webadresse (HTTPS)</label>
        <input id="fav-url" type="url" value={form.url} required maxLength={2048}
          placeholder="https://example.org/unterseite"
          onChange={e=>setForm({...form,url:e.target.value})}/>
        <p className="um-hint">Die Adresse kannst du selbst eintragen oder später
          ändern. Der Plenaro-Proxy öffnet die Webseite mit einer getrennten
          Sitzung. Aus Sicherheitsgründen sind lokale Server-, Netzwerk- und
          Metadaten-Adressen im persönlichen Proxy gesperrt.</p>
        <label htmlFor="fav-user">Anmeldename (optional)</label>
        <input id="fav-user" value={form.username} autoComplete="off" maxLength={180}
          onChange={e=>setForm({...form,username:e.target.value})}/>
        <label htmlFor="fav-password">Passwort (optional)</label>
        <input id="fav-password" type="password" value={form.password??''}
          placeholder={current?.hasPassword?'Gespeichert – leer lassen zum Beibehalten':'Passwort'}
          autoComplete="new-password"
          onChange={e=>setForm({...form,password:e.target.value})}/>
        {current?.hasPassword&&<label className="favorites-check">
          <input type="checkbox" checked={form.clearPassword===true}
            onChange={e=>setForm({...form,clearPassword:e.target.checked,
              password:e.target.checked?'':form.password})}/>
          Gespeichertes Passwort löschen
        </label>}
        <label className="favorites-check"><input type="checkbox"
          checked={form.enabled} onChange={e=>
            setForm({...form,enabled:e.target.checked})}/>Webseite aktiv</label>
        <label className="favorites-check"><input type="checkbox"
          checked={form.autoLogin} onChange={e=>
            setForm({...form,autoLogin:e.target.checked})}/>
          Automatische Anmeldung über ein übliches HTML-Loginformular versuchen</label>
        <p className="um-hint">Passwörter bleiben verschlüsselt gespeichert.
          SSO, MFA und spezielle Loginverfahren können eine interaktive
          Anmeldung erforderlich machen. Manche Webseiten verbieten die
          Darstellung in eingebetteten Fenstern.</p>
        <ErrorMessage message={error}/>
        {info&&<p role="status" className="um-feedback success">{info}</p>}
        <div className="favorites-toolbar">
          <button type="submit" className="button button-primary" disabled={busy}>
            <Save size={16}/>Speichern</button>
          {current&&<>
            <Link className="button button-secondary" to={'/favorites/'+current.id}>
              <ExternalLink size={15}/>Öffnen</Link>
            <button type="button" className="button button-secondary"
              disabled={busy} onClick={()=>void erase()}>
              <Trash2 size={15}/>Entfernen</button>
          </>}
        </div>
      </form>
    </div>
  </section>;
}

export function FavoritePage(){
  const {id}=useParams();
  const [favorite,setFavorite]=useState<Favorite|null>(null);
  const [url,setUrl]=useState('');
  const [error,setError]=useState('');
  const [loading,setLoading]=useState(true);
  const [revision,setRevision]=useState(0);
  // React.StrictMode mounts effects twice in development. Starting the proxy
  // twice revokes the first server-side session, so the iframe can be handed
  // an already invalid single-use bootstrap token. Share ONE in-flight
  // request across both effect invocations for the same favorite/revision.
  const startup=useRef<{
    key:string;promise:Promise<{item:Favorite;url:string}>
  }|null>(null);
  useEffect(()=>{
    let active=true;
    setLoading(true);setError('');setUrl('');
    const key=(id??'')+':'+revision;
    if(startup.current?.key!==key){
      const promise=(async()=>{
        if(!id)throw new Error('Favorit nicht gefunden.');
        const items=await loadFavorites();
        const item=items.find(x=>x.id===id&&x.enabled);
        if(!item)throw new Error('Favorit nicht vorhanden oder deaktiviert.');
        const result=await startFavorite(id);
        const destination=new URL(result.url);
        // The dedicated-host proxy uses /_plenaro/favorite-bootstrap.
        // A shared favorites host instead needs /<approved-slug>/_plenaro/...
        // Only allow the currently selected favorite's exact slug, never an
        // arbitrary path returned from the start endpoint.
        const legacyBootstrap=destination.pathname==='/_plenaro/favorite-bootstrap';
        const sharedBootstrap=destination.pathname===
          '/'+item.slug+'/_plenaro/favorite-bootstrap';
        if(destination.protocol!=='https:'||
          destination.origin===window.location.origin||
          destination.username!==''||destination.password!==''||
          (!legacyBootstrap&&!sharedBootstrap))
          throw new Error('Ungültige Favoriten-Proxy-Adresse.');
        return {item,url:destination.toString()};
      })();
      startup.current={key,promise};
    }
    void startup.current.promise.then(({item,url})=>{
      if(active){setFavorite(item);setUrl(url);}
    }).catch(e=>{
      if(active){setError(e instanceof Error?e.message:'Webseite nicht erreichbar.');
        setLoading(false);}
    });
    return()=>{active=false;};
  },[id,revision]);
  return <div className="page znuny-web-portal favorite-browser-page">
    <div className="znuny-web-portal-toolbar content-card">
      <div><span className="eyebrow"><Star size={14}/>Plenaro / Favoriten</span>
        <h1>{favorite?.name??'Webfavorit'}</h1>
        <p>Geschützte Webanwendung mit eigener Browsersitzung.</p></div>
      <div className="favorites-toolbar">
        <button type="button" className="button button-secondary"
          onClick={()=>setRevision(n=>n+1)} disabled={loading}>
          <RefreshCw size={15}/>Neu verbinden</button>
        {favorite?.url&&<a className="button button-secondary"
          href={favorite.url} target="_blank" rel="noopener noreferrer">
          <ExternalLink size={15}/>Originalseite öffnen</a>}
        <Link className="button button-secondary" to="/settings?section=favorites">Favoriten verwalten</Link>
      </div>
    </div>
    {error&&<div className="content-card znuny-portal-error" role="alert">
      <strong>Favorit konnte nicht geöffnet werden</strong><p>{error}</p></div>}
    {loading&&!error&&<div role="status" className="znuny-native-loading">
      Webseite wird geöffnet …</div>}
    {!error&&url&&<div className="znuny-portal-frame-wrap znuny-native-frame-wrap">
      <div className="znuny-portal-security-caption">
        <ShieldCheck size={14}/>Webseite über den Plenaro-Proxy. Manche
        Webseiten benötigen externe Skripte, verbieten Einbettungen oder
        sind aus dem Servernetz nicht erreichbar. Falls die Ansicht leer
        bleibt, die Originalseite über den oberen Link öffnen.</div>
      <iframe key={url} title={favorite?.name??'Webfavorit'}
        src={url} className="znuny-native-frame"
        sandbox="allow-same-origin allow-scripts allow-forms allow-popups allow-modals allow-downloads"
        referrerPolicy="no-referrer"
        onLoad={()=>setLoading(false)}/>
    </div>}
  </div>;
}
