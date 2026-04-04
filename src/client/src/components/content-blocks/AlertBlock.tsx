import { AlertCircle, AlertTriangle, Info, CheckCircle } from 'lucide-react';
import type { ContentBlock } from '../../types';

interface Props {
  block: ContentBlock;
}

const alertIcons = {
  info: Info,
  warning: AlertTriangle,
  error: AlertCircle,
  success: CheckCircle,
};

export function AlertBlock({ block }: Props) {
  const level = block.level ?? 'info';
  const Icon = alertIcons[level];
  const message = typeof block.body === 'string' ? block.body : String(block.body ?? '');

  return (
    <div className={`block-alert alert-${level}`}>
      <Icon size={18} />
      <span>{block.label ?? message}</span>
    </div>
  );
}
