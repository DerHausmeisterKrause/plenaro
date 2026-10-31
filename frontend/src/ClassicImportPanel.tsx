import { useEffect, useState, type ChangeEvent } from 'react';
import { DatabaseBackup, FileJson2, RotateCcw, Upload } from 'lucide-react';
import { ClassicArchiveOverviewPanel, ClassicImportedSettings } from './ClassicArchivePanels';
import {
  commitClassicImport, listClassicImports, parseClassicFile, previewClassicImport,
  undoClassicImport, type ClassicImport, type ImportBatch, type ImportPreview,
} from './classicImportApi';

export function ClassicImportPanel() {
  const [document, setDocument] = useState<ClassicImport | null>(null);
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [batches, setBatches] = useState<ImportBatch[]>([]);
  const [refreshVersion, setRefreshVersion] = useState(0);
  const [settingsBatch, setSettingsBatch] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  async function refreshBatches() {
    try { setBatches(await listClassicImports()); setRefreshVersion(value => value + 1); }
    catch (e) { setError(e instanceof Error ? e.message : 'Importverlauf nicht verfügbar.'); }
  }
  useEffect(() => { void refreshBatches(); }, []);

  async function chooseFile(event: ChangeEvent<HTMLInputElement>) {
    setDocument(null); setPreview(null); setError(''); setNotice('');
    const file = event.target.files?.[0];
    if (!file) return;
    if (file.size > 12 * 1024 * 1024) {
      setError('Importdatei ist größer als 12 MiB.'); return;
    }
    setBusy(true);
    try {
      const parsed = parseClassicFile(await file.text());
      const result = await previewClassicImport(parsed);
      setDocument(parsed); setPreview(result);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Importvorschau fehlgeschlagen.');
    } finally { setBusy(false); }
  }

  async function commit() {
    if (!document || !preview || busy) return;
    if (!window.confirm('Altdaten jetzt für dein Benutzerkonto importieren? Bitte vorher PostgreSQL-Backup erstellen.')) return;
    setBusy(true); setError(''); setNotice('');
    try {
      const result = await commitClassicImport(document);
      setNotice(`Import abgeschlossen: ${result.inserted} neue Datensätze, ${result.skipped} bereits vorhanden oder übersprungen. Import-ID: ${result.batchId}`);
      setDocument(null); setPreview(null);
      await refreshBatches();
    } catch (e) { setError(e instanceof Error ? e.message : 'Import fehlgeschlagen.'); }
    finally { setBusy(false); }
  }

  async function rollback(id: string) {
    if (busy || !window.confirm('Diesen Import rückgängig machen? Plenaro bricht ab, falls importierte Daten inzwischen bearbeitet wurden oder abhängige Daten existieren.')) return;
    setBusy(true); setError(''); setNotice('');
    try {
      await undoClassicImport(id);
      setNotice('Import wurde vollständig rückgängig gemacht.');
      await refreshBatches();
    } catch (e) { setError(e instanceof Error ? e.message : 'Rücknahme nicht möglich.'); }
    finally { setBusy(false); }
  }

  return <section className="content-card um-panel classic-import-panel" aria-label="Classic-Import">
    <div className="um-section-title"><DatabaseBackup size={20}/><h2>Desktop-Daten importieren</h2></div>
    <p>Übernimm Aufgaben, Zeitsegmente, abgeschlossene Aufgaben-Zeitbuchungen und Tagesmarker
      aus der SQLite-Datenbank von Plenaro Classic. Nur für dein Benutzerkonto.</p>
    <p className="um-hint">Der Konverter liegt im GitHub-Repository unter <code>tools/export_classic.py</code>.
      Er liest eine Kopie von <code>TaskTool.db</code> lokal und bereinigt die
      <code> settings.json</code>. Die rohe SQLite-Datei und Passwörter werden nicht hochgeladen.</p>
    <pre className="classic-import-command">python tools/export_classic.py TaskTool.db -s settings.json -o plenaro-import.json</pre>
    <label className="classic-import-upload"><FileJson2 size={16}/>Bereinigte Importdatei auswählen
      <input type="file" accept=".json,application/json" disabled={busy} onChange={e => void chooseFile(e)}/>
    </label>
    {busy && <p role="status">Importdaten werden geprüft oder verarbeitet …</p>}
    {error && <p role="alert" className="um-feedback failure">{error}</p>}
    {notice && <p role="status" className="um-feedback success">{notice}</p>}
    {preview && <>
      <h3>Importvorschau · Desktop-Schema v{document?.schemaVersion}</h3>
      <div className="classic-import-summary">
        <span>Aufgaben <strong>{preview.newTasks} / {preview.taskCount}</strong></span>
        <span>Zeitsegmente <strong>{preview.newSegments} / {preview.segmentCount}</strong></span>
        <span>Zeitbuchungen <strong>{preview.newTimeEntries} / {preview.timeCount}</strong></span>
        <span>Tagesmarker <strong>{preview.newMarkers} / {preview.markerCount}</strong></span>
        {(preview.metadataCount ?? 0) > 0 && <span>Classic-Feldinformationen <strong>{preview.newMetadata} / {preview.metadataCount}</strong></span>}
        {(preview.bookingCount ?? 0) > 0 && <span>Historische Ticketbuchungen <strong>{preview.newBookings} / {preview.bookingCount}</strong></span>}
        {(preview.monthlyCount ?? 0) > 0 && <span>Alte Monatskorrekturen <strong>{preview.newMonthly} / {preview.monthlyCount}</strong></span>}
      </div>
      <p className="um-hint">Jeweils: neu / im Export. Bereits importierte Einträge werden nicht doppelt angelegt.
        Bestehende Webdaten werden nicht überschrieben.</p>
      <p>{preview.reminderLeadMinutes !== null
        ? `Erinnerungsvorlauf aus Classic: ${preview.reminderLeadMinutes} Minuten (wird übernommen).`
        : 'Keine kompatible Reminder-Einstellung enthalten.'}</p>
      <p className="um-hint">Die Vorschau ist noch keine Buchung. Bei Konflikten wird der gesamte Import zurückgerollt.</p>
      {preview.warnings.length > 0 && <details className="classic-import-warnings">
        <summary>{preview.warnings.length} Importhinweise anzeigen</summary>
        <ul>{preview.warnings.slice(0, 50).map((message, i) => <li key={i}>{message}</li>)}</ul>
      </details>}
      <button type="button" className="button button-primary" disabled={busy} onClick={() => void commit()}>
        <Upload size={16}/>Import ausführen
      </button>
    </>}
    <ClassicArchiveOverviewPanel refreshVersion={refreshVersion}/>
    <h3>Importverlauf und sichere Rücknahme</h3>
    {batches.length === 0 ? <p className="um-hint">Noch kein Import ausgeführt.</p> :
      <div className="classic-import-batches">{batches.map(batch =>
        <div className="classic-import-batch" key={batch.id}>
          <span><strong>{new Intl.DateTimeFormat('de-DE',{dateStyle:'short',timeStyle:'short'}).format(new Date(batch.createdAt))}</strong>
            <small>{batch.rowCount} Datensätze · Quelle: {batch.sourceId}</small></span>
          <div className="classic-archive-batch-actions">
            <button className="button button-secondary" type="button"
              onClick={() => setSettingsBatch(current => current === batch.id ? null : batch.id)}>
              {settingsBatch === batch.id ? 'Einstellungen schließen' : 'Archivierte Einstellungen'}
            </button>
            <button className="button button-secondary" type="button" disabled={busy}
              onClick={() => void rollback(batch.id)}><RotateCcw size={14}/>Rückgängig</button>
          </div>
          {settingsBatch === batch.id && <ClassicImportedSettings batchId={batch.id}/>} 
        </div>)}</div>}
    <p className="um-hint">Rücknahme ist nur möglich, wenn die importierten Datensätze unverändert sind und keine
      weiteren Zeitdaten an diesen Aufgaben hängen. Vor dem Import zusätzlich eine Datenbanksicherung erstellen.</p>
    <p className="um-hint">Noch nicht migriert: Ticketbuchungen, Outlook-Verknüpfungen, Wiki-Caches,
      Zugangsdaten, offene Timer und Anwesenheits-/Pausenzeiten. Historische Ticketbuchungen,
      Classic-Feldinformationen und Monatskorrekturen stehen im Archiv, sind aber nicht aktiv.</p>
  </section>;
}
