import { useEffect, useState, type FormEvent } from 'react';
import { CheckCircle2, Globe2, Palette, Save, ShieldCheck } from 'lucide-react';
import {
  readPersonalPreferences, readWorkspacePreferences,
  savePersonalPreferences, saveWorkspacePreferences,
  type PersonalPreferences, type WorkspacePreferences,
} from './settingsApi';

function Notice({ message, error }: { message: string; error: string }) {
  return <>
    {message && <p className="um-feedback success" role="status"><CheckCircle2 size={16} />{message}</p>}
    {error && <p className="um-feedback failure" role="alert">{error}</p>}
  </>;
}

export function PersonalPreferencesPanel({ onSaved }: { onSaved: (settings: PersonalPreferences) => void }) {
  const [form, setForm] = useState<PersonalPreferences>({ theme: 'light', weekStart: 'inherit' });
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');

  useEffect(() => {
    let active = true;
    void readPersonalPreferences().then(value => {
      if (active) { setForm(value); setLoading(false); }
    }).catch(reason => {
      if (active) { setError(reason instanceof Error ? reason.message : 'Laden fehlgeschlagen.'); setLoading(false); }
    });
    return () => { active = false; };
  }, []);

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (busy) return;
    setBusy(true); setError(''); setMessage('');
    try {
      await savePersonalPreferences(form);
      onSaved(form);
      setMessage('Deine Einstellungen wurden gespeichert.');
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Speichern fehlgeschlagen.'); }
    finally { setBusy(false); }
  }

  return <section className="content-card um-panel" aria-labelledby="my-preferences-heading">
    <div className="um-section-title"><Palette size={20}/><h2 id="my-preferences-heading">Persönliche Einstellungen</h2></div>
    <p>Diese Einstellungen gelten ausschließlich für dein Konto und bleiben nach einer erneuten Anmeldung erhalten.</p>
    {loading ? <p role="status">Lade deine Einstellungen …</p> : <form className="um-form" onSubmit={e => void submit(e)}>
      <label htmlFor="pref-theme">Darstellung</label>
      <select id="pref-theme" value={form.theme} onChange={e => setForm({ ...form, theme: e.target.value as PersonalPreferences['theme'] })}>
        <option value="light">Hell</option>
        <option value="dark">Dunkel</option>
      </select>
      <label htmlFor="pref-week">Wochenbeginn</label>
      <select id="pref-week" value={form.weekStart} onChange={e => setForm({ ...form, weekStart: e.target.value as PersonalPreferences['weekStart'] })}>
        <option value="inherit">Standard der Instanz</option>
        <option value="monday">Montag</option>
        <option value="sunday">Sonntag</option>
      </select>
      <p className="um-hint">Der Wochenbeginn wird für die zukünftige Kalender- und Wochenplanung vorgemerkt.</p>
      <button type="submit" className="button button-primary" disabled={busy}><Save size={16}/>{busy ? 'Speichere …' : 'Einstellungen speichern'}</button>
    </form>}
    <Notice message={message} error={error}/>
  </section>;
}

export function WorkspacePreferencesPanel({ admin, onSaved }: {
  admin: boolean;
  onSaved: (settings: WorkspacePreferences) => void;
}) {
  const [form, setForm] = useState<WorkspacePreferences>({
    instanceName: 'Plenaro', supportEmail: '', defaultWeekStart: 'monday',
    fetchOtrsAccounting: true,
  });
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');

  useEffect(() => {
    let active = true;
    void readWorkspacePreferences().then(value => {
      if (active) { setForm(value); setLoading(false); }
    }).catch(reason => {
      if (active) { setError(reason instanceof Error ? reason.message : 'Laden fehlgeschlagen.'); setLoading(false); }
    });
    return () => { active = false; };
  }, []);

  async function submit(e: FormEvent) {
    e.preventDefault(); if (busy) return;
    setBusy(true); setError(''); setMessage('');
    try {
      await saveWorkspacePreferences(form);
      onSaved(form);
      setMessage('Instanzeinstellungen gespeichert.');
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Speichern fehlgeschlagen.'); }
    finally { setBusy(false); }
  }

  return <section className="content-card um-panel" aria-labelledby="workspace-preferences-heading">
    <div className="um-section-title"><Globe2 size={20}/><h2 id="workspace-preferences-heading">Instanzeinstellungen</h2></div>
    <p>{admin
      ? 'Diese Werte gelten für alle Benutzer. Änderungen sind Administratoren vorbehalten.'
      : 'Globale Vorgaben deiner Plenaro-Instanz (nur lesbar).'}</p>
    {loading ? <p role="status">Lade Instanzeinstellungen …</p> :
      admin ? <form className="um-form" onSubmit={e => void submit(e)}>
        <label htmlFor="workspace-name">Instanzname</label>
        <input id="workspace-name" value={form.instanceName} minLength={2} maxLength={80} required
          onChange={e => setForm({ ...form, instanceName: e.target.value })}/>
        <label htmlFor="workspace-support">Supportkontakt (optional)</label>
        <input id="workspace-support" type="email" maxLength={254} value={form.supportEmail}
          onChange={e => setForm({ ...form, supportEmail: e.target.value })} placeholder="support@beispiel.de"/>
        <label htmlFor="workspace-week">Standard-Wochenbeginn</label>
        <select id="workspace-week" value={form.defaultWeekStart} onChange={e => setForm({ ...form, defaultWeekStart: e.target.value as WorkspacePreferences['defaultWeekStart'] })}>
          <option value="monday">Montag</option><option value="sunday">Sonntag</option>
        </select>
        <label className="um-check" htmlFor="workspace-otrs-accounting">
          <input id="workspace-otrs-accounting" type="checkbox"
            checked={form.fetchOtrsAccounting!==false}
            onChange={e=>setForm({...form,fetchOtrsAccounting:e.target.checked})}/>
          OTRS-Zeiten im Zeitreport abrufen und berücksichtigen
        </label>
        <p className="um-hint">Aktiv: persönliche OTRS-Zeitbuchungen abrufen,
          mit lokal bestätigten Plenaro-Buchungen abgleichen und fehlende Zeiten
          ergänzen (ohne Doppelzählung). Deaktiviert: der Zeitreport fragt OTRS
          überhaupt nicht ab und verwendet nur lokale Daten. Das betrifft
          ausschließlich den Report, nicht den Ticket-Sync oder normale Buchungen.</p>
        <p className="um-hint">Diese Werte enthalten keine Passwörter oder API-Schlüssel. Integrations-Zugangsdaten bekommen später einen eigenen sicheren Speicher.</p>
        <button type="submit" className="button button-primary" disabled={busy}><Save size={16}/>{busy ? 'Speichere …' : 'Instanzeinstellungen speichern'}</button>
      </form> : <div className="um-readonly">
        <div className="um-account-data"><span>Instanz</span><strong>{form.instanceName}</strong></div>
        <div className="um-account-data"><span>Support</span><strong>{form.supportEmail || 'Nicht hinterlegt'}</strong></div>
        <div className="um-account-data"><span>Standard-Wochenbeginn</span><strong>{form.defaultWeekStart === 'monday' ? 'Montag' : 'Sonntag'}</strong></div>
        <div className="um-account-data"><span>OTRS-Zeitabruf im Report</span><strong>{form.fetchOtrsAccounting!==false ? 'Aktiv' : 'Deaktiviert – nur lokale Zeiten'}</strong></div>
      </div>}
    <Notice message={message} error={error}/>
    {admin && <p className="um-hint" style={{ marginTop: 16 }}><ShieldCheck size={14} aria-hidden="true" style={{ verticalAlign: 'middle' }}/> Änderungen werden serverseitig mit Adminrolle und CSRF-Schutz geprüft.</p>}
  </section>;
}
