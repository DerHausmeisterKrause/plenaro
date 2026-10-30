import { useEffect, useState, type FormEvent } from 'react';
import { BellRing, Save } from 'lucide-react';
import { readReminderPreferences, saveReminderPreferences, type ReminderPreferences } from './notificationApi';

export function ReminderSettingsPanel() {
  const [settings, setSettings] = useState<ReminderPreferences>({ enabled: true, leadMinutes: 2 });
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  useEffect(() => {
    let active = true;
    void readReminderPreferences()
      .then(value => { if (active) setSettings(value); })
      .catch(e => { if (active) setError(e instanceof Error ? e.message : 'Laden fehlgeschlagen.'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (busy || !Number.isInteger(settings.leadMinutes) ||
        settings.leadMinutes < 0 || settings.leadMinutes > 1440) return;
    setBusy(true); setError(''); setNotice('');
    try {
      await saveReminderPreferences(settings);
      setNotice('Reminder-Einstellungen gespeichert.');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Speichern fehlgeschlagen.');
    } finally { setBusy(false); }
  }

  return <section className="content-card um-panel" aria-label="Reminder-Einstellungen">
    <div className="um-section-title"><BellRing size={20}/><h2>Benachrichtigungen und Reminder</h2></div>
    <p>Persönliche Erinnerungen für den Start deiner eigenen Aufgaben-Zeitsegmente. Die Einstellung gilt geräteübergreifend.</p>
    {loading ? <p role="status">Lade Reminder-Einstellungen …</p> :
      <form className="plenaro-reminder-settings" onSubmit={e => void submit(e)}>
        <label className="classic-inline-check">
          <input type="checkbox" checked={settings.enabled}
            onChange={e => setSettings(previous => ({ ...previous, enabled: e.target.checked }))}/>
          Zeitsegment-Erinnerungen aktivieren
        </label>
        <label htmlFor="reminder-lead">Vorwarnung in Minuten</label>
        <input id="reminder-lead" type="number" min="0" max="1440" step="1" required
          value={settings.leadMinutes}
          onChange={e => setSettings(previous => ({ ...previous, leadMinutes: Number(e.target.value) }))}/>
        <p className="um-hint">Standard: 2 Minuten vor Beginn. 0 deaktiviert nur die Vorwarnung; die Start-Erinnerung bleibt aktiv.
          Geplante und aktive Aufgaben werden berücksichtigt. Bereits quittierte Hinweise erscheinen nicht erneut.</p>
        <button type="submit" className="button button-primary" disabled={busy}>
          <Save size={16}/>{busy ? 'Speichere …' : 'Reminder-Einstellungen speichern'}
        </button>
      </form>}
    {error && <p role="alert" className="um-feedback failure">{error}</p>}
    {notice && <p role="status" className="um-feedback success">{notice}</p>}
    <p className="um-hint">Die Glocke oben zeigt ungelesene Erinnerungen. Für System-Benachrichtigungen ist HTTPS und eine einmalige Browserfreigabe notwendig.
      Bei geschlossenem Browser gibt es derzeit keinen Web-Push-Dienst.</p>
  </section>;
}
