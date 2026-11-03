import { useEffect, useState, type FormEvent } from 'react';
import { KeyRound, Save, ShieldCheck, Trash2 } from 'lucide-react';
import { listSecretNames, setSecret, deleteSecret, type SecretScope } from './secretsApi';

function SecretGroup({ scope }: { scope: SecretScope }) {
  const [names, setNames] = useState<string[]>([]);
  const [name, setName] = useState('');
  const [value, setValue] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [enabled, setEnabled] = useState(true);
  const label = scope === 'me' ? 'Persönliche Zugangsdaten' : 'Globale Integrations-Zugangsdaten';

  async function reload() { setNames(await listSecretNames(scope)); }
  useEffect(() => {
    let active = true;
    void listSecretNames(scope).then(items => { if (active) setNames(items); })
      .catch(reason => {
        if (active) {
          setEnabled(false);
          setError(reason instanceof Error ? reason.message : 'Secret-Speicher nicht verfügbar.');
        }
      });
    return () => { active = false; };
  }, [scope]);

  async function save(e: FormEvent) {
    e.preventDefault(); if (busy) return;
    setBusy(true); setError(''); setMessage('');
    try {
      await setSecret(scope, name, value);
      setValue(''); setName('');
      await reload();
      setMessage('Zugangsdaten verschlüsselt gespeichert. Der Wert wird nicht erneut angezeigt.');
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Speichern fehlgeschlagen.'); }
    finally { setBusy(false); }
  }
  async function remove(secretName: string) {
    if (busy || !window.confirm(`Secret „${secretName}“ wirklich löschen?`)) return;
    setBusy(true); setMessage(''); setError('');
    try {
      await deleteSecret(scope, secretName);
      await reload();
      setMessage('Secret gelöscht.');
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Löschen fehlgeschlagen.'); }
    finally { setBusy(false); }
  }

  return <section className="content-card um-panel" aria-labelledby={`vault-${scope}`}>
    <div className="um-section-title"><KeyRound size={20}/><h2 id={`vault-${scope}`}>{label}</h2></div>
    <p>Verschlüsselte Ablage für zukünftige Integrationen. Vorhandene Werte sind absichtlich nicht auslesbar.</p>
    {enabled && <>
      <div className="um-secret-list">
        {names.length === 0 && <p className="um-hint">Noch keine Zugangsdaten gespeichert.</p>}
        {names.map(item => <div className="um-secret-row" key={item}>
          <span><ShieldCheck size={15}/>{item}</span>
          <button type="button" disabled={busy} className="button button-secondary"
            aria-label={`Secret ${item} löschen`} onClick={() => void remove(item)}><Trash2 size={15}/></button>
        </div>)}
      </div>
      <form className="um-form" onSubmit={e => void save(e)}>
        <label htmlFor={`secret-name-${scope}`}>Secret-Name</label>
        <input id={`secret-name-${scope}`} value={name} required minLength={2} maxLength={64}
          pattern="[a-z0-9][a-z0-9._-]+" placeholder="z. B. znuny.api-token"
          autoComplete="off" onChange={e => setName(e.target.value)}/>
        <label htmlFor={`secret-value-${scope}`}>Neuer Secret-Wert</label>
        <input id={`secret-value-${scope}`} type="password" value={value}
          required maxLength={4096} autoComplete="new-password"
          onChange={e => setValue(e.target.value)}/>
        <p className="um-hint">Bei gleichem Namen wird der bisherige Wert ersetzt. Kein Klartextabruf über die Web-API.</p>
        <button type="submit" className="button button-primary" disabled={busy}>
          <Save size={16}/>{busy ? 'Speichere …' : 'Verschlüsselt speichern'}
        </button>
      </form>
    </>}
    {message && <p role="status" className="um-feedback success">{message}</p>}
    {error && <p role="alert" className="um-feedback failure">{error}</p>}
  </section>;
}

export function SecretsSettings({ admin }: { admin: boolean }) {
  return <div className="um-settings-grid">
    <SecretGroup scope="me"/>
    {admin && <SecretGroup scope="global"/>}
  </div>;
}
