import {useCallback,useEffect,useState,type FormEvent} from 'react';
import {BookOpenText,CheckCircle2,Save,ShieldCheck,Trash2} from 'lucide-react';
import {Link} from 'react-router-dom';
import {
  readWikiSettings,readWikiAdmin,saveWikiLogin,deleteWikiLogin,
  saveWikiAdmin,type WikiAdminInput,type WikiAdminSettings,type WikiSettings
} from './wikiApi';

const emptyAdmin:WikiAdminSettings={
  configured:false,name:'Firmenwiki',baseUrl:'',homePath:'/'
};

/** Admin owns the server; every user owns only their Wiki credentials. */
export function WikiSettingsPanel({admin}:{admin:boolean}){
  const [server,setServer]=useState<WikiAdminSettings>(emptyAdmin);
  const [serverForm,setServerForm]=useState<WikiAdminInput>({
    name:'Firmenwiki',baseUrl:'',homePath:'/'
  });
  const [myWiki,setMyWiki]=useState<WikiSettings|null>(null);
  const [username,setUsername]=useState('');
  const [password,setPassword]=useState('');
  const [busy,setBusy]=useState(false);
  const [loading,setLoading]=useState(true);
  const [notice,setNotice]=useState('');
  const [error,setError]=useState('');

  const refresh=useCallback(async()=>{
    const [userWiki,adminWiki]=await Promise.all([
      readWikiSettings(),admin?readWikiAdmin():Promise.resolve(null)
    ]);
    setMyWiki(userWiki);setUsername(userWiki.username);
    if(adminWiki){
      setServer(adminWiki);
      setServerForm({name:adminWiki.name||'Firmenwiki',
        baseUrl:adminWiki.baseUrl,homePath:adminWiki.homePath||'/'});
    }
  },[admin]);

  useEffect(()=>{
    let active=true;
    void refresh().then(()=>{if(active)setLoading(false);})
      .catch(reason=>{if(active){setLoading(false);
        setError(reason instanceof Error?reason.message:'Wiki-Einstellungen konnten nicht geladen werden.');}});
    return()=>{active=false;};
  },[refresh]);

  async function saveServer(e:FormEvent){
    e.preventDefault();if(busy)return;
    setBusy(true);setError('');setNotice('');
    try{
      await saveWikiAdmin(serverForm);
      await refresh();
      setNotice('Der Wiki-Server ist zentral für alle Benutzer eingerichtet.');
    }catch(reason){setError(reason instanceof Error?reason.message:'Wiki-Server konnte nicht gespeichert werden.');}
    finally{setBusy(false);}
  }

  async function saveCredentials(e:FormEvent){
    e.preventDefault();if(busy)return;
    setBusy(true);setError('');setNotice('');
    try{
      await saveWikiLogin(username.trim(),password);
      setPassword('');await refresh();
      setNotice('Deine persönlichen Wiki-Zugangsdaten wurden verschlüsselt gespeichert.');
    }catch(reason){setError(reason instanceof Error?reason.message:'Wiki-Zugangsdaten konnten nicht gespeichert werden.');}
    finally{setBusy(false);}
  }

  async function clearCredentials(){
    if(busy||!myWiki?.hasPassword)return;
    if(!window.confirm('Deine persönlichen Wiki-Zugangsdaten entfernen?'))return;
    setBusy(true);setError('');setNotice('');
    try{
      await deleteWikiLogin();
      setUsername('');setPassword('');await refresh();
      setNotice('Deine Wiki-Zugangsdaten wurden entfernt.');
    }catch(reason){setError(reason instanceof Error?reason.message:'Zugangsdaten konnten nicht gelöscht werden.');}
    finally{setBusy(false);}
  }

  return <div className="wiki-settings">
    {admin&&<section className="content-card um-panel">
      <div className="um-section-title"><ShieldCheck size={20}/>
        <h2>Zentrale Wiki-Konfiguration (Administrator)</h2></div>
      <p>Diese Verbindung gilt für <strong>alle Plenaro-Benutzer</strong>.
        Nur Administratoren können die Serveradresse und die Startseite festlegen.
        Benutzer dürfen die Wiki-Adresse nicht ändern.</p>
      {!loading&&<form className="um-form" onSubmit={e=>void saveServer(e)}>
        <label htmlFor="wiki-admin-name">Wiki-Bezeichnung</label>
        <input id="wiki-admin-name" required maxLength={120}
          value={serverForm.name}
          onChange={e=>setServerForm({...serverForm,name:e.target.value})}/>
        <label htmlFor="wiki-admin-url">Wiki-Serveradresse (HTTPS)</label>
        <input id="wiki-admin-url" type="url" required
          placeholder="https://wiki.example.org/"
          value={serverForm.baseUrl}
          onChange={e=>setServerForm({...serverForm,baseUrl:e.target.value})}/>
        <label htmlFor="wiki-admin-home">Wiki-Startseite / Pfad</label>
        <input id="wiki-admin-home" required value={serverForm.homePath}
          placeholder="/" maxLength={2048}
          onChange={e=>setServerForm({...serverForm,homePath:e.target.value})}/>
        <p className="um-hint">Die Serveradresse muss eine HTTPS-Origin ohne Unterpfad sein.
          Die Startseite kann beispielsweise / oder /display/IT sein.
          Bestehende Wiki-Zugangsdaten der Benutzer bleiben beim Ändern erhalten.
          Bereits laufende Wiki-Sitzungen werden neu aufgebaut.</p>
        <button type="submit" className="button button-primary" disabled={busy}>
          <Save size={16}/>Zentrale Wiki-Konfiguration speichern</button>
      </form>}
      {server.configured&&<p className="um-hint">Aktuell aktiv: {server.name}</p>}
    </section>}

    <section className="content-card um-panel">
      <div className="um-section-title"><BookOpenText size={20}/>
        <h2>Meine Wiki-Anmeldung</h2></div>
      {loading?<p role="status">Wiki-Einstellungen werden geladen …</p>:
        !myWiki?.configured?<p>Der Administrator hat noch keinen zentralen Wiki-Server
          freigegeben. Sobald dies erfolgt, kannst du hier deine eigenen Zugangsdaten eingeben.</p>:
        <>
          <p>Wiki: <strong>{myWiki.name}</strong>.
            Trage deinen persönlichen Confluence-Benutzernamen und dein Passwort ein.
            Plenaro versucht beim Öffnen der Wissensdatenbank automatisch,
            dich mit deinem Benutzerkonto am Wiki anzumelden.</p>
          <form className="um-form" onSubmit={e=>void saveCredentials(e)}>
            <label htmlFor="wiki-user">Mein Wiki-Benutzername</label>
            <input id="wiki-user" required autoComplete="off"
              maxLength={180} value={username}
              onChange={e=>setUsername(e.target.value)}/>
            <label htmlFor="wiki-pass">Mein Wiki-Passwort</label>
            <input id="wiki-pass" type="password"
              required={!myWiki.hasPassword} autoComplete="new-password"
              value={password} onChange={e=>setPassword(e.target.value)}
              placeholder={myWiki.hasPassword
                ? 'Gespeichert – leer lassen zum Beibehalten'
                : 'Wiki-Passwort eingeben'}/>
            <p className="um-hint">Das Passwort wird ausschließlich im persönlichen
              verschlüsselten Plenaro-Vault gespeichert und nicht an andere Benutzer
              oder die Wiki-Browserseite übermittelt. Die Anmeldung erfolgt
              serverseitig und nur über die freigegebene HTTPS-Wiki-Adresse.</p>
            <div className="favorites-toolbar">
              <button type="submit" className="button button-primary" disabled={busy}>
                <Save size={16}/>Wiki-Zugangsdaten speichern</button>
              {myWiki.ready&&<Link to="/wiki" className="button button-secondary">
                Wissensdatenbank öffnen</Link>}
              {myWiki.hasPassword&&<button type="button" className="button button-secondary"
                disabled={busy} onClick={()=>void clearCredentials()}>
                <Trash2 size={16}/>Zugangsdaten entfernen</button>}
            </div>
          </form>
          <p className="um-hint">Automatischer Login ist für deine Confluence-Anmeldung
            vorgesehen. Falls das Wiki zusätzliche Faktoren, CAPTCHA oder SSO verlangt,
            meldet Plenaro den fehlgeschlagenen automatischen Login.</p>
        </>}
    </section>
    {notice&&<p className="um-feedback success" role="status">
      <CheckCircle2 size={16}/>{notice}</p>}
    {error&&<p className="um-feedback failure" role="alert">{error}</p>}
  </div>;
}
