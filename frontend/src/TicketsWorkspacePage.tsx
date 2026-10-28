import { Inbox, Search, Ticket } from 'lucide-react';
import { Link } from 'react-router-dom';

/**
 * Deliberately no mock tickets. The ticket adapter (Znuny/OTRS) is a separate
 * milestone; this is the classic two-pane workspace scaffold it will populate.
 */
export function TicketsWorkspacePage() {
  return <div className="page classic-workspace">
    <div className="page-heading">
      <span className="eyebrow">PLENARO / TICKETSYSTEM</span>
      <h1>Ticketsystem</h1>
      <p>Links Tickets auswählen, rechts bearbeiten – der Aufbau entspricht dem gewohnten Arbeitsplatz.</p>
    </div>
    <div className="classic-split">
      <section className="classic-list-pane" aria-label="Ticketliste">
        <div className="classic-list-header">
          <div><strong>Meine Tickets</strong><span>Keine Verbindung eingerichtet</span></div>
          <Ticket size={20} color="currentColor"/>
        </div>
        <div className="classic-search" aria-disabled="true">
          <Search size={16}/><input aria-label="Tickets suchen" disabled placeholder="Ticketnummer, Betreff …"/>
        </div>
        <div className="classic-list-scroll">
          <div className="classic-empty-list">
            <Inbox size={30}/>
            <h2>Noch keine Tickets geladen</h2>
            <p>Die echte Znuny/OTRS-Anbindung folgt in Phase 6. Es werden keine Beispiel-Tickets erzeugt.</p>
          </div>
        </div>
      </section>
      <section className="classic-detail-pane" aria-label="Ticketdetails">
        <div className="classic-detail-empty">
          <Ticket size={42}/>
          <h2>Ticket auswählen</h2>
          <p>Wenn die Ticketsystem-Verbindung eingerichtet ist, erscheinen hier Ticketdetails, Kommunikation und Aktionen. Die Liste bleibt dabei links stehen.</p>
          <Link to="/settings" className="button button-secondary">Integrationseinstellungen öffnen</Link>
        </div>
      </section>
    </div>
  </div>;
}
