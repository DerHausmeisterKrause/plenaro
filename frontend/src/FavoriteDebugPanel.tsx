import {useCallback,useEffect,useState} from 'react';
import {Bug,RefreshCw,Trash2} from 'lucide-react';
import {Link} from 'react-router-dom';
import {
  readFavoriteDebugSetting,setFavoriteDebugSetting,
  readFavoriteDebugEvents,clearFavoriteDebugEvents,
  type FavoriteDebugEntry
} from './favoriteDebugApi';

const descriptions:Record<string,string>={
  'session-start':'Favoriten-Verbindung wird aufgebaut',
  'session-ready':'Proxy-Sitzung und Bootstrap bereit',
  'bootstrap-success':'Einbettung hat die Proxy-Sitzung übernommen',
  'session-revoked':'Favorit oder Benutzerberechtigung wurde verändert',
  'login-disabled':'Automatische Anmeldung ist deaktiviert',
  'login-server-or-browser':'Server-/Browser-Anmeldung vorgesehen',
  'login-dedicated-dom':'Automatische Browser-Anmeldung über eigenen HTTPS-Host vorgesehen',
  'login-credentials-missing':'Benutzername oder Passwort fehlen',
  'headless-login-attempt':'Privater Login-Browser wird aufgerufen',
  'headless-login-cookies':'Privater Browser hat eine Sitzung übergeben',
  'headless-login-fallback':'Privater Browser nicht erfolgreich, HTML-Fallback wird versucht',
  'html-login-attempt':'Klassischen HTML-Loginversuch gestartet',
  'html-login-attempt-finished':'HTML-Loginversuch beendet (Anmeldung nicht sicher bestätigt)',
  'login-fallback-error':'Automatische Anmeldung fehlgeschlagen; manuelle Anmeldung möglich',
  'login-start-failed':'Favoriten-Verbindung konnte nicht aufgebaut werden',
  'wiki-login-check':'Anmeldung an der Wissensdatenbank wird geprüft',
  'http-response':'Antwort vom Zielserver',
  'request-blocked':'Anfrage aus Sicherheitsgründen blockiert',
  'target-blocked':'Ziel außerhalb des freigegebenen Servers blockiert',
  'redirect-cross-origin':'Weiterleitung zu einem anderen Host blockiert',
  'login-next-rewritten':'Login-Weiterleitung auf Favoriten-Pfad angepasst',
  'login-next-unchanged':'Login-Weiterleitung nicht verändert',
  'autofill-origin-required':'Browser-Autofill braucht einen eigenen HTTPS-Host',
  'autofill-injected':'Autofill wurde in die isolierte HTML-Seite eingebunden',
  'autofill-password-missing':'Autofill-Passwort nicht verfügbar',
  'autofill-vault-unconfigured':'Sicherer Passwortspeicher nicht verfügbar',
  'private-address-blocked':'Unzulässige Serveradresse blockiert',
  'upstream-unavailable':'Zielserver nicht erreichbar, Verbindung oder TLS prüfen',
  'upstream-timeout':'Zielserver hat nicht rechtzeitig geantwortet',
  'upstream-resource-limit':'Antwort übersteigt das Proxy-Größenlimit',
};

export function FavoriteDebugSettingsPanel(){
  const [enabled,setEnabled]=useState(false);
  const [loading,setLoading]=useState(true);
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  useEffect(()=>{
    let active=true;
    void readFavoriteDebugSetting().then(value=>{
      if(active){setEnabled(value.enabled);setLoading(false);}
    }).catch(e=>{
      if(active){setError(e instanceof Error?e.message:'Laden fehlgeschlagen.');
        setLoading(false);}
    });
    return()=>{active=false;};
  },[]);
  async function toggle(next:boolean){
    if(busy)return;
    setBusy(true);setError('');
    try{await setFavoriteDebugSetting(next);setEnabled(next);}
    catch(e){setError(e instanceof Error?e.message:'Speichern fehlgeschlagen.');}
    finally{setBusy(false);}
  }
  return <section className="content-card favorites-debug-settings"
    aria-labelledby="favorites-debug-settings-title">
    <div className="um-section-title"><Bug size={20}/>
      <h2 id="favorites-debug-settings-title">Favoriten-Debugmodus (Administration)</h2>
    </div>
    <p>Aktiviert nur für dein Administratorkonto eine Diagnoseansicht direkt
      unter eingebetteten Favoriten. Sie zeigt HTTP-Fehler, gesperrte Anfragen,
      Weiterleitungen, Timeouts und die Login-Abläufe.</p>
    {loading?<p role="status">Lade Debug-Einstellung …</p>:
      <label className="um-check" htmlFor="favorites-debug-enable">
        <input id="favorites-debug-enable" type="checkbox" checked={enabled}
          disabled={busy||!!error} onChange={e=>void toggle(e.target.checked)}/>
        Favoriten-Debugmodus für mein Administratorkonto aktivieren
      </label>}
    {error&&<p role="alert" className="um-feedback failure">{error}</p>}
    <p className="um-hint">Standardmäßig aus. Nur Administratoren können
      diese Einstellung ändern oder die Logs lesen. Erfasst werden ausschließlich
      technische Ereignisse ohne URL-Parameter, Passwörter, Cookies oder
      Anfrage-/Antwortinhalte. Pro Favorit bleiben höchstens 250 Einträge
      im Arbeitsspeicher; beim Deaktivieren werden sie gelöscht.</p>
  </section>;
}

export function FavoriteDebugConsole({favoriteId}:{favoriteId:string}){
  const [enabled,setEnabled]=useState<boolean|null>(null);
  const [entries,setEntries]=useState<FavoriteDebugEntry[]>([]);
  const [onlyProblems,setOnlyProblems]=useState(false);
  const [error,setError]=useState('');
  const [clearing,setClearing]=useState(false);
  const fetchLogs=useCallback(async()=>{
    const next=await readFavoriteDebugEvents(favoriteId,0);
    setEnabled(next.enabled);
    setEntries(next.events);
    setError('');
  },[favoriteId]);
  useEffect(()=>{
    let active=true;
    const update=async()=>{
      try{
        const next=await readFavoriteDebugEvents(favoriteId,0);
        if(active){setEnabled(next.enabled);setEntries(next.events);setError('');}
      }catch(e){
        if(active)setError(e instanceof Error?e.message:'Fehler beim Laden der Diagnose.');
      }
    };
    void update();
    const interval=window.setInterval(()=>{
      if(!document.hidden)void update();
    },3000);
    return()=>{active=false;window.clearInterval(interval);};
  },[favoriteId]);
  async function clear(){
    if(clearing)return;
    setClearing(true);setError('');
    try{await clearFavoriteDebugEvents(favoriteId);setEntries([]);
      await fetchLogs();}
    catch(e){setError(e instanceof Error?e.message:'Protokoll konnte nicht geleert werden.');}
    finally{setClearing(false);}
  }
  const visible=onlyProblems?entries.filter(e=>e.level!=='info'):entries;
  return <section className="content-card favorites-debug-console"
    aria-label="Favoriten-Diagnose">
    <div className="favorites-debug-heading">
      <h2><Bug size={19}/>Favoriten-Diagnose</h2>
      <div className="favorites-toolbar">
        <button type="button" className="button button-secondary"
          onClick={()=>void fetchLogs()}><RefreshCw size={14}/>Aktualisieren</button>
        <button type="button" className="button button-secondary" disabled={clearing}
          onClick={()=>void clear()}><Trash2 size={14}/>Log leeren</button>
      </div>
    </div>
    {error&&<p role="alert" className="um-feedback failure">{error}</p>}
    {enabled===null?<p role="status">Diagnoseeinstellungen werden geladen …</p>:
      enabled===false?<p>Debugmodus deaktiviert. Unter
        {' '}<Link to="/settings?section=favorites">Einstellungen → Webseiten / Favoriten</Link>
        {' '}kannst du ihn für dein Administratorkonto einschalten.</p>:<>
        <label className="favorites-debug-filter">
          <input type="checkbox" checked={onlyProblems}
            onChange={e=>setOnlyProblems(e.target.checked)}/>
          Nur Warnungen und Fehler anzeigen
        </label>
        <div className="favorites-debug-output" role="log" aria-live="off">
          {visible.length?visible.map(entry=><div key={entry.id}
            className={'favorites-debug-entry '+entry.level}>
            <time>{new Date(entry.at).toLocaleTimeString('de-DE')}</time>
            <span className="favorites-debug-level">{entry.level.toUpperCase()}</span>
            <span>{entry.status??'–'}</span>
            <span>{entry.method||'–'} · {entry.resource}</span>
            <span title={entry.code}>{descriptions[entry.code]??entry.code}</span>
          </div>):
          <p className="um-hint">Noch keine passenden Ereignisse. Öffne oder
            aktualisiere den Favoriten, um neue Proxy-Anfragen zu sehen.</p>}
        </div>
        <p className="um-hint">Automatische Aktualisierung alle 3 Sekunden.
          Die Diagnose sieht serverseitige Proxy-Anfragen, aber nicht die
          JavaScript-Konsole einer fremden Webseite oder durch den Browser
          vor dem Proxy blockierte externe Anfragen.</p>
      </>}
  </section>;
}
