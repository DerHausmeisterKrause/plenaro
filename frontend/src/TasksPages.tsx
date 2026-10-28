import { useCallback, useEffect, useState, type FormEvent } from 'react';
import {
  CalendarDays, CheckCircle2, Circle, ClipboardList, ListFilter, Pencil,
  Play, Plus, RotateCcw, Search, Trash2, X,
} from 'lucide-react';
import { Link } from 'react-router-dom';
import { DayMarkersPanel } from './DayMarkersPanel';
import {
  changeTaskStatus, createTask, deleteTask, listTasks, localDate, taskStatusLabel,
  todayTasks, updateTask, type TaskInput, type TaskStatus, type WorkTask,
} from './tasksApi';

const statusOptions: TaskStatus[] = ['planned', 'active', 'completed', 'cancelled'];
const emptyTask: TaskInput = {
  title: '', description: '', status: 'planned', priority: 3, tags: [], plannedFor: null,
};

function TaskEditor({ initial, onSave, onCancel, saving }: {
  initial?: WorkTask;
  onSave: (value: TaskInput) => Promise<void>;
  onCancel: () => void;
  saving: boolean;
}) {
  const [title, setTitle] = useState(initial?.title ?? '');
  const [description, setDescription] = useState(initial?.description ?? '');
  const [status, setStatus] = useState<TaskStatus>(initial?.status ?? 'planned');
  const [priority, setPriority] = useState(initial?.priority?.toString() ?? '3');
  const [tags, setTags] = useState(initial?.tags.join(', ') ?? '');
  const [plannedFor, setPlannedFor] = useState(initial?.plannedFor ?? '');
  const [error, setError] = useState('');

  async function submit(event: FormEvent) {
    event.preventDefault(); setError('');
    const parsedTags = tags.split(',').map(item => item.trim()).filter(Boolean);
    if (parsedTags.length > 10 || parsedTags.some(item => item.length > 32)) {
      setError('Maximal 10 Tags mit jeweils höchstens 32 Zeichen.'); return;
    }
    try {
      await onSave({
        ...emptyTask, title: title.trim(), description, status,
        priority: priority === '' ? null : Number(priority),
        tags: parsedTags, plannedFor: plannedFor || null,
      });
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Speichern fehlgeschlagen.');
    }
  }

  return <section className="content-card tasks-editor" aria-label={initial ? 'Aufgabe bearbeiten' : 'Neue Aufgabe'}>
    <div className="tasks-editor-head">
      <h2>{initial ? 'Aufgabe bearbeiten' : 'Neue Aufgabe'}</h2>
      <button className="button button-secondary" type="button" onClick={onCancel} aria-label="Schließen">
        <X size={17}/>
      </button>
    </div>
    <form className="um-form" onSubmit={event => void submit(event)}>
      <label htmlFor="task-title">Titel</label>
      <input id="task-title" maxLength={255} minLength={1} required value={title}
        onChange={e => setTitle(e.target.value)} placeholder="Was steht an?"/>
      <label htmlFor="task-description">Beschreibung</label>
      <textarea id="task-description" rows={3} maxLength={10000}
        value={description} onChange={e => setDescription(e.target.value)}
        placeholder="Details, Notizen, nächster Schritt …"/>
      <div className="tasks-editor-fields">
        <div className="um-form">
          <label htmlFor="task-state">Status</label>
          <select id="task-state" value={status}
            onChange={e => setStatus(e.target.value as TaskStatus)}>
            {statusOptions.map(item => <option key={item} value={item}>{taskStatusLabel[item]}</option>)}
          </select>
        </div>
        <div className="um-form">
          <label htmlFor="task-priority">Priorität</label>
          <select id="task-priority" value={priority} onChange={e => setPriority(e.target.value)}>
            <option value="">Keine</option>
            <option value="1">1 · Sehr hoch</option>
            <option value="2">2 · Hoch</option>
            <option value="3">3 · Normal</option>
            <option value="4">4 · Niedrig</option>
            <option value="5">5 · Sehr niedrig</option>
          </select>
        </div>
        <div className="um-form">
          <label htmlFor="task-date">Geplant für</label>
          <input id="task-date" type="date" value={plannedFor}
            onChange={e => setPlannedFor(e.target.value)}/>
        </div>
      </div>
      <label htmlFor="task-tags">Tags (kommagetrennt)</label>
      <input id="task-tags" value={tags} maxLength={340}
        onChange={e => setTags(e.target.value)} placeholder="z. B. IT, Kunde, Wichtig"/>
      <p className="um-hint">Die Aufgabe wird nur in deinem Konto gespeichert. Keine Demo-Daten.</p>
      {error && <p role="alert" className="um-feedback failure">{error}</p>}
      <div className="tasks-editor-buttons">
        <button disabled={saving} type="submit" className="button button-primary">
          <CheckCircle2 size={16}/>{saving ? 'Speichere …' : 'Aufgabe speichern'}
        </button>
        <button type="button" className="button button-secondary" onClick={onCancel}>Abbrechen</button>
      </div>
    </form>
  </section>;
}

function TaskCard({ task, onEdit, onStatus, onDelete, busy = false }: {
  task: WorkTask;
  onEdit?: () => void;
  onStatus: (status: TaskStatus) => void;
  onDelete?: () => void;
  busy?: boolean;
}) {
  const completed = task.status === 'completed';
  return <article className="content-card tasks-card">
    <div className="tasks-card-top">
      <div className="tasks-card-main">
        <span className={`tasks-state tasks-state-${task.status}`}>
          {completed ? <CheckCircle2 size={14}/> : <Circle size={14}/>}
          {taskStatusLabel[task.status]}
        </span>
        <h3>{task.title}</h3>
        {task.description && <p className="tasks-description">{task.description}</p>}
        <div className="tasks-tags">
          {task.priority !== null && <span className={task.priority <= 2 ? 'tasks-priority-high' : ''}>
            Priorität {task.priority}
          </span>}
          {task.plannedFor && <span><CalendarDays size={13}/>{new Intl.DateTimeFormat('de-DE').format(new Date(task.plannedFor + 'T12:00:00'))}</span>}
          {task.tags.map(tag => <span key={tag}>#{tag}</span>)}
        </div>
      </div>
      <div className="tasks-actions">
        {onEdit && <button className="button button-secondary" type="button" disabled={busy}
          onClick={onEdit} aria-label={`${task.title} bearbeiten`}><Pencil size={15}/></button>}
        {task.status === 'planned' && <button className="button button-secondary" type="button" disabled={busy}
          onClick={() => onStatus('active')}><Play size={15}/>Starten</button>}
        {!completed && task.status !== 'cancelled' && <button className="button button-primary" type="button" disabled={busy}
          onClick={() => onStatus('completed')}><CheckCircle2 size={15}/>Erledigen</button>}
        {(completed || task.status === 'cancelled') && <button className="button button-secondary" type="button" disabled={busy}
          onClick={() => onStatus('planned')}><RotateCcw size={15}/>Wieder öffnen</button>}
        {onDelete && <button className="button button-secondary" type="button" disabled={busy}
          onClick={onDelete} aria-label={`${task.title} löschen`}><Trash2 size={15}/></button>}
      </div>
    </div>
  </article>;
}

export function TasksPage() {
  const [tasks, setTasks] = useState<WorkTask[]>([]);
  const [query, setQuery] = useState('');
  const [filter, setFilter] = useState('');
  const [editor, setEditor] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  const load = useCallback(async () => {
    const result = await listTasks(query, filter);
    setTasks(result);
    return result;
  }, [query, filter]);

  useEffect(() => {
    let active = true;
    setLoading(true);
    void listTasks(query, filter).then(result => { if (active) { setTasks(result); setLoading(false); } })
      .catch(reason => { if (active) { setError(reason instanceof Error ? reason.message : 'Aufgaben nicht erreichbar.'); setLoading(false); } });
    return () => { active = false; };
  }, [query, filter]);

  async function save(value: TaskInput) {
    if (busy) return;
    setBusy(true); setError(''); setNotice('');
    try {
      if (editor && editor !== 'new') await updateTask(editor, value);
      else await createTask(value);
      await load(); setEditor(null); setNotice('Aufgabe gespeichert.');
    } catch (reason) {
      throw reason;
    } finally { setBusy(false); }
  }

  async function change(id: string, status: TaskStatus) {
    if (busy) return;
    setBusy(true); setError(''); setNotice('');
    try {
      await changeTaskStatus(id, status);
      await load();
      setNotice(status === 'completed' ? 'Aufgabe erledigt.' : 'Aufgabenstatus aktualisiert.');
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Änderung fehlgeschlagen.'); }
    finally { setBusy(false); }
  }

  async function remove(task: WorkTask) {
    if (busy || !window.confirm(`Aufgabe „${task.title}“ dauerhaft löschen?`)) return;
    setBusy(true); setError(''); setNotice('');
    try {
      await deleteTask(task.id); await load();
      setEditor(null); setNotice('Aufgabe gelöscht.');
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Löschen fehlgeschlagen.'); }
    finally { setBusy(false); }
  }

  const current = tasks.find(item => item.id === editor);
  return <div className="page">
    <div className="page-heading tasks-head">
      <div><span className="eyebrow">PLENARO / KERNAUFGABEN</span>
        <h1>Aufgaben</h1>
        <p>Deine Aufgaben planen, bearbeiten, erledigen und wieder öffnen.</p></div>
      <button className="button button-primary" type="button"
        onClick={() => { setEditor(editor === 'new' ? null : 'new'); setError(''); }}>
        <Plus size={17}/>Neue Aufgabe
      </button>
    </div>
    {notice && <p className="um-feedback success" role="status">{notice}</p>}
    {error && <p className="um-feedback failure" role="alert">{error}</p>}
    {editor && <TaskEditor key={editor} initial={current} saving={busy}
      onSave={save} onCancel={() => setEditor(null)}/>}
    <section className="content-card tasks-filters" aria-label="Aufgabenfilter">
      <div className="tasks-search"><Search size={17}/><input aria-label="Aufgaben suchen" placeholder="Titel oder Beschreibung suchen …"
        value={query} onChange={e => setQuery(e.target.value)} maxLength={120}/></div>
      <div className="tasks-select"><ListFilter size={17}/>
        <select aria-label="Aufgabenstatus filtern" value={filter} onChange={e => setFilter(e.target.value)}>
          <option value="">Alle Aufgaben</option>
          {statusOptions.map(item => <option key={item} value={item}>{taskStatusLabel[item]}</option>)}
        </select>
      </div>
    </section>
    {loading ? <p role="status">Lade Aufgaben …</p> : <div className="tasks-list">
      {tasks.length === 0 ? <section className="content-card tasks-empty">
        <ClipboardList size={30}/><h2>Keine Aufgaben gefunden</h2>
        <p>{query || filter ? 'Ändere deine Filter oder die Suche.' : 'Lege deine erste Aufgabe an, um mit Plenaro zu starten.'}</p>
        {!query && !filter && <button className="button button-primary" type="button"
          onClick={() => setEditor('new')}><Plus size={16}/>Erste Aufgabe anlegen</button>}
      </section> : tasks.map(task => <TaskCard key={task.id} task={task} busy={busy}
        onEdit={() => setEditor(task.id)}
        onStatus={status => void change(task.id, status)}
        onDelete={() => void remove(task)}/>)}
      {tasks.length >= 200 && <p className="um-hint">Es werden maximal 200 Aufgaben angezeigt. Nutze die Suche oder den Statusfilter.</p>}
    </div>}
  </div>;
}

export function TodayTasksPage() {
  const [tasks, setTasks] = useState<WorkTask[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [refresh, setRefresh] = useState(0);
  const today = localDate();

  useEffect(() => {
    let active = true;
    setLoading(true);
    void todayTasks(today).then(result => { if (active) { setTasks(result); setLoading(false); } })
      .catch(reason => { if (active) { setError(reason instanceof Error ? reason.message : 'Heute konnte nicht geladen werden.'); setLoading(false); } });
    return () => { active = false; };
  }, [today, refresh]);

  async function change(id: string, status: TaskStatus) {
    if (busy) return;
    setBusy(true); setError('');
    try {
      await changeTaskStatus(id, status);
      setTasks(await todayTasks(today));
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Änderung fehlgeschlagen.'); }
    finally { setBusy(false); }
  }

  const sections = [
    { name: 'Aktuell', items: tasks.filter(task => task.status === 'active') },
    { name: 'Geplant', items: tasks.filter(task => task.status === 'planned') },
    { name: 'Erledigt', items: tasks.filter(task => task.status === 'completed') },
  ];
  return <div className="page">
    <div className="page-heading tasks-head">
      <div><span className="eyebrow">PLENARO / TAGESÜBERSICHT</span>
        <h1>Heute</h1>
        <p>{new Intl.DateTimeFormat('de-DE', { dateStyle: 'full' }).format(new Date())} · Deine geplanten und laufenden Aufgaben.</p></div>
      <Link className="button button-primary" to="/tasks"><Plus size={17}/>Aufgabe anlegen</Link>
    </div>
    <DayMarkersPanel/>
    {error && <p className="um-feedback failure" role="alert">{error}</p>}
    {loading ? <p role="status">Lade Tagesübersicht …</p> : <div className="tasks-today-grid">
      {sections.map(section => <section key={section.name} className="tasks-today-section" aria-label={section.name}>
        <div className="tasks-today-heading"><h2>{section.name}</h2><span>{section.items.length}</span></div>
        {section.items.length === 0 ? <div className="content-card tasks-today-empty">Keine Aufgaben</div> :
          section.items.map(task => <TaskCard key={task.id} task={task} busy={busy}
            onStatus={status => void change(task.id, status)}/>)}
      </section>)}
      <div className="tasks-today-footer">
        <Link to="/tasks" className="text-link">Alle Aufgaben anzeigen</Link>
        <button type="button" className="button button-secondary"
          onClick={() => setRefresh(value => value + 1)}>Aktualisieren</button>
      </div>
    </div>}
  </div>;
}
