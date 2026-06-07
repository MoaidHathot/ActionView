import { ExternalLink, FileText, GitPullRequest, Bug, BookOpen, Activity, Globe } from 'lucide-react';
import type { ContentBlock, LinkItem } from '../../types';

interface Props {
  block: ContentBlock;
}

/**
 * Renders one or many external links.
 *
 * - Single-link mode: `block.url` (or `block.body` as a fallback) + optional
 *   `block.label` for the heading and `block.body` for the description.
 * - Multi-link mode: `block.links: [{ url, label, body, icon }, ...]`.
 *
 * The single-link mode finally honors `body` as a description (which the
 * previous implementation silently dropped on every sample that set it).
 *
 * Icon names come from the Lucide set; a small allowlist provides
 * sensible defaults for common DevOps targets (pr, ticket, runbook, ...).
 */
export function LinkBlock({ block }: Props) {
  const items: LinkItem[] = block.links && block.links.length > 0
    ? block.links
    : [{
        url: block.url ?? (typeof block.body === 'string' ? block.body : ''),
        label: block.label,
        body: block.url ? (typeof block.body === 'string' ? block.body : undefined) : undefined,
        icon: block.icon,
      }];

  if (block.links && block.links.length > 0 && block.label) {
    return (
      <div className="block-link block-link-group">
        <h4 className="block-label">{block.label}</h4>
        <ul className="block-link-list">
          {items.map((item, i) => (
            <li key={i}><LinkRow item={item} /></li>
          ))}
        </ul>
      </div>
    );
  }

  return (
    <div className="block-link">
      {items.length === 1 ? (
        <LinkRow item={items[0]} />
      ) : (
        <ul className="block-link-list">
          {items.map((item, i) => (
            <li key={i}><LinkRow item={item} /></li>
          ))}
        </ul>
      )}
    </div>
  );
}

function LinkRow({ item }: { item: LinkItem }) {
  if (!item.url) return null;
  const Icon = resolveIcon(item.icon, item.url);
  const label = item.label ?? item.url;
  return (
    <div className="block-link-row">
      <a
        className="block-link-anchor"
        href={item.url}
        target="_blank"
        rel="noopener noreferrer"
      >
        <Icon size={14} />
        <span>{label}</span>
      </a>
      {item.body && <div className="block-link-body">{item.body}</div>}
    </div>
  );
}

function resolveIcon(name: string | undefined, url: string) {
  if (name) {
    const named = ICON_MAP[name.toLowerCase()];
    if (named) return named;
  }
  // Domain-based default: GitHub gets a code-fork icon, everything else gets ExternalLink.
  try {
    const host = new URL(url).hostname.toLowerCase();
    if (host === 'github.com' || host.endsWith('.github.com')) return GitPullRequest;
  } catch {
    /* not a URL we can parse */
  }
  return ExternalLink;
}

const ICON_MAP: Record<string, typeof ExternalLink> = {
  pr: GitPullRequest,
  'pull-request': GitPullRequest,
  'git-pull-request': GitPullRequest,
  ticket: Bug,
  bug: Bug,
  runbook: BookOpen,
  doc: BookOpen,
  docs: BookOpen,
  dashboard: Activity,
  monitoring: Activity,
  external: ExternalLink,
  link: ExternalLink,
  web: Globe,
  file: FileText,
};
