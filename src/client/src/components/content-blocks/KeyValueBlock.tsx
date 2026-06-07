import type { ContentBlock, RichCell } from '../../types';
import { RichCellView } from '../RichCellView';

interface Props {
  block: ContentBlock;
}

/**
 * Two-column key/value grid. Values may be plain strings or rich-cell
 * objects (link, status, code, copy, badge, markdown, image).
 *
 * The 140px fixed-width key column from the old implementation is gone;
 * keys auto-size up to ~40% of the container so longer labels don't get
 * crushed.
 */
export function KeyValueBlock({ block }: Props) {
  if (!block.pairs) return null;
  const entries = Object.entries(block.pairs);

  return (
    <div className="block-keyvalue">
      {block.label && <h4 className="block-label">{block.label}</h4>}
      <dl className="kv-grid">
        {entries.map(([key, value]) => (
          <div key={key} className="kv-row">
            <dt className="kv-key">{key}</dt>
            <dd className="kv-value">
              <RichCellView cell={value as RichCell} />
            </dd>
          </div>
        ))}
      </dl>
    </div>
  );
}
