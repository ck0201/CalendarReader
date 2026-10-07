import React from 'react';

interface MetricCardProps {
  label: string;
  value: string;
  caption: string;
  iconType: 'scheduled' | 'meetings' | 'focus' | 'busy';
}

export const MetricCard: React.FC<MetricCardProps> = ({
  label,
  value,
  caption,
  iconType,
}) => {
  const renderIcon = () => {
    switch (iconType) {
      case 'scheduled':
        return (
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <circle cx="12" cy="12" r="10" />
            <polyline points="12 6 12 12 16 14" />
          </svg>
        );
      case 'meetings':
        return (
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
            <circle cx="9" cy="7" r="4" />
            <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
            <path d="M16 3.13a4 4 0 0 1 0 7.75" />
          </svg>
        );
      case 'focus':
        return (
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <circle cx="12" cy="12" r="10" />
            <circle cx="12" cy="12" r="6" />
            <circle cx="12" cy="12" r="2" />
          </svg>
        );
      case 'busy':
        return (
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
            <line x1="16" y1="2" x2="16" y2="6" />
            <line x1="8" y1="2" x2="8" y2="6" />
            <line x1="3" y1="10" x2="21" y2="10" />
          </svg>
        );
    }
  };

  return (
    <div className="metric-card">
      <div className="metric-header">
        <span className="metric-label">{label}</span>
        <div className={`metric-icon ${iconType}`}>{renderIcon()}</div>
      </div>
      <div className="metric-value">{value}</div>
      <div className="metric-caption">{caption}</div>
    </div>
  );
};
