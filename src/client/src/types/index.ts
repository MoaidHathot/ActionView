// TypeScript types matching the C# models

export type Severity = 'low' | 'medium' | 'high' | 'critical';
export type EntryStatus = 'pending' | 'viewed' | 'archived';
export type ContentBlockType =
  | 'markdown' | 'code' | 'json' | 'table' | 'keyValue' | 'link'
  | 'section' | 'divider' | 'alert' | 'image' | 'diff' | 'video'
  | 'gallery' | 'timeline' | 'tabs' | 'stat' | 'file' | 'chart'
  | 'diagram' | 'beforeAfter';
export type AlertLevel = 'info' | 'warning' | 'error' | 'success';
export type ActionStyle = 'default' | 'primary' | 'success' | 'danger';
export type PostActionBehavior = 'archive' | 'keep' | 'delete';
export type CommandType = 'http' | 'cli';
export type ActionParameterType = 'text' | 'multiline' | 'select' | 'number' | 'boolean';
export type Trend = 'up' | 'down' | 'flat';

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
  groupId?: string;
  groupLabel?: string;
  pinned: boolean;
  priority: number;
}

/**
 * A cell in a TableBlock or a value in a KeyValueBlock. Either a plain
 * string (the simple case) or a typed object that carries richer rendering
 * instructions. Producers can emit a status pill, link, copy-to-clipboard
 * chip, code-formatted span, or markdown inline without shoehorning into
 * a plain string.
 */
export type RichCell =
  | string
  | { type: 'text'; value: string; mono?: boolean }
  | { type: 'link'; url: string; label?: string; icon?: string }
  | { type: 'status'; level: AlertLevel; label: string }
  | { type: 'badge'; label: string; color?: string }
  | { type: 'code'; value: string; language?: string }
  | { type: 'copy'; value: string; display?: string }
  | { type: 'markdown'; value: string }
  | { type: 'image'; url: string; alt?: string };

export interface LinkItem {
  url: string;
  label?: string;
  body?: string;
  icon?: string;
}

export interface GalleryImage {
  url: string;
  alt?: string;
  caption?: string;
  timestampUrl?: string;
  thumbnail?: string;
}

export interface ImageAnnotation {
  shape: 'arrow' | 'box' | 'circle' | 'text';
  x: number;
  y: number;
  width?: number;
  height?: number;
  label?: string;
  level?: AlertLevel;
}

export interface CodeAnnotation {
  line: number;
  level?: AlertLevel;
  body: string;
  author?: string;
}

export interface VideoChapter {
  at: number;
  label: string;
}

export interface TimelineEvent {
  at: string;
  label: string;
  body?: string;
  level?: AlertLevel;
  icon?: string;
}

export interface TabItem {
  label: string;
  content?: ContentBlock[];
  badge?: string;
}

export interface ChartSeries {
  name: string;
  data: number[];
  color?: string;
}

export interface ContentBlock {
  type: ContentBlockType | string;
  label?: string;
  body?: unknown;

  // Code / Diff
  language?: string;
  filename?: string;
  highlight?: number[];
  showLineNumbers?: boolean;
  wordWrap?: boolean;
  annotations?: CodeAnnotation[];
  mode?: string;
  oldFilename?: string;
  newFilename?: string;

  // Table
  columns?: string[];
  rows?: RichCell[][];
  sortable?: boolean;
  filterable?: boolean;

  // KeyValue
  pairs?: Record<string, RichCell>;

  // Section / Tabs
  title?: string;
  content?: ContentBlock[];
  actions?: EntryAction[];
  defaultCollapsed?: boolean;
  badge?: string;

  // Alert
  level?: AlertLevel;
  dismissible?: boolean;

  // Link / Image / Video / File shared
  url?: string;
  links?: LinkItem[];
  icon?: string;

  // Image
  alt?: string;
  caption?: string;
  maxWidth?: number;
  timestampUrl?: string;
  imageAnnotations?: ImageAnnotation[];

  // Before/after slider
  beforeUrl?: string;
  afterUrl?: string;
  beforeLabel?: string;
  afterLabel?: string;

  // Gallery
  images?: GalleryImage[];

  // Video
  provider?: string;
  videoId?: string;
  startTime?: number;
  endTime?: number;
  poster?: string;
  chapters?: VideoChapter[];

  // Timeline
  events?: TimelineEvent[];

  // Tabs
  tabs?: TabItem[];

  // Stat
  value?: string;
  delta?: string;
  trend?: Trend;
  unit?: string;
  sparkline?: number[];

  // File
  fileSize?: number;
  mimeType?: string;

  // Chart
  chartType?: 'line' | 'bar' | 'area' | 'pie';
  series?: ChartSeries[];
  xAxis?: string[];
}

export interface EntryAction {
  label: string;
  style: ActionStyle;
  confirmMessage?: string;
  command: ActionCommand;
  parameters?: ActionParameter[];
  onSuccess: PostActionBehavior;
  undoCommand?: ActionCommand;
  undoWindowSeconds?: number;
}

export interface ActionParameter {
  name: string;
  label: string;
  type: ActionParameterType;
  default?: string;
  options?: string[];
  required?: boolean;
  placeholder?: string;
  helpText?: string;
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

export interface EntryUpdateRequest {
  title?: string;
  subtitle?: string;
  severity?: Severity;
  tags?: string[];
  content?: ContentBlock[];
  actions?: EntryAction[];
  priority?: number;
}

export interface BatchResult {
  dismissed?: number;
  deleted?: number;
  succeeded?: number;
  failed?: number;
  total: number;
}

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

export interface EntryFilters {
  type?: string;
  severity?: string;
  source?: string;
  tags?: string;
  search?: string;
}
