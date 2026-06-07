import { FileText, FileCode, FileImage, FileVideo, FileAudio, FileArchive, Download, File as FileIcon } from 'lucide-react';
import type { ContentBlock } from '../../types';
import { rewriteImageUrl } from '../../utils/imageUrl';

interface Props {
  block: ContentBlock;
}

/**
 * Renders a downloadable file attachment. The URL is routed through
 * /api/files when it's a file:// URL (subject to fileAccess.allowedRoots),
 * or used directly for http(s):// URLs.
 *
 * Icon is chosen by mime type / extension. Size (when supplied) is
 * displayed in a human-friendly form (KB / MB / GB).
 */
export function FileBlock({ block }: Props) {
  const rawUrl = block.url ?? (typeof block.body === 'string' ? block.body : '');
  const url = rewriteImageUrl(rawUrl);
  const filename = block.filename ?? guessFilename(rawUrl) ?? 'download';
  const Icon = pickIcon(block.mimeType, filename);

  if (!url) {
    return (
      <div className="block-file block-file-missing">
        <div className="block-file-missing-msg">File block has no url.</div>
      </div>
    );
  }

  return (
    <div className="block-file">
      {block.label && <h4 className="block-label">{block.label}</h4>}
      <a className="file-card" href={url} download={filename} target="_blank" rel="noopener noreferrer">
        <span className="file-card-icon"><Icon size={28} /></span>
        <span className="file-card-meta">
          <span className="file-card-name">{filename}</span>
          <span className="file-card-sub">
            {block.mimeType && <span className="file-card-type">{block.mimeType}</span>}
            {block.fileSize !== undefined && <span className="file-card-size">{formatSize(block.fileSize)}</span>}
          </span>
        </span>
        <span className="file-card-download" title="Download"><Download size={16} /></span>
      </a>
      {block.caption && <div className="file-caption">{block.caption}</div>}
    </div>
  );
}

function guessFilename(url: string): string | null {
  try {
    const u = new URL(url, 'http://localhost');
    const last = u.pathname.split('/').filter(Boolean).pop();
    return last ? decodeURIComponent(last) : null;
  } catch {
    return null;
  }
}

function pickIcon(mime: string | undefined, filename: string) {
  const ext = (filename.match(/\.([^./\\]+)$/)?.[1] ?? '').toLowerCase();
  const m = (mime ?? '').toLowerCase();
  if (m.startsWith('image/') || ['png', 'jpg', 'jpeg', 'gif', 'webp', 'svg', 'bmp', 'avif', 'ico'].includes(ext)) return FileImage;
  if (m.startsWith('video/') || ['mp4', 'webm', 'mov', 'mkv', 'avi'].includes(ext)) return FileVideo;
  if (m.startsWith('audio/') || ['mp3', 'wav', 'ogg', 'flac', 'm4a'].includes(ext)) return FileAudio;
  if (m.startsWith('text/') || ['txt', 'log', 'md', 'rtf'].includes(ext)) return FileText;
  if (['zip', 'tar', 'gz', '7z', 'rar', 'bz2', 'xz'].includes(ext)) return FileArchive;
  if (['js', 'ts', 'tsx', 'jsx', 'py', 'rb', 'go', 'rs', 'cs', 'java', 'cpp', 'c', 'h', 'sh', 'ps1', 'json', 'yaml', 'yml', 'xml', 'html', 'css', 'sql'].includes(ext)) return FileCode;
  return FileIcon;
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
}
