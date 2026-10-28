import { useState, type FormEvent } from 'react';
import { ArrowRight, CheckCircle2, KeyRound, LockKeyhole, ShieldCheck } from 'lucide-react';
import { completeSetup } from './authApi';

export function SetupPage({ onCompleted }: { onCompleted: () => void }) {
  const [step, setStep] = useState(1);
  const [email, setEmail] = useState('');
  const [name, setName] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [code, setCode] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const insecure = window.location.protocol === 'http:' &&
    !['127.0.0.1', 'localhost', '[::1]'].includes(window.location.hostname);

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (busy) return;
    if (password !== confirm) {
      setError('Die Passwörter stimmen nicht überein.');
      return;
    }
    setBusy(true);
    setError('');
    try {
      await completeSetup(code.trim(), name.trim(), email.trim(), password);
      setPassword('');
      setConfirm('');
      setCode('');
      onCompleted();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Die Einrichtung ist fehlgeschlagen.');
    } finally {
      setBusy(false);
    }
  }

  return <main className="auth-standalone-main">
    <section className="content-card setup-card" aria-label="Plenaro einrichten">
      <div className="auth-brand"><span className="auth-brand-icon">P</span><strong>plenaro<span>.</span></strong></div>
      <div className="setup-progress"><span>ERSTEINRICHTUNG</span><span>Schritt {step} von 2</span></div>
      <div className="setup-progress-bar"><div style={{ width: step === 1 ? '50%' : '100%' }} /></div>
      {step === 1 ? <>
        <div className="empty-icon"><ShieldCheck size={27}/></div>
        <h1>Willkommen bei Plenaro.</h1>
        <p>Deine neue Instanz wurde noch nicht eingerichtet. Wir erstellen jetzt das erste Administratorkonto.</p>
        <div className="setup-feature"><CheckCircle2 size={18}/><span>Einmalige Ersteinrichtung</span></div>
        <div className="setup-feature"><CheckCircle2 size={18}/><span>Geschütztes Administratorkonto</span></div>
        <div className="setup-feature"><CheckCircle2 size={18}/><span>Keine voreingestellten Zugangsdaten</span></div>
        <button className="button button-primary auth-submit" type="button" onClick={() => setStep(2)}>
          Einrichtung starten <ArrowRight size={17}/>
        </button>
      </> : <>
        <div className="empty-icon"><LockKeyhole size={27}/></div>
        <h1>Administrator erstellen</h1>
        <p>Lege die Zugangsdaten für dein erstes Administratorkonto fest.</p>
        {insecure && <p role="alert" className="auth-warning">
          Bitte HTTPS verwenden: Über HTTP werden Passwörter unverschlüsselt übertragen.
        </p>}
        <form className="auth-form" onSubmit={e => void submit(e)}>
          <label htmlFor="setup-name">Anzeigename</label>
          <input id="setup-name" autoComplete="name" value={name} maxLength={128} required
            onChange={e => setName(e.target.value)} />
          <label htmlFor="setup-email">E-Mail-Adresse</label>
          <input id="setup-email" type="email" autoComplete="username" value={email} maxLength={256} required
            onChange={e => setEmail(e.target.value)} />
          <label htmlFor="setup-password">Passwort</label>
          <input id="setup-password" type="password" autoComplete="new-password" value={password} minLength={12} required
            onChange={e => setPassword(e.target.value)} />
          <p className="setup-field-hint">Mindestens 12 Zeichen, Groß- und Kleinbuchstabe, Zahl und Sonderzeichen.</p>
          <label htmlFor="setup-confirm">Passwort bestätigen</label>
          <input id="setup-confirm" type="password" autoComplete="new-password" value={confirm} required
            onChange={e => setConfirm(e.target.value)} />
          <div className="setup-token-help">
            <div className="setup-token-head"><KeyRound size={17}/><strong>Einmaliger Einrichtungscode</strong></div>
            <p>Führe einmal auf deinem Docker-Host folgenden Befehl aus und kopiere den ausgegebenen Code:</p>
            <code>docker compose exec web dotnet Plenaro.Api.dll --setup-token</code>
            <p>Der Code ist 15 Minuten gültig. Ein neuer Code macht den bisherigen ungültig.</p>
          </div>
          <label htmlFor="setup-code">Einrichtungscode</label>
          <input id="setup-code" value={code} autoComplete="off" required minLength={64} maxLength={64}
            onChange={e => setCode(e.target.value)} />
          {error && <p className="auth-error" role="alert">{error}</p>}
          <button disabled={busy} type="submit" className="button button-primary auth-submit">
            {busy ? 'Administrator wird erstellt …' : 'Administrator erstellen'} <ArrowRight size={17}/>
          </button>
          <button className="button button-secondary setup-back" type="button" onClick={() => setStep(1)}>
            Zurück
          </button>
        </form>
      </>}
    </section>
  </main>;
}
