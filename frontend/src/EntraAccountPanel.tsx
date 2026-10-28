import { useEffect, useState } from 'react';
import { Link2, ShieldCheck, Unlink } from 'lucide-react';
import { useSearchParams } from 'react-router-dom';
import { confirmEntraLink, entraStatus, unlinkEntra, pendingEntraIdentity, type EntraStatus, type PendingEntraIdentity } from './entraApi';

export function EntraAccountPanel() {
  const [params, setParams] = useSearchParams();
  const [status, setStatus] = useState<EntraStatus | null>(null);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [identity, setIdentity] = useState<PendingEntraIdentity | null>(null);
  const pending = params.get('entra') === 'pending';

  useEffect(() => {
    let mounted = true;
    void entraStatus().then(value => { if (mounted) setStatus(value); })
      .catch(() => { if (mounted) setStatus({ enabled: false, linked: false }); });
    return () => { mounted = false; };
  }, []);

  useEffect(() => {
    if (!pending) return;
    let mounted = true;
    void pendingEntraIdentity().then(value => { if (mounted) setIdentity(value); })
      .catch(reason => { if (mounted) setError(reason instanceof Error ? reason.message : 'Verknüpfung abgelaufen.'); });
    return () => { mounted = false; };
  }, [pending]);

  if (!status?.enabled) return null;

  async function perform(action: () => Promise<void>, success: string) {
    if (busy) return;
    setBusy(true); setError(''); setMessage('');
    try {
      await action();
      setStatus(await entraStatus());
      setMessage(success);
      if (pending) { setIdentity(null); setParams({}, { replace: true }); }
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Microsoft-Anfrage fehlgeschlagen.');
    } finally {
      setBusy(false);
    }
  }

  return <section className="content-card um-panel" aria-labelledby="entra-heading">
    <div className="um-section-title"><ShieldCheck size={20}/><h2 id="entra-heading">Microsoft Entra ID</h2></div>
    <p>Verknüpfe dein bestehendes Plenaro-Konto mit deiner Microsoft-Unternehmensidentität. Die lokalen Admin-/User-Rollen bleiben unverändert.</p>
    {status.linked ? <>
      <p className="um-feedback success"><Link2 size={16}/>Microsoft-Konto verknüpft</p>
      <button type="button" className="button button-secondary" disabled={busy}
        onClick={() => void perform(unlinkEntra, 'Microsoft-Verknüpfung entfernt. Dein lokaler Login bleibt nutzbar.')}>
        <Unlink size={16}/>Verknüpfung entfernen
      </button>
    </> : pending ? <>
      <p>Microsoft hat deine Identität geprüft. Prüfe das Microsoft-Konto und den Tenant, bevor du die Verbindung ausdrücklich bestätigst.</p>
      {identity && <div className="um-account-data"><span>Microsoft-Konto / Tenant</span><strong>{identity.account} ({identity.tenantId})</strong></div>}
      <button type="button" className="button button-primary" disabled={busy || !identity}
        onClick={() => void perform(confirmEntraLink, 'Microsoft-Konto erfolgreich verbunden.')}>
        <Link2 size={16}/>Verknüpfung bestätigen
      </button>
    </> : <a className="button button-secondary" href="/api/v1/auth/entra/link/start">
      <Link2 size={16}/>Microsoft-Konto verknüpfen
    </a>}
    {error && <p className="um-feedback failure" role="alert">{error}</p>}
    {message && <p className="um-feedback success" role="status">{message}</p>}
  </section>;
}
