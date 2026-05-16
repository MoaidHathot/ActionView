import { formatDistanceToNow } from '../utils/time';
import type { Entry, Severity } from '../types';
import {
  GitPullRequest, AlertTriangle, Rocket, Zap, Bell,
  CircleDot, Circle, Pin, ChevronDown, ChevronRight, X,
} from 'lucide-react';
import { useState } from 'react';

interface Props {
  entries: Entry[];
  selectedId?: string;
  onSelect: (entry: Entry) => void;
  onDismiss: (id: string) => void;
  // Batch selection
  selectionMode: boolean;
  selectedIds: Set<string>;
  onToggleSelect: (id: string) => void;
  onSelectAll: () => void;
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
  entries, selectedId, onSelect, onDismiss, selectionMode, selectedIds, onToggleSelect, onSelectAll,
}: Props) {
  const [collapsedGroups, setCollapsedGroups] = useState<Set<string>>(new Set());

  if (entries.length === 0) {
    return (
      <div className="entry-list-empty">
        <Bell size={32} />
        <p>No pending entries</p>
        <p className="subtle">New entries will appear here when your orchestration tools create them.</p>
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

  const renderEntry = (entry: Entry) => {
    const Icon = entry.icon ? iconMap[entry.icon] : CircleDot;
    const isSelected = entry.id === selectedId;
    const isViewed = entry.status === 'viewed';
    const isChecked = selectedIds.has(entry.id);

    return (
      <div
        key={entry.id}
        className={`entry-list-item ${isSelected ? 'selected' : ''} ${isChecked ? 'checked' : ''}`}
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
            <span className="entry-list-item-title">{entry.title}</span>
          </div>
          {entry.subtitle && (
            <div className="entry-list-item-subtitle">{entry.subtitle}</div>
          )}
          <div className="entry-list-item-meta">
            <span className="entry-source">{entry.source}</span>
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
        {!selectionMode && (
          <button
            type="button"
            className="entry-list-item-dismiss"
            title="Dismiss (d)"
            aria-label={`Dismiss ${entry.title}`}
            onClick={(e) => {
              e.stopPropagation();
              onDismiss(entry.id);
            }}
          >
            <X size={14} />
          </button>
        )}
      </div>
    );
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
              return group.entries.map(renderEntry);
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
                {!isCollapsed && group.entries.map(renderEntry)}
              </div>
            );
          })
        : entries.map(renderEntry)}
    </div>
  );
}
