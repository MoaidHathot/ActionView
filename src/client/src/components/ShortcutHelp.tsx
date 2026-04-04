import type { KeyboardShortcut } from '../hooks/useKeyboardShortcuts';

interface Props {
  shortcuts: KeyboardShortcut[];
  visible: boolean;
  onClose: () => void;
}

export function ShortcutHelp({ shortcuts, visible, onClose }: Props) {
  if (!visible) return null;

  return (
    <div className="shortcut-overlay" onClick={onClose}>
      <div className="shortcut-dialog" onClick={(e) => e.stopPropagation()}>
        <h3>Keyboard Shortcuts</h3>
        <div className="shortcut-list">
          {shortcuts.map((s) => (
            <div key={s.key} className="shortcut-row">
              <kbd className="shortcut-key">{s.label}</kbd>
              <span className="shortcut-desc">{s.description}</span>
            </div>
          ))}
        </div>
        <div className="shortcut-footer">
          Press <kbd className="shortcut-key">?</kbd> to close
        </div>
      </div>
    </div>
  );
}
