import React from 'react';
import type { EventFilterType } from '../../core/types/models';

interface EventFilterBarProps {
  currentFilter: EventFilterType;
  onFilterChange: (filter: EventFilterType) => void;
}

export const EventFilterBar: React.FC<EventFilterBarProps> = ({
  currentFilter,
  onFilterChange,
}) => {
  return (
    <div className="event-filter-selector">
      <button
        type="button"
        className={`event-filter-btn ${currentFilter === 'all' ? 'active' : ''}`}
        onClick={() => onFilterChange('all')}
      >
        All Events
      </button>
      <button
        type="button"
        className={`event-filter-btn ${currentFilter === 'train' ? 'active' : ''}`}
        onClick={() => onFilterChange('train')}
      >
        Train Journeys
      </button>
      <button
        type="button"
        className={`event-filter-btn ${currentFilter === 'other' ? 'active' : ''}`}
        onClick={() => onFilterChange('other')}
      >
        Other Events
      </button>
    </div>
  );
};
