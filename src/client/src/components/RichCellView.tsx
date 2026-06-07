import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import {
  ExternalLink, Info, AlertTriangle, AlertCircle, CheckCircle,
} from 'lucide-react';
import type { RichCell } from '../types';
import { CopyButton } from './CopyButton';
import { rewriteImageUrl } from '../utils/imageUrl';

interface Props {
  cell: RichCell;
  /** When true (default), markdown values render inline (no <p> wrapper). */
  inline?: boolean;
}

/**
 * Renders a single cell from a TableBlock row or a value from a KeyValueBlock.
 *
 * A cell can be:
 *   - a plain string  -> rendered as text
 *   - { type: 'text', value, mono? }     -> plain text, optionally monospaced
 *   - { type: 'link', url, label?, icon? } -> external link with icon
 *   - { type: 'status', level, label }    -> colored status pill
 *   - { type: 'badge', label, color? }    -> neutral or custom-colored badge
 *   - { type: 'code', value, language? }  -> inline <code>
 *   - { type: 'copy', value, display? }   -> display + copy-to-clipboard icon
 *   - { type: 'markdown', value }         -> inline markdown
 *   - { type: 'image', url, alt? }        -> tiny thumbnail
 */
export function RichCellView({ cell, inline = true }: Props) {
  // String shortcut — by far the most common case.
  if (typeof cell === 'string') {
    return <span className="rich-cell rich-cell-text">{cell}</span>;
  }
  if (!cell || typeof cell !== 'object') {
    return <span className="rich-cell rich-cell-text">{String(cell ?? '')}</span>;
  }

  switch (cell.type) {
    case 'text':
      return (
        <span className={`rich-cell rich-cell-text${cell.mono ? ' rich-cell-mono' : ''}`}>
          {cell.value}
        </span>
      );

    case 'link':
      return (
        <a
          className="rich-cell rich-cell-link"
          href={cell.url}
          target="_blank"
          rel="noopener noreferrer"
        >
          <ExternalLink size={12} />
          <span>{cell.label ?? cell.url}</span>
        </a>
      );

    case 'status': {
      const Icon = STATUS_ICONS[cell.level] ?? Info;
      return (
        <span className={`rich-cell rich-cell-status rich-cell-status-${cell.level}`}>
          <Icon size={12} />
          <span>{cell.label}</span>
        </span>
      );
    }

    case 'badge': {
      const style = cell.color
        ? { backgroundColor: cell.color, color: '#fff', borderColor: cell.color }
        : undefined;
      return (
        <span className="rich-cell rich-cell-badge" style={style}>{cell.label}</span>
      );
    }

    case 'code':
      return (
        <code className={`rich-cell rich-cell-code language-${cell.language ?? 'text'}`}>
          {cell.value}
        </code>
      );

    case 'copy':
      return (
        <span className="rich-cell rich-cell-copy">
          <code className="rich-cell-copy-display">{cell.display ?? cell.value}</code>
          <CopyButton value={cell.value} iconSize={12} />
        </span>
      );

    case 'markdown':
      return (
        <span className="rich-cell rich-cell-markdown">
          <ReactMarkdown
            remarkPlugins={[remarkGfm]}
            components={inline ? INLINE_MARKDOWN_COMPONENTS : undefined}
          >
            {cell.value}
          </ReactMarkdown>
        </span>
      );

    case 'image':
      return (
        <img
          className="rich-cell rich-cell-image"
          src={rewriteImageUrl(cell.url)}
          alt={cell.alt ?? ''}
          loading="lazy"
        />
      );

    default:
      // Unknown cell shape - render as JSON for debugging visibility.
      return (
        <span className="rich-cell rich-cell-unknown">
          {JSON.stringify(cell)}
        </span>
      );
  }
}

/**
 * Extracts plain-text representation of a cell, used by sorting and filtering.
 */
export function richCellText(cell: RichCell): string {
  if (typeof cell === 'string') return cell;
  if (!cell || typeof cell !== 'object') return String(cell ?? '');
  switch (cell.type) {
    case 'text':
    case 'code':
    case 'copy':
    case 'markdown':
      return cell.value ?? '';
    case 'link':
      return cell.label ?? cell.url ?? '';
    case 'status':
    case 'badge':
      return cell.label ?? '';
    case 'image':
      return cell.alt ?? cell.url ?? '';
    default:
      return JSON.stringify(cell);
  }
}

const STATUS_ICONS = {
  info: Info,
  warning: AlertTriangle,
  error: AlertCircle,
  success: CheckCircle,
} as const;

// Override block-level markdown elements to render inline (no <p> wrappers
// inside table cells / kv values, which would break the layout).
const INLINE_MARKDOWN_COMPONENTS = {
  p: ({ children }: { children?: React.ReactNode }) => <>{children}</>,
};
