import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { CalendarDays, ChevronLeft, ChevronRight, Save, ShieldAlert } from 'lucide-react';
import {
  getMarkerBudget, getMarkerSummary, getMonthMarkers, saveDayMarker, saveMarkerBudget,
  type BudgetSummary, type DayMarker, type DayType,
} from './dayMarkersApi';
import { localDate } from './tasksApi';

function quotaLabel(limit: number | null, used: number, remaining: number | null) {
  return limit === null
    ? 'Noch nicht konfiguriert'
    : `${used} / ${limit} Tage · ${Math.max(remaining ?? 0, 0)} verfügbar`;
}

function QuotaWarnings({ summary }: { summary: BudgetSummary }) {
  return <>
    {summary.ulExhausted && <p role="alert" className="um-feedback failure">
      <ShieldAlert size={16}/>UL-Kontingent für {summary.year} aufgebraucht{summary.ulExceeded ? ' und überschritten' : ''}.
    </p>}
    {summary.brExhausted && <p role="alert" className="um-feedback failure">
      <ShieldAlert size={16}/>BR-Kontingent für {String(summary.month).padStart(2, '0')}/{summary.year} aufgebraucht{summary.brExceeded ? ' und überschritten' : ''}.
    </p>}
  </>;
}

export function DayMarkerBudgetSettings() {
  const [annual, setAnnual] = useState('');
  const [monthly, setMonthly] = useState('');
  const [loaded, setLoaded] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [summary, setSummary] = useState<BudgetSummary | null>(null);
  const now = new Date();

  async function reload() {
    const [budget, current] = await Promise.all([
      getMarkerBudget(), getMarkerSummary(now.getFullYear(), now.getMonth() + 1),
    ]);
    setAnnual(budget.annualUlDays?.toString() ?? '');
    setMonthly(budget.monthlyBrDays?.toString() ?? '');
    setSummary(current);
  }

  useEffect(() => {
    let active = true;
    void Promise.all([
      getMarkerBudget(), getMarkerSummary(now.getFullYear(), now.getMonth() + 1),
    ]).then(([b, s]) => {
      if (!active) return;
      setAnnual(b.annualUlDays?.toString() ?? '');
      setMonthly(b.monthlyBrDays?.toString() ?? '');
      setSummary(s);
      setLoaded(true);
    }).catch(e => { if (active) { setError(e instanceof Error ? e.message : 'Einstellungen nicht verfügbar.'); setLoaded(true); } });
    return () => { active = false; };
  }, []);

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (busy) return;
    setBusy(true); setError(''); setNotice('');
    try {
      const a = annual.trim() === '' ? null : Number(annual);
      const b = monthly.trim() === '' ? null : Number(monthly);
      if ((a !== null && (!Number.isInteger(a) || a < 0 || a > 366)) ||
          (b !== null && (!Number.isInteger(b) || b < 0 || b > 31)))
        throw new Error('Bitte gültige ganze Tage eingeben (UL: 0–366, BR: 0–31).');
      await saveMarkerBudget({ annualUlDays: a, monthlyBrDays: b });
      await reload();
      setNotice('Deine Kontingente wurden gespeichert.');
    } catch (e) { setError(e instanceof Error ? e.message : 'Speichern fehlgeschlagen.'); }
    finally { setBusy(false); }
  }

  return <section className="content-card um-panel" aria-labelledby="quota-settings-heading">
    <div className="um-section-title"><CalendarDays size={20}/><h2 id="quota-settings-heading">UL- und BR-Kontingente</h2></div>
    <p>Persönliche Vorgaben: Urlaubstage pro Kalenderjahr und BR-Tage pro Kalendermonat. AM wird ohne Kontingent erfasst.</p>
    {!loaded ? <p role="status">Lade Kontingente …</p> :
      <form className="um-form" onSubmit={e => void submit(e)}>
        <label htmlFor="quota-annual">UL – Urlaubstage pro Jahr</label>
        <input id="quota-annual" inputMode="numeric" type="number" min="0" max="366" step="1"
          value={annual} placeholder="Noch nicht festgelegt" onChange={e => setAnnual(e.target.value)}/>
        <label htmlFor="quota-monthly">BR – erlaubte Tage pro Monat</label>
        <input id="quota-monthly" inputMode="numeric" type="number" min="0" max="31" step="1"
          value={monthly} placeholder="Noch nicht festgelegt" onChange={e => setMonthly(e.target.value)}/>
        <p className="um-hint">Leer = nicht konfiguriert. 0 = kein Kontingent. Am Jahres- bzw. Monatswechsel beginnen die Zähler automatisch neu. Es werden nur markierte Tage gezählt.</p>
        {summary && <>
          <p className="um-hint">UL {summary.year}: {quotaLabel(summary.annualUlDays, summary.usedUlDays, summary.remainingUlDays)}</p>
          <p className="um-hint">BR aktueller Monat: {quotaLabel(summary.monthlyBrDays, summary.usedBrDays, summary.remainingBrDays)}</p>
          <QuotaWarnings summary={summary}/>
        </>}
        {error && <p className="um-feedback failure" role="alert">{error}</p>}
        {notice && <p className="um-feedback success" role="status">{notice}</p>}
        <button type="submit" className="button button-primary" disabled={busy}><Save size={16}/>{busy ? 'Speichern …' : 'Kontingente speichern'}</button>
      </form>}
  </section>;
}

function monthStep(current: string, delta: number): string {
  const [year, month] = current.split('-').map(Number);
  const date = new Date(year, month - 1 + delta, 1);
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`;
}

export function DayMarkersPanel() {
  const [viewMonth, setViewMonth] = useState(() => localDate().slice(0, 7));
  const [selected, setSelected] = useState(() => localDate());
  const [markers, setMarkers] = useState<DayMarker[]>([]);
  const [summary, setSummary] = useState<BudgetSummary | null>(null);
  const [dayType, setDayType] = useState<DayType>('Normal');
  const [isBr, setIsBr] = useState(false);
  const [isHo, setIsHo] = useState(false);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [refresh, setRefresh] = useState(0);
  const [year, month] = viewMonth.split('-').map(Number);

  useEffect(() => {
    let active = true;
    setLoading(true);
    void Promise.all([getMonthMarkers(year, month), getMarkerSummary(year, month)])
      .then(([list, quota]) => {
        if (!active) return;
        setMarkers(list); setSummary(quota); setLoading(false);
      }).catch(e => {
        if (!active) return;
        setLoading(false); setError(e instanceof Error ? e.message : 'Tagesmarker nicht erreichbar.');
      });
    return () => { active = false; };
  }, [year, month, refresh]);

  useEffect(() => {
    const item = markers.find(m => m.day === selected);
    setDayType(item?.dayType ?? 'Normal');
    setIsBr(item?.isBr ?? false);
    setIsHo(item?.isHo ?? false);
  }, [selected, markers]);

  const days = useMemo(() => {
    const total = new Date(year, month, 0).getDate();
    return Array.from({ length: total }, (_, index) =>
      `${viewMonth}-${String(index + 1).padStart(2, '0')}`);
  }, [year, month, viewMonth]);
  const offset = (new Date(year, month - 1, 1).getDay() + 6) % 7;

  function goMonth(next: string) {
    if (next < '2000-01' || next > '2099-12') return;
    setViewMonth(next);
    setSelected(next + '-01');
    setError(''); setNotice('');
  }

  async function save() {
    if (busy) return;
    setBusy(true); setError(''); setNotice('');
    try {
      await saveDayMarker(selected, dayType, isBr, isHo);
      // Refresh both usage counters and the individual marker from the database.
      setRefresh(v => v + 1);
      setNotice('Tagesmarker gespeichert.');
    } catch (e) { setError(e instanceof Error ? e.message : 'Speichern fehlgeschlagen.'); }
    finally { setBusy(false); }
  }

  return <section className="content-card day-marker-panel" aria-labelledby="marker-heading">
    <div className="day-marker-head">
      <div><h2 id="marker-heading">Tagesmarker · UL / AM / BR</h2>
        <p className="um-hint">Tage auswählen und markieren. BR kann unabhängig von UL oder AM gesetzt werden.</p></div>
      <div className="day-marker-navigation">
        <button type="button" className="button button-secondary" aria-label="Vorheriger Monat"
          onClick={() => goMonth(monthStep(viewMonth, -1))}><ChevronLeft size={16}/></button>
        <input aria-label="Monat auswählen" type="month" min="2000-01" max="2099-12"
          value={viewMonth} onChange={e => goMonth(e.target.value)}/>
        <button type="button" className="button button-secondary" aria-label="Nächster Monat"
          onClick={() => goMonth(monthStep(viewMonth, 1))}><ChevronRight size={16}/></button>
      </div>
    </div>
    {summary && <div className="day-marker-quotas">
      <span><strong>UL {summary.year}</strong> {quotaLabel(summary.annualUlDays, summary.usedUlDays, summary.remainingUlDays)}</span>
      <span><strong>BR {String(month).padStart(2, '0')}/{year}</strong> {quotaLabel(summary.monthlyBrDays, summary.usedBrDays, summary.remainingBrDays)}</span>
    </div>}
    {summary && <QuotaWarnings summary={summary}/>}
    {error && <p role="alert" className="um-feedback failure">{error}</p>}
    {notice && <p role="status" className="um-feedback success">{notice}</p>}
    {loading ? <p role="status">Lade Tagesmarker …</p> : <>
      <div className="day-marker-calendar" aria-label={`Tagesmarker ${viewMonth}`}>
        {['Mo','Di','Mi','Do','Fr','Sa','So'].map(day => <div className="day-marker-weekday" key={day}>{day}</div>)}
        {Array.from({ length: offset }, (_, i) => <span key={`empty-${i}`}/>)}
        {days.map(day => {
          const item = markers.find(marker => marker.day === day);
          return <button key={day} type="button" onClick={() => setSelected(day)}
            className={`day-marker-day${selected === day ? ' selected' : ''}`}
            aria-pressed={selected === day} aria-label={`${day}: ${item?.dayType ?? 'Normal'}${item?.isBr ? ', BR' : ''}`}>
            <strong>{Number(day.slice(-2))}</strong>
            {item && <span className="day-marker-label">{item.dayType === 'Normal' ? '' : item.dayType}{item.isBr ? ' BR' : ''}{item.isHo ? ' HO' : ''}</span>}
          </button>;
        })}
      </div>
      <div className="day-marker-editor">
        <strong>{new Intl.DateTimeFormat('de-DE', { dateStyle: 'full' }).format(new Date(selected + 'T12:00:00'))}</strong>
        <fieldset className="day-marker-choices">
          <legend>Tagesart</legend>
          {(['Normal', 'UL', 'AM'] as DayType[]).map(type =>
            <label key={type}><input type="radio" name="day-type" value={type}
              checked={dayType === type} onChange={() => setDayType(type)}/>{type}</label>)}
        </fieldset>
        <label className="day-marker-br"><input type="checkbox" checked={isHo}
          onChange={e => setIsHo(e.target.checked)}/> HO – Homeoffice</label>
        <label className="day-marker-br"><input type="checkbox" checked={isBr}
          onChange={e => setIsBr(e.target.checked)}/> BR-Tag markieren</label>
        {summary && dayType === 'UL' && !markers.some(m => m.day === selected && m.dayType === 'UL') &&
          summary.ulExhausted && <p className="um-feedback failure" role="alert">Achtung: Dein Jahresurlaub ist bereits ausgeschöpft. Eine zusätzliche UL-Markierung überschreitet das Kontingent.</p>}
        {summary && isBr && !markers.some(m => m.day === selected && m.isBr) &&
          summary.brExhausted && <p className="um-feedback failure" role="alert">Achtung: Das BR-Monatskontingent ist bereits ausgeschöpft.</p>}
        <div className="day-marker-buttons">
          <button type="button" className="button button-primary" disabled={busy} onClick={() => void save()}>
            <Save size={16}/>{busy ? 'Speichere …' : 'Marker speichern'}
          </button>
        </div>
        <p className="um-hint">„Normal“ ohne BR entfernt alle Marker dieses Tages. Kontingentüberschreitungen werden gewarnt, nicht automatisch blockiert.</p>
      </div>
    </>}
  </section>;
}
