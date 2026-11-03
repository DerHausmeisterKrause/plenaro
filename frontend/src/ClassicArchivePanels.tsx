import { useEffect, useState } from 'react';
import { Archive } from 'lucide-react';
import {
  readTaskArchive, readArchiveOverview, readImportedSettings, readClassicMonthly,
  type ClassicArchiveOverview, type ClassicTaskArchive, type ClassicMonthlyAdjustment,
} from './classicImportApi';

export function ClassicTaskArchivePanel({ taskId }: { taskId: string }) {
  const [data, setData] = useState<ClassicTaskArchive | null>(null);
  const [error, setError] = useState('');
  useEffect(() => {
    let active = true;
    setData(null); setError('');
    void readTaskArchive(taskId).then(result => { if (active) setData(result); })
      .catch(() => { if (active) setError('Classic-Archiv konnte nicht geladen werden.'); });
    return () => { active = false; };
  }, [taskId]);

  if (error) return <p className="um-hint" role="status">{error}</p>;
  if (!data || (!data.metadata && data.ticketBookings.length === 0)) return null;
  return <section className="classic-detail-section classic-archive-detail" aria-label="Classic-Archiv">
    <h3><Archive size={16}/> Historische Classic-Daten</h3>
    <p className="um-hint">Nur zur Information. Keine automatische Znuny-Buchung oder Outlook-Synchronisation.</p>
    {data.metadata && <dl>
      <dt>Angeheftet</dt><dd>{data.metadata.isPinned ? 'Ja' : 'Nein'}</dd>
      <dt>Znuny zugewiesen (Classic)</dt><dd>{data.metadata.isZnunyAssigned ? 'Ja' : 'Nein'}</dd>
      <dt>Geteilt (Classic)</dt><dd>{data.metadata.isPlenaroShared ? 'Ja' : 'Nein'}</dd>
      <dt>Alter Ticketstatus</dt><dd>{data.metadata.ticketState || '–'}</dd>
      <dt>Gebuchte Zeit in Classic</dt><dd>{data.metadata.ticketSecondsBooked ?? 0} Sekunden (Referenzwert)</dd>
      {data.metadata.ticketUrl && <><dt>Alte Ticketadresse</dt><dd className="classic-archive-long">{data.metadata.ticketUrl}</dd></>}
      {data.metadata.outlookEntryId && <><dt>Outlook-Referenz</dt><dd>Vorhanden (nicht verbunden)</dd></>}
    </dl>}
    {data.ticketBookings.length > 0 && <>
      <h4>Historische Ticketbuchungen ({data.ticketBookings.length})</h4>
      <ul className="classic-archive-bookings">{data.ticketBookings.map(item =>
        <li key={item.legacyId}>
          <strong>{item.ticketNumber || 'Ticket ohne Nummer'}</strong>
          <span>{item.bookedMinutes} Min. · {item.status}</span>
          <small>{item.shortDescription || item.note || 'Ohne Beschreibung'}</small>
        </li>)}</ul>
    </>}
  </section>;
}

export function ClassicArchiveOverviewPanel({ refreshVersion }: { refreshVersion: number }) {
  const [overview, setOverview] = useState<ClassicArchiveOverview | null>(null);
  const [monthly, setMonthly] = useState<ClassicMonthlyAdjustment[]>([]);
  useEffect(() => {
    let active = true;
    void Promise.all([readArchiveOverview(), readClassicMonthly()])
      .then(([data, months]) => {
        if (active) { setOverview(data); setMonthly(months); }
      }).catch(() => { if (active) setOverview(null); });
    return () => { active = false; };
  }, [refreshVersion]);
  if (!overview) return null;
  return <div className="classic-import-archive-overview" aria-label="Classic-Archivbestand">
    <strong>Historisches Classic-Archiv (nur lesbar)</strong>
    <div className="classic-import-summary">
      <span>Aufgaben-Metadaten <strong>{overview.metadataCount}</strong></span>
      <span>Ticketbuchungen <strong>{overview.bookingCount}</strong></span>
      <span>Monatskorrekturen <strong>{overview.monthlyCount}</strong></span>
      <span>Einstellungssätze <strong>{overview.settingsBatches}</strong></span>
    </div>
    {monthly.length > 0 && <details>
      <summary>Historische Monatskorrekturen anzeigen ({monthly.length}{overview.monthlyCount > monthly.length ? '+' : ''})</summary>
      <ul className="classic-archive-bookings">{monthly.map((item, i) =>
        <li key={item.month + '-' + item.createdAt + '-' + i}>
          <strong>{item.month}</strong>
          <span>{(item.seconds / 3600).toLocaleString('de-DE', { maximumFractionDigits: 2 })} Stunden (Archiv)</span>
          <small>{item.note || 'Ohne Notiz'}</small>
        </li>)}</ul>
    </details>}
    <p className="um-hint">Archivwerte werden nicht als neue Ticketbuchungen oder zusätzliche Arbeitszeit verrechnet.</p>
  </div>;
}

export function ClassicImportedSettings({ batchId }: { batchId: string }) {
  const [settings, setSettings] = useState<Record<string, unknown> | null>(null);
  const [error, setError] = useState('');
  useEffect(() => {
    let active = true;
    void readImportedSettings(batchId).then(value => { if (active) setSettings(value); })
      .catch(() => { if (active) setError('Einstellungen nicht verfügbar.'); });
    return () => { active = false; };
  }, [batchId]);
  if (error) return <p role="alert">{error}</p>;
  if (!settings) return <p role="status">Lade archivierte Einstellungen …</p>;
  const entries = Object.entries(settings).filter(([,v]) => v !== null);
  if (entries.length === 0) return <p className="um-hint">Keine weiteren Einstellungen archiviert.</p>;
  return <dl className="classic-archive-settings">{entries.map(([name,value]) =>
    <div key={name}><dt>{name}</dt><dd>{String(value)}</dd></div>)}</dl>;
}
