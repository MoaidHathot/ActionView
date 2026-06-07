import {
  LineChart, Line, BarChart, Bar, AreaChart, Area, PieChart, Pie, Cell,
  XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer,
} from 'recharts';
import type { ContentBlock, ChartSeries } from '../../types';

interface Props {
  block: ContentBlock;
}

const DEFAULT_COLORS = ['#818cf8', '#34d399', '#f87171', '#fbbf24', '#60a5fa', '#a78bfa', '#fb923c', '#22d3ee'];

/**
 * Renders one of four chart variants (line, bar, area, pie) using recharts.
 *
 * Data shape:
 *   - line / bar / area: `xAxis: string[]` (labels) and `series: [{ name, data: number[], color? }]`
 *   - pie: a single `series[0]` whose `data` parallels `xAxis` (slice names)
 *
 * Designed for compact monitoring snapshots ("last 24h error rate") - not
 * a substitute for a real BI dashboard.
 */
export function ChartBlock({ block }: Props) {
  const chartType = block.chartType ?? 'line';
  const series = block.series ?? [];
  const xAxis = block.xAxis ?? [];

  if (series.length === 0 || (chartType !== 'pie' && xAxis.length === 0)) {
    return (
      <div className="block-chart block-chart-missing">
        <div className="block-chart-missing-msg">Chart block has no data.</div>
      </div>
    );
  }

  return (
    <div className="block-chart">
      {block.label && <h4 className="block-label">{block.label}</h4>}
      <div className="chart-wrap">
        <ResponsiveContainer width="100%" height={260}>
          {renderChart(chartType, xAxis, series)}
        </ResponsiveContainer>
      </div>
      {block.caption && <div className="chart-caption">{block.caption}</div>}
    </div>
  );
}

function renderChart(type: string, xAxis: string[], series: ChartSeries[]) {
  if (type === 'pie') {
    const pie = series[0];
    const data = (pie.data ?? []).map((v, i) => ({
      name: xAxis[i] ?? `Slice ${i + 1}`,
      value: v,
    }));
    return (
      <PieChart>
        <Tooltip />
        <Legend />
        <Pie data={data} dataKey="value" nameKey="name" cx="50%" cy="50%" outerRadius={90} label>
          {data.map((_, i) => (
            <Cell key={i} fill={DEFAULT_COLORS[i % DEFAULT_COLORS.length]} />
          ))}
        </Pie>
      </PieChart>
    );
  }

  // line / bar / area share the same data flattening
  const data = xAxis.map((label, i) => {
    const row: Record<string, string | number> = { x: label };
    for (const s of series) row[s.name] = s.data[i] ?? 0;
    return row;
  });

  if (type === 'bar') {
    return (
      <BarChart data={data} margin={{ top: 8, right: 8, bottom: 8, left: 0 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
        <XAxis dataKey="x" stroke="var(--text-muted)" fontSize={12} />
        <YAxis stroke="var(--text-muted)" fontSize={12} />
        <Tooltip contentStyle={{ background: 'var(--bg-surface)', border: '1px solid var(--border)' }} />
        <Legend />
        {series.map((s, i) => (
          <Bar key={s.name} dataKey={s.name} fill={s.color ?? DEFAULT_COLORS[i % DEFAULT_COLORS.length]} />
        ))}
      </BarChart>
    );
  }

  if (type === 'area') {
    return (
      <AreaChart data={data} margin={{ top: 8, right: 8, bottom: 8, left: 0 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
        <XAxis dataKey="x" stroke="var(--text-muted)" fontSize={12} />
        <YAxis stroke="var(--text-muted)" fontSize={12} />
        <Tooltip contentStyle={{ background: 'var(--bg-surface)', border: '1px solid var(--border)' }} />
        <Legend />
        {series.map((s, i) => (
          <Area
            key={s.name}
            type="monotone"
            dataKey={s.name}
            stroke={s.color ?? DEFAULT_COLORS[i % DEFAULT_COLORS.length]}
            fill={s.color ?? DEFAULT_COLORS[i % DEFAULT_COLORS.length]}
            fillOpacity={0.25}
          />
        ))}
      </AreaChart>
    );
  }

  // default: line
  return (
    <LineChart data={data} margin={{ top: 8, right: 8, bottom: 8, left: 0 }}>
      <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
      <XAxis dataKey="x" stroke="var(--text-muted)" fontSize={12} />
      <YAxis stroke="var(--text-muted)" fontSize={12} />
      <Tooltip contentStyle={{ background: 'var(--bg-surface)', border: '1px solid var(--border)' }} />
      <Legend />
      {series.map((s, i) => (
        <Line
          key={s.name}
          type="monotone"
          dataKey={s.name}
          stroke={s.color ?? DEFAULT_COLORS[i % DEFAULT_COLORS.length]}
          strokeWidth={2}
          dot={false}
        />
      ))}
    </LineChart>
  );
}
