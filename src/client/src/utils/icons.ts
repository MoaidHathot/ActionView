import type { ComponentType } from 'react';
import { createElement } from 'react';
import {
  Briefcase, User, Users, House, Rocket, Bug, Flame, Bell, AlertTriangle,
  GitPullRequest, Code, Server, Shield, Star, Heart, Calendar, Mail, Folder,
  Zap, CheckCircle, MessageSquare, Tag, Bookmark, Inbox, Activity, Clock,
  FileText, Globe, Database, Terminal,
} from 'lucide-react';

type IconComponent = ComponentType<{ size?: number }>;

/**
 * Curated kebab-case -> lucide component map used for view pills. Kept small and
 * explicit (rather than dynamically importing all of lucide) to keep the bundle
 * lean and the picker focused on category-style icons.
 */
const ICONS: Record<string, IconComponent> = {
  briefcase: Briefcase,
  user: User,
  users: Users,
  home: House,
  house: House,
  rocket: Rocket,
  bug: Bug,
  flame: Flame,
  bell: Bell,
  'alert-triangle': AlertTriangle,
  'git-pull-request': GitPullRequest,
  code: Code,
  server: Server,
  shield: Shield,
  star: Star,
  heart: Heart,
  calendar: Calendar,
  mail: Mail,
  folder: Folder,
  zap: Zap,
  'check-circle': CheckCircle,
  'message-square': MessageSquare,
  tag: Tag,
  bookmark: Bookmark,
  inbox: Inbox,
  activity: Activity,
  clock: Clock,
  'file-text': FileText,
  globe: Globe,
  database: Database,
  terminal: Terminal,
};

/** Ordered list of icon names offered in the view icon picker. */
export const VIEW_ICON_NAMES: string[] = Object.keys(ICONS).filter((n) => n !== 'house');

/** Resolves a kebab-case icon name to a lucide component, or undefined if unknown. */
export function getIcon(name?: string | null): IconComponent | undefined {
  if (!name) return undefined;
  return ICONS[name.trim().toLowerCase()];
}

/**
 * Renders an icon by name as a React node (or null when unknown). Using
 * createElement here keeps callers from assigning a dynamically-resolved
 * component to a PascalCase variable in their own render.
 */
export function renderIcon(name?: string | null, size = 14) {
  const icon = getIcon(name);
  return icon ? createElement(icon, { size }) : null;
}
