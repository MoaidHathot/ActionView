import type { ContentBlock } from '../../types';

interface Props {
  block: ContentBlock;
}

export function TableBlock({ block }: Props) {
  if (!block.columns || !block.rows) return null;

  return (
    <div className="block-table">
      {block.label && <h4 className="block-label">{block.label}</h4>}
      <table>
        <thead>
          <tr>
            {block.columns.map((col, i) => (
              <th key={i}>{col}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {block.rows.map((row, ri) => (
            <tr key={ri}>
              {row.map((cell, ci) => (
                <td key={ci}>{cell}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
