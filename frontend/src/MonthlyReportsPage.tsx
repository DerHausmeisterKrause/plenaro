import { useEffect, useMemo, useState } from 'react';
import { CalendarDays, ChevronLeft, ChevronRight, Clock3, Download, FileSpreadsheet, ListChecks, RefreshCw } from 'lucide-react';

type Day = { taskId: string; taskTitle: string; date: string; trackedSeconds: number; adjustmentMinutes: number };
type Report = {
  year: number; month: number; timeZone: string;
  trackedSeconds: number; adjustmentMinutes: number; netSeconds: number;
  completedTasks: number; plannedSegments: number; plannedSegmentSeconds: number;
  ulDays: number; amDays: number; brDays: number; hoDays: number; days: Day[];
};

const zone = Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
const initialMonth = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
};
const query = (month: string) => {
  const [year, m] = month.split('-').map(Number);
  return new URLSearchParams({ year: String(year), month: String(m), timeZone: zone }).toString();
};
const shift = (month: string, amount: number) => {
  const [year, m] = month.split('-').map(Number);
  const date = new Date(year, m - 1 + amount, 1);
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`;
};
const duration = (seconds: number) => {
  const sign = seconds < 0 ? '−' : '';
  const abs = Math.abs(Math.round(seconds));
  return `${sign}${Math.floor(abs / 3600)}:${String(Math.floor((abs % 3600) / 60)).padStart(2, '0')} h`;
};
const monthTitle = (month: string) => {
  const [year, m] = month.split('-').map(Number);
  return new Intl.DateTimeFormat('de-DE', { month: 'long', year: 'numeric' }).format(new Date(year, m - 1, 1));
};

export function MonthlyReportsPage() {
  const [month, setMonth] = useState(initialMonth);
  const [report, setReport] = useState<Report | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [refresh, setRefresh] = useState(0);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setReport(null);
    setError('');
    void fetch('/api/v1/reports/monthly?' + query(month), {
      credentials: 'same-origin', cache: 'no-store',
    }).then(async response => {
      if (!response.ok) throw new Error('Monatsbericht konnte nicht geladen werden.');
      return await response.json() as Report;
    }).then(data => { if (active) { setReport(data); setLoading(false); } })
      .catch(reason => { if (active) { setError(reason instanceof Error ? reason.message : 'Report nicht erreichbar.'); setLoading(false); } });
    return () => { active = false; };
  }, [month, refresh]);

  const tasks = useMemo(() => {
    const map = new Map<string, { id: string; title: string; seconds: number; corrections: number }>();
    for (const day of report?.days ?? []) {
      const previous = map.get(day.taskId);
      if (previous) {
        previous.seconds += day.trackedSeconds;
        previous.corrections += day.adjustmentMinutes;
      } else {
        map.set(day.taskId, { id: day.taskId, title: day.taskTitle,
          seconds: day.trackedSeconds, corrections: day.adjustmentMinutes });
      }
    }
    return [...map.values()].sort((a, b) =>
      (b.seconds + b.corrections * 60) - (a.seconds + a.corrections * 60));
  }, [report]);

  function change(next: string) {
    if (next >= '2000-01' && next <= '2099-12') setMonth(next);
  }

  return <div className="page report-page">
    <div className="page-heading report-top">
      <div><span className="eyebrow">PLENARO / AUSWERTUNGEN</span>
        <h1>Monatsberichte</h1>
        <p>Deine tatsächlich gebuchten Aufgabenzeiten, manuelle Korrekturen und Tagesmarker. Ohne Outlook-Anbindung.</p>
      </div>
      <div className="report-controls">
        <button type="button" className="button button-secondary" aria-label="Vorheriger Monat"
          onClick={() => change(shift(month, -1))}><ChevronLeft size={17}/></button>
        <input type="month" aria-label="Berichtsmonat" min="2000-01" max="2099-12"
          value={month} onChange={event => change(event.target.value)}/>
        <button type="button" className="button button-secondary" aria-label="Nächster Monat"
          onClick={() => change(shift(month, 1))}><ChevronRight size={17}/></button>
        <button type="button" className="button button-secondary" aria-label="Bericht aktualisieren"
          onClick={() => setRefresh(value => value + 1)}><RefreshCw size={17}/></button>
      </div>
    </div>
    <div className="report-heading-row">
      <h2>{monthTitle(month)}</h2>
      {report && <a className="button button-primary" href={'/api/v1/reports/monthly.csv?' + query(month)}>
        <Download size={16}/>CSV exportieren
      </a>}
    </div>
    <p className="um-hint">Zeitzone: {zone} · Gebuchte Zeit = abgeschlossene Timer-Sitzungen; laufende Timer und Pausen werden nicht gezählt.</p>
    {error && <p className="um-feedback failure" role="alert">{error}</p>}
    {loading && <p role="status">Lade Monatsbericht …</p>}
    {report && <>
      <div className="report-kpis">
        <article className="content-card report-kpi">
          <span><Clock3 size={17}/>Netto-Aufgabenzeit</span>
          <strong>{duration(report.netSeconds)}</strong>
          <small>Timer: {duration(report.trackedSeconds)} · Korrektur: {report.adjustmentMinutes > 0 ? '+' : ''}{report.adjustmentMinutes} Min.</small>
        </article>
        <article className="content-card report-kpi">
          <span><ListChecks size={17}/>Aufgaben erledigt</span>
          <strong>{report.completedTasks}</strong>
          <small>Abschlussdatum in diesem Monat</small>
        </article>
        <article className="content-card report-kpi">
          <span><CalendarDays size={17}/>Geplante Segmente</span>
          <strong>{report.plannedSegments}</strong>
          <small>{duration(report.plannedSegmentSeconds)} geplant – nicht gebucht</small>
        </article>
        <article className="content-card report-kpi">
          <span><FileSpreadsheet size={17}/>Tagesmarker</span>
          <strong>{report.ulDays + report.amDays + report.brDays + report.hoDays}</strong>
          <small>UL {report.ulDays} · AM {report.amDays} · BR {report.brDays} · HO {report.hoDays}</small>
        </article>
      </div>
      <section className="content-card report-section" aria-labelledby="report-tasks">
        <div className="report-section-head"><h2 id="report-tasks">Zeit nach Aufgaben</h2><span>{tasks.length} Aufgaben</span></div>
        {tasks.length === 0 ? <p className="report-empty">In diesem Monat sind keine abgeschlossenen Timer-Sitzungen oder Zeitkorrekturen vorhanden.</p>
          : <div className="report-table-scroll"><table className="report-table">
            <thead><tr><th>Aufgabe</th><th>Timer</th><th>Korrektur</th><th>Netto</th></tr></thead>
            <tbody>{tasks.map(task => <tr key={task.id}>
              <td>{task.title}</td><td>{duration(task.seconds)}</td>
              <td>{task.corrections > 0 ? '+' : ''}{task.corrections} Min.</td>
              <td><strong>{duration(task.seconds + task.corrections * 60)}</strong></td>
            </tr>)}</tbody>
          </table></div>}
      </section>
      <section className="content-card report-section" aria-labelledby="report-daily">
        <div className="report-section-head"><h2 id="report-daily">Tagesübersicht</h2><span>{report.days.length} Einträge</span></div>
        {report.days.length === 0 ? <p className="report-empty">Keine Zeitbuchungen oder Korrekturen.</p>
          : <div className="report-table-scroll"><table className="report-table">
            <thead><tr><th>Datum</th><th>Aufgabe</th><th>Timer</th><th>Korrektur</th><th>Netto</th></tr></thead>
            <tbody>{report.days.map(day => <tr key={day.taskId + day.date}>
              <td>{new Intl.DateTimeFormat('de-DE').format(new Date(day.date + 'T12:00:00'))}</td>
              <td>{day.taskTitle}</td><td>{duration(day.trackedSeconds)}</td>
              <td>{day.adjustmentMinutes > 0 ? '+' : ''}{day.adjustmentMinutes} Min.</td>
              <td><strong>{duration(day.trackedSeconds + day.adjustmentMinutes * 60)}</strong></td>
            </tr>)}</tbody>
          </table></div>}
      </section>
      <p className="um-hint">Negative Korrekturen werden nicht ausgeblendet. CSV-Dateien enthalten dieselben Werte in Sekunden bzw. Minuten. Tagesmarker sind keine Arbeitszeitbuchungen.</p>
    </>}
  </div>;
}
