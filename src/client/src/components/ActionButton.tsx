import { useState } from 'react';
import type { EntryAction } from '../types';

interface Props {
  action: EntryAction;
  onClick: () => Promise<void> | void;
  loading?: boolean;
}

export function ActionButton({ action, onClick, loading }: Props) {
  const [confirming, setConfirming] = useState(false);
  const [isLoading, setIsLoading] = useState(false);

  const styleClass = `action-btn action-${action.style}`;

  const handleClick = async () => {
    if (action.confirmMessage && !confirming) {
      setConfirming(true);
      return;
    }

    setConfirming(false);
    setIsLoading(true);
    try {
      await onClick();
    } finally {
      setIsLoading(false);
    }
  };

  const handleCancel = () => {
    setConfirming(false);
  };

  if (confirming) {
    return (
      <div className="action-confirm">
        <span className="confirm-message">{action.confirmMessage}</span>
        <button className="action-btn action-danger" onClick={handleClick} disabled={isLoading}>
          {isLoading ? 'Executing...' : 'Confirm'}
        </button>
        <button className="action-btn action-default" onClick={handleCancel}>
          Cancel
        </button>
      </div>
    );
  }

  return (
    <button
      className={styleClass}
      onClick={handleClick}
      disabled={isLoading || loading}
    >
      {isLoading ? 'Executing...' : action.label}
    </button>
  );
}
