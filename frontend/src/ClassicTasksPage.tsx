import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { CalendarDays, CheckCircle2, ClipboardList, Pencil, Play, Plus, RotateCcw, Search, Trash2, X } from 'lucide-react';
import {
  changeTaskStatus, createTask, deleteTask, listTasks, taskStatusLabel, updateTask,
  type TaskInput, type TaskStatus, type WorkTask,
} from './tasksApi';

const statuses: TaskStatus[] = ['planned', 'active', 'completed', 'cancelled'];
const newTask = (): TaskInput => ({
  title: '', description: '', status: 'planned', priority: 3, tags: [], plannedFor: null,
});

function TaskForm({ task, saving, onCancel, onSave }: {
  task?: WorkTask;
  saving: boolean;
  onCancel: () => void;
  onSave: (data: TaskInput) => Promise<void>;
}) {
  const [title, setTitle] = useState(task?.title ?? '');
  const [description, setDescription] = useState(task?.description ?? '');
  const [status, setStatus] = useState<TaskStatus>(task?.status ?? 'planned');
  const [priority, setPriority] = useState(task?.priority === null ? '' : String(task?.priority ?? 3));
  const [date, setDate] = useState(task?.plannedFor ?? '');
  const [tags, setTags] = useState(task?.tags.join(', ') ?? '');
  const [error, setError] = useState('');

  async function submit(e: FormEvent) {
    e.preventDefault(); setError('');
    const values = [...new Set(tags.split(',').map(s => s.trim()).filter(Boolean))];
    if (values.length > 10 || values.some(s => s.length > 32)) {
      setError('Maximal zehn Tags mit höchstens 32 Zeichen pro Tag.'); return;
    }
    try {
      await onSave({
        title: title.trim(), description, status,
        priority: priority ? Number(priority) : null,
        tags: values, plannedFor: date || null,
      });
    } catch (e) { setError(e instanceof Error ? e.message : 'Speichern fehlgeschlagen.'); }
  }

  return <form onSubmit={e => void submit(e)} className="classic-task-editor um-form">
    <div className="classic-detail-heading">
      <h2>{task ? 'Aufgabe bearbeiten' : 'Neue Aufgabe'}</h2>
      <button type="button" className="button button-secondary" onClick={onCancel} aria-label="Editor schließen"><X size={17}/></button>
    </div>
    <label htmlFor="classic-title">Titel</label>
    <input id="classic-title" aria-label="Titel" required minLength={1} maxLength={255} autoFocus
      value={title} onChange={e => setTitle(e.target.value)} placeholder="Was muss erledigt werden?"/>
    <label htmlFor="classic-description">Beschreibung</label>
    <textarea id="classic-description" rows={6} maxLength={10000}
      value={description} onChange={e => setDescription(e.target.value)}/>
    <div className="classic-form-grid">
      <div><label htmlFor="classic-status">Status</label>
        <select id="classic-status" value={status} onChange={e => setStatus(e.target.value as TaskStatus)}>
          {statuses.map(v => <option value={v} key={v}>{taskStatusLabel[v]}</option>)}
        </select>
      </div>
      <div><label htmlFor="classic-priority">Priorität</label>
        <select id="classic-priority" value={priority} onChange={e => setPriority(e.target.value)}>
          <option value="">Keine</option>
          {[1,2,3,4,5].map(v => <option key={v} value={v}>{v}</option>)}
        </select>
      </div>
      <div><label htmlFor="classic-date">Geplant für</label>
        <input id="classic-date" type="date" value={date} onChange={e => setDate(e.target.value)}/>
      </div>
    </div>
    <label htmlFor="classic-tags">Tags (mit Kommas trennen)</label>
    <input id="classic-tags" maxLength={340} value={tags} onChange={e => setTags(e.target.value)} placeholder="Kunde, Wichtig, Projekt"/>
    {error && <p role="alert" className="um-feedback failure">{error}</p>}
    <div className="classic-detail-actions">
      <button type="submit" className="button button-primary" disabled={saving}><CheckCircle2 size={16}/>{saving ? 'Speichern …' : 'Speichern'}</button>
      <button type="button" className="button button-secondary" onClick={onCancel}>Abbrechen</button>
    </div>
  </form>;
}

/** Classic task-tool pattern: compact list on the left, selected record on the right. */
export function ClassicTasksPage() {
  const [tasks, setTasks] = useState<WorkTask[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [mode, setMode] = useState<'view' | 'edit' | 'new'>('view');
  const [query, setQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [onlyOpen, setOnlyOpen] = useState(false);
  const [quickTitle, setQuickTitle] = useState('');
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');

  const refresh = useCallback(async () => {
    const result = await listTasks(query, statusFilter);
    setTasks(result);
    setSelectedId(id => id && result.some(t => t.id === id) ? id : result[0]?.id ?? null);
    return result;
  }, [query, statusFilter]);

  useEffect(() => {
    let active = true;
    setLoading(true);
    void listTasks(query, statusFilter).then(data => {
      if (!active) return;
      setTasks(data);
      setSelectedId(id => id && data.some(t => t.id === id) ? id : data[0]?.id ?? null);
      setLoading(false);
    }).catch(e => { if (active) { setError(e instanceof Error ? e.message : 'Laden fehlgeschlagen.'); setLoading(false); } });
    return () => { active = false; };
  }, [query, statusFilter]);

  const visible = onlyOpen ? tasks.filter(t => t.status !== 'completed' && t.status !== 'cancelled') : tasks;
  const selected = tasks.find(t => t.id === selectedId);
  const openCount = tasks.filter(t => t.status !== 'completed' && t.status !== 'cancelled').length;

  async function run(action: () => Promise<void>, message: string, after?: () => void) {
    if (busy) return;
    setBusy(true); setError(''); setNotice('');
    try {
      await action();
      await refresh();
      after?.();
      setNotice(message);
    } catch (e) { const message = e instanceof Error ? e.message : 'Vorgang fehlgeschlagen.'; setError(message); throw new Error(message); }
    finally { setBusy(false); }
  }

  async function quickAdd(e: FormEvent) {
    e.preventDefault();
    const title = quickTitle.trim();
    if (!title || busy) return;
    try {
      await run(() => createTask({ ...newTask(), title }), 'Aufgabe erstellt.', () => { setQuickTitle(''); setMode('view'); });
    } catch { /* Error is shown above the panes. */ }
  }

  async function save(input: TaskInput) {
    await run(() => mode === 'edit' && selected
      ? updateTask(selected.id, input) : createTask(input), 'Aufgabe gespeichert.', () => setMode('view'));
  }

  async function statusChange(value: TaskStatus) {
    if (!selected) return;
    try { await run(() => changeTaskStatus(selected.id, value), 'Status aktualisiert.'); }
    catch { /* handled in run */ }
  }

  async function remove() {
    if (!selected || !window.confirm(`Aufgabe „${selected.title}“ endgültig löschen?`)) return;
    try { await run(() => deleteTask(selected.id), 'Aufgabe gelöscht.', () => setMode('view')); }
    catch { /* handled in run */ }
  }

  return <div className="page classic-workspace">
    <div className="page-heading classic-page-heading">
      <div><span className="eyebrow">PLENARO / AUFGABEN</span><h1>Aufgaben</h1>
        <p>Wie im klassischen Plenaro: links auswählen, rechts direkt bearbeiten.</p></div>
      <button type="button" className="button button-primary" onClick={() => setMode('new')}>
        <Plus size={16}/>Neue Aufgabe
      </button>
    </div>
    {error && <p role="alert" className="um-feedback failure">{error}</p>}
    {notice && <p role="status" className="um-feedback success">{notice}</p>}
    <div className="classic-split">
      <section className="classic-list-pane" aria-label="Aufgabenliste">
        <div className="classic-list-header">
          <div><strong>Meine Aufgaben</strong><span>{tasks.length} geladen · {openCount} offen</span></div>
          <button type="button" className="button button-secondary" onClick={() => void refresh().catch(e => setError(String(e)))}
            aria-label="Aufgaben aktualisieren">↻</button>
        </div>
        <form className="classic-quick-add" onSubmit={e => void quickAdd(e)}>
          <input aria-label="Schnell eine Aufgabe hinzufügen" required maxLength={255}
            value={quickTitle} onChange={e => setQuickTitle(e.target.value)}
            placeholder="Neue Aufgabe …"/>
          <button type="submit" className="button button-primary" disabled={busy} aria-label="Aufgabe schnell hinzufügen"><Plus size={18}/></button>
        </form>
        <div className="classic-search">
          <Search size={16}/><input aria-label="Aufgaben suchen" value={query}
            onChange={e => setQuery(e.target.value)} placeholder="Suchen …" maxLength={120}/>
        </div>
        <div className="classic-filters">
          <label><span>Status</span>
            <select value={statusFilter} aria-label="Aufgabenstatus filtern" onChange={e => setStatusFilter(e.target.value)}>
              <option value="">Alle</option>
              {statuses.map(v => <option key={v} value={v}>{taskStatusLabel[v]}</option>)}
            </select>
          </label>
          <label className="classic-inline-check"><input type="checkbox" checked={onlyOpen} onChange={e => setOnlyOpen(e.target.checked)}/>Alle offenen</label>
        </div>
        <div className="classic-list-scroll" role="list">
          {loading && <p role="status" className="classic-empty-list">Lade Aufgaben …</p>}
          {!loading && visible.length === 0 && <div className="classic-empty-list">
            <ClipboardList size={26}/><h2>Keine Aufgaben gefunden</h2>
            <p>{tasks.length === 0 ? 'Erstelle die erste Aufgabe über das Eingabefeld.' : 'Ändere den Filter oder die Suche.'}</p>
          </div>}
          {visible.map(task => <button type="button" role="listitem" key={task.id}
            className={`classic-list-item${selectedId === task.id && mode !== 'new' ? ' active' : ''}`}
            aria-current={selectedId === task.id && mode !== 'new' ? 'true' : undefined}
            onClick={() => { setSelectedId(task.id); setMode('view'); }}>
            <span className="classic-list-item-top">
              <strong>{task.title}</strong>
              {task.priority && <span className="classic-priority">P{task.priority}</span>}
            </span>
            <span className="classic-list-item-bottom">
              <span className={`tasks-state tasks-state-${task.status}`}>{taskStatusLabel[task.status]}</span>
              {task.plannedFor && <time dateTime={task.plannedFor}>{new Intl.DateTimeFormat('de-DE').format(new Date(task.plannedFor + 'T12:00:00'))}</time>}
            </span>
          </button>)}
        </div>
        {tasks.length >= 200 && <p className="um-hint classic-list-limit">Maximal 200 Aufgaben geladen. Suche und filtere für weitere Ergebnisse.</p>}
      </section>

      <section className="classic-detail-pane" aria-label="Aufgabendetails">
        {mode === 'new' ? <TaskForm key="new" saving={busy} onSave={save} onCancel={() => setMode('view')}/> :
          !selected ? <div className="classic-detail-empty"><ClipboardList size={36}/>
            <h2>Aufgabe auswählen</h2>
            <p>Wähle links eine Aufgabe oder erstelle eine neue.</p>
          </div> :
          mode === 'edit' ? <TaskForm key={selected.id} task={selected} saving={busy}
            onSave={save} onCancel={() => setMode('view')}/> :
            <div className="classic-task-detail">
              <div className="classic-detail-heading">
                <div><span className="eyebrow">AUFGABENDETAILS</span><h2>{selected.title}</h2></div>
                <button type="button" className="button button-secondary" onClick={() => setMode('edit')}>
                  <Pencil size={16}/>Bearbeiten
                </button>
              </div>
              <div className="classic-detail-chips">
                <span className={`tasks-state tasks-state-${selected.status}`}>{taskStatusLabel[selected.status]}</span>
                <span>Priorität {selected.priority ?? '–'}</span>
                {selected.plannedFor && <span><CalendarDays size={15}/>{new Intl.DateTimeFormat('de-DE').format(new Date(selected.plannedFor + 'T12:00:00'))}</span>}
              </div>
              <div className="classic-detail-section">
                <h3>Beschreibung</h3>
                <p>{selected.description || 'Noch keine Beschreibung hinterlegt.'}</p>
              </div>
              <div className="classic-detail-section"><h3>Tags</h3>
                <div className="classic-detail-chips">
                  {selected.tags.length ? selected.tags.map(tag => <span key={tag}>#{tag}</span>) : <span>Keine Tags</span>}
                </div>
              </div>
              <div className="classic-detail-actions">
                {selected.status === 'planned' && <button className="button button-secondary" type="button" disabled={busy}
                  onClick={() => void statusChange('active')}><Play size={16}/>Starten</button>}
                {selected.status !== 'completed' && selected.status !== 'cancelled' &&
                  <button className="button button-primary" type="button" disabled={busy}
                    onClick={() => void statusChange('completed')}><CheckCircle2 size={16}/>Erledigen</button>}
                {(selected.status === 'completed' || selected.status === 'cancelled') &&
                  <button className="button button-secondary" type="button" disabled={busy}
                    onClick={() => void statusChange('planned')}><RotateCcw size={16}/>Wieder öffnen</button>}
                <button type="button" className="button button-secondary" disabled={busy} onClick={() => void remove()}>
                  <Trash2 size={16}/>Löschen
                </button>
              </div>
              <p className="um-hint">Nur du kannst diese Aufgabe sehen und bearbeiten.</p>
            </div>}
      </section>
    </div>
  </div>;
}
