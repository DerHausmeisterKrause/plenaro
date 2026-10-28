import {
  BookOpenText, CalendarDays, ChartNoAxesCombined, ClipboardList,
  LayoutDashboard, ListTodo, Settings2, Sparkles, Ticket,
  type LucideIcon,
} from 'lucide-react';

export type ModuleKey =
  | 'today' | 'week' | 'tasks' | 'tickets'
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
  { key: 'today', label: 'Heute', path: '/today', icon: LayoutDashboard, phase: 5, description: 'Deine Aufgaben, Termine und Fokuszeiten im Blick.' },
  { key: 'week', label: 'Wochenplanung', path: '/week', icon: CalendarDays, phase: 5, description: 'Wochentermine, Segmente und verfügbare Zeiträume.' },
  { key: 'tasks', label: 'Aufgaben', path: '/tasks', icon: ListTodo, phase: 5, description: 'Aufgaben erstellen, priorisieren und verwalten.' },
  { key: 'tickets', label: 'Ticketsystem', path: '/tickets', icon: Ticket, phase: 6, description: 'Znuny / OTRS und weitere Ticketsysteme anbinden.' },
  { key: 'wiki', label: 'Wissensdatenbank', path: '/wiki', icon: BookOpenText, phase: 7, description: 'Wiki-Inhalte und angebundene Wissensquellen durchsuchen.' },
  { key: 'ai', label: 'KI-Assistent', path: '/ai', icon: Sparkles, phase: 7, description: 'Aufgaben und Wissen mit KI-Unterstützung bearbeiten.' },
  { key: 'reports', label: 'Berichte', path: '/reports', icon: ChartNoAxesCombined, phase: 5, description: 'Arbeitszeit- und Tätigkeitsberichte auswerten.' },
  { key: 'settings', label: 'Einstellungen', path: '/settings', icon: Settings2, phase: 3, description: 'Benutzer, Integrationen und persönliche Einstellungen.' },
];

export const moduleIcons: Record<ModuleKey, LucideIcon> = {
  today: LayoutDashboard,
  week: CalendarDays,
  tasks: ClipboardList,
  tickets: Ticket,
  wiki: BookOpenText,
  ai: Sparkles,
  reports: ChartNoAxesCombined,
  settings: Settings2,
};
