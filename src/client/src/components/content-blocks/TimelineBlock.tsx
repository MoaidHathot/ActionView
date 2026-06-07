import { Info, AlertTriangle, AlertCircle, CheckCircle, Circle } from 'lucide-react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import type { ContentBlock, TimelineEvent, AlertLevel } from '../../types';
import { allowEntryImageUrl } from '../../utils/imageUrl';

interface Props {
  block: ContentBlock;
}

/**
 * Vertical timeline of dated events. Common shape for incident RCAs:
 * "12:00 alert fired -> 12:05 rollback initiated -> 12:30 resolved".
 *
 * Each event has a free-form timestamp string, a label, optional markdown
 * body, optional level (colors the dot + connector), and optional icon
 * name (Lucide).
 */
export function TimelineBlock({ block }: Props) {
  const events = block.events ?? [];
  return (
    <div className="block-timeline">
      {block.label && <h4 className="block-label">{block.label}</h4>}
      <ol className="timeline-list">
        {events.map((ev, i) => (
          <TimelineRow key={i} event={ev} isLast={i === events.length - 1} />
        ))}
      </ol>
    </div>
  );
}

function TimelineRow({ event, isLast }: { event: TimelineEvent; isLast: boolean }) {
  const level = event.level ?? 'info';
  const Icon = pickIcon(level);
  return (
    <li className={`timeline-event timeline-event-${level}`}>
      <div className="timeline-marker">
        <span className="timeline-dot"><Icon size={12} /></span>
        {!isLast && <span className="timeline-connector" />}
      </div>
      <div className="timeline-content">
        <div className="timeline-header">
          <span className="timeline-time">{event.at}</span>
          <span className="timeline-label">{event.label}</span>
        </div>
        {event.body && (
          <div className="timeline-body">
            <ReactMarkdown remarkPlugins={[remarkGfm]} urlTransform={allowEntryImageUrl}>{event.body}</ReactMarkdown>
          </div>
        )}
      </div>
    </li>
  );
}

function pickIcon(level: AlertLevel) {
  switch (level) {
    case 'warning': return AlertTriangle;
    case 'error':   return AlertCircle;
    case 'success': return CheckCircle;
    case 'info':    return Info;
    default:        return Circle;
  }
}
