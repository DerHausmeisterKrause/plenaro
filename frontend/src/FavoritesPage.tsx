import {useCallback,useEffect,useState,type FormEvent} from 'react';
import {Link,useParams} from 'react-router-dom';
import {ArrowDown,ArrowUp,ExternalLink,Plus,RefreshCw,Save,ShieldCheck,
  Star,Trash2} from 'lucide-react';
import {listUsers,type ManagedUser} from './userApi';
import {
  loadFavoriteApps,loadFavorites,createFavorite,updateFavorite,removeFavorite,
  startFavorite,loadApprovedApps,createApprovedApp,updateApprovedApp,
  removeApprovedApp,type AppInput,type FavoriteApp,type Favorite,
  type FavoriteInput
} from './favoritesApi';

const blankFavorite=(appId=''):FavoriteInput=>({
  appId,name:'',path:'/',username:'',autoLogin:false,enabled:true,
  sortOrder:0,password:'',clearPassword:false
});
const blankApp=():AppInput=>({
  name:'',slug:'',upstreamUrl:'https://',enabled:true,
  availableToAll:false,allowedUsers:[]
});
function ErrorMessage({message}:{message:string}){
  return message?<p role="alert" className="um-feedback failure">{message}</p>:null;
}

export function FavoriteSettingsPanel({onChanged}:{onChanged:()=>void}){
  const [apps,setApps]=useState<FavoriteApp[]>([]);
  const [favorites,setFavorites]=useState<Favorite[]>([]);
  const [selected,setSelected]=useState('');
  const [form,setForm]=useState<FavoriteInput>(blankFavorite());
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  const [info,setInfo]=useState('');
  const [loaded,setLoaded]=useState(false);
  const reload=useCallback(async()=>{
    const [newApps,entries]=await Promise.all([loadFavoriteApps(),loadFavorites()]);
    setApps(newApps);setFavorites(entries);setLoaded(true);
  },[]);
  useEffect(()=>{void reload().catch(e=>setError(String(e)));},[reload]);
  const current=favorites.find(x=>x.id===selected);
  function choose(item:Favorite){
    setSelected(item.id);
    setForm({appId:item.appId,name:item.name,path:item.path,
      username:item.username,autoLogin:item.autoLogin,enabled:item.enabled,
      sortOrder:item.sortOrder,password:'',clearPassword:false});
    setError('');setInfo('');
  }
  function newFavorite(){
    setSelected('');setForm(blankFavorite(apps[0]?.id??''));
    setError('');setInfo('');
  }
  async function save(e:FormEvent){
    e.preventDefault();if(busy)return;
    setBusy(true);setError('');setInfo('');
    try{
      if(!apps.some(a=>a.id===form.appId))
        throw new Error('Bitte eine freigegebene Anwendung auswählen.');
      if(!form.name.trim())throw new Error('Name fehlt.');
      const toSave={...form,name:form.name.trim(),
        sortOrder:current?form.sortOrder:favorites.length};
      const id=current?current.id:(await createFavorite(toSave)).id;
      if(current)await updateFavorite(current.id,toSave);
      await reload();
      setSelected(id);setForm({...toSave,password:'',clearPassword:false});
      setInfo('Favorit gespeichert.');
      onChanged();
    }catch(e){setError(e instanceof Error?e.message:'Speichern fehlgeschlagen.');}
    finally{setBusy(false);}
  }
  async function erase(){
    if(!current||busy)return;
    if(!window.confirm('Favorit „'+current.name+'“ entfernen?'))return;
    setBusy(true);setError('');setInfo('');
    try{
      await removeFavorite(current.id);newFavorite();await reload();
      onChanged();setInfo('Favorit entfernt.');
    }catch(e){setError(e instanceof Error?e.message:'Entfernen fehlgeschlagen.');}
    finally{setBusy(false);}
  }
  async function move(direction:-1|1){
    if(!current||busy)return;
    const items=[...favorites].sort((a,b)=>a.sortOrder-b.sortOrder);
    const index=items.findIndex(a=>a.id===current.id);
    const other=items[index+direction];
    if(!other)return;
    setBusy(true);setError('');
    try{
      await updateFavorite(current.id,{
        appId:current.appId,name:current.name,path:current.path,
        username:current.username,autoLogin:current.autoLogin,
        enabled:current.enabled,sortOrder:other.sortOrder,password:''
      });
      await updateFavorite(other.id,{
        appId:other.appId,name:other.name,path:other.path,
        username:other.username,autoLogin:other.autoLogin,
        enabled:other.enabled,sortOrder:current.sortOrder,password:''
      });
      await reload();setForm(x=>({...x,sortOrder:other.sortOrder}));
      onChanged();
    }catch(e){setError(e instanceof Error?e.message:'Sortieren fehlgeschlagen.');}
    finally{setBusy(false);}
  }
  return <section className="content-card favorites-panel">
    <div className="um-section-title"><Star size={20}/><h2>Meine Favoriten</h2></div>
    <p>Wie im klassischen Plenaro: Webseiten direkt als eigenen Reiter öffnen.
      Du kannst nur Anwendungen wählen, die ein Administrator freigegeben hat.</p>
    <div className="favorites-toolbar">
      <button type="button" className="button button-secondary"
        onClick={newFavorite}><Plus size={15}/>Favorit hinzufügen</button>
      <button type="button" className="button button-secondary"
        disabled={!current||busy} onClick={()=>void move(-1)}
        aria-label="Favorit nach oben"><ArrowUp size={15}/></button>
      <button type="button" className="button button-secondary"
        disabled={!current||busy} onClick={()=>void move(1)}
        aria-label="Favorit nach unten"><ArrowDown size={15}/></button>
    </div>
    <div className="favorites-editor-grid">
      <div className="favorites-list" aria-label="Meine Favoriten">
        {favorites.map(f=><button type="button" key={f.id}
          className={'favorites-list-item'+(f.id===selected?' selected':'')}
          onClick={()=>choose(f)}>
          <Star size={15}/><span>{f.name}</span>
        </button>)}
        {loaded&&!favorites.length&&<p>Noch keine Favoriten vorhanden.</p>}
      </div>
      <form className="um-form favorites-form" onSubmit={e=>void save(e)}>
        <label htmlFor="fav-name">Name</label>
        <input id="fav-name" value={form.name} maxLength={120} required
          onChange={e=>setForm({...form,name:e.target.value})}/>
        <label htmlFor="fav-app">Freigegebene Webseite</label>
        <select id="fav-app" value={form.appId} required disabled={!!current}
          onChange={e=>setForm({...form,appId:e.target.value})}>
          <option value="">Bitte auswählen</option>
          {apps.map(a=><option key={a.id} value={a.id}>{a.name}</option>)}
        </select>
        <label htmlFor="fav-path">Unterseite / Pfad</label>
        <input id="fav-path" value={form.path} required
          placeholder="/wiki/start" onChange={e=>setForm({...form,path:e.target.value})}/>
        <label htmlFor="fav-user">Anmeldename (optional)</label>
        <input id="fav-user" value={form.username} autoComplete="off"
          onChange={e=>setForm({...form,username:e.target.value})}/>
        <label htmlFor="fav-password">Passwort (optional)</label>
        <input id="fav-password" type="password" value={form.password??''}
          placeholder={current?.hasPassword?'Gespeichert – leer lassen zum Beibehalten':'Neues Passwort'}
          autoComplete="new-password"
          onChange={e=>setForm({...form,password:e.target.value})}/>
        {current?.hasPassword&&<label className="favorites-check">
          <input type="checkbox" checked={form.clearPassword===true}
            onChange={e=>setForm({...form,clearPassword:e.target.checked,
              password:e.target.checked?'':form.password})}/>
          Gespeichertes Passwort löschen
        </label>}
        <label className="favorites-check"><input type="checkbox" checked={form.enabled}
          onChange={e=>setForm({...form,enabled:e.target.checked})}/>Aktiv</label>
        <label className="favorites-check"><input type="checkbox" checked={form.autoLogin}
          onChange={e=>setForm({...form,autoLogin:e.target.checked})}/>
          Automatische Anmeldung über ein übliches HTML-Loginformular versuchen</label>
        <p className="um-hint">Passwörter bleiben verschlüsselt auf dem Plenaro-Server.
          SSO, Mehrfaktor-Anmeldung und individuelle Loginformulare benötigen gegebenenfalls
          eine manuelle Anmeldung im eingebetteten Fenster.</p>
        <ErrorMessage message={error}/>
        {info&&<p role="status" className="um-feedback success">{info}</p>}
        <div className="favorites-toolbar">
          <button type="submit" className="button button-primary" disabled={busy||!apps.length}>
            <Save size={16}/>Speichern</button>
          {current&&<>
            <Link className="button button-secondary" to={'/favorites/'+current.id}>
              <ExternalLink size={15}/>Öffnen</Link>
            <button type="button" className="button button-secondary"
              disabled={busy} onClick={()=>void erase()}><Trash2 size={15}/>Entfernen</button>
          </>}
        </div>
        {loaded&&!apps.length&&<p>Es sind noch keine Webanwendungen freigegeben.
          Bitte in der Administration zuerst eine Anwendung anlegen.</p>}
      </form>
    </div>
  </section>;
}

export function FavoriteAppsAdminPanel({onChanged}:{onChanged:()=>void}){
  const [apps,setApps]=useState<FavoriteApp[]>([]);
  const [people,setPeople]=useState<ManagedUser[]>([]);
  const [selected,setSelected]=useState('');
  const [form,setForm]=useState<AppInput>(blankApp());
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  const [info,setInfo]=useState('');
  const [loaded,setLoaded]=useState(false);
  const refresh=useCallback(async()=>{
    const [entries,users]=await Promise.all([loadApprovedApps(),listUsers()]);
    setApps(entries);setPeople(users);setLoaded(true);
  },[]);
  useEffect(()=>{void refresh().catch(e=>setError(String(e)));},[refresh]);
  const current=apps.find(a=>a.id===selected);
  function choose(app:FavoriteApp){
    setSelected(app.id);
    setForm({name:app.name,slug:app.slug,upstreamUrl:app.upstreamUrl??'https://',
      enabled:app.enabled,availableToAll:app.availableToAll,
      allowedUsers:app.allowedUsers??[]});
    setError('');setInfo('');
  }
  function fresh(){setSelected('');setForm(blankApp());setError('');setInfo('');}
  async function save(e:FormEvent){
    e.preventDefault();if(busy)return;
    setBusy(true);setError('');setInfo('');
    try{
      const payload={...form,name:form.name.trim(),slug:form.slug.trim().toLowerCase(),
        upstreamUrl:form.upstreamUrl.trim()};
      const id=current?current.id:(await createApprovedApp(payload)).id;
      if(current)await updateApprovedApp(current.id,payload);
      await refresh();setSelected(id);setForm(payload);
      setInfo('Webanwendung freigegeben.');onChanged();
    }catch(e){setError(e instanceof Error?e.message:'Speichern fehlgeschlagen.');}
    finally{setBusy(false);}
  }
  async function erase(){
    if(!current||busy)return;
    if(!window.confirm('Anwendung entfernen? Zugehörige Favoriten werden gelöscht.'))return;
    setBusy(true);setError('');
    try{await removeApprovedApp(current.id);await refresh();fresh();
      setInfo('Anwendung entfernt.');onChanged();}
    catch(e){setError(e instanceof Error?e.message:'Löschen fehlgeschlagen.');}
    finally{setBusy(false);}
  }
  return <section className="content-card favorites-panel">
    <div className="um-section-title"><ShieldCheck size={20}/><h2>Webanwendungen freigeben</h2></div>
    <p>Nur Administratoren bestimmen, welche HTTPS-Webseiten Plenaro als Proxy
      bereitstellen darf. Benutzer können anschließend eigene Favoriten dazu anlegen.
      Ein NGINX-Neustart pro Anwendung ist nicht erforderlich.</p>
    <div className="favorites-toolbar">
      <button type="button" className="button button-secondary" onClick={fresh}>
        <Plus size={15}/>Anwendung hinzufügen</button>
    </div>
    <div className="favorites-editor-grid">
      <div className="favorites-list" aria-label="Freigegebene Anwendungen">
        {apps.map(a=><button key={a.id} type="button"
          className={'favorites-list-item'+(a.id===selected?' selected':'')}
          onClick={()=>choose(a)}>{a.name}</button>)}
        {loaded&&!apps.length&&<p>Noch keine Anwendungen vorhanden.</p>}
      </div>
      <form className="um-form favorites-form" onSubmit={e=>void save(e)}>
        <label htmlFor="app-name">Name</label>
        <input id="app-name" value={form.name} required maxLength={120}
          onChange={e=>setForm({...form,name:e.target.value})}/>
        <label htmlFor="app-slug">Subdomain (z. B. wiki)</label>
        <input id="app-slug" value={form.slug} required maxLength={40}
          pattern="[a-z][a-z0-9-]+" onChange={e=>setForm({...form,slug:e.target.value})}/>
        <label htmlFor="app-upstream">Interne HTTPS-Adresse (Serverursprung)</label>
        <input id="app-upstream" type="url" value={form.upstreamUrl} required disabled={!!current}
          onChange={e=>setForm({...form,upstreamUrl:e.target.value})}/>
        <p className="um-hint">Nur HTTPS-Origin, etwa https://wiki.example.org/.
          Die Zieladresse bleibt in der Administration; keine freie Zielwahl durch Benutzer.
          Bitte keine echten Betriebsadressen ins GitHub-Repository kopieren.</p>
        <label className="favorites-check"><input type="checkbox"
          checked={form.enabled} onChange={e=>setForm({...form,enabled:e.target.checked})}/>
          Anwendung aktiv</label>
        <label className="favorites-check"><input type="checkbox"
          checked={form.availableToAll}
          onChange={e=>setForm({...form,availableToAll:e.target.checked})}/>
          Für alle angemeldeten Plenaro-Benutzer freigeben</label>
        {!form.availableToAll&&<fieldset className="favorites-users">
          <legend>Erlaubte Benutzer</legend>
          {people.map(p=><label className="favorites-check" key={p.id}>
            <input type="checkbox" checked={form.allowedUsers.includes(p.id)}
              onChange={e=>setForm({...form,allowedUsers:e.target.checked?
                [...form.allowedUsers,p.id]:form.allowedUsers.filter(x=>x!==p.id)})}/>
            {p.displayName} ({p.email})</label>)}
        </fieldset>}
        <ErrorMessage message={error}/>
        {info&&<p role="status" className="um-feedback success">{info}</p>}
        <div className="favorites-toolbar">
          <button type="submit" disabled={busy} className="button button-primary">
            <Save size={16}/>Speichern</button>
          {current&&<button className="button button-secondary" type="button"
            disabled={busy} onClick={()=>void erase()}>
            <Trash2 size={15}/>Entfernen</button>}
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
  useEffect(()=>{
    let active=true;
    setLoading(true);setError('');setUrl('');
    void(async()=>{
      try{
        if(!id)throw new Error('Favorit nicht gefunden.');
        const items=await loadFavorites();
        const item=items.find(x=>x.id===id&&x.enabled);
        if(!item)throw new Error('Favorit nicht vorhanden oder deaktiviert.');
        const result=await startFavorite(id);
        const destination=new URL(result.url);
        if(destination.protocol!=='https:'||
          destination.origin===window.location.origin||
          destination.pathname!=='/_plenaro/favorite-bootstrap')
          throw new Error('Ungültige Favoriten-Proxy-Adresse.');
        if(active){setFavorite(item);setUrl(destination.toString());}
      }catch(e){
        if(active){setError(e instanceof Error?e.message:'Webseite nicht erreichbar.');
          setLoading(false);}
      }
    })();
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
        <Link className="button button-secondary" to="/settings">Favoriten verwalten</Link>
      </div>
    </div>
    {error&&<div className="content-card znuny-portal-error" role="alert">
      <strong>Favorit konnte nicht geöffnet werden</strong><p>{error}</p></div>}
    {loading&&!error&&<div role="status" className="znuny-native-loading">
      Webseite wird geöffnet …</div>}
    {!error&&url&&<div className="znuny-portal-frame-wrap znuny-native-frame-wrap">
      <div className="znuny-portal-security-caption">
        <ShieldCheck size={14}/>Browser-Origin getrennt, Verbindung über Plenaro.</div>
      <iframe key={url} title={favorite?.name??'Webfavorit'}
        src={url} className="znuny-native-frame"
        sandbox="allow-same-origin allow-scripts allow-forms allow-popups allow-modals allow-downloads"
        referrerPolicy="no-referrer"
        onLoad={()=>setLoading(false)}/>
    </div>}
  </div>;
}
