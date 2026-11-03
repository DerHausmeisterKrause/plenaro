import { useEffect, useState, type FormEvent } from 'react';
import { CheckCircle2, KeyRound, ShieldAlert, ShieldCheck, Trash2 } from 'lucide-react';

type EntraAdminStatus = {
  enabled: boolean;
  vaultReady: boolean;
  httpsRequired: boolean;
  tenantId: string;
  clientId: string;
  secretConfigured: boolean;
};

async function requestToken(): Promise<string> {
  const r = await fetch('/api/v1/auth/csrf', { credentials: 'same-origin', cache: 'no-store' });
  if (!r.ok) throw new Error('Sicherheitstoken konnte nicht abgerufen werden.');
  return (await r.json() as { token: string }).token;
}

export function EntraAdminSettings() {
  const [status, setStatus] = useState<EntraAdminStatus | null>(null);
  const [tenantId, setTenantId] = useState('');
  const [clientId, setClientId] = useState('');
  const [secret, setSecret] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  async function refresh() {
    const r = await fetch('/api/v1/admin/entra/', { credentials: 'same-origin', cache: 'no-store' });
    if (!r.ok) throw new Error('Entra-Einstellungen konnten nicht geladen werden.');
    const data = await r.json() as EntraAdminStatus;
    setStatus(data);
    setTenantId(data.tenantId);
    setClientId(data.clientId);
    setSecret('');
  }

  useEffect(() => {
    let live = true;
    void fetch('/api/v1/admin/entra/', { credentials: 'same-origin', cache: 'no-store' })
      .then(async r => {
        if (!r.ok) throw new Error('Entra-Einstellungen konnten nicht geladen werden.');
        return await r.json() as EntraAdminStatus;
      }).then(d => { if (live) { setStatus(d); setTenantId(d.tenantId); setClientId(d.clientId); } })
      .catch(e => { if (live) setError(e instanceof Error ? e.message : 'Laden fehlgeschlagen.'); });
    return () => { live = false; };
  }, []);

  async function save(e: FormEvent) {
    e.preventDefault(); if (busy) return;
    setBusy(true); setError(''); setNotice('');
    try {
      const csrf = await requestToken();
      const r = await fetch('/api/v1/admin/entra/', {
        method: 'PUT', credentials: 'same-origin',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf },
        body: JSON.stringify({ tenantId: tenantId.trim(), clientId: clientId.trim(), clientSecret: secret }),
      });
      if (!r.ok) {
        const data = await r.json().catch(() => ({})) as { error?: string };
        throw new Error(data.error ?? 'Entra-Konfiguration konnte nicht gespeichert werden.');
      }
      await refresh();
      setNotice('Microsoft Entra ist eingerichtet. Der Login ist ohne Containerneustart aktiv.');
    } catch (e) { setError(e instanceof Error ? e.message : 'Speichern fehlgeschlagen.'); }
    finally { setBusy(false); }
  }

  async function remove() {
    if (busy || !window.confirm('Microsoft-SSO deaktivieren? Der lokale Login bleibt erhalten.')) return;
    setBusy(true); setError(''); setNotice('');
    try {
      const csrf = await requestToken();
      const r = await fetch('/api/v1/admin/entra/', {
        method: 'DELETE', credentials: 'same-origin', headers: { 'X-CSRF-TOKEN': csrf },
      });
      if (!r.ok) throw new Error('Entra konnte nicht deaktiviert werden.');
      await refresh();
      setNotice('Microsoft-SSO deaktiviert. Lokale Benutzerkonten bleiben unverändert.');
    } catch (e) { setError(e instanceof Error ? e.message : 'Deaktivierung fehlgeschlagen.'); }
    finally { setBusy(false); }
  }

  return <section className="content-card um-panel" aria-labelledby="admin-entra-heading">
    <div className="um-section-title"><ShieldCheck size={20}/><h2 id="admin-entra-heading">Microsoft Entra ID – Administration</h2></div>
    <p>Tenant, App-Registrierung und Client-Secret direkt hier verwalten. Alle Angaben werden zusammen verschlüsselt im Plenaro-Vault gespeichert, nicht in .env oder einem Klartext-Datenbankfeld.</p>
    {status && <p role="status" className={status.enabled ? 'um-feedback success' : 'um-hint'}>
      {status.enabled ? <><CheckCircle2 size={16}/>Microsoft-Login aktiviert</> : 'Microsoft-Login ist deaktiviert.'}
    </p>}
    {status && !status.httpsRequired && <p role="alert" className="um-feedback failure">
      <ShieldAlert size={16}/>Aktiviere zuerst vertrauenswürdiges HTTPS über NGINX.
    </p>}
    {status && !status.vaultReady && <p role="alert" className="um-feedback failure">
      <KeyRound size={16}/>Der verschlüsselte Vault benötigt einen getrennten Masterkey auf dem Docker-Host.
    </p>}
    <form className="um-form" onSubmit={e => void save(e)}>
      <label htmlFor="entra-admin-tenant">Directory (Tenant) ID</label>
      <input id="entra-admin-tenant" required value={tenantId} onChange={e => setTenantId(e.target.value)}
        placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" autoComplete="off"/>
      <label htmlFor="entra-admin-client">Application (Client) ID</label>
      <input id="entra-admin-client" required value={clientId} onChange={e => setClientId(e.target.value)}
        placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" autoComplete="off"/>
      <label htmlFor="entra-admin-secret">Client-Secret {status?.secretConfigured ? '– leer lassen, um das vorhandene beizubehalten' : ''}</label>
      <input id="entra-admin-secret" type="password" minLength={16} maxLength={4096}
        required={!status?.secretConfigured} value={secret} autoComplete="new-password"
        onChange={e => setSecret(e.target.value)} placeholder="Secret Value (nicht Secret ID)"/>
      <p className="um-hint">Entra App Registration (Single Tenant): Web-Redirects <code>https://DEIN-FQDN/signin-entra</code> und <code>https://DEIN-FQDN/signin-graph</code>. Delegierte Graph-Berechtigungen: User.Read, Calendars.Read, offline_access. Eine Genehmigung durch deinen Tenant-Administrator kann erforderlich sein.</p>
      {error && <p role="alert" className="um-feedback failure">{error}</p>}
      {notice && <p role="status" className="um-feedback success">{notice}</p>}
      <div className="classic-detail-actions">
        <button type="submit" disabled={busy || !status?.vaultReady || !status?.httpsRequired} className="button button-primary">
          <ShieldCheck size={16}/>{busy ? 'Speichern …' : 'Microsoft-Verbindung speichern'}
        </button>
        {status?.enabled && <button type="button" disabled={busy} className="button button-secondary"
          onClick={() => void remove()}><Trash2 size={16}/>Microsoft-SSO deaktivieren</button>}
      </div>
    </form>
  </section>;
}
