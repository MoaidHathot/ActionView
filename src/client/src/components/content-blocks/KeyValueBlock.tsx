import type { ContentBlock } from '../../types';

interface Props {
  block: ContentBlock;
}

export function KeyValueBlock({ block }: Props) {
  if (!block.pairs) return null;

  return (
    <div className="block-key-value">
      {block.label && <h4 className="block-label">{block.label}</h4>}
      <dl>
        {Object.entries(block.pairs).map(([key, value]) => (
          <div key={key} className="kv-row">
            <dt>{key}</dt>
            <dd>{value}</dd>
          </div>
        ))}
      </dl>
    </div>
  );
}
