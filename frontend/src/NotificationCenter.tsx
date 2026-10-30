import { useCallback, useEffect, useState } from 'react';
import { Bell, BellOff, Check, RefreshCw, X } from 'lucide-react';
import { Link } from 'react-router-dom';
import {
  listReminders, markReminderRead, reminderKey, shouldShowBrowserAlert,
  type TaskReminder,
} from './notificationApi';

const browserKey = (id: string) => 'plenaro-browser-notifications:' + id;
const shownKey = (id: string, reminder: TaskReminder) => 'plenaro-alert-shown:' + reminderKey(id, reminder);

function label(item: TaskReminder) {
  return item.kind === 'lead' ? 'Beginnt bald' : 'Jetzt geplant';
}

function localTime(iso: string) {
  return new Intl.DateTimeFormat('de-DE', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(iso));
}

export function NotificationCenter({ userId }: { userId: string }) {
  const [opened, setOpened] = useState(false);
  const [items, setItems] = useState<TaskReminder[]>([]);
  const [error, setError] = useState('');
  const [processing, setProcessing] = useState<string | null>(null);
  const [browserEnabled, setBrowserEnabled] = useState(() => {
    try { return window.localStorage.getItem(browserKey(userId)) === 'true'; }
    catch { return false; }
  });

  const reload = useCallback(async () => {
    try {
      const current = await listReminders();
      setItems(current);
      setError('');
      if (!browserEnabled || !('Notification' in window) || Notification.permission !== 'granted')
        return;
      for (const notice of current) {
        if (!shouldShowBrowserAlert(notice)) continue;
        const key = shownKey(userId, notice);
        try {
          if (window.localStorage.getItem(key)) continue;
          // Only record a notification as shown if the browser actually accepted it.
          new Notification('Plenaro · ' + label(notice), {
            body: notice.taskTitle + ' · ' + localTime(notice.startsAt),
            tag: key,
          });
          window.localStorage.setItem(key, 'true');
        } catch { /* OS/browser notifications are best effort; inbox remains available. */ }
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Benachrichtigungen nicht erreichbar.');
    }
  }, [userId, browserEnabled]);

  useEffect(() => {
    void reload();
    const id = window.setInterval(() => void reload(), 20000);
    const onVisible = () => { if (document.visibilityState === 'visible') void reload(); };
    document.addEventListener('visibilitychange', onVisible);
    return () => {
      window.clearInterval(id);
      document.removeEventListener('visibilitychange', onVisible);
    };
  }, [reload]);

  async function enableBrowserAlerts() {
    if (!('Notification' in window) || !window.isSecureContext) {
      setError('Browserbenachrichtigungen benötigen einen unterstützten Browser und HTTPS.');
      return;
    }
    try {
      const permission = await Notification.requestPermission();
      if (permission !== 'granted') {
        setError('Benachrichtigungsberechtigung wurde nicht erteilt.');
        return;
      }
      window.localStorage.setItem(browserKey(userId), 'true');
      setBrowserEnabled(true);
      setError('');
    } catch { setError('Browserberechtigung konnte nicht aktiviert werden.'); }
  }

  function disableBrowserAlerts() {
    try { window.localStorage.setItem(browserKey(userId), 'false'); } catch { /* storage optional */ }
    setBrowserEnabled(false);
  }

  async function acknowledge(item: TaskReminder) {
    const key = reminderKey(userId, item);
    if (processing) return;
    setProcessing(key);
    try {
      await markReminderRead(item);
      setItems(previous => previous.filter(x => reminderKey(userId, x) !== key));
      setError('');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Quittierung fehlgeschlagen.');
    } finally { setProcessing(null); }
  }

  return <div className="plenaro-notification-center" aria-label="Benachrichtigungszentrale">
    <button type="button" className="icon-button plenaro-notification-toggle"
      title="Benachrichtigungen" aria-label={`Benachrichtigungen öffnen, ${items.length} ungelesen`}
      aria-expanded={opened} onClick={() => setOpened(x => !x)}>
      <Bell size={19}/>
      {items.length > 0 && <span className="plenaro-notification-count">{items.length > 50 ? '50+' : items.length}</span>}
    </button>
    {opened && <section className="plenaro-notification-panel" aria-label="Benachrichtigungen">
      <div className="plenaro-notification-heading">
        <strong>Benachrichtigungen</strong>
        <div className="plenaro-notification-actions">
          <button className="icon-button" type="button" onClick={() => void reload()} aria-label="Benachrichtigungen aktualisieren"><RefreshCw size={16}/></button>
          <button className="icon-button" type="button" onClick={() => setOpened(false)} aria-label="Benachrichtigungen schließen"><X size={16}/></button>
        </div>
      </div>
      {error && <p className="um-feedback failure" role="alert">{error}</p>}
      <p className="um-hint">Erinnerungen erscheinen hier, während Plenaro im Browser geöffnet ist.</p>
      {items.length === 0 ? <p className="plenaro-notification-empty">Keine ungelesenen Erinnerungen.</p> :
        <div className="plenaro-notification-list">
          {items.map(item => <article key={reminderKey(userId, item)} className="plenaro-notification-item">
            <span className="eyebrow">{label(item)}</span>
            <strong>{item.taskTitle}</strong>
            <small>{localTime(item.startsAt)}</small>
            <div className="plenaro-notification-actions">
              <Link to="/today" className="classic-text-action" onClick={() => setOpened(false)}>Aufgaben öffnen</Link>
              <button type="button" className="button button-secondary" disabled={!!processing}
                onClick={() => void acknowledge(item)}><Check size={14}/>Gelesen</button>
            </div>
          </article>)}
        </div>}
      <div className="plenaro-notification-bottom">
        {browserEnabled && 'Notification' in window && Notification.permission === 'granted' ?
          <button className="button button-secondary" type="button" onClick={disableBrowserAlerts}><BellOff size={15}/>Browserhinweise deaktivieren</button> :
          <button className="button button-secondary" type="button" onClick={() => void enableBrowserAlerts()}><Bell size={15}/>Browserhinweise aktivieren</button>}
        <Link to="/settings" className="classic-text-action" onClick={() => setOpened(false)}>Vorlauf einstellen</Link>
      </div>
    </section>}
  </div>;
}
