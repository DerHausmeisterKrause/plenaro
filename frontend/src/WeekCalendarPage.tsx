import { useEffect, useMemo, useState } from 'react';
import { ArrowUpRight, CalendarDays, ChevronLeft, ChevronRight, Link2, RefreshCw, Unplug } from 'lucide-react';
import { Link, useSearchParams } from 'react-router-dom';
import {
  disconnectOutlook, graphStatus, readOutlookWeek,
  type GraphStatus, type OutlookWeek, type OutlookEvent,
} from './graphApi';

function mondayOf(value: Date) {
  const d = new Date(value.getFullYear(), value.getMonth(), value.getDate());
  d.setDate(d.getDate() - ((d.getDay() + 6) % 7));
  return d;
}

function plusDays(value: Date, days: number) {
  const next = new Date(value);
  next.setDate(next.getDate() + days);
  return next;
}

function shortDate(value: Date) {
  return new Intl.DateTimeFormat('de-DE', { day: 'numeric', month: 'short' }).format(value);
}

function eventTime(value: string, allDay: boolean) {
  return allDay ? 'Ganztägig' : new Intl.DateTimeFormat('de-DE', {
    hour: '2-digit', minute: '2-digit',
  }).format(new Date(value));
}

export function WeekCalendarPage() {
  const [query] = useSearchParams();
  const [weekStart, setWeekStart] = useState(() => mondayOf(new Date()));
  const [status, setStatus] = useState<GraphStatus | null>(null);
  const [data, setData] = useState<OutlookWeek | null>(null);
  const [loading, setLoading] = useState(true);
  const [eventsLoading, setEventsLoading] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [reload, setReload] = useState(0);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let active = true;
    void graphStatus().then(value => { if (active) { setStatus(value); setLoading(false); } })
      .catch(reason => { if (active) {
        setLoading(false);
        setError(reason instanceof Error ? reason.message : 'Verbindungsstatus fehlgeschlagen.');
      } });
    return () => { active = false; };
  }, [reload]);

  useEffect(() => {
    const result = query.get('graph');
    if (result === 'connected') setNotice('Outlook-Kalender erfolgreich verbunden.');
    if (result === 'failed') setError('Microsoft-Freigabe fehlgeschlagen oder abgebrochen.');
  }, [query]);

  useEffect(() => {
    if (!status?.connected) { setData(null); return; }
    let active = true;
    setEventsLoading(true); setError('');
    void readOutlookWeek(weekStart, plusDays(weekStart, 7))
      .then(result => { if (active) setData(result); })
      .catch(reason => { if (active) { setData(null); setError(reason instanceof Error ? reason.message : 'Laden fehlgeschlagen.'); } })
      .finally(() => { if (active) setEventsLoading(false); });
    return () => { active = false; };
  }, [status?.connected, weekStart, reload]);

  const days = useMemo(() => Array.from({ length: 7 }, (_, i) => plusDays(weekStart, i)), [weekStart]);
  const grouped = useMemo(() => days.map(day => {
    const key = day.toDateString();
    return {
      day, key,
      events: (data?.events ?? []).filter(event =>
        event.start && new Date(event.start).toDateString() === key),
    };
  }), [days, data]);

  async function disconnect() {
    if (busy) return;
    setBusy(true); setError('');
    try { await disconnectOutlook(); setNotice('Outlook-Kalender getrennt.'); setReload(n => n + 1); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Trennung fehlgeschlagen.'); }
    finally { setBusy(false); }
  }

  return <div className="page">
    <div className="page-heading">
      <span className="eyebrow">PLENARO / WOCHE / MICROSOFT GRAPH</span>
      <h1>Meine Woche</h1>
      <p>Deine Outlook-Termine – sicher über Microsoft Graph synchronisiert.</p>
    </div>
    {notice && <p role="status" className="um-feedback success">{notice}</p>}
    {error && <p role="alert" className="um-feedback failure">{error}</p>}

    {loading ? <section className="content-card"><p role="status">Prüfe Outlook-Verbindung …</p></section>
      : !status?.enabled ? <section className="content-card calendar-onboarding">
        <CalendarDays size={30}/>
        <h2>Microsoft Graph ist noch nicht eingerichtet</h2>
        <p>Die Wochenplanung ist vorbereitet. Sobald der Administrator Entra ID aktiviert, kannst du hier deinen Outlook-Kalender verbinden.</p>
        <p className="um-hint">Der lokale Plenaro-Login und alle anderen Module bleiben unverändert.</p>
      </section>
      : !status.linked ? <section className="content-card calendar-onboarding">
        <Link2 size={30}/>
        <h2>Zuerst Microsoft-Konto verknüpfen</h2>
        <p>Verbinde unter Einstellungen deine bestehende lokale Plenaro-Identität mit Microsoft Entra ID.</p>
        <Link className="button button-primary" to="/settings">Zu den Einstellungen</Link>
      </section>
      : !status.vaultReady ? <section className="content-card calendar-onboarding">
        <CalendarDays size={30}/>
        <h2>Verschlüsselter Speicher fehlt</h2>
        <p>Für Outlook muss der Administrator den Secret-Vault aktivieren. Microsoft-Tokens werden nicht unverschlüsselt gespeichert.</p>
      </section>
      : !status.connected ? <section className="content-card calendar-onboarding">
        <CalendarDays size={30}/>
        <h2>Outlook-Kalender verbinden</h2>
        <p>Erteile Plenaro einmalig die Berechtigung <strong>Calendars.Read</strong> für deinen eigenen Kalender. Du kannst die Verbindung jederzeit wieder trennen.</p>
        <a className="button button-primary" href="/api/v1/graph/connect/start"><Link2 size={16}/>Outlook verbinden</a>
        <p className="um-hint">Nur Leserechte – Plenaro kann damit keine Termine erstellen oder ändern.</p>
      </section>
      : <>
        <div className="calendar-toolbar">
          <div>
            <strong>{shortDate(weekStart)} – {shortDate(plusDays(weekStart, 6))}</strong>
            <p className="um-hint">Outlook-Kalender · nur lesen</p>
          </div>
          <div className="calendar-actions">
            <button className="button button-secondary" type="button" aria-label="Vorherige Woche" onClick={() => setWeekStart(d => plusDays(d, -7))}><ChevronLeft size={18}/></button>
            <button className="button button-secondary" type="button" onClick={() => setWeekStart(mondayOf(new Date()))}>Diese Woche</button>
            <button className="button button-secondary" type="button" aria-label="Nächste Woche" onClick={() => setWeekStart(d => plusDays(d, 7))}><ChevronRight size={18}/></button>
            <button className="button button-secondary" type="button" aria-label="Outlook-Termine neu laden" onClick={() => setReload(n => n + 1)}><RefreshCw size={17}/></button>
            <button className="button button-secondary" type="button" disabled={busy} onClick={() => void disconnect()}><Unplug size={16}/>Trennen</button>
          </div>
        </div>
        {eventsLoading ? <section className="content-card"><p role="status">Lade Outlook-Termine …</p></section> :
          <div className="calendar-week">
            {grouped.map(({ day, key, events }) => <section key={key} className="content-card calendar-day">
              <div className="calendar-day-head">
                <strong>{new Intl.DateTimeFormat('de-DE', { weekday: 'long' }).format(day)}</strong>
                <span>{shortDate(day)}</span>
              </div>
              {events.length === 0 ? <p className="calendar-empty">Keine Termine</p> :
                events.map((event: OutlookEvent) => <article className="calendar-event" key={event.id}>
                  <span className="calendar-time">{eventTime(event.start, event.isAllDay)}{!event.isAllDay && event.end ? '–' + eventTime(event.end, false) : ''}</span>
                  <strong>{event.subject || '(Ohne Betreff)'}</strong>
                  {event.location && <small>{event.location}</small>}
                  <span className="um-hint">{event.showAs === 'free' ? 'Frei' : 'Belegt'}</span>
                  {event.webLink?.startsWith('https://outlook.') && <a href={event.webLink} target="_blank" rel="noreferrer" className="calendar-open">In Outlook öffnen <ArrowUpRight size={13}/></a>}
                </article>)}
            </section>)}
          </div>}
        {data?.hasMore && <p className="um-feedback failure">Die Woche enthält über 100 Termine. Weitere Termine werden in dieser ersten Version noch nicht geladen.</p>}
      </>}
  </div>;
}
