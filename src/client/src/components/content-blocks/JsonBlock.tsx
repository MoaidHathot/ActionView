import { useState, useCallback, type ReactNode } from 'react';
import { ChevronDown, ChevronRight } from 'lucide-react';
import type { ContentBlock } from '../../types';
import { CopyButton } from '../CopyButton';

interface Props {
  block: ContentBlock;
}

/**
 * Renders any JSON value (object, array, primitive) as a folding tree.
 *
 * Each object / array is collapsible at any depth (click the chevron or
 * the key label). A copy button copies the full JSON of the block.
 * Primitives are syntax-colored (strings green, numbers cyan, etc).
 */
export function JsonBlock({ block }: Props) {
  const data = block.body;
  const pretty = (() => {
    try { return JSON.stringify(data, null, 2); }
    catch { return String(data); }
  })();

  return (
    <div className="block-json">
      <div className="json-header">
        {block.label && <h4 className="block-label json-label">{block.label}</h4>}
        <div className="json-header-actions">
          <CopyButton value={pretty} iconSize={13} />
        </div>
      </div>
      <div className="json-tree">
        <JsonNode value={data} depth={0} isLast={true} keyName={null} />
      </div>
    </div>
  );
}

interface NodeProps {
  value: unknown;
  depth: number;
  isLast: boolean;
  keyName: string | null;
}

function JsonNode({ value, depth, isLast, keyName }: NodeProps) {
  const [expanded, setExpanded] = useState(depth < 2);
  const toggle = useCallback(() => setExpanded((e) => !e), []);

  if (value === null) return <Line keyName={keyName} depth={depth}><span className="json-null">null</span>{!isLast && ','}</Line>;

  if (typeof value === 'string') {
    return <Line keyName={keyName} depth={depth}><span className="json-string">"{escapeString(value)}"</span>{!isLast && ','}</Line>;
  }
  if (typeof value === 'number') {
    return <Line keyName={keyName} depth={depth}><span className="json-number">{value}</span>{!isLast && ','}</Line>;
  }
  if (typeof value === 'boolean') {
    return <Line keyName={keyName} depth={depth}><span className="json-bool">{String(value)}</span>{!isLast && ','}</Line>;
  }

  if (Array.isArray(value)) {
    if (value.length === 0) {
      return <Line keyName={keyName} depth={depth}><span className="json-bracket">[]</span>{!isLast && ','}</Line>;
    }
    return (
      <div className="json-block-node">
        <div className="json-line" style={{ paddingLeft: depth * 14 }}>
          <button className="json-toggle" onClick={toggle} aria-label={expanded ? 'Collapse' : 'Expand'}>
            {expanded ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
          </button>
          {keyName !== null && <span className="json-key">"{keyName}": </span>}
          <span className="json-bracket">[</span>
          {!expanded && <span className="json-collapsed-summary"> {value.length} items </span>}
          {!expanded && <span className="json-bracket">]</span>}
          {!expanded && !isLast && ','}
        </div>
        {expanded && (
          <>
            {value.map((item, i) => (
              <JsonNode key={i} value={item} depth={depth + 1} isLast={i === value.length - 1} keyName={null} />
            ))}
            <div className="json-line" style={{ paddingLeft: depth * 14 + 14 }}>
              <span className="json-bracket">]</span>{!isLast && ','}
            </div>
          </>
        )}
      </div>
    );
  }

  if (typeof value === 'object') {
    const entries = Object.entries(value as Record<string, unknown>);
    if (entries.length === 0) {
      return <Line keyName={keyName} depth={depth}><span className="json-bracket">{'{}'}</span>{!isLast && ','}</Line>;
    }
    return (
      <div className="json-block-node">
        <div className="json-line" style={{ paddingLeft: depth * 14 }}>
          <button className="json-toggle" onClick={toggle} aria-label={expanded ? 'Collapse' : 'Expand'}>
            {expanded ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
          </button>
          {keyName !== null && <span className="json-key">"{keyName}": </span>}
          <span className="json-bracket">{'{'}</span>
          {!expanded && <span className="json-collapsed-summary"> {entries.length} keys </span>}
          {!expanded && <span className="json-bracket">{'}'}</span>}
          {!expanded && !isLast && ','}
        </div>
        {expanded && (
          <>
            {entries.map(([k, v], i) => (
              <JsonNode key={k} value={v} depth={depth + 1} isLast={i === entries.length - 1} keyName={k} />
            ))}
            <div className="json-line" style={{ paddingLeft: depth * 14 + 14 }}>
              <span className="json-bracket">{'}'}</span>{!isLast && ','}
            </div>
          </>
        )}
      </div>
    );
  }

  return <Line keyName={keyName} depth={depth}><span>{String(value)}</span>{!isLast && ','}</Line>;
}

function Line({ keyName, depth, children }: { keyName: string | null; depth: number; children: ReactNode }) {
  return (
    <div className="json-line" style={{ paddingLeft: depth * 14 + (keyName === null ? 14 : 14) }}>
      {keyName !== null && <span className="json-key">"{keyName}": </span>}
      {children}
    </div>
  );
}

function escapeString(s: string): string {
  return s
    .replace(/\\/g, '\\\\')
    .replace(/"/g, '\\"')
    .replace(/\n/g, '\\n')
    .replace(/\r/g, '\\r')
    .replace(/\t/g, '\\t');
}
