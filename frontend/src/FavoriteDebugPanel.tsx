import {useCallback,useEffect,useState} from 'react';
import {Bug,RefreshCw,Trash2} from 'lucide-react';
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
  'login-shared-dom-opt-in':'Alternative Browser-Anmeldung bewusst aktiviert (gemeinsamer Host, Klartext-Risiko)',
  'autofill-shared-injected':'Alternative Browser-Anmeldung einmalig in die Webseite eingebunden',
  'login-dedicated-dom':'Automatische Browser-Anmeldung über eigenen HTTPS-Host vorgesehen',
  'login-credentials-missing':'Benutzername oder Passwort fehlen',
  'headless-login-attempt':'Privater Login-Browser wird aufgerufen',
  'browser-worker-unreachable':'Privater Browserdienst nicht erreichbar. Kein zusätzlicher Container erforderlich: klassischer Login wird versucht; JavaScript-Login bleibt manuell.',
  'browser-target-blocked':'Browser-Login: Zieladresse nicht zugelassen',
  'browser-no-login-fields':'Im Browser keine passenden Loginfelder gefunden',
  'browser-no-login-action':'Im Browser keine eindeutige Anmeldeaktion gefunden',
  'browser-login-not-confirmed':'Browseranmeldung wurde nicht bestätigt',
  'browser-foreign-login':'Browseranmeldung würde einen fremden Login-Host benötigen',
  'browser-no-session-cookies':'Browser: Anmeldung erkannt, aber keine Sitzungscookies vorhanden',
  'browser-no-valid-session-cookies':'Browser hat keine gültigen Sitzungscookies zurückgegeben',
  'browser-login-confirmed':'Browser hat eine gültige Sitzung übernommen',
  'browser-transport-uncertain':'Browserantwort unklar; doppelte Anmeldung wird vermieden',
  'browser-worker-error':'Browserdienst antwortet mit einem Fehler',
  'browser-worker-busy':'Browserdienst ausgelastet',
  'shared-origin-autofill-disabled':'Gemeinsamer Favoriten-Host: Passwortübergabe an JavaScript absichtlich gesperrt (kein DNS-Fehler)',
  'html-login-page-rejected':'HTML-Loginseite antwortet mit Fehlerstatus',
  'html-login-redirect-missing':'HTML-Loginseite leitet ohne Ziel weiter',
  'html-login-redirect-cross-origin':'HTML-Login würde zu fremdem Server weiterleiten',
  'html-login-navigation-blocked':'HTML-Login-Navigation nicht zulässig',
  'html-login-navigation-limit':'HTML-Login-Routing-Limit erreicht',
  'html-login-page-not-html':'Loginziel liefert kein HTML-Formular',
  'html-login-page-too-large':'HTML-Loginseite über dem Größenlimit',
  'html-login-form-not-found':'Kein geeignetes klassisches HTML-Anmeldeformular gefunden; JavaScript-Login muss manuell erfolgen',
  'html-login-post-rejected':'Server hat den HTML-Login-POST abgelehnt',
  'html-login-followup-rejected':'Weiterleitung nach HTML-Login fehlgeschlagen',
  'html-login-post-sent-unconfirmed':'HTML-Anmeldedaten gesendet; gültige Sitzung noch nicht bestätigt',
  'upstream-400-csrf-hint':'Hinweis aus Serverantwort: CSRF-/XSRF-Prüfung könnte HTTP 400 verursachen',
  'upstream-400-origin-hint':'Hinweis aus Serverantwort: Origin-/Referer-Prüfung könnte HTTP 400 verursachen',
  'upstream-400-session-hint':'Hinweis aus Serverantwort: Sitzung/Anmeldung könnte HTTP 400 verursachen',
  'upstream-400-validation-hint':'Hinweis aus Serverantwort: Pflichtfelder/Validierung könnten HTTP 400 verursachen',
  'upstream-400-content-type-hint':'Hinweis aus Serverantwort: Content-Type könnte HTTP 400 verursachen',
  'headless-login-cookies':'Privater Browser hat eine Sitzung übergeben',
  'headless-login-fallback':'Privater Browser nicht erfolgreich, HTML-Fallback wird versucht',
  'atoss-zk-login-attempt':'ATOSS: sichere ZK-Anmeldung im vorhandenen Plenaro-Backend gestartet',
  'atoss-zk-login-confirmed':'ATOSS: ZK-Anmeldung und anschließende Sitzung bestätigt',
  'atoss-zk-login-unconfirmed':'ATOSS: ZK-Anmeldeereignisse gesendet, Anmeldung aber nicht bestätigt (nicht erneut versucht)',
  'atoss-zk-dialog-initialization-failed':'ATOSS: Dialoginitialisierung fehlgeschlagen (noch keine Zugangsdaten gesendet)',
  'atoss-zk-username-rejected':'ATOSS: Benutzername-Ereignis vom ZK-Server abgewiesen (Passwort nicht gesendet)',
  'atoss-zk-password-rejected':'ATOSS: Passwort-Ereignis nicht bestätigt (kein Anmeldeklick gesendet)',
  'atoss-zk-click-rejected':'ATOSS: Anmeldeklick nicht bestätigt; kein automatischer Wiederholungsversuch',
  'atoss-zk-session-unconfirmed':'ATOSS: Anmeldeklick gesendet, die bestehende ZK-Sitzung zeigt aber weiter den Login oder keine bestätigte Anmeldeseite',
  'atoss-zk-login-page-unavailable':'ATOSS: dynamische ZK-Anmeldeseite nicht abrufbar',
  'atoss-zk-frame-http-error':'ATOSS: ZK-Anmeldeseite antwortet mit HTTP-Fehler (Status beachten)',
  'atoss-zk-frame-not-html':'ATOSS: Server lieferte statt einer Anmeldeseite ein anderes Format',
  'atoss-zk-frame-redirect-blocked':'ATOSS: ZK-Anmeldeseite leitet außerhalb der erlaubten HTTPS-Anwendung weiter',
  'atoss-zk-frame-redirect-limit':'ATOSS: ZK-Anmeldeseite hat zu viele Weiterleitungen',
  'atoss-zk-frame-too-large':'ATOSS: ZK-Anmeldeseite überschreitet das zulässige Größenlimit',
  'atoss-zk-frame-navigation-blocked':'ATOSS: ZK-Anmeldeseite hat eine nicht zugelassene Adresse',
  'atoss-zk-login-form-unavailable':'ATOSS: ZK-Anmeldefelder oder Login-Schaltfläche nicht eindeutig erkannt',
  'atoss-zk-field-rejected':'ATOSS: ZK-Server hat die Benutzername-Aktualisierung nicht angenommen',
  'atoss-zk-not-applicable':'ATOSS: Sitzung liefert nicht den erwarteten ZK-Loginaufbau',
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
  'upstream-unavailable':'Zielserver nicht erreichbar – Netzwerk und Firewall prüfen',
  'upstream-unreachable':'Kein Netzwerkpfad zum Zielserver – Routing und Firewall prüfen',
  'upstream-refused':'TCP-Verbindung abgelehnt – Port und Firewall prüfen',
  'upstream-dns':'DNS-Auflösung fehlgeschlagen',
  'upstream-tls':'TLS- oder Zertifikatsprüfung fehlgeschlagen',
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
          disabled={busy} onChange={e=>void toggle(e.target.checked)}/>
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
  // No card, placeholder or vertical gap unless the administrator
  // explicitly opted in under Settings -> Webseiten / Favoriten.
  // Non-admin users never mount this component (FavoritePage admin guard).
  if(enabled!==true)return null;
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
    <>
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
            <span title="Die Kennung identifiziert denselben Ressourcenpfad ohne dessen URL offenzulegen">{entry.method||'–'} · {entry.resource}{entry.route?' · #'+entry.route:''}</span>
            <span title={entry.code}>{descriptions[entry.code]??entry.code}</span>
          </div>):
          <p className="um-hint">Noch keine passenden Ereignisse. Öffne oder
            aktualisiere den Favoriten, um neue Proxy-Anfragen zu sehen.</p>}
        </div>
        <p className="um-hint">Automatische Aktualisierung alle 3 Sekunden.
          Gleiche Kennung (#) bedeutet gleicher Ressourcenpfad; die URL
          selbst bleibt verborgen. Ein Hinweis auf eine HTTP-400-Ursache ist
          keine sichere Diagnose. Die Konsole sieht serverseitige Proxy-Anfragen,
          aber nicht die
          JavaScript-Konsole einer fremden Webseite oder durch den Browser
          vor dem Proxy blockierte externe Anfragen.</p>
      </>
  </section>;
}
