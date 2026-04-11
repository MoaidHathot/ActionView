import { formatDistanceToNow } from '../utils/time';
import { highlightText } from '../utils/highlight';
import type { Entry, Severity } from '../types';
import {
  GitPullRequest, AlertTriangle, Rocket, Zap, Bell,
  CircleDot, Circle, Pin, ChevronDown, ChevronRight,
  Inbox, Plus,
} from 'lucide-react';
import { useState } from 'react';

interface Props {
  entries: Entry[];
  selectedId?: string;
  onSelect: (entry: Entry) => void;
  // Batch selection
  selectionMode: boolean;
  selectedIds: Set<string>;
  onToggleSelect: (id: string) => void;
  onSelectAll: () => void;
  /** Current search query for highlighting */
  searchQuery?: string;
  /** Timestamp tick to force re-render for relative times */
  _tick?: number;
}

const severityColors: Record<Severity, string> = {
  critical: '#ef4444',
  high: '#f97316',
  medium: '#eab308',
  low: '#22c55e',
};

const iconMap: Record<string, React.ComponentType<{ size?: number }>> = {
  'git-pull-request': GitPullRequest,
  'alert-triangle': AlertTriangle,
  'rocket': Rocket,
  'zap': Zap,
  'bell': Bell,
};

interface GroupedEntries {
  groupId: string | null;
  groupLabel: string | null;
  entries: Entry[];
}

function groupEntries(entries: Entry[]): GroupedEntries[] {
  const groups: GroupedEntries[] = [];
  const groupMap = new Map<string, GroupedEntries>();

  for (const entry of entries) {
    if (entry.groupId) {
      let group = groupMap.get(entry.groupId);
      if (!group) {
        group = { groupId: entry.groupId, groupLabel: entry.groupLabel ?? entry.groupId, entries: [] };
        groupMap.set(entry.groupId, group);
        groups.push(group);
      }
      group.entries.push(entry);
    } else {
      // Ungrouped entries go into individual "groups"
      groups.push({ groupId: null, groupLabel: null, entries: [entry] });
    }
  }

  return groups;
}

export function EntryList({
  entries, selectedId, onSelect, selectionMode, selectedIds, onToggleSelect, onSelectAll,
  searchQuery, _tick,
}: Props) {
  const [collapsedGroups, setCollapsedGroups] = useState<Set<string>>(new Set());

  // suppress unused warning - _tick forces re-renders for timestamps
  void _tick;

  if (entries.length === 0) {
    return (
      <div className="entry-list-empty">
        <Inbox size={40} strokeWidth={1.2} />
        <p className="empty-title">Queue is clear</p>
        <p className="subtle">No pending entries. New items will appear here automatically when your orchestration tools create them.</p>
        <div className="empty-hint">
          <Plus size={12} />
          <span>Drop a JSON file into the inbox directory, use the CLI, or POST to <code>/api/entries</code></span>
        </div>
      </div>
    );
  }

  const groups = groupEntries(entries);
  const hasGroups = groups.some((g) => g.groupId !== null);

  const toggleGroup = (groupId: string) => {
    setCollapsedGroups((prev) => {
      const next = new Set(prev);
      if (next.has(groupId)) next.delete(groupId);
      else next.add(groupId);
      return next;
    });
  };

  // Count pinned entries for the separator
  const pinnedCount = entries.filter((e) => e.pinned).length;

  const renderEntry = (entry: Entry, showPinSeparator: boolean) => {
    const Icon = entry.icon ? iconMap[entry.icon] : CircleDot;
    const isSelected = entry.id === selectedId;
    const isViewed = entry.status === 'viewed';
    const isChecked = selectedIds.has(entry.id);

    return (
      <div key={entry.id}>
        {showPinSeparator && (
          <div className="pinned-separator">
            <Pin size={10} />
            <span>Pinned</span>
            <span className="pinned-count">{pinnedCount}</span>
            <div className="pinned-separator-line" />
          </div>
        )}
        <div
          className={`entry-list-item ${isSelected ? 'selected' : ''} ${isChecked ? 'checked' : ''} ${entry.pinned ? 'pinned-entry' : ''}`}
          onClick={() => {
            if (selectionMode) {
              onToggleSelect(entry.id);
            } else {
              onSelect(entry);
            }
          }}
        >
          {selectionMode && (
            <div className="entry-checkbox" onClick={(e) => { e.stopPropagation(); onToggleSelect(entry.id); }}>
              <input
                type="checkbox"
                checked={isChecked}
                onChange={() => {}}
                tabIndex={-1}
              />
            </div>
          )}
          <div className="entry-list-item-indicator">
            {isViewed ? (
              <Circle size={8} />
            ) : (
              <CircleDot size={8} style={{ color: severityColors[entry.severity] }} />
            )}
          </div>
          <div className="entry-list-item-content">
            <div className="entry-list-item-header">
              {entry.pinned && <Pin size={12} className="pin-icon" />}
              {Icon && <Icon size={14} />}
              <span className="entry-list-item-title">
                {highlightText(entry.title, searchQuery)}
              </span>
            </div>
            {entry.subtitle && (
              <div className="entry-list-item-subtitle">
                {highlightText(entry.subtitle, searchQuery)}
              </div>
            )}
            <div className="entry-list-item-meta">
              <span className="entry-source">
                {highlightText(entry.source, searchQuery)}
              </span>
              <span className="entry-time">{formatDistanceToNow(entry.createdAt)}</span>
              <span
                className="severity-dot"
                style={{ backgroundColor: severityColors[entry.severity] }}
                title={entry.severity}
              />
              {entry.priority > 0 && (
                <span className="entry-priority" title={`Priority: ${entry.priority}`}>
                  P{entry.priority}
                </span>
              )}
            </div>
          </div>
        </div>
      </div>
    );
  };

  // When not grouped, show pin separator before the first pinned entry
  const renderEntriesWithPinSeparator = (entries: Entry[]) => {
    let firstPinnedShown = false;
    return entries.map((entry) => {
      const showSep = entry.pinned && !firstPinnedShown && pinnedCount > 0;
      if (showSep) firstPinnedShown = true;
      return renderEntry(entry, showSep);
    });
  };

  return (
    <div className="entry-list">
      {selectionMode && (
        <div className="entry-list-select-all">
          <button className="select-all-btn" onClick={onSelectAll}>
            {selectedIds.size === entries.length ? 'Deselect All' : 'Select All'}
          </button>
        </div>
      )}
      {hasGroups
        ? groups.map((group) => {
            if (group.groupId === null) {
              return group.entries.map((e) => renderEntry(e, false));
            }

            const isCollapsed = collapsedGroups.has(group.groupId);
            return (
              <div key={group.groupId} className="entry-group">
                <div
                  className="entry-group-header"
                  onClick={() => toggleGroup(group.groupId!)}
                >
                  {isCollapsed ? <ChevronRight size={14} /> : <ChevronDown size={14} />}
                  <span className="entry-group-label">{group.groupLabel}</span>
                  <span className="entry-group-count">{group.entries.length}</span>
                </div>
                {!isCollapsed && group.entries.map((e) => renderEntry(e, false))}
              </div>
            );
          })
        : renderEntriesWithPinSeparator(entries)}
    </div>
  );
}
