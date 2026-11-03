import { useEffect, useState } from 'react';
import { Download, RefreshCw } from 'lucide-react';
import { downloadMonthCsv, readMonthReport, type MonthReport } from './reportsApi';

function currentMonth(): string {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
}

function duration(seconds: number): string {
  const safe = Math.max(0, seconds);
  return (safe / 3600).toLocaleString('de-DE', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' h';
}

export function ReportsPage() {
  const [month, setMonth] = useState(currentMonth);
  const [report, setReport] = useState<MonthReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setError(''); setReport(null);
    void readMonthReport(month, controller.signal).then(data => {
      if (!controller.signal.aborted) setReport(data);
    }).catch(err => {
      if (!controller.signal.aborted) setError(err instanceof Error ? err.message : 'Fehler beim Laden.');
    }).finally(() => {
      if (!controller.signal.aborted) setLoading(false);
    });
    return () => controller.abort();
  }, [month]);

  async function exportCsv() {
    setExporting(true); setError('');
    try { await downloadMonthCsv(month); }
    catch (err) { setError(err instanceof Error ? err.message : 'CSV-Export fehlgeschlagen.'); }
    finally { setExporting(false); }
  }

  return <div className="page plenaro-reports">
    <div className="page-heading">
      <span className="eyebrow">PLENARO / AUSWERTUNG</span>
      <h1>Monatsberichte</h1>
      <p>Deine aufgabenbezogenen Timerzeiten und manuellen Korrekturen. Keine Anwesenheit, Pausen oder Sollzeiten.</p>
    </div>

    <section className="content-card reports-filter" aria-label="Berichtszeitraum">
      <label htmlFor="report-month">Monat auswählen</label>
      <input type="month" id="report-month" min="2000-01" max="2100-12"
        value={month} onChange={e => { if (e.target.value) setMonth(e.target.value); }}/>
      <button className="button button-secondary" type="button" disabled={!report || exporting || loading}
        onClick={() => void exportCsv()}>
        <Download size={17} aria-hidden="true" />
        {exporting ? 'Exportiere …' : 'CSV exportieren'}
      </button>
    </section>

    {error && <p className="um-feedback failure" role="alert">{error}</p>}
    {loading && <div className="content-card reports-empty" role="status"><RefreshCw size={18}/> Monatsbericht wird geladen …</div>}
    {!loading && report && <>
      <div className="reports-summary" aria-label="Monatsübersicht">
        <div className="content-card"><span>Gesamte Aufgabenzeit</span><strong>{duration(report.totalSeconds)}</strong></div>
        <div className="content-card"><span>Timerzeit</span><strong>{duration(report.timerSeconds)}</strong></div>
        <div className="content-card"><span>Manuelle Korrekturen</span><strong>{report.adjustmentMinutes > 0 ? '+' : ''}{report.adjustmentMinutes} Min.</strong></div>
        <div className="content-card"><span>Aufgaben mit Aktivität</span><strong>{report.taskCount}</strong></div>
      </div>
      <section className="content-card reports-table-card" aria-label="Aufgabenzeiten im Monat">
        <h2>Aufgaben im {new Intl.DateTimeFormat('de-DE', { month: 'long', year: 'numeric', timeZone: 'UTC' })
          .format(new Date(month + '-01T12:00:00Z'))}</h2>
        {report.tasks.length === 0
          ? <p className="reports-empty">In diesem Monat sind keine aufgabenbezogenen Zeiten oder Korrekturen erfasst.</p>
          : <div className="reports-table-scroll">
            <table className="reports-table">
              <thead><tr><th>Aufgabe</th><th>Status</th><th>Timer</th><th>Korrektur</th><th>Gesamt</th><th>Sitzungen</th></tr></thead>
              <tbody>{report.tasks.map(task =>
                <tr key={task.taskId}>
                  <td>{task.title}</td><td>{task.status}</td><td>{duration(task.timerSeconds)}</td>
                  <td>{task.adjustmentMinutes > 0 ? '+' : ''}{task.adjustmentMinutes} Min.</td>
                  <td><strong>{duration(task.totalSeconds)}</strong></td><td>{task.sessionCount}</td>
                </tr>)}</tbody>
              <tfoot><tr><th>Summe</th><td></td><td>{duration(report.timerSeconds)}</td>
                <td>{report.adjustmentMinutes} Min.</td><td>{duration(report.totalSeconds)}</td><td>{report.sessionCount}</td></tr></tfoot>
            </table>
          </div>}
      </section>
      <p className="um-hint reports-note">Monatsgrenzen: Europe/Berlin. Übergreifende Timer-Sitzungen werden dem jeweiligen Monat anteilig zugerechnet. Laufende Timer sind bis zum Abrufzeitpunkt enthalten. Korrekturen zählen im Monat ihrer Erfassung. Negative Gesamtdauern pro Aufgabe werden auf null begrenzt; deshalb kann die Summe der Aufgabengesamtzeiten von Timerzeit plus Korrektursumme abweichen. Gelöschte Aufgaben werden nicht als Tätigkeitsposition rekonstruiert.</p>
    </>}
  </div>;
}
