import { useEffect, useState, type FormEvent } from 'react';
import { PlugZap, Save, ShieldCheck, RotateCcw, EyeOff } from 'lucide-react';
import {
  getZnunyPersonal, getZnunyServer, saveZnunyPersonal, saveZnunyServer,
  resetZnunyServer, saveZnunyPassword, deleteZnunyPassword, testZnunyConnection,
  znunyConnectionMessage, type ZnunyServerSettings, type ZnunyPersonalSettings,
  type ZnunyPersonalView,
} from './znunyApi';

type TextServerKey = {
  [K in keyof ZnunyServerSettings]:
    ZnunyServerSettings[K] extends string ? K : never
}[keyof ZnunyServerSettings];

const routes: Array<[TextServerKey,string]> = [
  ['ticketSearchRoute','TicketSearch-Route'],
  ['ticketGetRouteTemplate','TicketGet-Routenvorlage'],
  ['ticketUpdateRoute','TicketUpdate-Routenvorlage (noch nicht aktiv)'],
  ['agentListRoute','Agentenliste-Route'],
  ['ticketCreateRoute','TicketCreate-Route (noch nicht aktiv)'],
  ['dynamicFieldOptionsRoute','DynamicFieldOptions-Route'],
];
const extras: Array<[TextServerKey,string]> = [
  ['ticketCreateQueue','Standard-Queue'], ['ticketCreateState','TicketCreate-Status'],
  ['ticketCreatePriority','TicketCreate-Priorität'], ['ticketCreateType','Tickettyp'],
  ['ticketCreateCustomerUser','CustomerUser'], ['ticketReplyTemplate','Antwortvorlage'],
  ['costCenterFieldName','Kostenstellenfeld'], ['orderFieldName','Auftragsfeld'],
  ['costCenterOptions','Kostenstellenoptionen'], ['orderOptions','Auftragsoptionen'],
];

export function ZnunySettingsPanel({ admin }: { admin: boolean }) {
  const [server, setServer] = useState<ZnunyServerSettings | null>(null);
  const [personal, setPersonal] = useState<ZnunyPersonalView | null>(null);
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  useEffect(() => {
    let active = true;
    void Promise.all([getZnunyServer(),getZnunyPersonal()])
      .then(([config, user]) => { if (active) {setServer(config);setPersonal(user);} })
      .catch(e => {if(active)setError(e instanceof Error ? e.message : 'Einstellungen konnten nicht geladen werden.');})
      .finally(() => {if(active)setLoading(false);});
    return () => {active=false;};
  }, []);

  function editServer<K extends keyof ZnunyServerSettings>(key: K, value: ZnunyServerSettings[K]) {
    setServer(old => old ? {...old,[key]:value} : old);
    setNotice('');
  }
  function editUser<K extends keyof ZnunyPersonalSettings>(key: K, value: ZnunyPersonalSettings[K]) {
    setPersonal(old => old ? {...old,[key]:value} : old);
    setNotice('');
  }

  async function saveUser(e: FormEvent) {
    e.preventDefault();
    if (!personal || busy) return;
    setBusy(true);setError('');setNotice('');
    try {
      const {
        passwordConfigured: _passwordConfigured,
        vaultReady: _vaultReady,
        ...values
      } = personal;
      await saveZnunyPersonal(values);
      if (password) {
        await saveZnunyPassword(password);
        setPassword('');
        setPersonal(prev => prev ? {...prev,passwordConfigured:true} : prev);
      }
      setNotice('Persönliche Znuny-Einstellungen gespeichert.');
    } catch(e) {
      setError(e instanceof Error ? e.message : 'Speichern fehlgeschlagen.');
    } finally {setBusy(false);}
  }
  async function saveServer(e: FormEvent) {
    e.preventDefault();
    if (!server || !admin || busy) return;
    setBusy(true);setError('');setNotice('');
    try {
      await saveZnunyServer(server);
      setNotice('Znuny-API-Standardeinstellungen gespeichert.');
    } catch(e) {
      setError(e instanceof Error ? e.message : 'Speichern fehlgeschlagen.');
    } finally {setBusy(false);}
  }
  async function resetServer() {
    if (!admin || busy || !window.confirm('Znuny-API auf die Classic-Standardwerte zurücksetzen?')) return;
    setBusy(true);setError('');setNotice('');
    try {
      await resetZnunyServer();
      setServer(await getZnunyServer());
      setNotice('API-Standardwerte aus Plenaro Classic wiederhergestellt.');
    } catch(e) {setError(e instanceof Error ? e.message : 'Zurücksetzen fehlgeschlagen.');}
    finally {setBusy(false);}
  }
  async function removePassword() {
    if (busy || !window.confirm('Gespeichertes persönliches Znuny-Passwort löschen?')) return;
    setBusy(true);setError('');setNotice('');
    try {
      await deleteZnunyPassword();
      setPassword('');
      setPersonal(prev => prev ? {...prev,passwordConfigured:false} : prev);
      setNotice('Znuny-Passwort entfernt.');
    } catch(e) {setError(e instanceof Error ? e.message : 'Passwort konnte nicht entfernt werden.');}
    finally {setBusy(false);}
  }
  async function check() {
    if (busy) return;
    setBusy(true);setError('');setNotice('');
    try {
      const result = await testZnunyConnection();
      const explanation = znunyConnectionMessage(result.status);
      if (result.success) setNotice(explanation);
      else setError(explanation);
    } catch(e) {setError(e instanceof Error ? e.message : 'Verbindungstest fehlgeschlagen.');}
    finally {setBusy(false);}
  }
  return <div className="znuny-settings">
    <div className="page-heading"><span className="eyebrow">ZNUNY / OTRS</span>
      <h2>GenericTicketConnectorREST</h2>
      <p>API-Grundwerte aus Plenaro Classic. Noch keine automatische Ticketsynchronisation oder Ticketbuchung.</p>
    </div>
    {error && <p role="alert" className="um-feedback failure">{error}</p>}
    {notice && <p role="status" className="um-feedback success">{notice}</p>}
    {loading && <p role="status">Lade Ticketsystem-Einstellungen …</p>}

    {personal && <section className="content-card um-panel">
      <div className="um-section-title"><PlugZap size={20}/><h3>Meine Znuny-Verbindung</h3></div>
      <p>Benutzername, Agenten-ID und persönliche Ticketfilter. Andere Plenaro-Benutzer sehen diese Einstellungen nicht.</p>
      <form className="um-form znuny-settings-form" onSubmit={e => void saveUser(e)}>
        <label htmlFor="znuny-username">Znuny-Benutzername</label>
        <input id="znuny-username" required maxLength={256} autoComplete="username"
          value={personal.username} onChange={e => editUser('username',e.target.value)}/>
        <label htmlFor="znuny-password">Znuny-Passwort</label>
        <input id="znuny-password" type="password" maxLength={4096} autoComplete="new-password"
          value={password} onChange={e => setPassword(e.target.value)}
          disabled={!personal.vaultReady} placeholder={personal.passwordConfigured ? 'Gespeichert – zum Ersetzen neu eingeben' : 'Passwort eingeben'}/>
        <p className="um-hint"><ShieldCheck size={14}/> Das Passwort wird ausschließlich verschlüsselt im Secret-Vault gespeichert und nie zurück an den Browser ausgegeben.</p>
        <label htmlFor="znuny-agent">Eigene Agenten-ID</label>
        <input id="znuny-agent" type="number" min={0} max={2147483647} required
          value={personal.agentId} onChange={e => editUser('agentId',Number(e.target.value))}/>
        <label htmlFor="znuny-candidate-id">Kandidaten-Benutzer-ID</label>
        <input id="znuny-candidate-id" type="number" min={1} max={2147483647} required
          value={personal.candidateUserId} onChange={e => editUser('candidateUserId',Number(e.target.value))}/>
        <label htmlFor="znuny-keywords">Suchbegriffe für neue Ticketkandidaten</label>
        <textarea id="znuny-keywords" maxLength={2000} rows={2} value={personal.candidateKeywords}
          onChange={e => editUser('candidateKeywords',e.target.value)}/>
        <label htmlFor="znuny-excludes">Ausschlussbegriffe</label>
        <textarea id="znuny-excludes" maxLength={2000} rows={2} value={personal.candidateExcludeKeywords}
          onChange={e => editUser('candidateExcludeKeywords',e.target.value)}/>
        {([
          ['includeOwner','Eigene Tickets (Owner) berücksichtigen'],
          ['includeResponsible','Responsible-Tickets berücksichtigen'],
          ['notifyOnNewAssignedTickets','Bei neu zugewiesenen Tickets benachrichtigen'],
          ['notifyOnTicketChanges','Bei Ticketänderungen benachrichtigen'],
          ['showAllCandidatePoolTickets','Alle Kandidaten anzeigen'],
        ] as const).map(([key, text]) =>
          <label key={key} className="um-check">
            <input type="checkbox" checked={personal[key]} onChange={e => editUser(key,e.target.checked)}/>
            {text}
          </label>)}
        <div className="znuny-settings-actions">
          <button className="button button-primary" type="submit" disabled={busy}><Save size={16}/>Meine Einstellungen speichern</button>
          <button className="button button-secondary" type="button" onClick={() => void check()}
            disabled={busy || !personal.passwordConfigured || !personal.username || !personal.vaultReady}>
            <PlugZap size={16}/>Verbindung prüfen
          </button>
          {personal.passwordConfigured && <button className="button button-secondary" type="button" disabled={busy}
            onClick={() => void removePassword()}><EyeOff size={16}/>Passwort entfernen</button>}
        </div>
        {!personal.vaultReady && <p role="alert" className="um-hint">Der Secret-Vault ist nicht eingerichtet. Passwortspeicherung und Verbindungstest stehen nicht zur Verfügung.</p>}
      </form>
    </section>}

    {server && <section className="content-card um-panel">
      <div className="um-section-title"><ShieldCheck size={20}/><h3>Znuny-Server-API</h3></div>
      <p>Serverweite Einstellungen – nur Administratoren können sie verändern. Standardmäßig sind die Platzhalteradressen aus dem Desktop-Programm hinterlegt.</p>
      <form className="um-form znuny-settings-form" onSubmit={e => void saveServer(e)}>
        <label htmlFor="znuny-api-url">API-Basisadresse</label>
        <input id="znuny-api-url" type="url" required disabled={!admin} maxLength={2048}
          value={server.apiUrl} onChange={e => editServer('apiUrl',e.target.value)}/>
        <label htmlFor="znuny-web-url">Ticket-Webadresse</label>
        <input id="znuny-web-url" type="url" required disabled={!admin} maxLength={2048}
          value={server.webUrl} onChange={e => editServer('webUrl',e.target.value)}/>
        <p className="um-hint">HTTPS ist verpflichtend. Ersetze SERVER durch den tatsächlichen Hostnamen. Der API-Pfad muss den Webservice GenericTicketConnectorREST enthalten.</p>
        <div className="znuny-settings-grid">
          <label>TicketSearch-Methode
            <select disabled={!admin} value={server.ticketSearchMethod}
              onChange={e => editServer('ticketSearchMethod',e.target.value as 'GET'|'POST')}>
              <option>GET</option><option>POST</option>
            </select>
          </label>
          <label>TicketGet-Methode
            <select disabled={!admin} value={server.ticketGetMethod}
              onChange={e => editServer('ticketGetMethod',e.target.value as 'GET'|'POST')}>
              <option>GET</option><option>POST</option>
            </select>
          </label>
          <label>TicketCreate-Methode
            <select disabled={!admin} value={server.ticketCreateMethod}
              onChange={e => editServer('ticketCreateMethod',e.target.value as 'GET'|'POST')}>
              <option>POST</option><option>GET</option>
            </select>
          </label>
          <label>Suche: Authentifizierung
            <input value={server.ticketSearchAuthMode} readOnly/>
          </label>
        </div>
        <details className="znuny-settings-advanced">
          <summary>Classic-Routen, Sync-Zeiten und weitere API-Standardwerte</summary>
          <div className="um-form znuny-settings-form">
            {routes.map(([key,title]) => <label key={key}>{title}
              <input maxLength={160} value={server[key]} disabled={!admin}
                onChange={e => editServer(key,e.target.value)}/>
            </label>)}
            {([
              ['syncIntervalMinutes','Classic-Intervall (Legacy, nicht aktiv)',5,1440],
              ['candidateSyncIntervalMinutes','Classic-Kandidatenintervall (Legacy, nicht aktiv)',3,60],
              ['searchLimit','Maximaler TicketSearch-Umfang',10,500],
              ['articleLimit','Maximale Artikel je Ticket',1,100],
            ] as const).map(([key,title,min,max]) =>
              <label key={key}>{title}
                <input type="number" min={min} max={max} disabled={!admin} value={server[key]}
                  onChange={e => editServer(key,Number(e.target.value))}/>
              </label>)}
            <label className="um-check"><input type="checkbox" disabled={!admin}
              checked={server.onlyOpenTickets} onChange={e=>editServer('onlyOpenTickets',e.target.checked)}/>
              Nur offene Tickets</label>
            <label className="um-check"><input type="checkbox" disabled={!admin}
              checked={server.showClosedTickets} onChange={e=>editServer('showClosedTickets',e.target.checked)}/>
              Geschlossene Tickets anzeigen</label>
            {extras.map(([key,title]) => <label key={key}>{title}
              <input maxLength={2000} value={server[key]} disabled={!admin}
                onChange={e => editServer(key,e.target.value)}/>
            </label>)}
          </div>
        </details>
        {admin && <div className="znuny-settings-actions">
          <button type="submit" disabled={busy} className="button button-primary"><Save size={16}/>Server-API speichern</button>
          <button type="button" disabled={busy} className="button button-secondary"
            onClick={() => void resetServer()}><RotateCcw size={16}/>Classic-Standards wiederherstellen</button>
        </div>}
      </form>
      <p className="um-hint">API-Aktionen wie TicketCreate, TicketUpdate und Zeitbuchungen sind noch nicht freigeschaltet. Keine automatische Synchronisation im Hintergrund.</p>
    </section>}
  </div>;
}
