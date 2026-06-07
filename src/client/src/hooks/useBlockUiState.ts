import { useEffect, useState } from 'react';

/**
 * Per-block, per-user, per-entry state stored in localStorage.
 *
 * - "pinned": user has pinned this block to the top of the entry
 * - "hidden": user has stashed this block away (still listed in a tray)
 *
 * Keyed by `${entryId}.${blockKey}` where blockKey defaults to the
 * block index. We track pins/hides as two independent string-set
 * localStorage keys per entry so reads are O(1) and writes don't
 * collide with section-collapse / alert-dismiss state.
 */
type BlockUiKind = 'pinned' | 'hidden';

function storageKey(kind: BlockUiKind, entryId: string): string {
  return `actionview.${kind}.${entryId}`;
}

function readSet(kind: BlockUiKind, entryId: string): Set<string> {
  try {
    const raw = localStorage.getItem(storageKey(kind, entryId));
    if (!raw) return new Set();
    return new Set(JSON.parse(raw) as string[]);
  } catch {
    return new Set();
  }
}

function writeSet(kind: BlockUiKind, entryId: string, set: Set<string>): void {
  try {
    if (set.size === 0) localStorage.removeItem(storageKey(kind, entryId));
    else localStorage.setItem(storageKey(kind, entryId), JSON.stringify([...set]));
  } catch {
    /* ignore */
  }
}

/**
 * Hook: returns the user's pinned + hidden block keys for an entry,
 * plus toggle functions. Auto-syncs with localStorage and re-renders
 * subscribers when the sets change (within the same tab).
 */
export function useBlockUiState(entryId: string) {
  const [pinned, setPinned] = useState<Set<string>>(() => readSet('pinned', entryId));
  const [hidden, setHidden] = useState<Set<string>>(() => readSet('hidden', entryId));

  // Reset state when entry changes
  useEffect(() => {
    setPinned(readSet('pinned', entryId));
    setHidden(readSet('hidden', entryId));
  }, [entryId]);

  const togglePinned = (blockKey: string) => {
    setPinned((prev) => {
      const next = new Set(prev);
      if (next.has(blockKey)) next.delete(blockKey);
      else { next.add(blockKey); /* unhide on pin */ }
      writeSet('pinned', entryId, next);
      return next;
    });
    // Pinning unhides.
    setHidden((prev) => {
      if (!prev.has(blockKey)) return prev;
      const next = new Set(prev);
      next.delete(blockKey);
      writeSet('hidden', entryId, next);
      return next;
    });
  };

  const toggleHidden = (blockKey: string) => {
    setHidden((prev) => {
      const next = new Set(prev);
      if (next.has(blockKey)) next.delete(blockKey);
      else next.add(blockKey);
      writeSet('hidden', entryId, next);
      return next;
    });
    // Hiding unpins.
    setPinned((prev) => {
      if (!prev.has(blockKey)) return prev;
      const next = new Set(prev);
      next.delete(blockKey);
      writeSet('pinned', entryId, next);
      return next;
    });
  };

  const unhideAll = () => {
    setHidden(() => {
      writeSet('hidden', entryId, new Set());
      return new Set();
    });
  };

  return { pinned, hidden, togglePinned, toggleHidden, unhideAll };
}
