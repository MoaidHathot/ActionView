import type {
  Entry, ActionExecutionResult, DashboardStats, EntryUpdateRequest,
  BatchResult, EntryTemplate, EntryFilters, SavedView, SortOption, ClientConfig, ViewCounts,
  ActionEvent,
} from '../types';

const API_BASE = '/api';

async function fetchJson<T>(url: string, options?: RequestInit): Promise<T> {
  const res = await fetch(url, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
  });
  if (!res.ok) {
    const errorBody = await res.text().catch(() => '');
    throw new Error(`HTTP ${res.status}: ${errorBody || res.statusText}`);
  }
  if (res.status === 204) return undefined as T;
  return res.json();
}

function applyFilterParams(params: URLSearchParams, filters?: EntryFilters, sort?: SortOption) {
  if (filters?.type) params.set('type', filters.type);
  if (filters?.severity) params.set('severity', filters.severity);
  if (filters?.source) params.set('source', filters.source);
  if (filters?.tags) params.set('tags', filters.tags);
  if (filters?.tagMode) params.set('tagMode', filters.tagMode);
  if (filters?.search) params.set('search', filters.search);
  if (sort && sort.field !== 'default') {
    params.set('sort', sort.field);
    params.set('dir', sort.direction);
  }
}

export const api = {
  // --- Entries ---

  getEntries: (filters?: EntryFilters, sort?: SortOption) => {
    const params = new URLSearchParams();
    applyFilterParams(params, filters, sort);
    const qs = params.toString();
    return fetchJson<Entry[]>(`${API_BASE}/entries${qs ? `?${qs}` : ''}`);
  },

  getEntry: (id: string) =>
    fetchJson<Entry>(`${API_BASE}/entries/${id}`),

  updateEntry: (id: string, update: EntryUpdateRequest) =>
    fetchJson<Entry>(`${API_BASE}/entries/${id}`, {
      method: 'PUT',
      body: JSON.stringify(update),
    }),

  executeAction: (entryId: string, actionIndex: number, parameters?: Record<string, string>) =>
    fetchJson<ActionExecutionResult>(`${API_BASE}/entries/${entryId}/actions/${actionIndex}`, {
      method: 'POST',
      body: parameters ? JSON.stringify({ parameters }) : undefined,
    }),

  executeSectionAction: (
    entryId: string,
    blockPath: number[],
    actionIndex: number,
    parameters?: Record<string, string>,
  ) =>
    fetchJson<ActionExecutionResult>(
      `${API_BASE}/entries/${entryId}/blocks/${blockPath.join('.')}/actions/${actionIndex}`,
      {
        method: 'POST',
        body: parameters ? JSON.stringify({ parameters }) : undefined,
      },
    ),

  // Per-entry action history (audit log). Survives archive/dismiss/delete.
  getEntryHistory: (entryId: string, limit?: number) =>
    fetchJson<ActionEvent[]>(
      `${API_BASE}/entries/${entryId}/history${limit ? `?limit=${limit}` : ''}`,
    ),

  dismissEntry: (id: string) =>
    fetchJson<Entry>(`${API_BASE}/entries/${id}/dismiss`, { method: 'POST' }),

  deleteEntry: (id: string) =>
    fetchJson<void>(`${API_BASE}/entries/${id}`, { method: 'DELETE' }),

  pinEntry: (id: string) =>
    fetchJson<Entry>(`${API_BASE}/entries/${id}/pin`, { method: 'POST' }),

  undoEntry: (id: string) =>
    fetchJson<Entry>(`${API_BASE}/entries/${id}/undo`, { method: 'POST' }),

  // --- Batch operations ---

  batchDismiss: (ids: string[]) =>
    fetchJson<BatchResult>(`${API_BASE}/entries/batch/dismiss`, {
      method: 'POST',
      body: JSON.stringify({ ids }),
    }),

  batchDelete: (ids: string[]) =>
    fetchJson<BatchResult>(`${API_BASE}/entries/batch/delete`, {
      method: 'POST',
      body: JSON.stringify({ ids }),
    }),

  batchAction: (ids: string[], actionLabel: string, parameters?: Record<string, string>) =>
    fetchJson<BatchResult>(`${API_BASE}/entries/batch/action`, {
      method: 'POST',
      body: JSON.stringify({ ids, actionLabel, parameters }),
    }),

  // --- History ---

  getHistory: (filters?: EntryFilters, limit = 50, offset = 0, sort?: SortOption) => {
    const params = new URLSearchParams();
    applyFilterParams(params, filters, sort);
    params.set('limit', limit.toString());
    params.set('offset', offset.toString());
    return fetchJson<Entry[]>(`${API_BASE}/history?${params}`);
  },

  getHistoryEntry: (id: string) =>
    fetchJson<Entry>(`${API_BASE}/history/${id}`),

  // --- Stats ---

  getStats: () =>
    fetchJson<DashboardStats>(`${API_BASE}/stats`),

  // --- Templates ---

  getTemplates: () =>
    fetchJson<EntryTemplate[]>(`${API_BASE}/templates`),

  getTemplate: (type: string) =>
    fetchJson<EntryTemplate>(`${API_BASE}/templates/${type}`),

  getAutoDiscoveredTypes: () =>
    fetchJson<string[]>(`${API_BASE}/templates/auto-discovered`),

  createTemplate: (template: EntryTemplate) =>
    fetchJson<EntryTemplate>(`${API_BASE}/templates`, {
      method: 'POST',
      body: JSON.stringify(template),
    }),

  deleteTemplate: (type: string) =>
    fetchJson<void>(`${API_BASE}/templates/${type}`, { method: 'DELETE' }),

  // --- Views (saved filter presets) ---

  getViews: () =>
    fetchJson<SavedView[]>(`${API_BASE}/views`),

  // Replaces the full set of saved views and persists them to actionview.json.
  saveViews: (views: SavedView[]) =>
    fetchJson<SavedView[]>(`${API_BASE}/views`, {
      method: 'PUT',
      body: JSON.stringify(views),
    }),

  // Active-entry counts per view (for pill badges).
  getViewCounts: () =>
    fetchJson<ViewCounts>(`${API_BASE}/views/counts`),

  // --- Server config (read-only slice the dashboard mirrors) ---

  getConfig: () =>
    fetchJson<ClientConfig>(`${API_BASE}/config`),
};
