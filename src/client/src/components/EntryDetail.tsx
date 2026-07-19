import { useState, useCallback, useEffect, useRef, useMemo } from 'react';
import { Trash2, X, Edit3, Pin, PinOff, Search, Download, Eye, History, Pencil } from 'lucide-react';
import type { Entry, ActionEvent, ContentBlock } from '../types';
import type { UndoItem } from './UndoToast';
import { BlockRenderer } from './content-blocks/BlockRenderer';
import { ActionButton } from './ActionButton';
import { ActivityPanel } from './ActivityPanel';
import { EntryEditor } from './EntryEditor';
import { EntryErrorBoundary } from './EntryErrorBoundary';
import { BlockShell } from './BlockShell';
import { EntrySearch } from './EntrySearch';
import { api } from '../api/client';
import { createUndoItem } from './UndoToast';
import { useBlockUiState } from '../hooks/useBlockUiState';
import { deriveMarkers } from '../utils/markers';
import { expandRefs } from '../utils/refExpand';
import { useActionJobs } from '../context/ActionJobsProvider';
import { entryToMarkdown, entryToHtml, downloadFile } from '../utils/exportEntry';

/** True when any block (at any depth) has been edited from the dashboard. */
function contentHasEdits(blocks: ContentBlock[] | undefined): boolean {
  if (!blocks) return false;
  for (const b of blocks) {
    if (b.edited) return true;
    if (contentHasEdits(b.content)) return true;
  }
  return false;
}

interface Props {
  entry: Entry;
  onDismiss: (id: string) => void;
  onDelete: (id: string) => void;
  onEntryUpdated: (entry: Entry) => void;
  onUndoCreated?: (item: UndoItem) => void;
  defaultUndoWindow: number;
}

interface OrderedBlock {
  /** Original index in entry.content (used for keys + section action targeting). */
  origIndex: number;
  /** Stable string key for shell state. */
  blockKey: string;
  block: import('../types').ContentBlock;
}

export function EntryDetail({
  entry, onDismiss, onDelete, onEntryUpdated, onUndoCreated, defaultUndoWindow,
}: Props) {
  const [actionResult, setActionResult] = useState<{ success: boolean; message: string } | null>(null);
  const [editing, setEditing] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  const [exportMenuOpen, setExportMenuOpen] = useState(false);
  const [activityOpen, setActivityOpen] = useState(false);
  const [history, setHistory] = useState<ActionEvent[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);

  const contentRef = useRef<HTMLDivElement>(null);
  const { pinned, hidden, togglePinned, toggleHidden, unhideAll } = useBlockUiState(entry.id);
  const { jobs, upsert } = useActionJobs();

  // Outcome markers derived from the audit history (generic: any action's
  // label/style becomes a per-target status chip once it has run).
  const markers = useMemo(() => deriveMarkers(history), [history]);
  const hasEdits = useMemo(() => contentHasEdits(entry.content), [entry.content]);
  const expandRef = useCallback(
    (text: string, self?: ContentBlock) => expandRefs(text, entry, self),
    [entry],
  );

  const reloadHistory = useCallback(async () => {
    setHistoryLoading(true);
    try {
      setHistory(await api.getEntryHistory(entry.id));
    } catch (err) {
      console.error('Failed to load activity:', err);
    } finally {
      setHistoryLoading(false);
    }
  }, [entry.id]);

  // Track which finished jobs we've already surfaced (reset per entry).
  const processedJobs = useRef<Set<string>>(new Set());

  useEffect(() => {
    setActionResult(null);
    setEditing(false);
    setSearchOpen(false);
    setExportMenuOpen(false);
    setActivityOpen(false);
    processedJobs.current = new Set();
    void reloadHistory();
  }, [entry.id, reloadHistory]);

  // When a background job for this entry finishes, surface the result, reload
  // the activity/markers, and create an undo toast if the action supports it.
  useEffect(() => {
    let sawTerminal = false;
    for (const job of jobs.values()) {
      if (job.entryId !== entry.id) continue;
      const terminal = job.status === 'succeeded' || job.status === 'failed' || job.status === 'cancelled';
      if (!terminal || processedJobs.current.has(job.id)) continue;
      processedJobs.current.add(job.id);
      sawTerminal = true;
      setActionResult({ success: job.status === 'succeeded', message: job.message ?? job.status });
      if (job.status === 'succeeded') {
        const action = entry.actions.find((a) => a.label === job.actionLabel);
        if (action?.undoCommand && onUndoCreated) {
          const windowSec = action.undoWindowSeconds ?? defaultUndoWindow;
          onUndoCreated(createUndoItem(entry.id, entry.title, action.label, windowSec));
        }
      }
    }
    if (sawTerminal) void reloadHistory();
  }, [jobs, entry.id, entry.actions, entry.title, onUndoCreated, defaultUndoWindow, reloadHistory]);

  // Deep-link anchor (#block-N): scroll once after first render.
  useEffect(() => {
    const hash = window.location.hash;
    if (!hash) return;
    const id = hash.slice(1);
    requestAnimationFrame(() => {
      const el = document.getElementById(id);
      if (el) {
        el.scrollIntoView({ block: 'center', behavior: 'smooth' });
        el.classList.add('block-shell-flash');
        setTimeout(() => el.classList.remove('block-shell-flash'), 1600);
      }
    });
  }, [entry.id]);

  const handleAction = useCallback(async (actionIndex: number, parameters?: Record<string, string>) => {
    try {
      const job = await api.executeAction(entry.id, actionIndex, parameters);
      upsert(job);
      void reloadHistory();
      return job;
    } catch (err) {
      setActionResult({ success: false, message: String(err) });
      throw err;
    }
  }, [entry.id, upsert, reloadHistory]);

  const handleBlockAction = useCallback(async (path: number[], actionIndex: number, parameters?: Record<string, string>) => {
    try {
      const job = await api.executeSectionAction(entry.id, path, actionIndex, parameters);
      upsert(job);
      void reloadHistory();
      return job;
    } catch (err) {
      setActionResult({ success: false, message: String(err) });
      throw err;
    }
  }, [entry.id, upsert, reloadHistory]);

  const handleDismiss = useCallback(async () => {
    try { await api.dismissEntry(entry.id); onDismiss(entry.id); }
    catch (err) { setActionResult({ success: false, message: `Dismiss failed: ${err}` }); }
  }, [entry.id, onDismiss]);

  const handleDelete = useCallback(async () => {
    if (!window.confirm('Permanently delete this entry?')) return;
    try { await api.deleteEntry(entry.id); onDelete(entry.id); }
    catch (err) { setActionResult({ success: false, message: `Delete failed: ${err}` }); }
  }, [entry.id, onDelete]);

  const handlePin = useCallback(async () => {
    try { const updated = await api.pinEntry(entry.id); onEntryUpdated(updated); }
    catch (err) { setActionResult({ success: false, message: `Pin toggle failed: ${err}` }); }
  }, [entry.id, onEntryUpdated]);

  const handleEditorSave = useCallback((updated: Entry) => {
    onEntryUpdated(updated);
    setEditing(false);
  }, [onEntryUpdated]);

  const exportAs = useCallback((format: 'markdown' | 'html' | 'json') => {
    const safeName = (entry.title || 'entry').replace(/[^A-Za-z0-9._-]+/g, '_').slice(0, 80) || 'entry';
    if (format === 'markdown') {
      downloadFile(`${safeName}.md`, entryToMarkdown(entry), 'text/markdown;charset=utf-8');
    } else if (format === 'html') {
      const md = entryToMarkdown(entry);
      downloadFile(`${safeName}.html`, entryToHtml(entry, md), 'text/html;charset=utf-8');
    } else {
      downloadFile(`${safeName}.json`, JSON.stringify(entry, null, 2), 'application/json;charset=utf-8');
    }
    setExportMenuOpen(false);
  }, [entry]);

  // Build display order: pinned blocks first (in original order), then the rest.
  const orderedBlocks = useMemo<OrderedBlock[]>(() => {
    const all: OrderedBlock[] = entry.content.map((block, origIndex) => ({
      origIndex,
      blockKey: String(origIndex),
      block,
    }));
    const pinnedItems: OrderedBlock[] = [];
    const others: OrderedBlock[] = [];
    for (const item of all) {
      if (pinned.has(item.blockKey)) pinnedItems.push(item);
      else others.push(item);
    }
    return [...pinnedItems, ...others];
  }, [entry.content, pinned]);

  if (editing) {
    return (
      <div className="entry-detail">
        <EntryEditor
          entry={entry}
          onSave={handleEditorSave}
          onCancel={() => setEditing(false)}
        />
      </div>
    );
  }

  return (
    <div className="entry-detail">
      <div className="entry-detail-header">
        <div className="entry-detail-title-row">
          <h2>{entry.title}</h2>
          <div className="entry-detail-title-actions">
            <button
              className={`icon-btn ${searchOpen ? 'active' : ''}`}
              onClick={() => setSearchOpen((v) => !v)}
              title="Find in entry (Ctrl+F)"
              aria-label="Find in entry"
            >
              <Search size={16} />
            </button>
            <button
              className={`icon-btn ${activityOpen ? 'active' : ''}`}
              onClick={() => setActivityOpen((v) => !v)}
              title="Activity / action history"
              aria-label="Activity"
            >
              <History size={16} />
              {history.length > 0 && <span className="icon-btn-count">{history.length}</span>}
            </button>
            <div className="export-menu-wrap">
              <button
                className={`icon-btn ${exportMenuOpen ? 'active' : ''}`}
                onClick={() => setExportMenuOpen((v) => !v)}
                title="Export entry"
                aria-label="Export entry"
                aria-haspopup="menu"
              >
                <Download size={16} />
              </button>
              {exportMenuOpen && (
                <div className="export-menu" role="menu">
                  <button role="menuitem" onClick={() => exportAs('markdown')}>Markdown (.md)</button>
                  <button role="menuitem" onClick={() => exportAs('html')}>HTML (.html)</button>
                  <button role="menuitem" onClick={() => exportAs('json')}>JSON (.json)</button>
                </div>
              )}
            </div>
            {hidden.size > 0 && (
              <button
                className="icon-btn"
                onClick={unhideAll}
                title={`Show ${hidden.size} hidden block${hidden.size === 1 ? '' : 's'}`}
                aria-label="Restore hidden blocks"
              >
                <Eye size={16} />
                <span className="icon-btn-count">{hidden.size}</span>
              </button>
            )}
            <button
              className={`icon-btn ${entry.pinned ? 'active' : ''}`}
              onClick={handlePin}
              title={entry.pinned ? 'Unpin' : 'Pin to top'}
            >
              {entry.pinned ? <PinOff size={16} /> : <Pin size={16} />}
            </button>
            <button className="icon-btn" onClick={() => setEditing(true)} title="Edit entry">
              <Edit3 size={16} />
            </button>
          </div>
        </div>
        {entry.subtitle && <p className="entry-detail-subtitle">{entry.subtitle}</p>}
        <div className="entry-detail-meta-row">
          {entry.type && (
            <span className="entry-type-badge">{entry.type}</span>
          )}
          <div className="entry-detail-tags">
            {entry.tags.map((tag) => (
              <span key={tag} className="tag">{tag}</span>
            ))}
          </div>
          {entry.priority > 0 && (
            <span className="entry-priority-badge">Priority {entry.priority}</span>
          )}
          {hasEdits && (
            <button
              className="entry-edited-badge"
              onClick={() => setActivityOpen(true)}
              title="This entry has edited content — open Activity"
            >
              <Pencil size={11} /> edited
            </button>
          )}
        </div>
        <EntrySearch
          containerRef={contentRef}
          open={searchOpen}
          onOpen={setSearchOpen}
        />
      </div>

      <div className="entry-detail-content" ref={contentRef}>
        {orderedBlocks.map(({ origIndex, blockKey, block }) => {
          const isPinned = pinned.has(blockKey);
          const isHidden = hidden.has(blockKey);
          const anchorId = `block-${origIndex}`;
          return (
            <BlockShell
              key={origIndex}
              blockKey={blockKey}
              anchorId={anchorId}
              pinned={isPinned}
              hidden={isHidden}
              blockType={block.type}
              label={block.label ?? block.title}
              onTogglePin={() => togglePinned(blockKey)}
              onToggleHide={() => toggleHidden(blockKey)}
            >
              <EntryErrorBoundary label={`${block.type} block #${origIndex + 1}`}>
                <BlockRenderer
                  block={block}
                  entryId={entry.id}
                  path={[origIndex]}
                  blockKey={blockKey}
                  onBlockAction={handleBlockAction}
                  markers={markers}
                  onEntryChanged={onEntryUpdated}
                  expandRef={expandRef}
                />
              </EntryErrorBoundary>
            </BlockShell>
          );
        })}
      </div>

      {activityOpen && (
        <div className="entry-activity">
          <div className="entry-activity-head">
            <span><History size={14} /> Activity</span>
            <button className="close-result" onClick={() => setActivityOpen(false)} aria-label="Close activity">
              <X size={14} />
            </button>
          </div>
          <ActivityPanel events={history} loading={historyLoading} />
        </div>
      )}

      {actionResult && (
        <div className={`action-result ${actionResult.success ? 'success' : 'error'}`}>
          {actionResult.message}
          <button className="close-result" onClick={() => setActionResult(null)}>
            <X size={14} />
          </button>
        </div>
      )}

      <div className="entry-detail-actions">
        <div className="entry-actions-custom">
          {entry.actions.map((action, i) => (
            <ActionButton
              key={i}
              action={action}
              draftKey={`${entry.id}.${i}`}
              marker={markers.byEntryAction.get(action.label)}
              expandDefault={(t) => expandRefs(t, entry)}
              onClick={(parameters) => handleAction(i, parameters)}
            />
          ))}
        </div>
        <div className="entry-actions-system">
          <button className="action-btn action-default" onClick={handleDismiss}>
            <X size={14} /> Dismiss
          </button>
          <button className="action-btn action-danger-outline" onClick={handleDelete}>
            <Trash2 size={14} /> Delete
          </button>
        </div>
      </div>
    </div>
  );
}
