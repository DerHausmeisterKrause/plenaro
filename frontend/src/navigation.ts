import {
  BookOpenText, CalendarDays, ChartNoAxesCombined,
  LayoutDashboard, Settings2, Sparkles, Ticket,
  type LucideIcon,
} from 'lucide-react';

export type ModuleKey =
  | 'today' | 'week' | 'ticketsystem'
  | 'wiki' | 'ai' | 'reports' | 'settings';

export type NavigationEntry = {
  key: ModuleKey;
  label: string;
  path: string;
  icon: LucideIcon;
  phase: number;
  description: string;
};

export const navigation: NavigationEntry[] = [
  { key: 'today', label: 'Arbeitsplatz', path: '/workspace', icon: LayoutDashboard, phase: 6, description: 'Tagesagenda, Tickets und eigene Aufgaben an einem Ort.' },
  { key: 'week', label: 'Wochenplanung', path: '/week', icon: CalendarDays, phase: 5, description: 'Wochentermine, Segmente und verfügbare Zeiträume.' },
  { key: 'ticketsystem', label: 'Ticketsystem', path: '/ticket-system', icon: Ticket, phase: 6, description: 'Die Znuny-Weboberfläche im geschützten Plenaro-Webzugang.' },
  { key: 'wiki', label: 'Wissensdatenbank', path: '/wiki', icon: BookOpenText, phase: 7, description: 'Wiki-Inhalte und angebundene Wissensquellen durchsuchen.' },
  { key: 'ai', label: 'KI-Assistent', path: '/ai', icon: Sparkles, phase: 7, description: 'Aufgaben und Wissen mit KI-Unterstützung bearbeiten.' },
  { key: 'reports', label: 'Berichte', path: '/reports', icon: ChartNoAxesCombined, phase: 5, description: 'Aufgabenzeiten und Monatsberichte auswerten und exportieren.' },
  { key: 'settings', label: 'Einstellungen', path: '/settings', icon: Settings2, phase: 3, description: 'Benutzer, Integrationen und persönliche Einstellungen.' },
];

export const moduleIcons: Record<ModuleKey, LucideIcon> = {
  today: LayoutDashboard,
  week: CalendarDays,
  ticketsystem: Ticket,
  wiki: BookOpenText,
  ai: Sparkles,
  reports: ChartNoAxesCombined,
  settings: Settings2,
};
