import React from 'react';
import type { CalendarEvent } from '../../core/types/models';
import { formatEventTime } from '../../shared/utils/dateUtils';
import { Badge } from '../../shared/components/Badge';

interface EventRowProps {
  event: CalendarEvent;
  isConflicted: boolean;
  isMultiAccount: boolean;
}

export const EventRow: React.FC<EventRowProps> = ({
  event,
  isConflicted,
  isMultiAccount,
}) => {
  return (
    <div className="event-row">
      <div className="event-info">
        <span className="event-time">{formatEventTime(event)}</span>
        <span className="event-title">{event.title}</span>
      </div>

      <div className="event-tags">
        {isMultiAccount && event.accountEmail && (
          <Badge variant="account" title={`Account: ${event.accountEmail}`}>
            {event.accountEmail}
          </Badge>
        )}
        {isConflicted && (
          <Badge variant="conflict" title="This event conflicts with another commitment">
            Conflict
          </Badge>
        )}
        {event.isTrainJourney && <Badge variant="train">Train Journey</Badge>}
        {event.isAllDay && <Badge variant="allday">All Day</Badge>}
        {event.isRecurring && <Badge variant="recurring">Recurring</Badge>}
        {event.attendeeCount > 1 && (
          <Badge variant="meeting">{event.attendeeCount} Guests</Badge>
        )}
      </div>
    </div>
  );
};
