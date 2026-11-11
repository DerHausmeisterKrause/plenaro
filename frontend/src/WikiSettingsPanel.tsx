import {useCallback,useEffect,useState,type FormEvent} from 'react';
import {BookOpenText,CheckCircle2,Plus,Save,ShieldCheck} from 'lucide-react';
import {Link} from 'react-router-dom';
import {
  loadFavoriteApps,loadFavorites,loadApprovedApps,createApprovedApp,createFavorite,
  type Favorite,type FavoriteApp
} from './favoritesApi';
import {readWikiSelection,saveWikiSelection} from './wikiApi';

type SelectableApp=Pick<FavoriteApp,'id'|'name'|'slug'>;

/** Classic WikiSettingsView: source, home URL, personal browser login. */
export function WikiSettingsPanel({admin,userId}:{admin:boolean;userId:string}){
  const [sources,setSources]=useState<Favorite[]>([]);
  const [apps,setApps]=useState<SelectableApp[]>([]);
  const [approved,setApproved]=useState<FavoriteApp[]>([]);
  const [selected,setSelected]=useState('');
  const [newApp,setNewApp]=useState('');
  const [name,setName]=useState('Mein Wiki');
  const [home,setHome]=useState('/');
  const [username,setUsername]=useState('');
  const [password,setPassword]=useState('');
  const [autoLogin,setAutoLogin]=useState(false);
  const [serverName,setServerName]=useState('Wiki');
  const [serverUrl,setServerUrl]=useState('');
  const [shareWithAll,setShareWithAll]=useState(false);
  const [busy,setBusy]=useState(false);
  const [loading,setLoading]=useState(true);
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('');

  const reload=useCallback(async()=>{
    const [favorites,allowed,preference,adminApps]=await Promise.all([
      loadFavorites(),loadFavoriteApps(),readWikiSelection(),
      admin?loadApprovedApps():Promise.resolve([] as FavoriteApp[])
    ]);
    setSources(favorites.filter(f=>f.enabled));
    setApps(allowed);
    setApproved(adminApps);
    setSelected(preference.favoriteId??'');
    setNewApp(current=>allowed.some(a=>a.id===current)?current:
      allowed.find(a=>a.slug==='wiki')?.id??allowed[0]?.id??'');
  },[admin]);

  useEffect(()=>{
    let active=true;
    void reload().then(()=>{if(active)setLoading(false);})
      .catch(reason=>{if(active){setLoading(false);
        setError(reason instanceof Error?reason.message:'Wissensdatenbank konnte nicht geladen werden.');}});
    return()=>{active=false;};
  },[reload]);

  async function choose(e:FormEvent){
    e.preventDefault();if(busy)return;
    setBusy(true);setError('');setNotice('');
    try{
      await saveWikiSelection(selected||null);
      setNotice('Standard-Wiki für dein Konto gespeichert.');
    }catch(reason){setError(reason instanceof Error?reason.message:'Speichern fehlgeschlagen.');}
    finally{setBusy(false);}
  }

  async function addFavorite(e:FormEvent){
    e.preventDefault();if(busy)return;
    setBusy(true);setError('');setNotice('');
    try{
      if(!apps.some(a=>a.id===newApp))throw new Error('Bitte eine freigegebene Wiki-Anwendung wählen.');
      if(!home.startsWith('/')||home.startsWith('//')||home.includes('#')||
         home.includes('\\')||home.includes('://'))
        throw new Error('Die Startseite muss ein relativer Pfad wie /display/TEAM sein.');
      const added=await createFavorite({
        appId:newApp,name:name.trim(),path:home,
        username:username.trim(),password,autoLogin,enabled:true,
        sortOrder:sources.length
      });
      await saveWikiSelection(added.id);
      await reload();
      setPassword('');
      setNotice('Wiki-Quelle hinzugefügt und als Standard gespeichert.');
    }catch(reason){setError(reason instanceof Error?reason.message:'Wiki konnte nicht eingerichtet werden.');}
    finally{setBusy(false);}
  }

  async function addApproved(e:FormEvent){
    e.preventDefault();if(busy)return;
    setBusy(true);setError('');setNotice('');
    try{
      const input=new URL(serverUrl.trim());
      if(input.protocol!=='https:'||input.username||input.password||
        input.search||input.hash||!input.hostname.includes('.'))
        throw new Error('Bitte eine gültige interne HTTPS-Wiki-Adresse ohne Query oder Zugangsdaten eingeben.');
      if(approved.some(a=>a.slug==='wiki'))
        throw new Error('Eine Wiki-Anwendung ist bereits freigegeben. Bestehende URL unter Favoriten verwalten.');
      const registered=await createApprovedApp({
        name:serverName.trim(),slug:'wiki',upstreamUrl:input.origin+'/',
        enabled:true,availableToAll:shareWithAll,
        allowedUsers:shareWithAll?[]:[userId]
      });
      await reload();
      setNewApp(registered.id);
      setHome(input.pathname||'/');
      setName(serverName.trim());
      setNotice('Wiki-Server freigegeben. Jetzt unten persönliche Wiki-Quelle hinzufügen.');
    }catch(reason){setError(reason instanceof Error?reason.message:'Wiki-Server konnte nicht freigegeben werden.');}
    finally{setBusy(false);}
  }

  const available=apps.filter(a=>!sources.some(f=>f.appId===a.id));
  const existingWiki=approved.find(a=>a.slug==='wiki');
  return <div className="wiki-settings">
    <section className="content-card um-panel">
      <div className="um-section-title"><BookOpenText size={20}/><h2>Wiki / Wissensdatenbank</h2></div>
      <p>Wie im Desktop-Plenaro verwendest du eine Wiki-Quelle mit eigener Startseite.
        Im Web wird sie über den bereits eingerichteten Plenaro-Favoriten-Proxy geöffnet.
        Zugangsdaten und die Sitzung bleiben benutzerbezogen.</p>
      {loading?<p role="status">Wiki-Einstellungen werden geladen …</p>:<>
        <form className="um-form" onSubmit={e=>void choose(e)}>
          <label htmlFor="wiki-default">Standard-Wiki</label>
          <select id="wiki-default" value={selected}
            onChange={e=>setSelected(e.target.value)}>
            <option value="">Keine Wiki-Quelle ausgewählt</option>
            {sources.map(s=><option key={s.id} value={s.id}>
              {s.name} ({s.appName})</option>)}
          </select>
          <p className="um-hint">Du kannst einen bereits funktionierenden Wiki-Favoriten übernehmen.
            Er wird in der Wissensdatenbank als eigener Browser angezeigt.</p>
          <div className="favorites-toolbar">
            <button className="button button-primary" type="submit" disabled={busy}>
              <Save size={16}/>Standard speichern</button>
            <Link className="button button-secondary" to="/wiki">Wissensdatenbank öffnen</Link>
            <Link className="button button-secondary" to="/settings?section=favorites">
              Favoriten und Anmeldedaten bearbeiten</Link>
          </div>
        </form>
      </>}
    </section>

    {admin&&!loading&&<section className="content-card um-panel">
      <div className="um-section-title"><ShieldCheck size={20}/><h2>Wiki-Server freigeben</h2></div>
      {existingWiki?<p>Der Server <strong>{existingWiki.name}</strong> ist bereits freigegeben:
        <code> {existingWiki.upstreamUrl}</code>. Die URL ist nach der Freigabe
        aus Sicherheitsgründen unveränderlich. Zugriffsrechte und weitere Quellen
        können unter <Link to="/settings?section=favorites">Favoriten</Link> verwaltet werden.</p>:
      <form className="um-form" onSubmit={e=>void addApproved(e)}>
        <p>Nur Administratoren können die Zieladresse festlegen.
          Es werden ausschließlich HTTPS-Wikis freigegeben.</p>
        <label htmlFor="wiki-server-name">Bezeichnung</label>
        <input id="wiki-server-name" required value={serverName} maxLength={120}
          onChange={e=>setServerName(e.target.value)}/>
        <label htmlFor="wiki-server-url">Wiki-Adresse (HTTPS)</label>
        <input id="wiki-server-url" required type="url" value={serverUrl}
          placeholder="https://wiki.beispiel.org/display/TEAM"
          onChange={e=>setServerUrl(e.target.value)}/>
        <label className="favorites-check">
          <input type="checkbox" checked={shareWithAll}
            onChange={e=>setShareWithAll(e.target.checked)}/>
          Für alle Plenaro-Benutzer freigeben</label>
        <p className="um-hint">Die Server-Origin wird freigegeben; die Unterseite wird
          als persönliche Startseite übernommen. Zugriffe erfolgen nie als freier URL-Proxy.</p>
        <button className="button button-primary" type="submit" disabled={busy}>
          <Plus size={16}/>Wiki-Server freigeben</button>
      </form>}
    </section>}

    {!loading&&<section className="content-card um-panel">
      <div className="um-section-title"><Plus size={20}/><h2>Persönliche Wiki-Quelle hinzufügen</h2></div>
      {available.length===0?<p>Aktuell gibt es keine weitere für dich freigegebene Anwendung.
        {sources.length>0?' Du kannst deine vorhandene Wiki-Quelle oben auswählen.':
        ' Ein Administrator muss zunächst den Wiki-Server freigeben.'}</p>:
      <form className="um-form" onSubmit={e=>void addFavorite(e)}>
        <label htmlFor="wiki-app">Freigegebener Wiki-Server</label>
        <select id="wiki-app" value={newApp} onChange={e=>setNewApp(e.target.value)}>
          {available.map(a=><option key={a.id} value={a.id}>{a.name}</option>)}
        </select>
        <label htmlFor="wiki-name">Name der Wiki-Quelle</label>
        <input id="wiki-name" value={name} required maxLength={120}
          onChange={e=>setName(e.target.value)}/>
        <label htmlFor="wiki-home">Startseite / Pfad</label>
        <input id="wiki-home" value={home} required maxLength={2048}
          placeholder="/" onChange={e=>setHome(e.target.value)}/>
        <label htmlFor="wiki-username">Benutzername (optional)</label>
        <input id="wiki-username" value={username} maxLength={180}
          onChange={e=>setUsername(e.target.value)} autoComplete="off"/>
        <label htmlFor="wiki-password">Passwort (optional)</label>
        <input id="wiki-password" type="password" value={password}
          onChange={e=>setPassword(e.target.value)} autoComplete="new-password"/>
        <label className="favorites-check">
          <input type="checkbox" checked={autoLogin}
            onChange={e=>setAutoLogin(e.target.checked)}/>
          Automatischen HTML-Login versuchen</label>
        <p className="um-hint">Ein Confluence-Login mit eigenem JavaScript kann weiterhin
          eine manuelle Anmeldung benötigen. Passwörter werden nur im verschlüsselten
          Plenaro-Vault gespeichert, nicht in Wiki-Einstellungen.</p>
        <button className="button button-primary" type="submit" disabled={busy}>
          <Plus size={16}/>Wiki hinzufügen und als Standard verwenden</button>
      </form>}
    </section>}
    {notice&&<p className="um-feedback success" role="status"><CheckCircle2 size={16}/>{notice}</p>}
    {error&&<p className="um-feedback failure" role="alert">{error}</p>}
  </div>;
}
