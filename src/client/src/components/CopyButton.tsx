import { Copy, Check } from 'lucide-react';
import { useCopyToClipboard } from '../hooks/useCopyToClipboard';

interface Props {
  value: string;
  /** Optional title attribute / tooltip text. */
  title?: string;
  /** Visual variant: 'icon' (compact, 24px) or 'icon-label' (with text). */
  variant?: 'icon' | 'icon-label';
  /** Additional CSS class names. */
  className?: string;
  /** Label shown next to the icon in 'icon-label' variant. Defaults to "Copy". */
  label?: string;
  /** Size of the icon in pixels. */
  iconSize?: number;
}

/**
 * Reusable copy-to-clipboard button used across content blocks.
 *
 * Renders a Lucide Copy icon (or Check on success) inside a small,
 * unobtrusive button. The "copied" state is purely visual and resets
 * after ~1.8s so the next click reads as a fresh copy.
 */
export function CopyButton({
  value, title, variant = 'icon', className, label = 'Copy', iconSize = 14,
}: Props) {
  const { copy, copied } = useCopyToClipboard();
  return (
    <button
      type="button"
      className={`copy-btn ${variant === 'icon-label' ? 'copy-btn-with-label' : ''} ${copied ? 'copy-btn-copied' : ''} ${className ?? ''}`}
      title={title ?? (copied ? 'Copied!' : 'Copy to clipboard')}
      aria-label={copied ? 'Copied' : 'Copy to clipboard'}
      onClick={(e) => {
        e.stopPropagation();
        void copy(value);
      }}
    >
      {copied ? <Check size={iconSize} /> : <Copy size={iconSize} />}
      {variant === 'icon-label' && <span>{copied ? 'Copied!' : label}</span>}
    </button>
  );
}
