import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { CheckCircle2, KeyRound, LockKeyhole, Plus, Save, Shield, ShieldCheck, UserRoundCog, Users } from 'lucide-react';
import { type CurrentUser } from './authApi';
import { PersonalPreferencesPanel, WorkspacePreferencesPanel } from './SettingsPreferences';
import { SecretsSettings } from './SecretsSettings';
import { EntraAccountPanel } from './EntraAccountPanel';
import { EntraAdminSettings } from './EntraAdminSettings';
import { DayMarkerBudgetSettings } from './DayMarkersPanel';
import { ReminderSettingsPanel } from './ReminderSettingsPanel';
import { ZnunySettingsPanel } from './ZnunySettingsPanel';
import { ZnunyCentralAdminPanel } from './ZnunyCentralAdminPanel';
import { ClassicImportPanel } from './ClassicImportPanel';
import { type PersonalPreferences, type WorkspacePreferences } from './settingsApi';
import { addUser, changePassword, listUsers, resetUserPassword, saveUser, type ManagedUser } from './userApi';

type Role = 'User' | 'Admin';

function Feedback({ success, error }: { success: string; error: string }) {
  return <>
    {success && <p className="um-feedback success" role="status"><CheckCircle2 size={16}/>{success}</p>}
    {error && <p className="um-feedback failure" role="alert">{error}</p>}
  </>;
}

function PasswordSettings() {
  const [oldPassword, setOldPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (busy) return;
    setError(''); setSuccess('');
    if (newPassword !== confirm) { setError('Die neuen Passwörter stimmen nicht überein.'); return; }
    setBusy(true);
    try {
      await changePassword(oldPassword, newPassword);
      setOldPassword(''); setNewPassword(''); setConfirm('');
      setSuccess('Passwort geändert. Andere Sitzungen wurden ungültig.');
    } catch (e) { setError(e instanceof Error ? e.message : 'Passwortänderung fehlgeschlagen.'); }
    finally { setBusy(false); }
  }

  return <section className="content-card um-panel" aria-labelledby="own-password">
    <div className="um-section-title"><KeyRound size={20}/><h2 id="own-password">Eigenes Passwort ändern</h2></div>
    <p>Für die Änderung benötigst du dein bisheriges Passwort.</p>
    <form className="um-form" onSubmit={e => void submit(e)}>
      <label htmlFor="old-password">Aktuelles Passwort</label>
      <input id="old-password" type="password" value={oldPassword} autoComplete="current-password" required onChange={e => setOldPassword(e.target.value)}/>
      <label htmlFor="new-password">Neues Passwort</label>
      <input id="new-password" type="password" value={newPassword} autoComplete="new-password" minLength={12} required onChange={e => setNewPassword(e.target.value)}/>
      <label htmlFor="confirm-password">Neues Passwort wiederholen</label>
      <input id="confirm-password" type="password" value={confirm} autoComplete="new-password" minLength={12} required onChange={e => setConfirm(e.target.value)}/>
      <p className="um-hint">Mindestens 12 Zeichen, Groß-/Kleinbuchstaben, Zahl und Sonderzeichen.</p>
      <Feedback success={success} error={error}/>
      <button className="button button-primary" disabled={busy} type="submit"><LockKeyhole size={16}/>{busy ? 'Speichere …' : 'Passwort ändern'}</button>
    </form>
  </section>;
}

function AdminUsers({ currentId }: { currentId: string }) {
  const [users, setUsers] = useState<ManagedUser[]>([]);
  const [selected, setSelected] = useState<string | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [email, setEmail] = useState('');
  const [name, setName] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState<Role>('User');
  const [createAgentId, setCreateAgentId] = useState('');
  const [editName, setEditName] = useState('');
  const [editRole, setEditRole] = useState<Role>('User');
  const [editAgentId, setEditAgentId] = useState('');
  const [editDisabled, setEditDisabled] = useState(false);
  const [resetPassword, setResetPassword] = useState('');
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const reload = useCallback(async () => {
    const result = await listUsers();
    setUsers(result);
    return result;
  }, []);

  useEffect(() => {
    let active = true;
    void listUsers().then(value => { if (active) { setUsers(value); setLoading(false); } })
      .catch(reason => { if (active) { setError(reason instanceof Error ? reason.message : 'Benutzer konnten nicht geladen werden.'); setLoading(false); } });
    return () => { active = false; };
  }, []);

  const selectedUser = users.find(u => u.id === selected);
  function selectUser(user: ManagedUser) {
    setSelected(user.id); setEditName(user.displayName); setEditRole(user.role);
    setEditDisabled(user.disabled); setEditAgentId(user.agentId?.toString() ?? '');
    setResetPassword(''); setCreateOpen(false);
    setError(''); setSuccess('');
  }

  function parseAgent(value: string): number | null {
    if (!value.trim()) return null;
    const id = Number(value.trim());
    if (!Number.isSafeInteger(id) || id < 1 || id > 2147483647)
      throw new Error('Die Znuny-Agenten-ID muss eine positive Ganzzahl sein.');
    return id;
  }

  async function create(e: FormEvent) {
    e.preventDefault(); if (busy) return;
    setBusy(true); setError(''); setSuccess('');
    try {
      await addUser(email.trim(), name.trim(), password, role, parseAgent(createAgentId));
      await reload();
      setEmail(''); setName(''); setPassword(''); setRole('User');
      setCreateAgentId(''); setCreateOpen(false);
      setSuccess('Benutzer wurde angelegt.');
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Anlegen fehlgeschlagen.'); }
    finally { setBusy(false); }
  }

  async function update(e: FormEvent) {
    e.preventDefault(); if (!selected || busy) return;
    setBusy(true); setError(''); setSuccess('');
    try {
      await saveUser(selected, editName.trim(), editRole, editDisabled, parseAgent(editAgentId));
      const changed = await reload();
      setSelected(changed.find(u => u.id === selected)?.id ?? null);
      setSuccess('Benutzer aktualisiert. Vorhandene Sitzungen wurden widerrufen.');
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Speichern fehlgeschlagen.'); }
    finally { setBusy(false); }
  }

  async function reset(e: FormEvent) {
    e.preventDefault(); if (!selected || busy) return;
    setBusy(true); setError(''); setSuccess('');
    try {
      await resetUserPassword(selected, resetPassword);
      setResetPassword('');
      setSuccess('Passwort zurückgesetzt. Übergib es ausschließlich über einen sicheren Kanal.');
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Zurücksetzen fehlgeschlagen.'); }
    finally { setBusy(false); }
  }

  return <section className="content-card um-panel" aria-labelledby="users-heading">
    <div className="um-panel-head">
      <div className="um-section-title"><Users size={20}/><h2 id="users-heading">Benutzerverwaltung</h2></div>
      <button type="button" className="button button-primary" onClick={() => {
        setCreateOpen(!createOpen); setSelected(null); setError(''); setSuccess('');
      }}><Plus size={16}/>{createOpen ? 'Abbrechen' : 'Benutzer anlegen'}</button>
    </div>
    <p>Verwalte lokale Konten, Rollen, Znuny-Agenten-IDs und den Zugriff auf Plenaro.
      Agenten-IDs vergibt ausschließlich die Administration.</p>
    {loading ? <p role="status">Lade Benutzer …</p> : <>
      <div className="um-users" role="list">
        {users.map(user => <button type="button" role="listitem" className={`um-user ${selected===user.id?'chosen':''}`} key={user.id} onClick={() => selectUser(user)}>
          <span className="um-avatar">{(user.displayName || user.email).slice(0, 1).toUpperCase()}</span>
          <span className="um-user-main"><strong>{user.displayName}</strong><small>{user.email}</small></span>
          {user.agentId !== null && <span className="um-agent-id">Znuny #{user.agentId}</span>}
          <span className={`um-role ${user.role==='Admin'?'admin':''}`}>{user.role}</span>
          <span className={`um-active ${user.disabled?'blocked':''}`}>{user.disabled ? 'Gesperrt' : 'Aktiv'}</span>
        </button>)}
      </div>
      {users.length === 0 && <p>Keine Benutzer vorhanden.</p>}
    </>}

    {createOpen && <form className="um-form um-editor" onSubmit={e => void create(e)}>
      <h3>Neuen Benutzer anlegen</h3>
      <label htmlFor="um-create-name">Anzeigename</label>
      <input id="um-create-name" value={name} onChange={e => setName(e.target.value)} maxLength={128} required/>
      <label htmlFor="um-create-email">E-Mail-Adresse</label>
      <input id="um-create-email" type="email" value={email} onChange={e => setEmail(e.target.value)} maxLength={256} required/>
      <label htmlFor="um-create-password">Startpasswort</label>
      <input id="um-create-password" type="password" autoComplete="new-password" value={password} minLength={12} onChange={e => setPassword(e.target.value)} required/>
      <p className="um-hint">Mindestens 12 Zeichen, Groß-/Kleinbuchstaben, Ziffer und Sonderzeichen. Passwort sicher übergeben.</p>
      <label htmlFor="um-create-role">Rolle</label>
      <select id="um-create-role" value={role} onChange={e => setRole(e.target.value as Role)}><option value="User">User</option><option value="Admin">Admin</option></select>
      <label htmlFor="um-create-agent-id">Znuny-Agenten-ID (optional)</label>
      <input id="um-create-agent-id" type="number" min={1} max={2147483647} step={1}
        placeholder="z. B. 42" value={createAgentId}
        onChange={e => setCreateAgentId(e.target.value)}/>
      <p className="um-hint">Nur Administratoren können die Agenten-ID hinterlegen.
        Die Zuordnung ist sofort aktiv und darf systemweit nur einmal vergeben werden.
        Für Benutzer ohne Znuny-Zugriff das Feld leer lassen.</p>
      <button disabled={busy} className="button button-primary" type="submit"><Plus size={16}/>Benutzer erstellen</button>
    </form>}

    {selectedUser && !createOpen && <div className="um-editor">
      <h3><UserRoundCog size={18}/> {selectedUser.displayName} bearbeiten</h3>
      <form className="um-form" onSubmit={e => void update(e)}>
        <label htmlFor="um-edit-name">Anzeigename</label>
        <input id="um-edit-name" value={editName} maxLength={128} required onChange={e => setEditName(e.target.value)}/>
        <label htmlFor="um-edit-role">Rolle</label>
        <select id="um-edit-role" value={editRole} disabled={selectedUser.id === currentId} onChange={e => setEditRole(e.target.value as Role)}><option value="User">User</option><option value="Admin">Admin</option></select>
        <label htmlFor="um-edit-agent-id">Znuny-Agenten-ID (nur Administration)</label>
        <input id="um-edit-agent-id" type="number" min={1} max={2147483647} step={1}
          placeholder="Keine Zuordnung" value={editAgentId}
          onChange={e => setEditAgentId(e.target.value)}/>
        <p className="um-hint">Eine leere Agenten-ID entzieht den Znuny-Ticketzugriff.
          Eine Änderung wird zusammen mit den Benutzerrechten gespeichert.</p>
        <label className="um-check"><input type="checkbox" checked={editDisabled} disabled={selectedUser.id === currentId} onChange={e => setEditDisabled(e.target.checked)}/> Konto sperren</label>
        {selectedUser.id === currentId && <p className="um-hint">Dein eigenes Administratorkonto kann nicht gesperrt oder herabgestuft werden.</p>}
        <button className="button button-primary" type="submit" disabled={busy}><Save size={16}/>Änderungen speichern</button>
      </form>
      <form className="um-form um-reset-form" onSubmit={e => void reset(e)}>
        <h3>Passwort zurücksetzen</h3>
        <p className="um-hint">Das neue Passwort ersetzt sofort das bisherige und meldet bestehende Sitzungen ab.</p>
        <label htmlFor="um-reset-password">Neues Passwort</label>
        <input id="um-reset-password" type="password" value={resetPassword} autoComplete="new-password" minLength={12} onChange={e => setResetPassword(e.target.value)} required/>
        <button type="submit" className="button button-secondary" disabled={busy}><KeyRound size={16}/>Passwort zurücksetzen</button>
      </form>
    </div>}
    <Feedback error={error} success={success}/>
  </section>;
}

type SettingsSection = 'general' | 'planning' | 'import' | 'microsoft' | 'ticket' | 'security' | 'admin';

export function SettingsPage({ user, onPersonalSaved, onWorkspaceSaved }: {
  user: CurrentUser | null | undefined;
  onPersonalSaved: (value: PersonalPreferences) => void;
  onWorkspaceSaved: (value: WorkspacePreferences) => void;
}) {
  const [section, setSection] = useState<SettingsSection>('general');
  if (!user) return null;
  const admin = user.roles.includes('Admin');
  const tabs: { key: SettingsSection; label: string }[] = [
    { key: 'general', label: 'Allgemein' },
    { key: 'planning', label: 'Aufgaben & Kalender' },
    { key: 'import', label: 'Datenimport' },
    { key: 'microsoft', label: 'Microsoft 365' },
    { key: 'ticket', label: 'Ticketsystem' },
    { key: 'security', label: 'Konto & Sicherheit' },
    ...(admin ? [{ key: 'admin' as const, label: 'Administration' }] : []),
  ];

  return <div className="page">
    <div className="page-heading">
      <span className="eyebrow">PLENARO / EINSTELLUNGEN</span>
      <h1>Einstellungen</h1>
      <p>Wie im klassischen Plenaro nach Arbeitsbereichen geordnet. Persönliche und globale Einstellungen bleiben getrennt.</p>
    </div>
    <nav className="classic-settings-tabs" aria-label="Einstellungskategorien">
      {tabs.map(tab => <button key={tab.key} type="button"
        className={`classic-settings-tab${section === tab.key ? ' active' : ''}`}
        aria-current={section === tab.key ? 'page' : undefined}
        onClick={() => setSection(tab.key)}>{tab.label}</button>)}
    </nav>
    {section === 'general' && <div className="um-settings-grid">
      <PersonalPreferencesPanel onSaved={onPersonalSaved}/>
      <WorkspacePreferencesPanel admin={admin} onSaved={onWorkspaceSaved}/>
    </div>}
    {section === 'planning' && <><DayMarkerBudgetSettings/><ReminderSettingsPanel/></>}
    {section === 'import' && <ClassicImportPanel/>}
    {section === 'microsoft' && <>
      {admin && <EntraAdminSettings/>}
      <EntraAccountPanel/>
      <section className="content-card um-panel">
        <h2>Outlook-Kalender</h2>
        <p>Die lokale Wochenplanung ist immer verfügbar. Wenn Microsoft verbunden wird, ergänzt Outlook dort die Termine.</p>
        <a href="/week" className="button button-secondary">Wochenplanung öffnen</a>
      </section>
    </>}
    {section === 'ticket' && <><ZnunySettingsPanel admin={admin}/>{admin && <ZnunyCentralAdminPanel/>}</>}
    {section === 'security' && <>
      <section className="content-card um-account">
        <div className="um-section-title"><ShieldCheck size={20}/><h2>Mein Konto</h2></div>
        <div className="um-account-data"><span>Name</span><strong>{user.name}</strong></div>
        <div className="um-account-data"><span>E-Mail</span><strong>{user.email}</strong></div>
        <div className="um-account-data"><span>Rolle</span><strong><Shield size={15}/>{admin ? 'Administrator' : 'Benutzer'}</strong></div>
      </section>
      <PasswordSettings/>
    </>}
    {section === 'admin' && admin && <>
      <AdminUsers currentId={user.id}/>
      <SecretsSettings admin={admin}/>
    </>}
  </div>;
}
