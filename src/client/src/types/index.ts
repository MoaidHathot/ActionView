// TypeScript types matching the C# models

export type Severity = 'low' | 'medium' | 'high' | 'critical';
export type EntryStatus = 'pending' | 'viewed' | 'archived';
export type ContentBlockType = 'markdown' | 'code' | 'json' | 'table' | 'keyValue' | 'link' | 'section' | 'divider' | 'alert';
export type AlertLevel = 'info' | 'warning' | 'error' | 'success';
export type ActionStyle = 'default' | 'primary' | 'success' | 'danger';
export type PostActionBehavior = 'archive' | 'keep' | 'delete';
export type CommandType = 'http' | 'cli';

export interface Entry {
  id: string;
  schemaVersion: string;
  type: string;
  source: string;
  createdAt: string;
  title: string;
  subtitle?: string;
  severity: Severity;
  icon?: string;
  tags: string[];
  content: ContentBlock[];
  actions: EntryAction[];
  status: EntryStatus;
  receivedAt?: string;
  viewedAt?: string;
  outcome?: EntryOutcome;
  // Grouping
  groupId?: string;
  groupLabel?: string;
  // Priority & Pinning
  pinned: boolean;
  priority: number;
}

export interface ContentBlock {
  type: ContentBlockType | string; // string for plugin block types
  label?: string;
  body?: unknown;
  language?: string;
  filename?: string;
  highlight?: number[];
  columns?: string[];
  rows?: string[][];
  pairs?: Record<string, string>;
  title?: string;
  content?: ContentBlock[];
  actions?: EntryAction[];
  level?: AlertLevel;
  url?: string;
}

export interface EntryAction {
  label: string;
  style: ActionStyle;
  confirmMessage?: string;
  command: ActionCommand;
  onSuccess: PostActionBehavior;
  undoCommand?: ActionCommand;
  undoWindowSeconds?: number;
}

export interface ActionCommand {
  type: CommandType;
  method?: string;
  url?: string;
  headers?: Record<string, string>;
  body?: unknown;
  program?: string;
  args?: string[];
  workingDirectory?: string;
}

export interface EntryOutcome {
  action: string;
  timestamp: string;
  success: boolean;
  resultMessage?: string;
}

export interface DashboardStats {
  totalPending: number;
  totalViewed: number;
  countByType: Record<string, number>;
  countBySeverity: Record<string, number>;
}

export interface ActionExecutionResult {
  success: boolean;
  message?: string;
  statusCode?: number;
  output?: string;
}

// --- Update request for editing entries ---
export interface EntryUpdateRequest {
  title?: string;
  subtitle?: string;
  severity?: Severity;
  tags?: string[];
  content?: ContentBlock[];
  actions?: EntryAction[];
  priority?: number;
}

// --- Batch request/response types ---
export interface BatchResult {
  dismissed?: number;
  deleted?: number;
  succeeded?: number;
  failed?: number;
  total: number;
}

// --- Template types ---
export interface EntryTemplate {
  type: string;
  description?: string;
  defaults?: EntryDefaults;
  contentTemplate?: ContentTemplateBlock[];
  expectedActions?: ActionTemplateBlock[];
}

export interface EntryDefaults {
  severity?: Severity;
  icon?: string;
  tags?: string[];
}

export interface ContentTemplateBlock {
  type: ContentBlockType | string;
  label?: string;
  required?: boolean;
  requiredKeys?: string[];
  keyAliases?: Record<string, string>;
  title?: string;
  titleAliases?: string[];
}

export interface ActionTemplateBlock {
  label: string;
  style?: ActionStyle;
}

// --- Filter state ---
export interface EntryFilters {
  type?: string;
  severity?: string;
  source?: string;
  tags?: string;
  search?: string;
}
