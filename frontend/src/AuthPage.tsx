import { useState, type FormEvent } from 'react';
import { ArrowRight, LockKeyhole, LogOut, ShieldAlert } from 'lucide-react';
import { Link, useNavigate } from 'react-router-dom';
import { login, logout, type CurrentUser } from './authApi';

export function LoginPage({ onLogin, user }: { onLogin: (value: CurrentUser) => void; user: CurrentUser | null }) {
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    setBusy(true);
    setError('');
    try {
      const signedIn = await login(email.trim(), password);
      onLogin(signedIn);
      setPassword('');
      navigate('/', { replace: true });
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Anmeldung fehlgeschlagen.');
    } finally {
      setBusy(false);
    }
  }

  if (user) {
    return <div className="auth-wrap"><section className="content-card auth-card">
      <div className="empty-icon"><LockKeyhole size={28}/></div>
      <h1>Bereits angemeldet</h1>
      <p>Du bist als {user.name || user.email} angemeldet.</p>
      <Link className="button button-primary" to="/">Zur Übersicht <ArrowRight size={17}/></Link>
    </section></div>;
  }

  const insecureLan = window.location.protocol === 'http:' &&
    !['127.0.0.1', 'localhost', '[::1]'].includes(window.location.hostname);

  return <div className="auth-wrap">
    <section className="content-card auth-card">
      <div className="empty-icon"><LockKeyhole size={28} aria-hidden="true"/></div>
      <span className="eyebrow">PLENARO / LOKALE ANMELDUNG</span>
      <h1>Willkommen zurück.</h1>
      <p>Melde dich mit deinem lokalen Plenaro-Konto an. Der erste Administrator wird einmalig auf dem Docker-Host eingerichtet.</p>
      {insecureLan && <p role="alert" className="auth-warning"><ShieldAlert size={17} aria-hidden="true"/> Achtung: HTTP überträgt Passwörter im LAN unverschlüsselt. Für echte Konten einen HTTPS-Reverse-Proxy verwenden.</p>}
      <form onSubmit={submit} className="auth-form">
        <label htmlFor="plenaro-email">E-Mail-Adresse</label>
        <input id="plenaro-email" autoComplete="username" type="email" value={email}
          onChange={event => setEmail(event.target.value)} required maxLength={256} />
        <label htmlFor="plenaro-password">Passwort</label>
        <input id="plenaro-password" autoComplete="current-password" type="password" value={password}
          onChange={event => setPassword(event.target.value)} required />
        {error && <p role="alert" className="auth-error">{error}</p>}
        <button type="submit" disabled={busy} className="button button-primary auth-submit">
          {busy ? 'Anmeldung läuft …' : 'Anmelden'} <ArrowRight size={17} aria-hidden="true" />
        </button>
      </form>
      <p className="auth-hint">Noch kein Administrator? Führe am Docker-Host <code>docker compose exec web dotnet Plenaro.Api.dll --bootstrap-admin</code> aus.</p>
    </section>
  </div>;
}

export function LogoutPage({ onLogout, user }: { onLogout: () => void; user: CurrentUser | null }) {
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  async function submit() {
    if (busy) return;
    setBusy(true);
    setError('');
    try {
      await logout();
      onLogout();
      navigate('/login', { replace: true });
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Abmeldung fehlgeschlagen.');
    } finally {
      setBusy(false);
    }
  }

  return <div className="auth-wrap"><section className="content-card auth-card">
    <div className="empty-icon"><LogOut size={28} aria-hidden="true"/></div>
    <h1>Abmelden</h1>
    <p>{user ? 'Möchtest du deine Plenaro-Sitzung beenden?' : 'Du bist derzeit nicht angemeldet.'}</p>
    {error && <p role="alert" className="auth-error">{error}</p>}
    {user && <button disabled={busy} className="button button-primary" type="button" onClick={() => void submit()}>
      <LogOut size={17}/> {busy ? 'Abmeldung läuft …' : 'Jetzt abmelden'}
    </button>}
    <Link className="button button-secondary" to="/">Zur Übersicht</Link>
  </section></div>;
}
