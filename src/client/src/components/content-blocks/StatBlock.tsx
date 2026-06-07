import { TrendingUp, TrendingDown, Minus } from 'lucide-react';
import type { ContentBlock } from '../../types';

interface Props {
  block: ContentBlock;
}

/**
 * Big-number stat with optional delta indicator and sparkline.
 *
 * Use for: alert summaries ("Error rate: 2.3% +0.5%"), monitoring
 * dashboards, deploy outcomes. Designed to be compact - several can
 * sit side-by-side when used in a section.
 *
 * Fields:
 *   - value: the big number (string so producers can format it - "$1,200", "2.3%")
 *   - delta: optional change indicator ("+0.5%", "-12")
 *   - trend: "up" | "down" | "flat" - colors the delta + picks an arrow icon
 *   - unit: optional unit suffix shown next to the value
 *   - sparkline: optional array of points for a small inline trend
 *   - label, caption: heading and supporting text
 */
export function StatBlock({ block }: Props) {
  const value = block.value ?? (typeof block.body === 'string' ? block.body : '');
  const trend = block.trend ?? 'flat';
  const TrendIcon = trend === 'up' ? TrendingUp : trend === 'down' ? TrendingDown : Minus;

  return (
    <div className={`block-stat block-stat-trend-${trend}`}>
      {block.label && <div className="stat-label">{block.label}</div>}
      <div className="stat-row">
        <div className="stat-value-wrap">
          <span className="stat-value">{value}</span>
          {block.unit && <span className="stat-unit">{block.unit}</span>}
        </div>
        {block.delta && (
          <div className={`stat-delta stat-delta-${trend}`}>
            <TrendIcon size={14} />
            <span>{block.delta}</span>
          </div>
        )}
      </div>
      {block.sparkline && block.sparkline.length > 1 && (
        <Sparkline points={block.sparkline} trend={trend} />
      )}
      {block.caption && <div className="stat-caption">{block.caption}</div>}
    </div>
  );
}

function Sparkline({ points, trend }: { points: number[]; trend: 'up' | 'down' | 'flat' }) {
  const width = 120;
  const height = 28;
  const min = Math.min(...points);
  const max = Math.max(...points);
  const range = max - min || 1;
  const stepX = points.length > 1 ? width / (points.length - 1) : 0;
  const coords = points.map((p, i) => {
    const x = i * stepX;
    const y = height - ((p - min) / range) * height;
    return `${x.toFixed(2)},${y.toFixed(2)}`;
  });
  const path = `M${coords.join(' L')}`;
  const color = trend === 'up' ? 'var(--success)' : trend === 'down' ? 'var(--danger)' : 'var(--text-muted)';
  return (
    <svg className="stat-sparkline" width={width} height={height} viewBox={`0 0 ${width} ${height}`} aria-hidden="true">
      <path d={path} fill="none" stroke={color} strokeWidth="1.5" />
    </svg>
  );
}
