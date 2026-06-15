import { useState, useEffect, useCallback, useMemo } from 'react';
import { Activity, Clock, FileText, Wifi, WifiOff, Rows3, Sun, Moon } from 'lucide-react';
import type { Entry, DashboardStats, EntryFilters, SavedView } from './types';
import type { UndoItem } from './components/UndoToast';
import { api } from './api/client';
import { useSignalR } from './hooks/useSignalR';
import { useKeyboardShortcuts } from './hooks/useKeyboardShortcuts';
import type { KeyboardShortcut } from './hooks/useKeyboardShortcuts';
import { useTheme } from './hooks/useTheme';
import { EntryList } from './components/EntryList';
import { EntryDetail } from './components/EntryDetail';
import { HistoryView } from './components/HistoryView';
import { TemplatesView } from './components/TemplatesView';
import { FilterBar } from './components/FilterBar';
import { ViewBar } from './components/ViewBar';
import { activeViewId, viewToFilters } from './utils/views';
import { BatchActionBar } from './components/BatchActionBar';
import { ShortcutHelp } from './components/ShortcutHelp';
import { ToastContainer, useToasts } from './components/ToastContainer';
import { UndoToastContainer } from './components/UndoToast';
import 'katex/dist/katex.min.css';
import './App.css';

type View = 'active' | 'history' | 'templates';

const DEFAULT_UNDO_WINDOW = 10;

export default function App() {
  const [view, setView] = useState<View>('active');
  const [entries, setEntries] = useState<Entry[]>([]);
  const [selectedEntry, setSelectedEntry] = useState<Entry | null>(null);
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [filters, setFilters] = useState<EntryFilters>({});
  const [views, setViews] = useState<SavedView[]>([]);
  const { toasts, addToast, dismissToast } = useToasts();
  const { theme, toggle: toggleTheme } = useTheme();

  // Batch selection state
  const [selectionMode, setSelectionMode] = useState(false);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  // Undo state
  const [undoItems, setUndoItems] = useState<UndoItem[]>([]);

  // Shortcut help
  const [showShortcuts, setShowShortcuts] = useState(false);

  // Derive unique types and sources from current entries for filter dropdowns
  const uniqueTypes = useMemo(
    () => [...new Set(entries.map((e) => e.type))].sort(),
    [entries],
  );
  const uniqueSources = useMemo(
    () => [...new Set(entries.map((e) => e.source))].sort(),
    [entries],
  );

  // Which saved view (if any) the current filters correspond to.
  const currentViewId = useMemo(() => activeViewId(filters, views), [filters, views]);

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

  useEffect(() => {
    const reloadIfVisible = () => {
      if (document.visibilityState === 'visible') {
        void loadEntries();
      }
    };

    document.addEventListener('visibilitychange', reloadIfVisible);
    window.addEventListener('online', reloadIfVisible);

    return () => {
      document.removeEventListener('visibilitychange', reloadIfVisible);
      window.removeEventListener('online', reloadIfVisible);
    };
  }, [loadEntries]);

  // Load saved views once on mount.
  useEffect(() => {
    api.getViews()
      .then(setViews)
      .catch((err) => console.error('Failed to load views:', err));
  }, []);

  const { isConnected } = useSignalR({
    onEntriesAdded: (newEntries) => {
      loadEntries();
      addToast(newEntries);
    },
    onEntryArchived: (archivedEntry) => {
      setEntries((prev) => prev.filter((e) => e.id !== archivedEntry.id));
      setStats((prev) => prev ? { ...prev, totalPending: Math.max(0, prev.totalPending - 1) } : prev);
      if (selectedEntry?.id === archivedEntry.id) {
        setSelectedEntry(null);
      }
    },
    onEntryDeleted: (entryId) => {
      setEntries((prev) => prev.filter((e) => e.id !== entryId));
      setStats((prev) => prev ? { ...prev, totalPending: Math.max(0, prev.totalPending - 1) } : prev);
      if (selectedEntry?.id === entryId) {
        setSelectedEntry(null);
      }
    },
    onEntryUpdated: (updatedEntry) => {
      setEntries((prev) => prev.map((e) => (e.id === updatedEntry.id ? updatedEntry : e)));
      if (selectedEntry?.id === updatedEntry.id) {
        setSelectedEntry(updatedEntry);
      }
    },
    onReconnected: () => {
      loadEntries();
    },
  });

  const handleSelectEntry = useCallback(async (entry: Entry) => {
    try {
      const fullEntry = await api.getEntry(entry.id);
      setSelectedEntry(fullEntry);
      setEntries((prev) => prev.map((e) => (e.id === fullEntry.id ? fullEntry : e)));
      if (entry.status === 'pending') {
        setStats((prev) => prev ? { ...prev, totalPending: Math.max(0, prev.totalPending - 1), totalViewed: prev.totalViewed + 1 } : prev);
      }
    } catch (err) {
      console.error('Failed to load entry:', err);
    }
  }, []);

  const handleDismiss = useCallback((id: string) => {
    // If the dismissed entry was the currently-selected one, auto-advance
    // to the next entry in the list so the user can keep pressing `d`
    // (or clicking Dismiss) to clear through the queue. Prefer the next
    // sibling; fall back to the previous one; null only if the list is
    // empty afterwards.
    let nextCandidate: Entry | null = null;
    if (selectedEntry?.id === id) {
      const idx = entries.findIndex((e) => e.id === id);
      if (idx >= 0) {
        nextCandidate = entries[idx + 1] ?? entries[idx - 1] ?? null;
      }
    }
    setEntries((prev) => prev.filter((e) => e.id !== id));
    if (selectedEntry?.id === id) {
      if (nextCandidate) {
        // Re-uses handleSelectEntry which loads the full entry, marks
        // it viewed, and updates stats — same as a manual click.
        handleSelectEntry(nextCandidate);
      } else {
        setSelectedEntry(null);
      }
    }
  }, [entries, selectedEntry, handleSelectEntry]);

  // Server-driven dismiss: calls the API and then updates local state.
  // Used by the keyboard shortcut and the inline list-row dismiss button,
  // both of which need to hit the server (unlike EntryDetail's own
  // Dismiss button, which already calls the API and then invokes
  // handleDismiss as a local-cleanup callback).
  const handleDismissById = useCallback(async (id: string) => {
    try {
      await api.dismissEntry(id);
      handleDismiss(id);
    } catch (err) {
      console.error('Dismiss failed:', err);
    }
  }, [handleDismiss]);

  const handleDelete = useCallback((id: string) => {
    setEntries((prev) => prev.filter((e) => e.id !== id));
    setSelectedEntry(null);
  }, []);

  const handleActionExecuted = useCallback(() => {
    loadEntries();
    setSelectedEntry(null);
  }, [loadEntries]);

  const handleEntryUpdated = useCallback((updated: Entry) => {
    setEntries((prev) => prev.map((e) => (e.id === updated.id ? updated : e)));
    setSelectedEntry(updated);
  }, []);

  // --- Saved views ---
  const handleApplyAll = useCallback(() => setFilters({}), []);

  const handleApplyView = useCallback((view: SavedView) => {
    setFilters(viewToFilters(view));
  }, []);

  const handleCreateView = useCallback(
    async (partial: { name: string; type?: string; tags?: string[] }) => {
      const draft: SavedView = {
        id: '',
        name: partial.name,
        type: partial.type,
        tags: partial.tags ?? [],
      };
      try {
        const saved = await api.saveViews([...views, draft]);
        setViews(saved);
        // Apply the new view (server-normalized; fall back to the last entry).
        const created =
          saved.find(
            (v) =>
              v.name === partial.name && (v.type || '') === (partial.type || ''),
          ) ?? saved[saved.length - 1];
        if (created) setFilters(viewToFilters(created));
      } catch (err) {
        console.error('Failed to save view:', err);
      }
    },
    [views],
  );

  const handleDeleteView = useCallback(
    async (id: string) => {
      const wasActive = activeViewId(filters, views) === id;
      try {
        const saved = await api.saveViews(views.filter((v) => v.id !== id));
        setViews(saved);
        if (wasActive) setFilters({});
      } catch (err) {
        console.error('Failed to delete view:', err);
      }
    },
    [views, filters],
  );

  // Clicking a tag chip in the list scopes the feed to that tag.
  const handleTagClick = useCallback((tag: string) => {
    setFilters((prev) => ({ ...prev, tags: tag }));
  }, []);

  // --- Batch selection ---
  const handleToggleSelect = useCallback((id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

  const handleSelectAll = useCallback(() => {
    setSelectedIds((prev) => {
      if (prev.size === entries.length) return new Set();
      return new Set(entries.map((e) => e.id));
    });
  }, [entries]);

  const handleClearSelection = useCallback(() => {
    setSelectedIds(new Set());
    setSelectionMode(false);
  }, []);

  const handleBatchComplete = useCallback(() => {
    setSelectedIds(new Set());
    setSelectionMode(false);
    loadEntries();
  }, [loadEntries]);

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
    setUndoItems((prev) => [...prev, item]);
  }, []);

  const handleUndoDismiss = useCallback((id: number) => {
    setUndoItems((prev) => prev.filter((i) => i.id !== id));
  }, []);

  const handleUndoComplete = useCallback(() => {
    loadEntries();
  }, [loadEntries]);

  // --- Navigation helpers for keyboard shortcuts ---
  const currentIndex = useMemo(
    () => (selectedEntry ? entries.findIndex((e) => e.id === selectedEntry.id) : -1),
    [entries, selectedEntry],
  );

  const selectByIndex = useCallback(
    (index: number) => {
      if (index >= 0 && index < entries.length) {
        handleSelectEntry(entries[index]);
      }
    },
    [entries, handleSelectEntry],
  );

  // --- Keyboard shortcuts definition ---
  const shortcuts: KeyboardShortcut[] = useMemo(
    () => [
      { key: 'j', label: 'j', description: 'Next entry', handler: () => selectByIndex(currentIndex + 1) },
      { key: 'k', label: 'k', description: 'Previous entry', handler: () => selectByIndex(Math.max(0, currentIndex - 1)) },
      { key: 'x', label: 'x', description: 'Toggle selection on focused entry', handler: () => {
        if (selectedEntry) {
          if (!selectionMode) setSelectionMode(true);
          handleToggleSelect(selectedEntry.id);
        }
      }},
      { key: 'd', label: 'd', description: 'Dismiss selected entry', handler: () => {
        if (selectedEntry) handleDismissById(selectedEntry.id);
      }},
      { key: 'e', label: 'e', description: 'Edit selected entry', handler: () => {
        // Editing is handled in EntryDetail — this is a hint
      }},
      { key: 'p', label: 'p', description: 'Toggle pin on selected entry', handler: async () => {
        if (selectedEntry) {
          try {
            const updated = await api.pinEntry(selectedEntry.id);
            handleEntryUpdated(updated);
          } catch {}
        }
      }},
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
      { key: '1', label: '1', description: 'Switch to Active view', handler: () => setView('active') },
      { key: '2', label: '2', description: 'Switch to History view', handler: () => setView('history') },
      { key: '3', label: '3', description: 'Switch to Templates view', handler: () => setView('templates') },
      {
        key: '?', label: '?', description: 'Show keyboard shortcuts',
        handler: () => setShowShortcuts((prev) => !prev),
      },
      {
        key: 'Escape', label: 'Esc', description: 'Close / deselect',
        global: true,
        handler: () => {
          if (showShortcuts) { setShowShortcuts(false); return; }
          if (selectionMode) { handleClearSelection(); return; }
          if (selectedEntry) { setSelectedEntry(null); return; }
        },
      },
    ],
    [
      currentIndex, selectByIndex, selectedEntry, selectionMode,
      handleDismissById, handleEntryUpdated, handleToggleSelect, handleClearSelection,
      showShortcuts,
    ],
  );

  useKeyboardShortcuts({ shortcuts, enabled: view === 'active' });

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
            className={`nav-btn ${view === 'active' ? 'active' : ''}`}
            onClick={() => setView('active')}
          >
            <Activity size={14} />
            Active
            {pendingCount > 0 && <span className="badge-small">{pendingCount}</span>}
          </button>
          <button
            className={`nav-btn ${view === 'history' ? 'active' : ''}`}
            onClick={() => setView('history')}
          >
            <Clock size={14} />
            History
          </button>
          <button
            className={`nav-btn ${view === 'templates' ? 'active' : ''}`}
            onClick={() => setView('templates')}
          >
            <FileText size={14} />
            Templates
          </button>
        </div>
        <div className="app-header-right">
          <button
            className="icon-btn"
            onClick={toggleTheme}
            title={theme === 'dark' ? 'Switch to light theme' : 'Switch to dark theme'}
            aria-label="Toggle theme"
          >
            {theme === 'dark' ? <Sun size={16} /> : <Moon size={16} />}
          </button>
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
        {view === 'active' ? (
          <div className="split-panel">
            <div className="panel-left">
              <ViewBar
                views={views}
                activeId={currentViewId}
                currentFilters={filters}
                onApplyAll={handleApplyAll}
                onApplyView={handleApplyView}
                onCreate={handleCreateView}
                onDelete={handleDeleteView}
              />
              <FilterBar
                filters={filters}
                onChange={setFilters}
                types={uniqueTypes}
                sources={uniqueSources}
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
                  onDismiss={handleDismissById}
                  selectionMode={selectionMode}
                  selectedIds={selectedIds}
                  onToggleSelect={handleToggleSelect}
                  onSelectAll={handleSelectAll}
                  onTagClick={handleTagClick}
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
        ) : view === 'history' ? (
          <HistoryView />
        ) : (
          <TemplatesView />
        )}
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
