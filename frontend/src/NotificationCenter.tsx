import { useCallback, useEffect, useRef, useState } from 'react';
import { Bell, BellOff, Check, RefreshCw, X } from 'lucide-react';
import { Link, useNavigate } from 'react-router-dom';
import { createPortal } from 'react-dom';
import {
  listReminders, markReminderRead, reminderKey, shouldShowBrowserAlert,
  type TaskReminder,
} from './notificationApi';
import { getUnreadTickets, ticketReadEvent, type CachedTicket } from './znunySyncApi';

const browserKey = (id: string) => 'plenaro-browser-notifications:' + id;
const promptKey = (id: string) => 'plenaro-notification-prompt:' + id;
const shownKey = (id: string, reminder: TaskReminder) => 'plenaro-alert-shown:' + reminderKey(id, reminder);
const articleKey = (id:string,ticket:string,article:string) =>
  ['plenaro-ticket-alert-shown',id,ticket,article].join(':');
const ticketPath = (ticketId:string) => '/workspace/ticket/'+encodeURIComponent(ticketId);

function label(item: TaskReminder) {
  return item.kind === 'lead' ? 'Beginnt bald' : 'Jetzt geplant';
}
function localTime(iso: string) {
  return new Intl.DateTimeFormat('de-DE', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(iso));
}
function browserSupported() {
  return 'Notification' in window && window.isSecureContext;
}
function hasPermission() {
  return browserSupported() && Notification.permission === 'granted';
}
function alreadyAsked(id:string) {
  try { return window.sessionStorage.getItem(promptKey(id))==='true'; }
  catch { return false; }
}
function rememberPrompt(id:string) {
  try { window.sessionStorage.setItem(promptKey(id),'true'); } catch { /* optional */ }
}

export function NotificationCenter({ userId }: { userId: string }) {
  const navigate=useNavigate();
  const [opened, setOpened] = useState(false);
  const [items, setItems] = useState<TaskReminder[]>([]);
  const [tickets,setTickets]=useState<CachedTicket[]>([]);
  const [error, setError] = useState('');
  const [ticketError,setTicketError]=useState('');
  const [permissionError,setPermissionError]=useState('');
  const [processing, setProcessing] = useState<string | null>(null);
  const [browserEnabled, setBrowserEnabled] = useState(() => {
    try { return window.localStorage.getItem(browserKey(userId)) === 'true'; }
    catch { return false; }
  });
  const [permission,setPermission]=useState(()=>hasPermission());
  const [prompt,setPrompt]=useState(()=>browserSupported() && !alreadyAsked(userId) &&
    !(hasPermission() && browserEnabled));
  const [enabling,setEnabling]=useState(false);
  const audio=useRef<AudioContext|null>(null);
  const shown=useRef(new Set<string>());
  const reminderGeneration=useRef(0);
  const ticketGeneration=useRef(0);
  const ticketBusy=useRef(false);
  const ticketQueued=useRef(false);
  const mounted=useRef(true);
  const browserActive=browserEnabled && permission;
  const count=items.length+tickets.length;

  // Audio is unlocked by a real click/key press. Browser notification sounds
  // remain the fallback when the audio context cannot be started.
  const unlockAudio=useCallback(()=>{
    if(!('AudioContext' in window))return;
    try {
      if(!audio.current)audio.current=new AudioContext();
      if(audio.current.state==='suspended')void audio.current.resume().catch(()=>{});
    }catch{ /* audio is optional */ }
  },[]);
  useEffect(()=>{
    mounted.current=true;
    return()=>{
      mounted.current=false;
      const ctx=audio.current;audio.current=null;
      if(ctx)void ctx.close().catch(()=>{});
    };
  },[]);
  useEffect(()=>{
    if(!browserActive)return;
    const unlock=()=>unlockAudio();
    window.addEventListener('pointerdown',unlock);
    window.addEventListener('keydown',unlock);
    return()=>{
      window.removeEventListener('pointerdown',unlock);
      window.removeEventListener('keydown',unlock);
    };
  },[browserActive,unlockAudio]);
  function seen(key:string) {
    if(shown.current.has(key))return true;
    try { return !!window.localStorage.getItem(key); } catch { return false; }
  }
  function markShown(key:string) {
    shown.current.add(key);
    try { window.localStorage.setItem(key,'true'); } catch { /* memory fallback */ }
  }
  const alertBrowser=useCallback((title:string,body:string,tag:string,path:string)=>{
    if(!browserEnabled || !hasPermission())return false;
    try {
      const ctx=audio.current;
      const soundReady=ctx?.state==='running';
      const notice=new Notification(title,{body,tag,...(soundReady?{silent:true}:{})});
      notice.onclick=()=>{
        window.focus();navigate(path);notice.close();
      };
      if(soundReady && ctx){
        try {
          const oscillator=ctx.createOscillator(),gain=ctx.createGain();
          const start=ctx.currentTime;
          oscillator.type='sine';oscillator.frequency.setValueAtTime(660,start);
          oscillator.frequency.setValueAtTime(880,start+0.12);
          gain.gain.setValueAtTime(0.06,start);
          gain.gain.exponentialRampToValueAtTime(0.001,start+0.25);
          oscillator.connect(gain);gain.connect(ctx.destination);
          oscillator.start(start);oscillator.stop(start+0.26);
          oscillator.onended=()=>{oscillator.disconnect();gain.disconnect();};
        }catch{ /* OS/browser controls may prevent audio */ }
      }
      return true;
    } catch { return false; }
  },[browserEnabled,navigate]);

  const reloadReminders=useCallback(async ()=>{
    const generation=++reminderGeneration.current;
    try{
      const current=await listReminders();
      if(!mounted.current||generation!==reminderGeneration.current)return;
      setItems(current);setError('');
      for(const notice of current){
        if(!shouldShowBrowserAlert(notice))continue;
        const key=shownKey(userId,notice);
        if(seen(key))continue;
        if(alertBrowser('Plenaro · '+label(notice),
          notice.taskTitle+' · '+localTime(notice.startsAt),key,
          '/workspace?task='+encodeURIComponent(notice.taskId)))markShown(key);
      }
    }catch(e){
      if(mounted.current&&generation===reminderGeneration.current)
        setError(e instanceof Error?e.message:'Benachrichtigungen nicht erreichbar.');
    }
  },[userId,alertBrowser]);
  const reloadTickets=useCallback(async ()=>{
    if(ticketBusy.current){ticketQueued.current=true;return;}
    ticketBusy.current=true;
    const generation=++ticketGeneration.current;
    try {
      do {
        ticketQueued.current=false;
        try{
          const current=(await getUnreadTickets()).filter(t=>!!t.unreadArticleCount);
          if(!mounted.current||generation!==ticketGeneration.current)return;
          // If a read happened during the request, fetch the confirmed state
          // instead of flashing a stale badge or replaying a read article.
          if(ticketQueued.current)continue;
          setTickets(current);setTicketError('');
          for(const ticket of current){
            const keys=(ticket.unreadArticleIds??[]).map(id=>articleKey(userId,ticket.ticketId,id))
              .filter(key=>!seen(key));
            if(!keys.length)continue;
            // Do not put potentially confidential ticket titles into OS banners.
            if(alertBrowser('Plenaro · Neue Ticket-Nachricht',
              'Ticket #'+(ticket.ticketNumber||ticket.ticketId)+' · '+keys.length+' neu',
              'plenaro-ticket:'+userId+':'+ticket.ticketId,ticketPath(ticket.ticketId)))
              keys.forEach(markShown);
          }
        }catch(e){
          if(mounted.current&&generation===ticketGeneration.current)
            setTicketError(e instanceof Error?e.message:'Ticket-Benachrichtigungen nicht erreichbar.');
        }
      }while(ticketQueued.current&&mounted.current);
    }finally{ticketBusy.current=false;}
  },[userId,alertBrowser]);
  const reload=useCallback(()=>{
    void reloadReminders();void reloadTickets();
  },[reloadReminders,reloadTickets]);
  useEffect(()=>{
    reload();
    const id=window.setInterval(reload,20000);
    const onVisible=()=>{
      setPermission(hasPermission());
      if(document.visibilityState==='visible')reload();
    };
    document.addEventListener('visibilitychange',onVisible);
    window.addEventListener('focus',onVisible);
    window.addEventListener(ticketReadEvent,reloadTickets);
    return()=>{
      window.clearInterval(id);
      document.removeEventListener('visibilitychange',onVisible);
      window.removeEventListener('focus',onVisible);
      window.removeEventListener(ticketReadEvent,reloadTickets);
      ++reminderGeneration.current;
    };
  },[reload,reloadTickets]);

  async function enableBrowserAlerts() {
    if(!browserSupported()){
      setPermissionError('Browserbenachrichtigungen benötigen einen unterstützten Browser und HTTPS.');
      return;
    }
    if(enabling)return;
    rememberPrompt(userId);
    unlockAudio();
    setEnabling(true);
    try{
      const result=Notification.permission==='granted'?'granted':await Notification.requestPermission();
      setPermission(result==='granted');
      if(result!=='granted'){
        setPermissionError(result==='denied'
          ?'Benachrichtigungen sind im Browser blockiert. Bitte in den Website-Berechtigungen erlauben.'
          :'Benachrichtigungsberechtigung wurde nicht erteilt.');
        setOpened(true);return;
      }
      try{window.localStorage.setItem(browserKey(userId),'true');}catch{ /* optional */ }
      setBrowserEnabled(true);setPermissionError('');
    }catch{
      setPermissionError('Browserberechtigung konnte nicht aktiviert werden.');setOpened(true);
    }finally{setEnabling(false);setPrompt(false);}
  }
  function disableBrowserAlerts(){
    try{window.localStorage.setItem(browserKey(userId),'false');}catch{ /* optional */ }
    rememberPrompt(userId);
    setBrowserEnabled(false);setPrompt(false);setPermissionError('');
  }
  async function acknowledge(item:TaskReminder){
    const key=reminderKey(userId,item);
    if(processing)return;
    setProcessing(key);
    try{
      await markReminderRead(item);
      ++reminderGeneration.current;
      setItems(previous=>previous.filter(x=>reminderKey(userId,x)!==key));setError('');
    }catch(e){setError(e instanceof Error?e.message:'Quittierung fehlgeschlagen.');}
    finally{setProcessing(null);}
  }

  return <div className="plenaro-notification-center" aria-label="Benachrichtigungszentrale">
    <button type="button" className="icon-button plenaro-notification-toggle"
      title="Benachrichtigungen" aria-label={`Benachrichtigungen öffnen, ${count} ungelesen`}
      aria-expanded={opened} onClick={()=>{if(!opened)reload();setOpened(x=>!x);}}>
      <Bell size={19}/>
      {count>0&&<span className="plenaro-notification-count">{count}</span>}
    </button>
    {prompt&&createPortal(<div className="plenaro-notification-prompt-overlay">
      <section role="dialog" aria-modal="true" aria-labelledby="plenaro-notification-prompt-title"
        className="plenaro-notification-prompt content-card">
        <Bell size={24}/>
        <h2 id="plenaro-notification-prompt-title">Benachrichtigungen aktivieren?</h2>
        <p>Möchtest du bei neuen Ticket-Nachrichten und geplanten Aufgaben einen Browserhinweis mit Ton erhalten?</p>
        <p className="um-hint">Die Glocke bleibt auch ohne Browserfreigabe aktiv.</p>
        <div className="plenaro-notification-actions">
          <button type="button" className="button button-secondary" disabled={enabling}
            onClick={disableBrowserAlerts} autoFocus>Nein</button>
          <button type="button" className="button" disabled={enabling}
            onClick={()=>void enableBrowserAlerts()}>Ja</button>
        </div>
      </section>
    </div>,document.body)}
    {opened&&<section className="plenaro-notification-panel" aria-label="Benachrichtigungen">
      <div className="plenaro-notification-heading">
        <strong>Benachrichtigungen</strong>
        <div className="plenaro-notification-actions">
          <button className="icon-button" type="button" onClick={reload} aria-label="Benachrichtigungen aktualisieren"><RefreshCw size={16}/></button>
          <button className="icon-button" type="button" onClick={()=>setOpened(false)} aria-label="Benachrichtigungen schließen"><X size={16}/></button>
        </div>
      </div>
      {error&&<p className="um-feedback failure" role="alert">{error}</p>}
      {ticketError&&<p className="um-feedback failure" role="alert">{ticketError}</p>}
      {permissionError&&<p className="um-feedback failure" role="alert">{permissionError}</p>}
      <p className="um-hint">Neue Ticket-Nachrichten und Erinnerungen, während Plenaro geöffnet ist.</p>
      {count===0?<p className="plenaro-notification-empty">Keine ungelesenen Benachrichtigungen.</p>:
        <div className="plenaro-notification-list">
          {tickets.map(ticket=><Link key={ticket.ticketId} to={ticketPath(ticket.ticketId)}
            className="plenaro-notification-item plenaro-ticket-notification"
            onClick={()=>setOpened(false)}>
            <span className="eyebrow">Neue Nachricht im Ticket #{ticket.ticketNumber||ticket.ticketId}</span>
            <strong>{ticket.title||'Ticket '+ticket.ticketId}</strong>
            <small>{ticket.unreadArticleCount} ungelesen · Ticket öffnen</small>
          </Link>)}
          {items.map(item=><article key={reminderKey(userId,item)} className="plenaro-notification-item">
            <span className="eyebrow">{label(item)}</span>
            <strong>{item.taskTitle}</strong>
            <small>{localTime(item.startsAt)}</small>
            <div className="plenaro-notification-actions">
              <Link to={'/workspace?task='+encodeURIComponent(item.taskId)} className="classic-text-action" onClick={()=>setOpened(false)}>Aufgabe öffnen</Link>
              <button type="button" className="button button-secondary" disabled={!!processing}
                onClick={()=>void acknowledge(item)}><Check size={14}/>Gelesen</button>
            </div>
          </article>)}
        </div>}
      <div className="plenaro-notification-bottom">
        {browserActive?
          <button className="button button-secondary" type="button" onClick={disableBrowserAlerts}><BellOff size={15}/>Browserhinweise deaktivieren</button>:
          <button className="button button-secondary" type="button" disabled={enabling} onClick={()=>void enableBrowserAlerts()}><Bell size={15}/>Browserhinweise aktivieren</button>}
        <Link to="/settings" className="classic-text-action" onClick={()=>setOpened(false)}>Vorlauf einstellen</Link>
      </div>
    </section>}
  </div>;
}
