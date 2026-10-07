import React, { useEffect, useMemo, useState } from 'react';
import type { CalendarEvent, EventFilterType } from '../../core/types/models';
import { calendarApiClient } from '../../core/api/calendarApiClient';
import { EventFilterBar } from './EventFilterBar';
import { EventRow } from './EventRow';

interface EventsSectionProps {
  events: CalendarEvent[];
  conflictingEventIds: Set<string>;
  isMultiAccount: boolean;
}

export const EventsSection: React.FC<EventsSectionProps> = ({
  events,
  conflictingEventIds,
  isMultiAccount,
}) => {
  const [filter, setFilter] = useState<EventFilterType>('all');
  const [trainLoading, setTrainLoading] = useState<boolean>(false);
  const [fetchedTrains, setFetchedTrains] = useState<CalendarEvent[] | null>(null);

  const [now] = useState(() => new Date());
  const todayStart = useMemo(
    () => new Date(now.getFullYear(), now.getMonth(), now.getDate(), 0, 0, 0),
    [now]
  );
  const twoMonthsLater = useMemo(() => {
    const d = new Date(todayStart);
    d.setMonth(d.getMonth() + 2);
    d.setHours(23, 59, 59, 999);
    return d;
  }, [todayStart]);

  const coversTwoMonths = useMemo(
    () =>
      events.some(
        (ev) => new Date(ev.start) > new Date(todayStart.getTime() + 50 * 24 * 60 * 60 * 1000)
      ),
    [events, todayStart]
  );

  useEffect(() => {
    if (filter !== 'train' || coversTwoMonths || fetchedTrains !== null) return;

    let isMounted = true;
    setTrainLoading(true);

    calendarApiClient
      .getEvents(todayStart, twoMonthsLater, 'train')
      .then((data) => {
        if (isMounted) {
          setFetchedTrains(data);
        }
      })
      .catch((err) => {
        console.error('Failed to fetch upcoming train journeys:', err);
        if (isMounted) {
          setFetchedTrains([]);
        }
      })
      .finally(() => {
        if (isMounted) {
          setTrainLoading(false);
        }
      });

    return () => {
      isMounted = false;
    };
  }, [filter, coversTwoMonths, fetchedTrains, todayStart, twoMonthsLater]);

  const trainJourneys = useMemo(() => {
    const source = coversTwoMonths ? events : (fetchedTrains ?? events);
    return source
      .filter(
        (ev) =>
          ev.isTrainJourney &&
          new Date(ev.end || ev.start) >= now &&
          new Date(ev.start) <= twoMonthsLater
      )
      .sort((a, b) => new Date(a.start).getTime() - new Date(b.start).getTime());
  }, [coversTwoMonths, events, fetchedTrains, now, twoMonthsLater]);

  const displayEvents = useMemo(() => {
    if (filter === 'train') return trainJourneys;
    if (filter === 'other') return events.filter((ev) => !ev.isTrainJourney);
    return events;
  }, [filter, trainJourneys, events]);

  const countBadgeText =
    filter === 'train'
      ? `${displayEvents.length} train journey${displayEvents.length === 1 ? '' : 's'}`
      : `${displayEvents.length} event${displayEvents.length === 1 ? '' : 's'}`;

  return (
    <div className="events-section">
      <div className="events-header">
        <div className="events-title-group">
          <h2>Events in this Period</h2>
          <span className="badge">{countBadgeText}</span>
        </div>
        <EventFilterBar currentFilter={filter} onFilterChange={setFilter} />
      </div>

      <div className="events-list">
        {trainLoading && filter === 'train' ? (
          <div className="empty-state">Loading train journeys...</div>
        ) : displayEvents.length > 0 ? (
          displayEvents.map((ev) => (
            <EventRow
              key={ev.id}
              event={ev}
              isConflicted={conflictingEventIds.has(ev.id)}
              isMultiAccount={isMultiAccount}
            />
          ))
        ) : (
          <div className="empty-state">
            {filter === 'train' &&
              'No upcoming train journeys scheduled in the next 2 months.'}
            {filter === 'other' && 'No other events scheduled in this period.'}
            {filter === 'all' && 'No events scheduled in this period.'}
          </div>
        )}
      </div>
    </div>
  );
};
