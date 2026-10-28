import { useEffect, useMemo, useState } from 'react';
import { CalendarDays, ChevronLeft, ChevronRight, Cloud, Link2, RefreshCw, Unplug } from 'lucide-react';
import { Link, useSearchParams } from 'react-router-dom';
import { DayMarkersPanel } from './DayMarkersPanel';
import { getMonthMarkers, type DayMarker } from './dayMarkersApi';
import { graphStatus, readOutlookWeek, disconnectOutlook, type GraphStatus, type OutlookEvent } from './graphApi';
import { listTasks, localDate, type WorkTask } from './tasksApi';

function startOfWeek(value: Date) {
  const date = new Date(value.getFullYear(), value.getMonth(), value.getDate());
  date.setDate(date.getDate() - ((date.getDay() + 6) % 7));
  return date;
}
function plusDays(value: Date, days: number) {
  const date = new Date(value);
  date.setDate(date.getDate() + days);
  return date;
}
function fmtDay(value: Date) {
  return new Intl.DateTimeFormat('de-DE', { weekday: 'short', day: '2-digit', month: '2-digit' }).format(value);
}
function formatClock(value: string) {
  return new Intl.DateTimeFormat('de-DE', { hour: '2-digit', minute: '2-digit' }).format(new Date(value));
}
function hourOf(value: string) { return new Date(value).getHours(); }

export function WeekCalendarPage() {
  const [params] = useSearchParams();
  const [weekStart, setWeekStart] = useState(() => startOfWeek(new Date()));
  const [tasks, setTasks] = useState<WorkTask[]>([]);
  const [markers, setMarkers] = useState<DayMarker[]>([]);
  const [outlook, setOutlook] = useState<OutlookEvent[]>([]);
  const [hasMore, setHasMore] = useState(false);
  const [graph, setGraph] = useState<GraphStatus | null>(null);
  const [graphError, setGraphError] = useState('');
  const [localError, setLocalError] = useState('');
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [refresh, setRefresh] = useState(0);
  const [showMarkers, setShowMarkers] = useState(false);
  const [selectedDay, setSelectedDay] = useState(() => localDate());
  const days = useMemo(() => Array.from({ length: 7 }, (_, i) => plusDays(weekStart, i)), [weekStart]);
  const until = useMemo(() => plusDays(weekStart, 7), [weekStart]);

  useEffect(() => {
    setSelectedDay(previous => days.some(d => localDate(d) === previous) ? previous : localDate(days[0]));
  }, [days]);

  // Local tasks and day markers are independent of Entra and are always loaded.
  useEffect(() => {
    let active = true;
    setLoading(true);
    setLocalError('');
    const first = days[0];
    const last = days[6];
    const ym = (d: Date) => [d.getFullYear(), d.getMonth() + 1] as const;
    const months = [ym(first)];
    if (first.getMonth() !== last.getMonth() || first.getFullYear() !== last.getFullYear()) months.push(ym(last));
    void Promise.all([
      listTasks(),
      ...months.map(([year, month]) => getMonthMarkers(year, month)),
    ]).then(([taskList, ...monthlyMarkers]) => {
      if (!active) return;
      setTasks(taskList as WorkTask[]);
      setMarkers((monthlyMarkers as DayMarker[][]).flat());
      setLoading(false);
    }).catch(e => {
      if (active) { setLocalError(e instanceof Error ? e.message : 'Lokaler Kalender nicht erreichbar.'); setLoading(false); }
    });
    return () => { active = false; };
  }, [days, refresh]);

  // Microsoft is only an additional source. Failures must not hide the local calendar.
  useEffect(() => {
    let active = true;
    void graphStatus().then(s => { if (active) setGraph(s); })
      .catch(() => { if (active) setGraph(null); });
    return () => { active = false; };
  }, [refresh]);

  useEffect(() => {
    if (!graph?.connected) { setOutlook([]); setHasMore(false); return; }
    let active = true;
    void readOutlookWeek(weekStart, until).then(data => {
      if (active) { setOutlook(data.events); setHasMore(data.hasMore); setGraphError(''); }
    }).catch(e => { if (active) { setOutlook([]); setGraphError(e instanceof Error ? e.message : 'Outlook nicht erreichbar.'); } });
    return () => { active = false; };
  }, [graph?.connected, weekStart, until, refresh]);

  const graphResult = params.get('graph');
  useEffect(() => {
    if (graphResult === 'connected') setRefresh(n => n + 1);
    if (graphResult === 'failed') setGraphError('Outlook-Verbindung fehlgeschlagen oder abgebrochen.');
  }, [graphResult]);

  async function unlink() {
    if (busy) return;
    setBusy(true);
    try {
      await disconnectOutlook();
      setOutlook([]); setGraphError(''); setRefresh(n => n + 1);
    } catch (e) { setGraphError(e instanceof Error ? e.message : 'Trennen fehlgeschlagen.'); }
    finally { setBusy(false); }
  }

  const today = localDate();
  const localCount = tasks.filter(t => t.plannedFor && days.some(d => localDate(d) === t.plannedFor)).length;
  const eventsOn = (date: string) => outlook.filter(e => e.start && localDate(new Date(e.start)) === date);
  const selectedTasks = tasks.filter(t => t.plannedFor === selectedDay);
  const selectedMarker = markers.find(m => m.day === selectedDay);
  const selectedEvents = eventsOn(selectedDay);
  const byHour = (date: string, hour: number) => eventsOn(date).filter(e => !e.isAllDay && hourOf(e.start) === hour);

  return <div className="page classic-workspace">
    <div className="page-heading classic-page-heading">
      <div><span className="eyebrow">PLENARO / WOCHENPLANUNG</span>
        <h1>Meine Woche</h1>
        <p>Dein lokaler Plenaro-Kalender. Outlook kann zusätzliche Termine anzeigen, ist aber niemals erforderlich.</p>
      </div>
      <div className="classic-week-controls">
        <button type="button" className="button button-secondary" aria-label="Vorherige Woche"
          onClick={() => setWeekStart(v => plusDays(v, -7))}><ChevronLeft size={18}/></button>
        <button type="button" className="button button-secondary" onClick={() => setWeekStart(startOfWeek(new Date()))}>Diese Woche</button>
        <button type="button" className="button button-secondary" aria-label="Nächste Woche"
          onClick={() => setWeekStart(v => plusDays(v, 7))}><ChevronRight size={18}/></button>
        <button type="button" className="button button-secondary" aria-label="Kalender aktualisieren"
          onClick={() => setRefresh(n => n + 1)}><RefreshCw size={16}/></button>
      </div>
    </div>

    <div className="classic-calendar-status">
      <span><CalendarDays size={16}/>Lokaler Kalender · {localCount} geplante Aufgaben</span>
      {graph?.connected ? <span><Cloud size={16}/>Outlook verbunden
        <button type="button" className="classic-text-action" disabled={busy} onClick={() => void unlink()}>Trennen <Unplug size={14}/></button></span>
      : graph?.enabled && graph.linked && graph.vaultReady ?
        <a href="/api/v1/graph/connect/start"><Link2 size={15}/>Outlook optional verbinden</a>
        : graph?.enabled && !graph.linked ?
        <Link to="/settings"><Link2 size={15}/>Microsoft-Konto optional verknüpfen</Link>
        : <span>Ohne Microsoft nutzbar</span>}
    </div>
    {localError && <p role="alert" className="um-feedback failure">{localError}</p>}
    {graphError && <p role="alert" className="um-feedback failure">Outlook: {graphError} – lokale Planung bleibt verfügbar.</p>}
    {hasMore && <p role="status" className="um-feedback failure">Outlook liefert weitere Termine. Die erste Graph-Version zeigt maximal 100 pro Woche.</p>}

    <div className="classic-calendar-layout">
      <section className="classic-calendar-main" aria-label="Wochenkalender">
        <div className="classic-calendar-scroll">
          <div className="classic-calendar-grid">
            <div className="classic-calendar-corner">Zeit</div>
            {days.map(d => {
              const date = localDate(d);
              const marker = markers.find(m => m.day === date);
              const dayTasks = tasks.filter(t => t.plannedFor === date);
              const allDay = eventsOn(date).filter(e => e.isAllDay || hourOf(e.start) < 6 || hourOf(e.start) >= 18);
              return <button type="button" key={date} className={`classic-calendar-header${selectedDay === date ? ' selected' : ''}${date === today ? ' today' : ''}`}
                aria-label={`${fmtDay(d)} auswählen`} aria-pressed={selectedDay === date}
                onClick={() => setSelectedDay(date)}>
                <strong>{fmtDay(d)}</strong>
                <span>{dayTasks.length ? `${dayTasks.length} Aufgaben` : '–'}</span>
                {marker && (marker.dayType !== 'Normal' || marker.isBr) &&
                  <span className="classic-calendar-markers">{marker.dayType !== 'Normal' ? marker.dayType : ''} {marker.isBr ? 'BR' : ''}</span>}
                {allDay.slice(0, 2).map(e => <span key={e.id} className="classic-calendar-all-day" title={e.subject}>{e.subject || 'Outlook'}</span>)}
              </button>;
            })}
            {Array.from({ length: 12 }, (_, i) => i + 6).map(hour => <div className="classic-calendar-time-row" key={hour}>
              <div className="classic-calendar-time">{String(hour).padStart(2, '0')}:00</div>
              {days.map(day => {
                const date = localDate(day);
                const slotEvents = byHour(date, hour);
                return <button type="button" key={date} className={`classic-calendar-slot${selectedDay === date ? ' selected' : ''}`}
                  aria-label={`${fmtDay(day)}, ${String(hour).padStart(2, '0')} Uhr`}
                  onClick={() => setSelectedDay(date)}>
                  {slotEvents.map(e => <span className="classic-calendar-event" key={e.id} title={e.subject}>
                    <strong>{formatClock(e.start)}</strong> {e.subject || 'Outlook-Termin'}
                  </span>)}
                </button>;
              })}
            </div>)}
          </div>
        </div>
        {loading && <p role="status" className="um-hint">Lade lokale Wochenplanung …</p>}
        <div className="classic-calendar-legend"><span>● Eigene Aufgaben</span><span>● UL / AM / BR</span><span>● Outlook (optional)</span></div>
      </section>

      <aside className="classic-calendar-aside" aria-label="Tagesdetails">
        <span className="eyebrow">AUSGEWÄHLTER TAG</span>
        <h2>{new Intl.DateTimeFormat('de-DE', { dateStyle: 'full' }).format(new Date(selectedDay + 'T12:00:00'))}</h2>
        <h3>Tagesmarker</h3>
        {selectedMarker ? <p className="classic-day-marker-value">
          {selectedMarker.dayType === 'Normal' ? 'Normal' : selectedMarker.dayType}
          {selectedMarker.isBr ? ' · BR' : ''}
        </p> : <p className="um-hint">Normal · Kein Marker</p>}
        <button type="button" className="button button-secondary" onClick={() => setShowMarkers(v => !v)}>
          {showMarkers ? 'Markereditor schließen' : 'UL / AM / BR bearbeiten'}
        </button>
        <h3>Geplante Aufgaben</h3>
        {selectedTasks.length ? selectedTasks.map(t => <div className="classic-day-entry" key={t.id}>
          <strong>{t.title}</strong><small>{t.status === 'completed' ? 'Erledigt' : 'Offen'} · Priorität {t.priority ?? '–'}</small>
        </div>) : <p className="um-hint">Für diesen Tag keine Aufgaben geplant.</p>}
        <Link className="classic-text-action" to="/tasks">Aufgaben verwalten</Link>
        {graph?.connected && <>
          <h3>Outlook-Termine</h3>
          {selectedEvents.length ? selectedEvents.map(e => <div key={e.id} className="classic-day-entry">
            <strong>{e.subject || 'Termin'}</strong>
            <small>{e.isAllDay ? 'Ganztägig' : formatClock(e.start)}</small>
          </div>) : <p className="um-hint">Keine Outlook-Termine.</p>}
        </>}
      </aside>
    </div>
    {showMarkers && <div className="classic-marker-editor">
      <DayMarkersPanel key={`${weekStart.toISOString()}-${refresh}`}/>
      <button type="button" className="button button-secondary" onClick={() => setRefresh(n => n + 1)}>
        Marker im Kalender aktualisieren
      </button>
    </div>}
  </div>;
}
