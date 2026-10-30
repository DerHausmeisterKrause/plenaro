import { useEffect, useMemo, useState, type DragEvent, type FormEvent } from 'react';
import { CalendarDays, ChevronLeft, ChevronRight, Cloud, GripVertical, Link2, RefreshCw, Save, Unplug } from 'lucide-react';
import { Link, useSearchParams } from 'react-router-dom';
import { DayMarkersPanel } from './DayMarkersPanel';
import { getMonthMarkers, type DayMarker } from './dayMarkersApi';
import { graphStatus, readOutlookWeek, disconnectOutlook, type GraphStatus, type OutlookEvent } from './graphApi';
import { listTasks, localDate, type WorkTask } from './tasksApi';
import { listWeekSegments, rescheduleTaskSegment, type TaskSegment } from './taskWorkApi';
import { calendarMove, conflictingLocalSegments, halfHourFromPosition, intervalsOverlap } from './segmentPlanner';

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
  const [segments, setSegments] = useState<TaskSegment[]>([]);
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
  const [selectedSegmentId, setSelectedSegmentId] = useState<string | null>(null);
  const [moveDay, setMoveDay] = useState('');
  const [moveClock, setMoveClock] = useState('09:00');
  const [dragId, setDragId] = useState<string | null>(null);
  const [hover, setHover] = useState<string | null>(null);
  const [moving, setMoving] = useState(false);
  const [moveError, setMoveError] = useState('');
  const [moveNotice, setMoveNotice] = useState('');
  const days = useMemo(() => Array.from({ length: 5 }, (_, i) => plusDays(weekStart, i)), [weekStart]);
  const until = useMemo(() => plusDays(weekStart, 5), [weekStart]);

  useEffect(() => {
    setSelectedDay(previous => days.some(d => localDate(d) === previous) ? previous : localDate(days[0]));
  }, [days]);

  // Local tasks and day markers are independent of Entra and are always loaded.
  useEffect(() => {
    let active = true;
    setLoading(true);
    setLocalError('');
    const first = days[0];
    const last = days[4];
    const ym = (d: Date) => [d.getFullYear(), d.getMonth() + 1] as const;
    const months = [ym(first)];
    if (first.getMonth() !== last.getMonth() || first.getFullYear() !== last.getFullYear()) months.push(ym(last));
    void Promise.all([
      listTasks(),
      Promise.all(months.map(([year, month]) => getMonthMarkers(year, month))),
      listWeekSegments(weekStart.toISOString(), until.toISOString()),
    ]).then(([taskList, monthlyMarkers, plannedSegments]) => {
      if (!active) return;
      setTasks(taskList);
      setMarkers(monthlyMarkers.flat());
      setSegments(plannedSegments);
      setLoading(false);
    }).catch(e => {
      if (active) { setLocalError(e instanceof Error ? e.message : 'Lokaler Kalender nicht erreichbar.'); setLoading(false); }
    });
    return () => { active = false; };
  }, [days, weekStart, until, refresh]);

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

  function chooseSegment(segment: TaskSegment) {
    setSelectedSegmentId(segment.id);
    const start = new Date(segment.startsAt);
    setMoveDay(localDate(start));
    const clampedHour = Math.min(17, Math.max(6, start.getHours()));
    const roundedMinute = start.getHours() > 17 ? 30 : start.getMinutes() >= 30 ? 30 : 0;
    setMoveClock(`${String(clampedHour).padStart(2, '0')}:${String(roundedMinute).padStart(2, '0')}`);
    setMoveError(''); setMoveNotice('');
  }

  async function moveSegment(segment: TaskSegment, day: string, hour: number, minute: number) {
    if (moving) return;
    setMoveError(''); setMoveNotice('');
    let input: ReturnType<typeof calendarMove>;
    try {
      input = calendarMove(segment, day, hour, minute);
    } catch (e) {
      setMoveError(e instanceof Error ? e.message : 'Ungültiges Ziel.');
      return;
    }
    if (input.startsAt === new Date(segment.startsAt).toISOString()) {
      setMoveNotice('Das Segment liegt bereits in diesem Zeitfenster.');
      return;
    }
    if (conflictingLocalSegments(segments, input.startsAt, input.endsAt, segment.id).length) {
      setMoveError('Dieser Zeitraum ist lokal durch ein anderes eigenes Zeitsegment belegt.');
      return;
    }
    // Microsoft Graph is read-only and may be incomplete (first 100 events).
    // Overlapping personal Outlook events are advisory, never participant free/busy.
    const busyOutlook = graph?.connected && outlook.some(event =>
      ['busy', 'oof', 'tentative'].includes(event.showAs.toLowerCase()) &&
      intervalsOverlap(input.startsAt, input.endsAt, event.start, event.end));
    if (busyOutlook && !window.confirm('In deinem Outlook-Kalender liegt ein möglicherweise belegter Termin. Nur lokal trotzdem verschieben?'))
      return;
    setMoving(true);
    try {
      await rescheduleTaskSegment(segment.taskId, segment.id, input);
      // Only update the local segment after the server confirmed the change.
      // A follow-up reload also drops segments moved outside the selected week.
      setSegments(previous => previous.map(s => s.id === segment.id
        ? { ...s, startsAt: input.startsAt, endsAt: input.endsAt } : s));
      setSelectedDay(day); setSelectedSegmentId(segment.id);
      setMoveDay(day); setMoveClock(`${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}`);
      setMoveNotice('Zeitsegment verschoben und gespeichert.');
      setRefresh(n => n + 1);
    } catch (e) {
      setMoveError(e instanceof Error ? e.message : 'Verschieben fehlgeschlagen.');
      setRefresh(n => n + 1); // Reload in case a concurrent edit changed the schedule.
    } finally { setMoving(false); setDragId(null); setHover(null); }
  }

  function dropMinute(event: DragEvent<HTMLElement>): 0 | 30 {
    const rect = event.currentTarget.getBoundingClientRect();
    return halfHourFromPosition(event.clientY, rect.top, rect.height);
  }

  async function saveSchedule(event: FormEvent) {
    event.preventDefault();
    const segment = segments.find(s => s.id === selectedSegmentId);
    if (!segment) return;
    const [hour, minute] = moveClock.split(':').map(Number);
    await moveSegment(segment, moveDay, hour, minute);
  }

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
  const selectedSegments = segments.filter(s => localDate(new Date(s.startsAt)) === selectedDay);
  const selectedSegment = segments.find(s => s.id === selectedSegmentId);
  const proposedMove = (() => {
    if (!selectedSegment || !moveDay) return null;
    try {
      const [hour, minute] = moveClock.split(':').map(Number);
      return calendarMove(selectedSegment, moveDay, hour, minute);
    } catch { return null; }
  })();
  const proposedLocalConflicts = proposedMove
    ? conflictingLocalSegments(segments, proposedMove.startsAt, proposedMove.endsAt, selectedSegmentId ?? undefined) : [];
  const proposedOutlookConflicts = proposedMove && graph?.connected
    ? outlook.filter(event => ['busy', 'oof', 'tentative'].includes(event.showAs.toLowerCase()) &&
      intervalsOverlap(proposedMove.startsAt, proposedMove.endsAt, event.start, event.end)) : [];
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
    {moveError && <p role="alert" className="um-feedback failure">{moveError}</p>}
    {moveNotice && <p role="status" className="um-feedback success">{moveNotice}</p>}
    <p className="um-hint classic-drag-hint">Lokale Zeitsegmente mit dem Griff in ein anderes Stundenfeld ziehen (30-Minuten-Raster).
      Alternativ ein Segment anklicken und im Tagesdetail per Formular verschieben. Outlook-Termine sind nicht verschiebbar.</p>
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
                {marker && (marker.dayType !== 'Normal' || marker.isBr || marker.isHo) &&
                  <span className="classic-calendar-markers">{marker.dayType !== 'Normal' ? marker.dayType : ''} {marker.isBr ? 'BR' : ''} {marker.isHo ? 'HO' : ''}</span>}
                {allDay.slice(0, 2).map(e => <span key={e.id} className="classic-calendar-all-day" title={e.subject}>{e.subject || 'Outlook'}</span>)}
              </button>;
            })}
            {Array.from({ length: 12 }, (_, i) => i + 6).map(hour => <div className="classic-calendar-time-row" key={hour}>
              <div className="classic-calendar-time">{String(hour).padStart(2, '0')}:00</div>
              {days.map(day => {
                const date = localDate(day);
                const slotEvents = byHour(date, hour);
                const slotSegments = segments.filter(s => localDate(new Date(s.startsAt)) === date && hourOf(s.startsAt) === hour);
                const slotKey = `${date}-${hour}`;
                return <div key={date}
                  className={`classic-calendar-slot${selectedDay === date ? ' selected' : ''}${hover?.startsWith(slotKey + '-') ? ' drop-target' : ''}`}
                  data-testid={`calendar-slot-${slotKey}`}
                  onDragOver={event => {
                    if (!dragId || moving) return;
                    event.preventDefault();
                    event.dataTransfer.dropEffect = 'move';
                    setHover(`${slotKey}-${dropMinute(event)}`);
                  }}
                  onDragLeave={() => setHover(null)}
                  onDrop={event => {
                    event.preventDefault();
                    const selected = segments.find(s => s.id === dragId);
                    const minute = dropMinute(event);
                    setHover(null); setDragId(null);
                    if (selected && !moving) void moveSegment(selected, date, hour, minute);
                  }}>
                  <button type="button" className="classic-calendar-slot-select"
                    aria-label={`${fmtDay(day)}, ${String(hour).padStart(2, '0')} Uhr auswählen`}
                    onClick={() => setSelectedDay(date)}>+ Zeitfeld</button>
                  {slotSegments.map(segment =>
                    <button type="button" draggable={!moving} className={`classic-calendar-event classic-calendar-task-segment classic-draggable-segment${selectedSegmentId === segment.id ? ' is-picked' : ''}`}
                      key={segment.id} title="Zum Verschieben ziehen oder für genaue Uhrzeit auswählen"
                      aria-label={`Zeitsegment ${segment.taskTitle}, ${formatClock(segment.startsAt)} auswählen und verschieben`}
                      onClick={() => { setSelectedDay(date); chooseSegment(segment); }}
                      onDragStart={event => {
                        setDragId(segment.id); setMoveError(''); setMoveNotice('');
                        event.dataTransfer.effectAllowed = 'move';
                        event.dataTransfer.setData('text/plain', segment.id);
                      }}
                      onDragEnd={() => { setDragId(null); setHover(null); }}>
                      <GripVertical size={13} aria-hidden="true"/>
                      <span><strong>{formatClock(segment.startsAt)}–{formatClock(segment.endsAt)}</strong>
                        {' '}{segment.taskTitle}</span>
                    </button>)}
                  {slotEvents.map(e => <span className="classic-calendar-event" key={e.id} title={e.subject}>
                    <strong>{formatClock(e.start)}</strong> {e.subject || 'Outlook-Termin'}
                  </span>)}
                  {hover === `${slotKey}-0` && <span className="classic-drop-indicator" aria-hidden="true">:00</span>}
                  {hover === `${slotKey}-30` && <span className="classic-drop-indicator" aria-hidden="true">:30</span>}
                </div>;
              })}
            </div>)}
          </div>
        </div>
        {loading && <p role="status" className="um-hint">Lade lokale Wochenplanung …</p>}
        <div className="classic-calendar-legend"><span>● Eigene Aufgaben</span><span>● Lokale Zeitsegmente</span><span>● UL / AM / BR / HO</span><span>● Outlook (optional)</span></div>
      </section>

      <aside className="classic-calendar-aside" aria-label="Tagesdetails">
        <span className="eyebrow">AUSGEWÄHLTER TAG</span>
        <h2>{new Intl.DateTimeFormat('de-DE', { dateStyle: 'full' }).format(new Date(selectedDay + 'T12:00:00'))}</h2>
        <h3>Tagesmarker</h3>
        {selectedMarker ? <p className="classic-day-marker-value">
          {selectedMarker.dayType === 'Normal' ? 'Normal' : selectedMarker.dayType}
          {selectedMarker.isBr ? ' · BR' : ''}{selectedMarker.isHo ? ' · HO' : ''}
        </p> : <p className="um-hint">Normal · Kein Marker</p>}
        <button type="button" className="button button-secondary" onClick={() => setShowMarkers(v => !v)}>
          {showMarkers ? 'Markereditor schließen' : 'UL / AM / BR bearbeiten'}
        </button>
        <h3>Geplante Aufgaben</h3>
        {selectedTasks.length ? selectedTasks.map(t => <div className="classic-day-entry" key={t.id}>
          <strong>{t.title}</strong><small>{t.status === 'completed' ? 'Erledigt' : 'Offen'} · Priorität {t.priority ?? '–'}</small>
        </div>) : <p className="um-hint">Für diesen Tag keine Aufgaben geplant.</p>}
        <Link className="classic-text-action" to="/tasks">Aufgaben verwalten</Link>
        <h3>Zeitsegmente</h3>
        {selectedSegments.length ? selectedSegments.map(segment =>
          <div className="classic-day-entry" key={segment.id}>
            <strong>{segment.taskTitle}</strong>
            <small>{formatClock(segment.startsAt)}–{formatClock(segment.endsAt)}</small>
            {segment.note && <small>{segment.note}</small>}
            {segment.participants.length > 0 && <small>{segment.participants.join(', ')}</small>}
            <button type="button" className="classic-text-action" disabled={moving}
              onClick={() => chooseSegment(segment)}>Zeitsegment verschieben</button>
          </div>) : <p className="um-hint">Keine Zeitsegmente für diesen Tag geplant.</p>}
        {selectedSegment && days.some(day => localDate(day) === moveDay) &&
          <form className="classic-segment-reschedule" aria-label="Zeitsegment verschieben" onSubmit={e => void saveSchedule(e)}>
            <strong>{selectedSegment.taskTitle} verschieben</strong>
            <label>Zieltag
              <select value={moveDay} onChange={e => setMoveDay(e.target.value)}>
                {days.map(d => <option key={localDate(d)} value={localDate(d)}>{fmtDay(d)}</option>)}
              </select>
            </label>
            <label>Neue Startzeit
              <select value={moveClock} onChange={e => setMoveClock(e.target.value)}>
                {Array.from({ length: 24 }, (_, i) => {
                  const value = `${String(6 + Math.floor(i / 2)).padStart(2, '0')}:${i % 2 ? '30' : '00'}`;
                  return <option key={value} value={value}>{value}</option>;
                })}
              </select>
            </label>
            {proposedMove && proposedLocalConflicts.length > 0 && <p className="um-feedback failure" role="status">
              Lokal belegt durch: {proposedLocalConflicts.map(s => s.taskTitle).join(', ')}
            </p>}
            {proposedMove && proposedLocalConflicts.length === 0 &&
              <p className="um-hint" role="status">Keine Überschneidung in den angezeigten lokalen Segmenten. Der Server prüft vor dem Speichern erneut.</p>}
            {proposedOutlookConflicts.length > 0 && <p className="um-feedback failure" role="status">
              Outlook-Warnung: {proposedOutlookConflicts.length} möglicherweise belegte eigene Termine. Keine Teilnehmerprüfung.
            </p>}
            {graph?.connected && (graphError || hasMore) && <p className="um-hint">Outlook-Daten möglicherweise unvollständig.</p>}
            <button className="button button-primary" type="submit" disabled={moving || !proposedMove || proposedLocalConflicts.length > 0}>
              <Save size={15}/>{moving ? 'Speichere …' : 'Zeitsegment verschieben'}
            </button>
            <p className="um-hint">Die Dauer bleibt erhalten. Notiz und Teilnehmer werden nicht verändert.</p>
          </form>}
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
