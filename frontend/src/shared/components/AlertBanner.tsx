import React from 'react';

interface AlertBannerProps {
  message: string | null;
  type?: 'info' | 'warning' | 'error';
  onClose?: () => void;
}

export const AlertBanner: React.FC<AlertBannerProps> = ({
  message,
  type = 'info',
  onClose,
}) => {
  if (!message) return null;

  return (
    <div className={`alert-banner ${type}`} role="alert">
      <span>{message}</span>
      {onClose && (
        <button
          onClick={onClose}
          className="account-disconnect-btn"
          style={{ marginLeft: 'auto', color: 'inherit' }}
          aria-label="Close alert"
        >
          &times;
        </button>
      )}
    </div>
  );
};
