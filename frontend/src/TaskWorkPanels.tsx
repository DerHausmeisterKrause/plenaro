import { useEffect, useState, type FormEvent } from 'react';
import { CalendarDays, Clock3, Pencil, Play, Save, Square, Trash2, X } from 'lucide-react';
import {
  adjustTaskTime, createTaskSegment, deleteTaskSegment, listTaskSegments,
  readTaskTime, secondsToClock, startTaskTimer, stopTaskTimer, updateTaskSegment,
  type SegmentInput, type TaskSegment, type TaskTimeSummary,
} from './taskWorkApi';
import { localDate } from './tasksApi';
import { Link } from 'react-router-dom';

const timeOf = (date: string) =>
  new Intl.DateTimeFormat('de-DE', { hour: '2-digit', minute: '2-digit' }).format(new Date(date));
const dateOf = (date: string) =>
  new Intl.DateTimeFormat('de-DE', { weekday: 'short', day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date(date));
const localTime = (input: string) => {
  const d = new Date(input);
  return [String(d.getHours()).padStart(2, '0'), String(d.getMinutes()).padStart(2, '0')].join(':');
};

export function TaskTimerPanel({ taskId }: { taskId: string | null }) {
  const [summary, setSummary] = useState<TaskTimeSummary | null>(null);
  const [tick, setTick] = useState(() => Date.now());
  const [amount, setAmount] = useState('15');
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  useEffect(() => {
    const interval = window.setInterval(() => setTick(Date.now()), 1000);
    return () => window.clearInterval(interval);
  }, []);
  useEffect(() => {
    let active = true;
    setSummary(null); setError(''); setNotice('');
    if (taskId) void readTaskTime(taskId).then(s => { if (active) setSummary(s); })
      .catch(e => { if (active) setError(e instanceof Error ? e.message : 'Timer nicht erreichbar.'); });
    return () => { active = false; };
  }, [taskId]);

  async function action(work: () => Promise<TaskTimeSummary>, text: string) {
    if (busy) return;
    setBusy(true); setError(''); setNotice('');
    try { const result = await work(); setSummary(result); setTick(Date.now()); setNotice(text); }
    catch (e) { setError(e instanceof Error ? e.message : 'Buchung fehlgeschlagen.'); }
    finally { setBusy(false); }
  }

  const running = summary?.runningSince
    ? Math.max(0, Math.floor((tick - Date.parse(summary.runningSince)) / 1000))
    : 0;
  const totalSeconds = (summary?.bookedSeconds ?? 0) + running;
  async function correct(delta: number) {
    if (!taskId || !Number.isInteger(delta) || delta === 0 || Math.abs(delta) > 1440) return;
    await action(() => adjustTaskTime(taskId, delta, note), 'Zeitkorrektur gespeichert.');
    setNote('');
  }

  return <section className="content-card plenaro-timer" aria-label="Aufgabenbezogener Timer">
    <span className="eyebrow">TIMER / BUCHUNG</span>
    {!taskId ? <p>Wähle eine Aufgabe für den Timer aus.</p> : <>
      <div className="plenaro-timer-time"><Clock3 size={20}/>{summary ? secondsToClock(totalSeconds) : '00:00:00'}</div>
      <p className="plenaro-timer-booked">
        Bereits gebucht: <strong>{summary ? Math.floor(summary.bookedSeconds / 60) : '–'} Min.</strong>
        {summary && <> · {summary.sessionCount} abgeschlossene Sitzungen</>}
      </p>
      <div className="plenaro-timer-controls">
        <button className="button button-primary" type="button" disabled={busy || !summary || !!summary.runningSince}
          onClick={() => void action(() => startTaskTimer(taskId), 'Timer gestartet.')}>
          <Play size={15}/>Start
        </button>
        <button className="button button-secondary" type="button" disabled={busy || !summary?.runningSince}
          onClick={() => void action(() => stopTaskTimer(taskId), 'Zeitbuchung abgeschlossen.')}>
          <Square size={14}/>Stop
        </button>
      </div>
      <p className="um-hint">Der Timer wird serverseitig gespeichert und läuft auch nach dem Schließen des Browsers weiter. Es kann nur ein Aufgabentimer gleichzeitig laufen.</p>
      <div className="plenaro-timer-adjust">
        <span className="eyebrow">MINUTEN KORRIGIEREN</span>
        <div className="plenaro-timer-presets">
          {[15, 30, 60, -15, -30, -60].map(minutes =>
            <button type="button" key={minutes} className={'plenaro-time-chip ' + (minutes < 0 ? 'negative' : '')}
              disabled={busy || !summary} onClick={() => void correct(minutes)}>
              {minutes > 0 ? '+' : ''}{minutes === 60 ? '1 h' : minutes === -60 ? '−1 h' : Math.abs(minutes) + ' Min.'}
            </button>)}
        </div>
        <form className="plenaro-timer-manual" onSubmit={e => {
          e.preventDefault(); void correct(Number(amount));
        }}>
          <label htmlFor="task-minutes">Korrektur in Minuten (+/−)</label>
          <input id="task-minutes" type="number" min="-1440" max="1440" step="1" required
            value={amount} onChange={e => setAmount(e.target.value)}/>
          <label htmlFor="task-booking-note">Notiz (optional)</label>
          <input id="task-booking-note" maxLength={500} value={note}
            onChange={e => setNote(e.target.value)} placeholder="Grund der Korrektur"/>
          <button type="submit" className="button button-secondary" disabled={busy || !summary}>Korrektur buchen</button>
        </form>
      </div>
      {error && <p role="alert" className="um-feedback failure">{error}</p>}
      {notice && <p role="status" className="um-feedback success">{notice}</p>}
    </>}
  </section>;
}

export function TaskSegmentsPanel({ taskId }: { taskId: string | null }) {
  const [segments, setSegments] = useState<TaskSegment[]>([]);
  const [editing, setEditing] = useState<string | null>(null);
  const [day, setDay] = useState(localDate());
  const [start, setStart] = useState('09:00');
  const [end, setEnd] = useState('09:30');
  const [note, setNote] = useState('');
  const [participants, setParticipants] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    let active = true;
    setSegments([]); setEditing(null); setError(''); setNotice('');
    if (!taskId) return;
    setLoading(true);
    void listTaskSegments(taskId).then(data => {
      if (active) { setSegments(data); setLoading(false); }
    }).catch(e => { if (active) { setError(e instanceof Error ? e.message : 'Segmente konnten nicht geladen werden.'); setLoading(false); } });
    return () => { active = false; };
  }, [taskId]);

  function reset() {
    setEditing(null); setDay(localDate()); setStart('09:00'); setEnd('09:30');
    setNote(''); setParticipants('');
  }

  function edit(segment: TaskSegment) {
    setEditing(segment.id);
    setDay(localDate(new Date(segment.startsAt)));
    setStart(localTime(segment.startsAt)); setEnd(localTime(segment.endsAt));
    setNote(segment.note); setParticipants(segment.participants.join(', '));
  }

  async function save(e: FormEvent) {
    e.preventDefault(); if (!taskId || busy) return;
    setError(''); setNotice('');
    const starts = new Date(day + 'T' + start + ':00');
    const ends = new Date(day + 'T' + end + ':00');
    if (Number.isNaN(starts.getTime()) || Number.isNaN(ends.getTime()) || ends <= starts) {
      setError('Endzeit muss nach der Startzeit liegen.'); return;
    }
    const emails = participants.split(/[,;\n]/).map(s => s.trim()).filter(Boolean);
    if (emails.length > 20) { setError('Maximal 20 Teilnehmer.'); return; }
    const input: SegmentInput = {
      startsAt: starts.toISOString(), endsAt: ends.toISOString(),
      note: note.trim(), participants: emails,
    };
    setBusy(true);
    try {
      if (editing) await updateTaskSegment(taskId, editing, input);
      else await createTaskSegment(taskId, input);
      setSegments(await listTaskSegments(taskId));
      reset(); setNotice('Segment gespeichert.');
    } catch (e) { setError(e instanceof Error ? e.message : 'Segment konnte nicht gespeichert werden.'); }
    finally { setBusy(false); }
  }

  async function remove(id: string) {
    if (!taskId || busy || !window.confirm('Zeitsegment endgültig löschen?')) return;
    setBusy(true); setError(''); setNotice('');
    try {
      await deleteTaskSegment(taskId, id);
      setSegments(await listTaskSegments(taskId));
      reset(); setNotice('Segment gelöscht.');
    } catch (e) { setError(e instanceof Error ? e.message : 'Löschen fehlgeschlagen.'); }
    finally { setBusy(false); }
  }

  return <section className="content-card plenaro-segments" aria-label="Aufgabensegmente">
    <span className="eyebrow">PLANUNG (SEGMENTE)</span>
    {!taskId ? <p>Wähle eine Aufgabe, um Zeitsegmente zu planen.</p> : <>
      <form className="plenaro-segment-form" onSubmit={e => void save(e)}>
        <div className="plenaro-segment-form-heading">
          <strong>{editing ? 'Segment bearbeiten' : 'Neues Segment'}</strong>
          {editing && <button type="button" className="button button-secondary" onClick={reset} aria-label="Bearbeitung abbrechen"><X size={15}/></button>}
        </div>
        <p className="um-hint">Lokale Planung; keine automatische Outlook-Buchung.</p>
        <div className="plenaro-segment-date">
          <label>Datum <input type="date" required min="2000-01-01" max="2099-12-31"
            value={day} onChange={e => setDay(e.target.value)}/></label>
          <label>Beginn <input type="time" required value={start} onChange={e => setStart(e.target.value)}/></label>
          <label>Ende <input type="time" required value={end} onChange={e => setEnd(e.target.value)}/></label>
        </div>
        <label>Kurzbeschreibung <input maxLength={1000} value={note} onChange={e => setNote(e.target.value)}/></label>
        <label>Teilnehmer (E-Mail) <input value={participants} onChange={e => setParticipants(e.target.value)}
          placeholder="name@firma.de, weitere@firma.de"/></label>
        <p className="um-hint">Mehrere E-Mail-Adressen mit Komma oder Semikolon trennen. Noch keine Kalendereinladungen.</p>
        <button type="submit" className="button button-primary" disabled={busy}><Save size={15}/>{editing ? 'Änderungen speichern' : 'Segment speichern'}</button>
      </form>
      {error && <p role="alert" className="um-feedback failure">{error}</p>}
      {notice && <p role="status" className="um-feedback success">{notice}</p>}
      {loading && <p role="status">Segmente werden geladen …</p>}
      <div className="plenaro-segment-history">
        {segments.map((s, index) => <article className="plenaro-segment-item" key={s.id}>
          <div className="plenaro-segment-form-heading"><strong>Segment {index + 1}</strong>
            <div className="plenaro-segment-actions">
              <button type="button" className="button button-secondary" onClick={() => edit(s)} disabled={busy} aria-label={'Segment ' + (index + 1) + ' bearbeiten'}><Pencil size={14}/></button>
              <button type="button" className="button button-secondary" onClick={() => void remove(s.id)} disabled={busy} aria-label={'Segment ' + (index + 1) + ' löschen'}><Trash2 size={14}/></button>
            </div>
          </div>
          <p>{dateOf(s.startsAt)} · {timeOf(s.startsAt)}–{timeOf(s.endsAt)}</p>
          {s.note && <p>{s.note}</p>}
          {s.participants.length > 0 && <p className="um-hint">{s.participants.join(', ')}</p>}
        </article>)}
      </div>
      <Link to="/week" className="classic-text-action"><CalendarDays size={16}/>Im Wochenkalender ansehen</Link>
    </>}
  </section>;
}
