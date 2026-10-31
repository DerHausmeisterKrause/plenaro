import { useEffect, useState, type FormEvent } from 'react';
import { Database, RefreshCw, ShieldCheck, Trash2 } from 'lucide-react';
import {
  deleteSyncPassword, getSyncStatus,
  resetCentralCache, runCentralSync, saveSyncConfig, saveSyncPassword,
  type SyncConfig, type SyncStatus,
} from './znunySyncApi';

export function ZnunyCentralAdminPanel() {
  const [status,setStatus]=useState<SyncStatus|null>(null);
  const [settings,setSettings]=useState<SyncConfig|null>(null);
  const [password,setPassword]=useState('');
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('');

  async function reload(){
    const next=await getSyncStatus();
    setStatus(next);
    setSettings({
      enabled:next.enabled,serviceUsername:next.serviceUsername,
      initialLookbackDays:next.initialLookbackDays,
      deltaIntervalMinutes:next.deltaIntervalMinutes,
      openIntervalMinutes:next.openIntervalMinutes,
      maxRequestsPerCycle:next.maxRequestsPerCycle
    });
  }
  useEffect(()=>{
    let alive=true;
    void getSyncStatus()
      .then(next=>{
        if(!alive)return;
        setStatus(next);
        setSettings({enabled:next.enabled,serviceUsername:next.serviceUsername,
          initialLookbackDays:next.initialLookbackDays,
          deltaIntervalMinutes:next.deltaIntervalMinutes,
          openIntervalMinutes:next.openIntervalMinutes,
          maxRequestsPerCycle:next.maxRequestsPerCycle});
      })
      .catch(e=>{if(alive)setError(e instanceof Error?e.message:'Sync-Status nicht verfügbar.');});
    return ()=>{alive=false;};
  },[]);

  async function action(fn:()=>Promise<void>){
    if(busy)return;
    setBusy(true);setError('');setNotice('');
    try{await fn();await reload();}
    catch(e){setError(e instanceof Error?e.message:'Aktion fehlgeschlagen.');}
    finally{setBusy(false);}
  }
  function edit<K extends keyof SyncConfig>(key:K,value:SyncConfig[K]){
    setSettings(prev=>prev?{...prev,[key]:value}:prev);
  }
  function save(e:FormEvent){
    e.preventDefault();
    if(!settings)return;
    void action(async()=>{
      await saveSyncConfig(settings);
      if(password){await saveSyncPassword(password);setPassword('');}
      setNotice('Zentrale Synchronisation gespeichert.');
    });
  }
  function run(){
    void action(async()=>{
      const result=await runCentralSync();
      setNotice(result.started
        ? `Sync-Lauf: ${result.status}; ${result.tickets} Tickets verarbeitet; ${result.requests} API-Anfragen.`
        : `Sync nicht gestartet: ${result.status}`);
    });
  }
  function clear(){
    if(status?.enabled||!window.confirm('Wirklich alle lokal zwischengespeicherten Znuny-Tickets löschen? Der nächste Erstimport beginnt erneut.'))return;
    void action(async()=>{await resetCentralCache();setNotice('Ticketcache und Sync-Fortschritt zurückgesetzt.');});
  }

  return <section className="content-card um-panel znuny-central-admin" aria-label="Zentrale Znuny-Synchronisation">
    <div className="um-section-title"><Database size={20}/><h3>Zentrale Znuny-Synchronisation</h3></div>
    <p>Eine gemeinsame Ticketdatenbank für alle Benutzer. Plenaro holt die Daten nur
      einmal vom Ticketsystem; Benutzer lesen ausschließlich ihre vom Administrator
      freigegebenen Agenten-Zuordnungen aus dem Cache.</p>
    {error&&<p role="alert" className="um-feedback failure">{error}</p>}
    {notice&&<p role="status" className="um-feedback success">{notice}</p>}
    {!settings&&!error&&<p>Synchronisationsstatus wird geladen …</p>}
    {status&&<div className="znuny-central-status">
      <span>Tickets im Cache <strong>{status.cachedTickets}</strong></span>
      <span>Erstimport <strong>{status.bootstrapFinishedAt?'Abgeschlossen':status.bootstrapCursor?'Läuft / fortsetzbar':'Nicht begonnen'}</strong></span>
      <span>Letzter Delta-Lauf <strong>{status.lastDeltaAt?new Date(status.lastDeltaAt).toLocaleString('de-DE'):'Noch keiner'}</strong></span>
      <span>Alte offene Tickets <strong>{status.openCursor?'Historischer Scan läuft':status.lastOpenAt?'Zuletzt '+new Date(status.lastOpenAt).toLocaleString('de-DE'):'Noch nicht geprüft'}</strong></span>
      {status.lastError&&<p role="alert">Letzter Sync-Fehlercode: {status.lastError}</p>}
      {status.sourceApiUrl&&<p className="um-hint">Cache gehört zu: {status.sourceApiUrl}</p>}
    </div>}
    {settings&&<form className="um-form znuny-settings-form" onSubmit={save}>
      <label>Technischer Znuny-Benutzer (global)
        <input required maxLength={256} value={settings.serviceUsername}
          onChange={e=>edit('serviceUsername',e.target.value)}/>
      </label>
      <label>Service-Passwort (nur zum Ersetzen eingeben)
        <input type="password" autoComplete="new-password" maxLength={4096}
          placeholder={status?.passwordConfigured?'Verschlüsselt gespeichert':'Noch kein Passwort'}
          value={password} onChange={e=>setPassword(e.target.value)}/>
      </label>
      <p className="um-hint">Das technische Konto benötigt im Znuny-Webservice ausreichend Leserechte
        für alle gewünschten Tickets. Passwort wird global verschlüsselt abgelegt.</p>
      <div className="znuny-settings-grid">
        {([
          ['initialLookbackDays','Erstimport rückwirkend (Tage)',7,3650],
          ['deltaIntervalMinutes','Änderungen prüfen (Minuten)',3,1440],
          ['openIntervalMinutes','Alte offene Tickets prüfen (Minuten)',15,10080],
          ['maxRequestsPerCycle','Maximale API-Anfragen je Durchlauf',20,1000],
        ] as const).map(([key,label,min,max])=>
          <label key={key}>{label}
            <input type="number" min={min} max={max} required value={settings[key]}
              onChange={e=>edit(key,Number(e.target.value))}/>
          </label>)}
      </div>
      <label className="um-check">
        <input type="checkbox" checked={settings.enabled}
          onChange={e=>edit('enabled',e.target.checked)}/>
        Zentrale Synchronisation aktivieren
      </label>
      <p className="um-hint">Der Erstimport arbeitet in Tagesabschnitten mit gespeichertem Fortschritt.
        Bei Fehlern wird der letzte Abschnitt erneut verarbeitet; bereits bekannte Ticket-IDs
        werden aktualisiert statt verdoppelt. Ältere offene Tickets werden zusätzlich in
        einem separaten historischen Suchlauf erfasst.</p>
      <div className="znuny-settings-actions">
        <button className="button button-primary" type="submit" disabled={busy}>
          <ShieldCheck size={16}/>Einstellungen speichern</button>
        <button className="button button-secondary" type="button" disabled={busy||!status?.enabled}
          onClick={run}><RefreshCw size={16}/>Synchronisation jetzt ausführen</button>
        <button className="button button-secondary" type="button" disabled={busy||status?.enabled}
          onClick={clear}><Trash2 size={16}/>Cache zurücksetzen</button>
        <button className="button button-secondary" type="button" disabled={busy||!status?.passwordConfigured}
          onClick={()=>void action(async()=>{await deleteSyncPassword();setNotice('Service-Passwort gelöscht.');})}>
          Service-Passwort löschen</button>
      </div>
    </form>}
    <p className="um-hint">Znuny-Agenten-IDs legst du unter Einstellungen → Administration → Benutzerverwaltung
      direkt beim Anlegen oder Bearbeiten eines Benutzers fest.</p>
  </section>;
}
