import { useEffect, useState, type FormEvent } from 'react';
import { CalendarDays, CheckCircle2, Clock3, ExternalLink, MapPin, Plus, RotateCcw, Save, Ticket } from 'lucide-react';
import { Link } from 'react-router-dom';
import { changeTaskStatus, createTask, listTasks, localDate, todayTasks, updateTask, type WorkTask, type TaskInput } from './tasksApi';
import { getMonthMarkers, saveDayMarker, type DayType, type DayMarker } from './dayMarkersApi';
import { TaskTimerPanel, TaskSegmentsPanel } from './TaskWorkPanels';
import { listWeekSegments, type TaskSegment } from './taskWorkApi';
import { graphStatus, readOutlookWeek, type OutlookEvent } from './graphApi';
import { agendaTimeLabel, buildTodayAgenda, localDayBounds } from './todayAgenda';

type Tab = 'today' | 'active' | 'new' | 'completed';
const labels: { key: Tab; label: string }[] = [
  { key: 'today', label: 'Heute' },
  { key: 'active', label: 'Aktuelle Aufgaben' },
  { key: 'new', label: 'Neue Aufgaben' },
  { key: 'completed', label: 'Erledigte Aufgaben' },
];

export function ClassicTodayPage() {
  const [tasks, setTasks] = useState<WorkTask[]>([]);
  const [todayTasksList, setTodayTasks] = useState<WorkTask[]>([]);
  const [segments, setSegments] = useState<TaskSegment[]>([]);
  const [outlookEvents, setOutlookEvents] = useState<OutlookEvent[]>([]);
  const [calendarError, setCalendarError] = useState('');
  const [outlookConnected, setOutlookConnected] = useState(false);
  const [refreshCalendar, setRefreshCalendar] = useState(0);
  const [selectedAgendaKey, setSelectedAgendaKey] = useState<string | null>(null);
  const [tab, setTab] = useState<Tab>('today');
  const [selected, setSelected] = useState<string | null>(null);
  const [quick, setQuick] = useState('');
  const [description, setDescription] = useState('');
  const [marker, setMarker] = useState<DayMarker | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [day, setCurrentDate] = useState(() => localDate());
  const today = new Date(day + 'T12:00:00');
  // Remain correct when the browser stays open over midnight.
  useEffect(() => {
    const interval = window.setInterval(() => {
      const next = localDate();
      setCurrentDate(previous => previous === next ? previous : next);
    }, 60_000);
    return () => window.clearInterval(interval);
  }, []);
  const [yy, mm] = day.slice(0, 7).split('-').map(Number);

  async function reload() {
    const { start, end } = localDayBounds(day);
    const [all, todayItems, days, plannedSegments] = await Promise.all([
      listTasks(), todayTasks(day), getMonthMarkers(yy, mm),
      listWeekSegments(start.toISOString(), end.toISOString()),
    ]);
    setTasks(all);
    setTodayTasks(todayItems);
    setSegments(plannedSegments);
    setMarker(days.find(item => item.day === day) ?? null);
    setSelected(id => id && [...all, ...todayItems].some(t => t.id === id) ? id : null);
    setRefreshCalendar(value => value + 1);
  }

  useEffect(() => {
    let active = true;
    setLoading(true);
    const { start, end } = localDayBounds(day);
    void Promise.all([
      listTasks(), todayTasks(day), getMonthMarkers(yy, mm),
      listWeekSegments(start.toISOString(), end.toISOString()),
    ]).then(([all, due, days, plannedSegments]) => {
      if (!active) return;
      setTasks(all); setTodayTasks(due); setSegments(plannedSegments);
      setMarker(days.find(item => item.day === day) ?? null);
      setSelected(null); setSelectedAgendaKey(null);
      setLoading(false);
    }).catch(e => { if (active) { setError(e instanceof Error ? e.message : 'Tagesplanung konnte nicht geladen werden.'); setLoading(false); } });
    return () => { active = false; };
  }, [day, yy, mm]);

  // Outlook is optional: a Graph error must not hide local tasks and segments.
  useEffect(() => {
    let active = true;
    setOutlookEvents([]); setCalendarError(''); setOutlookConnected(false);
    const { start, end } = localDayBounds(day);
    void graphStatus().then(async status => {
      if (!active || !status.connected) return;
      setOutlookConnected(true);
      const result = await readOutlookWeek(start, end);
      if (active) {
        setOutlookEvents(result.events);
        if (result.hasMore) setCalendarError('Outlook liefert mehr Termine als diese Abfrage anzeigen kann.');
      }
    }).catch(e => {
      if (active) setCalendarError(e instanceof Error ? e.message : 'Outlook-Termine nicht verfügbar.');
    });
    return () => { active = false; };
  }, [day, refreshCalendar]);

  const agenda = buildTodayAgenda(day, todayTasksList, segments, outlookEvents);
  const chosen = tasks.find(t => t.id === selected) ?? todayTasksList.find(t => t.id === selected);
  const rows = tab === 'active' ? tasks.filter(t => t.status === 'active' || t.status === 'planned') :
    tab === 'completed' ? tasks.filter(t => t.status === 'completed') : [];
  const selectedAppointment = tab === 'today'
    ? agenda.find(item => item.kind === 'outlook' && item.key === selectedAgendaKey)
    : undefined;
  const visibleSelected = tab === 'today'
    ? agenda.some(item => item.kind === 'task' && item.key === selectedAgendaKey && item.taskId === selected)
    : rows.some(t => t.id === selected);
  useEffect(() => {
    setDescription(chosen?.description ?? '');
  }, [chosen?.id, chosen?.description]);

  async function mutate(run: () => Promise<void>, message: string) {
    if (busy) return;
    setBusy(true); setError(''); setNotice('');
    try { await run(); await reload(); setNotice(message); }
    catch (e) { setError(e instanceof Error ? e.message : 'Änderung fehlgeschlagen.'); }
    finally { setBusy(false); }
  }

  async function quickAdd(e: FormEvent) {
    e.preventDefault();
    const title = quick.trim();
    if (!title) return;
    await mutate(() => createTask({
      title, description: '', status: 'planned', priority: 3, tags: [], plannedFor: day,
    }), 'Aufgabe angelegt.');
    setQuick('');
  }

  async function setDay(type: DayType, isBr: boolean, isHo: boolean) {
    await mutate(async () => saveDayMarker(day, type, isBr, isHo), 'Tagesmarker gespeichert.');
  }

  function inputFor(t: WorkTask): TaskInput {
    return { title: t.title, description, status: t.status,
      priority: t.priority, tags: t.tags, plannedFor: t.plannedFor };
  }

  return <div className="page plenaro-today">
    {error && <p role="alert" className="um-feedback failure">{error}</p>}
    {notice && <p role="status" className="um-feedback success">{notice}</p>}
    <div className="plenaro-today-columns">
      <section className="plenaro-today-left" aria-label="Arbeitsbereich Heute">
        <div className="content-card plenaro-today-quick">
          <form onSubmit={e => void quickAdd(e)}>
            <label htmlFor="today-add-task">Neue Aufgabe</label>
            <div className="plenaro-today-quick-row">
              <input id="today-add-task" maxLength={255} required value={quick}
                onChange={e => setQuick(e.target.value)} placeholder="Aufgabe schnell erfassen …"/>
              <button type="submit" disabled={busy} className="button button-primary" aria-label="Aufgabe hinzufügen"><Plus size={20}/></button>
            </div>
          </form>
        </div>
        <div className="content-card plenaro-workday">
          <div>
            <h2>Arbeitstag heute</h2>
            <p>{new Intl.DateTimeFormat('de-DE', { dateStyle: 'full' }).format(today)}</p>
            <p className="um-hint">Tagesmarker für deine persönliche Planung</p>
          </div>
          <div className="plenaro-marker-actions" aria-label="Tagesart einstellen">
            {(['Normal', 'UL', 'AM'] as DayType[]).map(t => <button type="button" key={t}
              className={`plenaro-marker-pill${(marker?.dayType ?? 'Normal') === t ? ' active' : ''}`}
              aria-pressed={(marker?.dayType ?? 'Normal') === t}
              disabled={busy} onClick={() => void setDay(t, marker?.isBr ?? false, marker?.isHo ?? false)}>{t}</button>)}
            <button type="button" className={`plenaro-marker-pill${marker?.isHo ? ' active' : ''}`}
              aria-pressed={marker?.isHo ?? false} disabled={busy}
              onClick={() => void setDay(marker?.dayType ?? 'Normal', marker?.isBr ?? false, !marker?.isHo)}>HO</button>
            <button type="button" className={`plenaro-marker-pill${marker?.isBr ? ' active' : ''}`}
              aria-pressed={marker?.isBr ?? false} disabled={busy}
              onClick={() => void setDay(marker?.dayType ?? 'Normal', !marker?.isBr, marker?.isHo ?? false)}>BR</button>
          </div>
        </div>
        <div className="content-card plenaro-today-list">
          <div className="plenaro-tabs" role="tablist" aria-label="Aufgabenansichten">
            {labels.map(item => <button role="tab" type="button" key={item.key}
              className={tab === item.key ? 'active' : ''} aria-selected={tab === item.key}
              onClick={() => { setTab(item.key); setSelected(null); setSelectedAgendaKey(null); }}>
              {item.label}{item.key === 'new' ? ' (0)' : ''}</button>)}
          </div>
          <div className="plenaro-list-title">
            <h2>{labels.find(item => item.key === tab)?.label}:</h2>
            <Link to="/tasks" className="classic-text-action">Alle Aufgaben</Link>
          </div>
          {tab === 'today' && outlookConnected && calendarError &&
            <p role="status" className="um-hint">{calendarError}</p>}
          {loading ? <p role="status" className="um-hint">Lade Tagesplanung …</p> :
            tab === 'today' ? (agenda.length === 0
              ? <p className="plenaro-empty-list">Heute keine geplanten Aufgaben oder Termine.</p>
              : <div className="plenaro-rows" aria-label="Tagesagenda">
                {agenda.map(item => <button type="button" key={item.key}
                  className={`plenaro-task-row plenaro-agenda-row${selectedAgendaKey === item.key ? ' selected' : ''}`}
                  onClick={() => {
                    setSelectedAgendaKey(item.key);
                    setSelected(item.taskId);
                  }}>
                  <span className="plenaro-agenda-time">
                    {item.allDay ? <CalendarDays size={15}/> : <Clock3 size={15}/>}
                    {agendaTimeLabel(item)}
                  </span>
                  <span className="plenaro-agenda-content">
                    <strong>{item.title}</strong>
                    {item.description && <small>{item.description}</small>}
                    {item.location && <small><MapPin size={12}/>{item.location}</small>}
                  </span>
                  <span className="plenaro-agenda-kind">{item.kind === 'outlook' ? 'Outlook' : 'Aufgabe'}</span>
                </button>)}
              </div>) :
            tab === 'new' ? <div className="plenaro-empty-list">
              <Ticket size={22}/>
              <strong>Keine neuen Tickets verfügbar</strong>
              <p>Neue Znuny/OTRS-Ticketkandidaten erscheinen hier nach Einrichtung des Ticketsystems.</p>
            </div> :
            rows.length === 0 ? <p className="plenaro-empty-list">Keine Aufgaben in dieser Ansicht.</p> :
            <div className="plenaro-rows">{rows.map(t => <button type="button" key={t.id}
              className={`plenaro-task-row${selected === t.id ? ' selected' : ''}`}
              onClick={() => setSelected(t.id)}>
              <span><strong>{t.title}</strong>{t.description && <small>{t.description}</small>}</span>
              <span className={`tasks-state tasks-state-${t.status}`}>{t.status === 'completed' ? 'Erledigt' : t.status === 'active' ? 'Aktuell' : 'Geplant'}</span>
            </button>)}</div>}
        </div>
      </section>
      <aside className="plenaro-today-right" aria-label="Aufgabedetails und Planung">
        <section className="content-card plenaro-today-detail" aria-label="Task Details">
          <span className="eyebrow">TASK DETAILS</span>
          {selectedAppointment ? <div className="plenaro-agenda-appointment">
            <h2>{selectedAppointment.title}</h2>
            <p><Clock3 size={15}/>{agendaTimeLabel(selectedAppointment)}</p>
            {selectedAppointment.location && <p><MapPin size={15}/>{selectedAppointment.location}</p>}
            {selectedAppointment.webLink && <a className="button button-secondary"
              href={selectedAppointment.webLink} target="_blank" rel="noopener noreferrer">
              <ExternalLink size={14}/>In Outlook öffnen</a>}
            <p className="um-hint">Persönlicher Outlook-Termin, nur lesbar.</p>
          </div> : !chosen || !visibleSelected ? <div className="classic-detail-empty">
            <h2>{tab === 'today' ? 'Tagespunkt auswählen' : 'Aufgabe auswählen'}</h2>
            <p>Wähle links eine Aufgabe oder einen Termin aus, um die Details zu öffnen.</p>
          </div> : <>
            <h2>{chosen.title}</h2>
            <label htmlFor="today-description">Beschreibung</label>
            <textarea id="today-description" rows={7} value={description}
              maxLength={10000} onChange={e => setDescription(e.target.value)}/>
            <div className="plenaro-today-detail-actions">
              <button type="button" className="button button-primary" disabled={busy}
                onClick={() => void mutate(() => updateTask(chosen.id, inputFor(chosen)), 'Beschreibung gespeichert.')}>
                <Save size={15}/>Speichern</button>
              {chosen.status !== 'completed' ?
                <button type="button" className="button button-secondary" disabled={busy}
                  onClick={() => void mutate(() => changeTaskStatus(chosen.id, 'completed'), 'Aufgabe erledigt.')}>
                  <CheckCircle2 size={15}/>Erledigt</button> :
                <button type="button" className="button button-secondary" disabled={busy}
                  onClick={() => void mutate(() => changeTaskStatus(chosen.id, 'active'), 'Aufgabe wieder geöffnet.')}>
                  <RotateCcw size={15}/>Wieder öffnen</button>}
              <Link to="/tasks" className="button button-secondary"><ExternalLink size={14}/>Weitere Details</Link>
            </div>
          </>}
        </section>
        <TaskTimerPanel taskId={chosen && visibleSelected ? chosen.id : null}/>
        <TaskSegmentsPanel taskId={chosen && visibleSelected ? chosen.id : null}/>
      </aside>
    </div>
  </div>;
}
