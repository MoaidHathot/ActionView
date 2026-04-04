import { useEffect, useCallback, useRef } from 'react';

export interface KeyboardShortcut {
  key: string;
  label: string;
  description: string;
  handler: () => void;
  /** If true, shortcut fires even when an input/textarea has focus */
  global?: boolean;
}

interface Options {
  shortcuts: KeyboardShortcut[];
  enabled?: boolean;
}

/**
 * Registers global keyboard shortcuts. Skips firing when the user
 * is typing in an input/textarea unless the shortcut is marked global.
 */
export function useKeyboardShortcuts({ shortcuts, enabled = true }: Options) {
  const shortcutsRef = useRef(shortcuts);
  shortcutsRef.current = shortcuts;

  const handleKeyDown = useCallback(
    (e: KeyboardEvent) => {
      if (!enabled) return;

      const target = e.target as HTMLElement;
      const isInput =
        target.tagName === 'INPUT' ||
        target.tagName === 'TEXTAREA' ||
        target.isContentEditable;

      for (const shortcut of shortcutsRef.current) {
        if (e.key === shortcut.key) {
          if (isInput && !shortcut.global) continue;
          // Don't intercept browser shortcuts
          if (e.ctrlKey || e.metaKey || e.altKey) continue;
          e.preventDefault();
          shortcut.handler();
          return;
        }
      }
    },
    [enabled],
  );

  useEffect(() => {
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [handleKeyDown]);
}
