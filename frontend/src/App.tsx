import { useEffect, useState } from 'react';
import {
  Activity, ArrowRight, ArrowUpRight, CalendarCheck2, CheckCircle2,
  ChevronRight, CircleAlert, Database, ExternalLink,
  LayoutGrid, LockKeyhole, LogIn, Menu, Moon,
  Server, ShieldCheck, Sparkles, Sun, X,
} from 'lucide-react';
import { Link, NavLink, Route, Routes, useLocation } from 'react-router-dom';
import { checkHealth, type HealthState } from './api';
import { navigation, type ModuleKey, type NavigationEntry } from './navigation';

type Theme = 'light' | 'dark';

const quickModules: ModuleKey[] = ['tasks', 'week', 'tickets', 'reports'];

function getInitialTheme(): Theme {
  try {
    return window.localStorage.getItem('plenaro-theme') === 'dark' ? 'dark' : 'light';
  } catch {
    return 'light';
  }
}

function useHealth() {
  const [web, setWeb] = useState<HealthState>('loading');
  const [database, setDatabase] = useState<HealthState>('loading');

  useEffect(() => {
    const controller = new AbortController();
    void checkHealth('/health/live', controller.signal).then((state) => {
      if (!controller.signal.aborted) setWeb(state);
    });
    void checkHealth('/health/ready', controller.signal).then((state) => {
      if (!controller.signal.aborted) setDatabase(state);
    });
    return () => controller.abort();
  }, []);

  return { web, database };
}

function Brand() {
  return (
    <Link to="/" className="brand" aria-label="Plenaro – Startseite">
      <div className="brand-mark"><span className="brand-mark-first" /><span className="brand-mark-second" /><span className="brand-mark-third" /></div>
      <div className="brand-text"><strong>plenaro<span>.</span></strong><small>WORKSPACE</small></div>
    </Link>
  );
}

function StatusPill({ state, label }: { state: HealthState; label: string }) {
  return (
    <span className={`status-pill status-${state}`} role="status">
      <span className="status-dot" aria-hidden="true" />
      {label}: {state === 'ready' ? 'Online' : state === 'loading' ? 'Prüfe …' : 'Nicht erreichbar'}
    </span>
  );
}

function Sidebar({ onNavigate }: { onNavigate: () => void }) {
  return (
    <>
      <div className="sidebar-top"><Brand /></div>
      <div className="sidebar-scroll">
        <span className="nav-caption">ARBEITSBEREICH</span>
        <nav className="nav-list" aria-label="Hauptnavigation">
          {navigation.slice(0, 4).map((item) => <SidebarLink key={item.key} item={item} onNavigate={onNavigate} />)}
        </nav>
        <span className="nav-caption second">TOOLS & VERWALTUNG</span>
        <nav className="nav-list" aria-label="Werkzeuge und Verwaltung">
          {navigation.slice(4).map((item) => <SidebarLink key={item.key} item={item} onNavigate={onNavigate} />)}
        </nav>
      </div>
      <div className="sidebar-footer">
        <div className="build-card">
          <div className="build-card-icon"><Sparkles size={18} aria-hidden="true" /></div>
          <strong>Plenaro 2.0</strong>
          <span>Web-Neuentwicklung</span>
          <span className="build-stage">Phase 2 / 7</span>
        </div>
        <Link className="sidebar-footer-link" to="/system" onClick={onNavigate}>
          <Activity size={17} aria-hidden="true" /> Systemstatus <ChevronRight size={16} aria-hidden="true" />
        </Link>
      </div>
    </>
  );
}

function SidebarLink({ item, onNavigate }: { item: NavigationEntry; onNavigate: () => void }) {
  const Icon = item.icon;
  return (
    <NavLink
      to={item.path}
      onClick={onNavigate}
      className={({ isActive }) => `nav-link${isActive ? ' active' : ''}`}
    >
      <Icon size={19} strokeWidth={1.85} aria-hidden="true" />
      <span>{item.label}</span>
      {item.key === 'ai' && <span className="nav-spark" aria-hidden="true">✦</span>}
    </NavLink>
  );
}

function SectionHeading({ eyebrow, title, description }: { eyebrow: string; title: string; description: string }) {
  return (
    <div className="page-heading">
      <span className="eyebrow">{eyebrow}</span>
      <h1>{title}</h1>
      <p>{description}</p>
    </div>
  );
}

function EmptyModule({ item, theme, toggleTheme }: { item: NavigationEntry; theme: Theme; toggleTheme: () => void }) {
  const Icon = item.icon;
  return (
    <div className="page">
      <SectionHeading eyebrow="PLENARO / ARBEITSBEREICH" title={item.label} description={item.description} />
      <div className="workspace-grid">
        <section className="content-card empty-module">
          <div className="empty-icon"><Icon size={30} strokeWidth={1.6} aria-hidden="true" /></div>
          <span className="phase-label">Geplant für Phase {item.phase}</span>
          <h2>Dieser Bereich wird aufgebaut.</h2>
          <p>Die Navigation und das Layout stehen bereits. Daten und Aktionen werden erst mit dem jeweiligen Backend freigeschaltet. Es werden noch keine Beispielaufgaben oder Tickets vorgetäuscht.</p>
          <Link className="button button-primary" to="/"><ArrowRight size={17} aria-hidden="true" /> Zur Übersicht</Link>
        </section>
        <aside className="content-card side-info">
          <span className="eyebrow">ENTWICKLUNGSSTATUS</span>
          <h3>Nächste Schritte</h3>
          <div className="milestone"><span className="milestone-dot complete"><CheckCircle2 size={16} /></span><div><strong>Weboberfläche</strong><span>Layout & Navigation</span></div></div>
          <div className="milestone"><span className="milestone-dot"><LockKeyhole size={16} /></span><div><strong>Lokale Anmeldung</strong><span>Folgt in Phase 3</span></div></div>
          <div className="milestone"><span className="milestone-dot"><CalendarCheck2 size={16} /></span><div><strong>Funktionen</strong><span>Werden schrittweise migriert</span></div></div>
          {item.key === 'settings' && (
            <div className="theme-panel">
              <strong>Darstellung</strong>
              <p>Dieses Gerät: {theme === 'dark' ? 'Dunkelmodus' : 'Hellmodus'}</p>
              <button type="button" className="button button-secondary" onClick={toggleTheme}>{theme === 'dark' ? <Sun size={17} /> : <Moon size={17} />} Design wechseln</button>
            </div>
          )}
        </aside>
      </div>
    </div>
  );
}

function ModuleTile({ item }: { item: NavigationEntry }) {
  const Icon = item.icon;
  return (
    <Link className="module-tile" to={item.path}>
      <div className="tile-icon"><Icon size={21} strokeWidth={1.8} aria-hidden="true" /></div>
      <div className="tile-details">
        <strong>{item.label}</strong>
        <span>In Vorbereitung · Phase {item.phase}</span>
      </div>
      <ArrowUpRight className="tile-arrow" size={18} aria-hidden="true" />
    </Link>
  );
}

function Dashboard({ web, database }: { web: HealthState; database: HealthState }) {
  return (
    <div className="page">
      <div className="overview-header">
        <div>
          <span className="eyebrow">DEIN WORKSPACE</span>
          <h1>Willkommen bei Plenaro<span className="h1-dot">.</span></h1>
          <p>Dein zentraler Arbeitsplatz für Aufgaben, Zeitplanung und IT-Service-Management.</p>
        </div>
        <span className="preview-badge"><span className="preview-dot" /> Vorschau · Phase 2</span>
      </div>
      <div className="announcement">
        <div className="announcement-icon"><Sparkles size={22} aria-hidden="true" /></div>
        <div>
          <strong>Die Webanwendung nimmt Gestalt an.</strong>
          <p>Das neue Layout ist verfügbar. Die Anmeldung und alle produktiven Funktionen werden in den nächsten Phasen ergänzt.</p>
        </div>
        <Link to="/system" aria-label="Systemstatus öffnen"><ArrowRight size={21} aria-hidden="true" /></Link>
      </div>
      <section className="section-group" aria-labelledby="overview-heading">
        <div className="section-title"><div><span className="section-overline">ÜBERSICHT</span><h2 id="overview-heading">Deine Arbeitsbereiche</h2></div><span className="section-meta">4 Module</span></div>
        <div className="module-grid">
          {quickModules.map((key) => {
            const item = navigation.find((entry) => entry.key === key)!;
            return <ModuleTile key={item.key} item={item} />;
          })}
        </div>
      </section>
      <div className="dashboard-lower">
        <section className="content-card next-steps" aria-labelledby="steps-heading">
          <div className="card-heading"><div className="card-heading-icon"><LayoutGrid size={19} aria-hidden="true" /></div><h2 id="steps-heading">Entwicklungsfahrplan</h2></div>
          <div className="progress-line"><span>Phase 1 – Docker-Grundgerüst</span><span className="progress-check"><CheckCircle2 size={16} /> Implementiert</span></div>
          <div className="progress-line active"><span>Phase 2 – Weboberfläche</span><span>In Arbeit</span></div>
          <div className="progress-line"><span>Phase 3 – Lokaler Admin & User</span><span>Geplant</span></div>
          <div className="progress-line"><span>Phase 4 – Entra & Outlook</span><span>Geplant</span></div>
          <a className="text-link" href="https://github.com/DerHausmeisterKrause/plenaro/issues" target="_blank" rel="noopener noreferrer">Alle GitHub-Aufgaben anzeigen <ExternalLink size={15} aria-hidden="true" /></a>
        </section>
        <section className="content-card system-summary" aria-labelledby="health-heading">
          <div className="card-heading"><div className="card-heading-icon blue"><Server size={19} aria-hidden="true" /></div><h2 id="health-heading">Systemstatus</h2></div>
          <p>Live-Prüfung deiner Plenaro-Instanz</p>
          <div className="health-row"><span><Activity size={17} aria-hidden="true" /> API / Webserver</span><StatusPill state={web} label="Web" /></div>
          <div className="health-row"><span><Database size={17} aria-hidden="true" /> PostgreSQL</span><StatusPill state={database} label="DB" /></div>
          <Link to="/system" className="text-link">Details anzeigen <ArrowRight size={16} aria-hidden="true" /></Link>
        </section>
      </div>
    </div>
  );
}

function SystemPage({ web, database }: { web: HealthState; database: HealthState }) {
  return (
    <div className="page">
      <SectionHeading eyebrow="PLENARO / SYSTEM" title="Systemstatus" description="Verbindungsstatus zum Webserver und zur PostgreSQL-Datenbank." />
      <div className="system-grid">
        <div className="content-card system-service"><div className="service-icon"><Server size={24}/></div><h2>Webserver</h2><StatusPill state={web} label="API" /><p>HTTP-Endpunkt: <code>/health/live</code></p></div>
        <div className="content-card system-service"><div className="service-icon"><Database size={24}/></div><h2>Datenbank</h2><StatusPill state={database} label="PostgreSQL" /><p>HTTP-Endpunkt: <code>/health/ready</code></p></div>
      </div>
      <div className="warning-note"><ShieldCheck size={20} aria-hidden="true" /><p><strong>Lokale Entwicklungsversion.</strong> Ohne Benutzeranmeldung und Rollenprüfung darf diese Instanz nicht öffentlich ins Internet gestellt werden. LAN-Zugriff nur im vertrauenswürdigen Netz.</p></div>
    </div>
  );
}

function AuthPreview({ mode }: { mode: 'login' | 'logout' }) {
  return (
    <div className="auth-wrap">
      <div className="content-card auth-card">
        <div className="empty-icon"><LockKeyhole size={28} aria-hidden="true" /></div>
        <span className="eyebrow">PHASE 3 / BENUTZERVERWALTUNG</span>
        <h1>{mode === 'login' ? 'Anmeldung folgt.' : 'Abmeldung noch nicht verfügbar.'}</h1>
        <p>Es gibt momentan noch keine Benutzerkonten oder Sitzungen. Lokale Accounts und Microsoft-Entra-SSO werden in den nächsten Phasen umgesetzt.</p>
        <Link className="button button-primary" to="/"><ArrowRight size={17} /> Zur Übersicht</Link>
      </div>
    </div>
  );
}

function NotFound() {
  return (
    <div className="auth-wrap">
      <div className="content-card auth-card">
        <div className="empty-icon"><CircleAlert size={28} aria-hidden="true" /></div>
        <span className="eyebrow">FEHLER 404</span>
        <h1>Seite nicht gefunden.</h1>
        <p>Die angeforderte Ansicht existiert nicht. Wechsle zurück zur Übersicht.</p>
        <Link className="button button-primary" to="/"><ArrowRight size={17} /> Zur Übersicht</Link>
      </div>
    </div>
  );
}

export function AppContent() {
  const location = useLocation();
  const [menuOpen, setMenuOpen] = useState(false);
  const [theme, setTheme] = useState<Theme>(getInitialTheme);
  const { web, database } = useHealth();

  useEffect(() => { document.documentElement.dataset.theme = theme; }, [theme]);
  useEffect(() => { setMenuOpen(false); }, [location.pathname]);

  function toggleTheme() {
    const next: Theme = theme === 'dark' ? 'light' : 'dark';
    setTheme(next);
    try { window.localStorage.setItem('plenaro-theme', next); } catch { /* session-only */ }
  }

  const current = navigation.find((entry) => location.pathname === entry.path);
  const pageTitle = current?.label ?? (location.pathname === '/system' ? 'Systemstatus' : 'Übersicht');

  return (
    <div className="app-shell">
      <a href="#main-content" className="skip-link">Zum Inhalt springen</a>
      {menuOpen && <button className="menu-overlay" type="button" onClick={() => setMenuOpen(false)} aria-label="Menü schließen" />}
      <aside id="mobile-navigation" className={`sidebar${menuOpen ? ' open' : ''}`} aria-label="Seitenmenü">
        <div className="mobile-close"><button className="icon-button" onClick={() => setMenuOpen(false)} aria-label="Menü schließen"><X size={22} /></button></div>
        <Sidebar onNavigate={() => setMenuOpen(false)} />
      </aside>
      <div className="main-column">
        <header className="topbar">
          <div className="topbar-left">
            <button type="button" className="icon-button mobile-menu" onClick={() => setMenuOpen(!menuOpen)} aria-label="Menü öffnen" aria-expanded={menuOpen} aria-controls="mobile-navigation"><Menu size={23} /></button>
            <div className="breadcrumb"><Link to="/">Plenaro</Link><ChevronRight size={15} aria-hidden="true" /><span>{pageTitle}</span></div>
          </div>
          <div className="topbar-right">
            <span className="topbar-environment"><span /> Lokaler Server</span>
            <button type="button" className="icon-button" onClick={toggleTheme} aria-label={theme === 'dark' ? 'Hellmodus aktivieren' : 'Dunkelmodus aktivieren'} title="Design ändern">{theme === 'dark' ? <Sun size={19} /> : <Moon size={19} />}</button>
            <Link to="/system" className="icon-button" aria-label="Systemstatus" title="Systemstatus"><Activity size={19}/></Link>
            <Link to="/login" className="login-button"><LogIn size={17} /> Anmelden</Link>
          </div>
        </header>
        <main id="main-content">
          <Routes>
            <Route path="/" element={<Dashboard web={web} database={database}/>} />
            {navigation.map((item) => <Route key={item.key} path={item.path} element={<EmptyModule item={item} theme={theme} toggleTheme={toggleTheme}/>} />)}
            <Route path="/system" element={<SystemPage web={web} database={database}/>} />
            <Route path="/login" element={<AuthPreview mode="login" />} />
            <Route path="/logout" element={<AuthPreview mode="logout" />} />
            <Route path="*" element={<NotFound />} />
          </Routes>
        </main>
        <footer className="footer"><span>© 2026 Plenaro · Self-hosted Community Preview</span><span>Web v0.2.0 · <Link to="/system">Systemstatus</Link></span></footer>
      </div>
    </div>
  );
}
