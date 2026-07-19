import type { ActionEvent, ActionStyle } from '../types';

/**
 * A per-target outcome marker derived from the action audit history: the most
 * recent action taken on a target (a section/comment or an entry action),
 * rendered as a generic status chip. Not PR-specific — the action's own label
 * and style ("Approve"/success, "Delete"/danger, "Acknowledge"/default, …)
 * supply the vocabulary and color.
 */
export interface OutcomeMarker {
  label: string;
  style: ActionStyle;
  success: boolean;
  timestamp: string;
  trigger: string;
}

export interface DerivedMarkers {
  /** Section actions keyed by positional block path ("3.0"). */
  byPath: Map<string, OutcomeMarker>;
  /** Section actions keyed by the owning block's stable id (when set). */
  byTargetId: Map<string, OutcomeMarker>;
  /** Entry-level actions keyed by action label. */
  byEntryAction: Map<string, OutcomeMarker>;
}

export function pathKey(path: number[] | undefined): string {
  return (path ?? []).join('.');
}

/**
 * Builds marker lookups from a newest-first list of audit events, keeping only
 * the most recent event per target so a re-run supersedes an earlier outcome.
 */
export function deriveMarkers(events: ActionEvent[]): DerivedMarkers {
  const byPath = new Map<string, OutcomeMarker>();
  const byTargetId = new Map<string, OutcomeMarker>();
  const byEntryAction = new Map<string, OutcomeMarker>();

  for (const ev of events) {
    const marker: OutcomeMarker = {
      label: ev.actionLabel,
      style: ev.actionStyle,
      success: ev.success,
      timestamp: ev.timestamp,
      trigger: ev.trigger,
    };
    if (ev.target === 'section') {
      const pk = pathKey(ev.path);
      if (pk && !byPath.has(pk)) byPath.set(pk, marker);
      if (ev.targetId && !byTargetId.has(ev.targetId)) byTargetId.set(ev.targetId, marker);
    } else if (ev.target === 'entry') {
      if (!byEntryAction.has(ev.actionLabel)) byEntryAction.set(ev.actionLabel, marker);
    }
  }

  return { byPath, byTargetId, byEntryAction };
}

/** Resolves a section's marker by stable id first, then by positional path. */
export function markerForSection(
  markers: DerivedMarkers | undefined,
  path: number[],
  id?: string,
): OutcomeMarker | undefined {
  if (!markers) return undefined;
  if (id && markers.byTargetId.has(id)) return markers.byTargetId.get(id);
  return markers.byPath.get(pathKey(path));
}
