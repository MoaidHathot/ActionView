import { useState, useEffect, useCallback, useMemo } from 'react';
import { Routes, Route, useNavigate, useParams, useLocation, Navigate } from 'react-router-dom';
import { Activity, Clock, Wifi, WifiOff, Rows3 } from 'lucide-react';
import type { Entry, DashboardStats, EntryFilters, EntryTemplate } from './types';
import type { UndoItem } from './components/UndoToast';
import { api } from './api/client';
import { useSignalR } from './hooks/useSignalR';
import { useKeyboardShortcuts } from './hooks/useKeyboardShortcuts';
import type { KeyboardShortcut } from './hooks/useKeyboardShortcuts';
import { useTimestampRefresh } from './hooks/useRelativeTime';
import { EntryList } from './components/EntryList';
import { EntryDetail } from './components/EntryDetail';
import { HistoryView } from './components/HistoryView';
import { FilterBar } from './components/FilterBar';
import { BatchActionBar } from './components/BatchActionBar';
import { ShortcutHelp } from './components/ShortcutHelp';
import { ToastContainer, useToasts } from './components/ToastContainer';
import { UndoToastContainer } from './components/UndoToast';
import './App.css';

const DEFAULT_UNDO_WINDOW = 10;

/** Inner component that uses route params for the active view */
function ActiveView({
  entries, stats, loading, filters, setFilters,
  selectionMode, setSelectionMode, selectedIds, setSelectedIds,
  setUndoItems, loadEntries, uniqueTypes, uniqueSources,
  templates,
}: {
  entries: Entry[];
  stats: DashboardStats | null;
  loading: boolean;
  filters: EntryFilters;
  setFilters: (f: EntryFilters) => void;
  selectionMode: boolean;
  setSelectionMode: (v: boolean | ((p: boolean) => boolean)) => void;
  selectedIds: Set<string>;
  setSelectedIds: (v: Set<string> | ((p: Set<string>) => Set<string>)) => void;
  setUndoItems: (v: UndoItem[] | ((p: UndoItem[]) => UndoItem[])) => void;
  loadEntries: () => void;
  uniqueTypes: string[];
  uniqueSources: string[];
  templates: EntryTemplate[];
}) {
  const { entryId } = useParams<{ entryId?: string }>();
  const navigate = useNavigate();
  const [selectedEntry, setSelectedEntry] = useState<Entry | null>(null);
  const timestampTick = useTimestampRefresh(30_000);

  // Load entry from URL param
  useEffect(() => {
    if (entryId) {
      api.getEntry(entryId)
        .then((entry) => setSelectedEntry(entry))
        .catch(() => {
          setSelectedEntry(null);
          navigate('/active', { replace: true });
        });
    } else {
      setSelectedEntry(null);
    }
  }, [entryId, navigate]);

  // When selectedEntry changes from the list, also sync local state
  const handleSelectEntry = useCallback(async (entry: Entry) => {
    try {
      const fullEntry = await api.getEntry(entry.id);
      setSelectedEntry(fullEntry);
      navigate(`/active/${entry.id}`);
    } catch (err) {
      console.error('Failed to load entry:', err);
    }
  }, [navigate]);

  const handleDismiss = useCallback((_id: string) => {
    setSelectedEntry(null);
    navigate('/active', { replace: true });
  }, [navigate]);

  const handleDelete = useCallback((_id: string) => {
    setSelectedEntry(null);
    navigate('/active', { replace: true });
  }, [navigate]);

  const handleActionExecuted = useCallback(() => {
    loadEntries();
    setSelectedEntry(null);
    navigate('/active', { replace: true });
  }, [loadEntries, navigate]);

  const handleEntryUpdated = useCallback((updated: Entry) => {
    setSelectedEntry(updated);
  }, []);

  // --- Batch selection ---
  const handleToggleSelect = useCallback((id: string) => {
    setSelectedIds((prev: Set<string>) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, [setSelectedIds]);

  const handleSelectAll = useCallback(() => {
    setSelectedIds((prev: Set<string>) => {
      if (prev.size === entries.length) return new Set();
      return new Set(entries.map((e) => e.id));
    });
  }, [entries, setSelectedIds]);

  const handleClearSelection = useCallback(() => {
    setSelectedIds(new Set());
    setSelectionMode(false);
  }, [setSelectedIds, setSelectionMode]);

  const handleBatchComplete = useCallback(() => {
    setSelectedIds(new Set());
    setSelectionMode(false);
    loadEntries();
  }, [loadEntries, setSelectedIds, setSelectionMode]);

  // Compute common action labels for batch bar
  const commonActions = useMemo(() => {
    if (selectedIds.size === 0) return [];
    const selected = entries.filter((e) => selectedIds.has(e.id));
    if (selected.length === 0) return [];
    const labelSets = selected.map((e) => new Set(e.actions.map((a) => a.label)));
    const first = labelSets[0];
    return [...first].filter((label) => labelSets.every((s) => s.has(label)));
  }, [entries, selectedIds]);

  // --- Undo ---
  const handleUndoCreated = useCallback((item: UndoItem) => {
    setUndoItems((prev: UndoItem[]) => [...prev, item]);
  }, [setUndoItems]);

  // Template description for the selected entry
  const templateDesc = useMemo(() => {
    if (!selectedEntry) return undefined;
    const tpl = templates.find((t) => t.type === selectedEntry.type);
    return tpl?.description;
  }, [selectedEntry, templates]);

  return (
    <div className="split-panel">
      <div className="panel-left">
        <FilterBar
          filters={filters}
          onChange={setFilters}
          types={uniqueTypes}
          sources={uniqueSources}
          stats={stats}
        />
        {selectionMode && selectedIds.size > 0 && (
          <BatchActionBar
            selectedIds={selectedIds}
            commonActions={commonActions}
            onClearSelection={handleClearSelection}
            onBatchComplete={handleBatchComplete}
          />
        )}
        {loading ? (
          <div className="loading">Loading...</div>
        ) : (
          <EntryList
            entries={entries}
            selectedId={selectedEntry?.id}
            onSelect={handleSelectEntry}
            selectionMode={selectionMode}
            selectedIds={selectedIds}
            onToggleSelect={handleToggleSelect}
            onSelectAll={handleSelectAll}
            searchQuery={filters.search}
            _tick={timestampTick}
          />
        )}
      </div>
      <div className="panel-right">
        {selectedEntry ? (
          <EntryDetail
            entry={selectedEntry}
            onDismiss={handleDismiss}
            onDelete={handleDelete}
            onActionExecuted={handleActionExecuted}
            onEntryUpdated={handleEntryUpdated}
            onUndoCreated={handleUndoCreated}
            defaultUndoWindow={DEFAULT_UNDO_WINDOW}
            templateDescription={templateDesc}
          />
        ) : (
          <div className="no-selection">
            <Activity size={48} strokeWidth={1} />
            <p>Select an entry to review</p>
            <p className="subtle">Press <kbd>?</kbd> for keyboard shortcuts</p>
          </div>
        )}
      </div>
    </div>
  );
}

export default function App() {
  const location = useLocation();
  const navigate = useNavigate();

  const [entries, setEntries] = useState<Entry[]>([]);
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [filters, setFilters] = useState<EntryFilters>({});
  const { toasts, addToast, dismissToast } = useToasts();

  // Batch selection state
  const [selectionMode, setSelectionMode] = useState(false);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  // Undo state
  const [undoItems, setUndoItems] = useState<UndoItem[]>([]);

  // Shortcut help
  const [showShortcuts, setShowShortcuts] = useState(false);

  // Templates (Feature 5)
  const [templates, setTemplates] = useState<EntryTemplate[]>([]);

  // Derive unique types and sources from current entries for filter dropdowns
  const uniqueTypes = useMemo(
    () => [...new Set(entries.map((e) => e.type))].sort(),
    [entries],
  );
  const uniqueSources = useMemo(
    () => [...new Set(entries.map((e) => e.source))].sort(),
    [entries],
  );

  const loadEntries = useCallback(async () => {
    try {
      const [entriesData, statsData] = await Promise.all([
        api.getEntries(filters),
        api.getStats(),
      ]);
      setEntries(entriesData);
      setStats(statsData);
    } catch (err) {
      console.error('Failed to load entries:', err);
    } finally {
      setLoading(false);
    }
  }, [filters]);

  useEffect(() => {
    loadEntries();
  }, [loadEntries]);

  // Load templates once (Feature 5)
  useEffect(() => {
    api.getTemplates()
      .then(setTemplates)
      .catch(() => setTemplates([]));
  }, []);

  const { isConnected } = useSignalR({
    onEntriesAdded: (newEntries) => {
      setEntries((prev) => [...newEntries, ...prev]);
      setStats((prev) => prev ? { ...prev, totalPending: prev.totalPending + newEntries.length } : prev);
      addToast(newEntries);
    },
    onEntryArchived: (archivedEntry) => {
      setEntries((prev) => prev.filter((e) => e.id !== archivedEntry.id));
      setStats((prev) => prev ? { ...prev, totalPending: Math.max(0, prev.totalPending - 1) } : prev);
    },
    onEntryDeleted: (entryId) => {
      setEntries((prev) => prev.filter((e) => e.id !== entryId));
      setStats((prev) => prev ? { ...prev, totalPending: Math.max(0, prev.totalPending - 1) } : prev);
    },
    onEntryUpdated: (updatedEntry) => {
      setEntries((prev) => prev.map((e) => (e.id === updatedEntry.id ? updatedEntry : e)));
    },
    onReconnected: () => {
      // Refresh all data after reconnection to pick up any events missed while disconnected
      loadEntries();
    },
  });

  // Fallback polling: when SignalR is disconnected, poll every 5 seconds
  useEffect(() => {
    if (isConnected) return;
    const interval = setInterval(() => {
      loadEntries();
    }, 5000);
    return () => clearInterval(interval);
  }, [isConnected, loadEntries]);

  // Derive current view from URL
  const isActiveView = location.pathname.startsWith('/active') || location.pathname === '/';

  // --- Undo handlers ---
  const handleUndoDismiss = useCallback((id: number) => {
    setUndoItems((prev) => prev.filter((i) => i.id !== id));
  }, []);

  const handleUndoComplete = useCallback(() => {
    loadEntries();
  }, [loadEntries]);

  // --- Keyboard shortcuts definition ---
  const shortcuts: KeyboardShortcut[] = useMemo(
    () => [
      { key: 'j', label: 'j', description: 'Next entry', handler: () => {} },
      { key: 'k', label: 'k', description: 'Previous entry', handler: () => {} },
      { key: 'x', label: 'x', description: 'Toggle selection on focused entry', handler: () => {} },
      { key: 'd', label: 'd', description: 'Dismiss selected entry', handler: () => {} },
      { key: 'e', label: 'e', description: 'Edit selected entry', handler: () => {} },
      { key: 'p', label: 'p', description: 'Toggle pin on selected entry', handler: () => {} },
      { key: '/', label: '/', description: 'Focus search', handler: () => {
        const input = document.querySelector<HTMLInputElement>('.filter-search-input');
        input?.focus();
      }},
      { key: 's', label: 's', description: 'Toggle batch selection mode', handler: () => {
        setSelectionMode((prev) => {
          if (prev) setSelectedIds(new Set());
          return !prev;
        });
      }},
      { key: '1', label: '1', description: 'Switch to Active view', handler: () => navigate('/active') },
      { key: '2', label: '2', description: 'Switch to History view', handler: () => navigate('/history') },
      {
        key: '?', label: '?', description: 'Show keyboard shortcuts',
        handler: () => setShowShortcuts((prev) => !prev),
      },
      {
        key: 'Escape', label: 'Esc', description: 'Close / deselect',
        global: true,
        handler: () => {
          if (showShortcuts) { setShowShortcuts(false); return; }
          if (selectionMode) { setSelectedIds(new Set()); setSelectionMode(false); return; }
          if (location.pathname.startsWith('/active/')) {
            navigate('/active');
            return;
          }
        },
      },
    ],
    [selectionMode, showShortcuts, navigate, location.pathname],
  );

  useKeyboardShortcuts({ shortcuts, enabled: isActiveView });

  const pendingCount = stats?.totalPending ?? 0;

  return (
    <div className="app">
      <header className="app-header">
        <div className="app-title">
          <Activity size={20} />
          <h1>ActionView</h1>
          {pendingCount > 0 && (
            <span className="badge">{pendingCount}</span>
          )}
        </div>
        <div className="app-nav">
          <button
            className={`nav-btn ${isActiveView ? 'active' : ''}`}
            onClick={() => navigate('/active')}
          >
            <Activity size={14} />
            Active
            {pendingCount > 0 && <span className="badge-small">{pendingCount}</span>}
          </button>
          <button
            className={`nav-btn ${location.pathname.startsWith('/history') ? 'active' : ''}`}
            onClick={() => navigate('/history')}
          >
            <Clock size={14} />
            History
          </button>
        </div>
        <div className="app-header-right">
          <button
            className={`icon-btn ${selectionMode ? 'active' : ''}`}
            onClick={() => {
              setSelectionMode((prev) => {
                if (prev) setSelectedIds(new Set());
                return !prev;
              });
            }}
            title="Toggle batch selection (s)"
          >
            <Rows3 size={16} />
          </button>
          <div className="app-status">
            {isConnected ? (
              <span className="status-connected"><Wifi size={14} /> Live</span>
            ) : (
              <span className="status-disconnected"><WifiOff size={14} /> Offline</span>
            )}
          </div>
        </div>
      </header>

      <main className="app-main">
        <Routes>
          <Route path="/" element={<Navigate to="/active" replace />} />
          <Route
            path="/active/:entryId?"
            element={
              <ActiveView
                entries={entries}
                stats={stats}
                loading={loading}
                filters={filters}
                setFilters={setFilters}
                selectionMode={selectionMode}
                setSelectionMode={setSelectionMode}
                selectedIds={selectedIds}
                setSelectedIds={setSelectedIds}
                setUndoItems={setUndoItems}
                loadEntries={loadEntries}
                uniqueTypes={uniqueTypes}
                uniqueSources={uniqueSources}
                templates={templates}
              />
            }
          />
          <Route path="/history/:entryId?" element={<HistoryView />} />
        </Routes>
      </main>
      <ToastContainer toasts={toasts} onDismiss={dismissToast} />
      <UndoToastContainer
        items={undoItems}
        onDismiss={handleUndoDismiss}
        onUndoComplete={handleUndoComplete}
      />
      <ShortcutHelp
        shortcuts={shortcuts}
        visible={showShortcuts}
        onClose={() => setShowShortcuts(false)}
      />
    </div>
  );
}
