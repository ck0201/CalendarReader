import React from 'react';
import type { ConflictEventSummary, ScheduleConflict } from '../../core/types/models';

interface ConflictTimelineProps {
  conflicts: ScheduleConflict[];
}

interface DayTimelineData {
  date: Date;
  formattedDate: string;
  conflicts: ScheduleConflict[];
  eventsMap: Map<string, ConflictEventSummary>;
}

export const ConflictTimeline: React.FC<ConflictTimelineProps> = ({ conflicts }) => {
  if (!conflicts || conflicts.length === 0) return null;

  // Group conflicts by calendar date of overlap
  const conflictsByDay = new Map<string, DayTimelineData>();
  conflicts.forEach((c) => {
    const oDate = new Date(c.overlapStart);
    const dateKey = oDate.toDateString();
    if (!conflictsByDay.has(dateKey)) {
      conflictsByDay.set(dateKey, {
        date: oDate,
        formattedDate: oDate.toLocaleDateString(undefined, {
          weekday: 'long',
          month: 'short',
          day: 'numeric',
          year: 'numeric',
        }),
        conflicts: [],
        eventsMap: new Map(),
      });
    }
    const dayData = conflictsByDay.get(dateKey)!;
    dayData.conflicts.push(c);
    if (c.eventA?.id) dayData.eventsMap.set(c.eventA.id, c.eventA);
    if (c.eventB?.id) dayData.eventsMap.set(c.eventB.id, c.eventB);
  });

  return (
    <div className="conflict-timeline-wrapper">
      <div className="timeline-header">
        <div className="timeline-title-group">
          <span className="timeline-title">Daily Conflict Timeline</span>
          <span className="timeline-subtitle">Visual inspection of overlapping commitments</span>
        </div>
      </div>

      <div className="conflict-timeline-container">
        {Array.from(conflictsByDay.values()).map((dayData) => {
          // Determine day time boundaries across conflicting events
          let minHour = 8;
          let maxHour = 20;

          dayData.eventsMap.forEach((ev) => {
            const s = new Date(ev.start);
            const e = new Date(ev.end);
            if (s.getHours() < minHour) minHour = Math.max(0, s.getHours() - 1);
            if (e.getHours() >= maxHour) maxHour = Math.min(24, e.getHours() + 1);
          });

          const dayStart = new Date(
            dayData.date.getFullYear(),
            dayData.date.getMonth(),
            dayData.date.getDate(),
            minHour,
            0,
            0
          );
          const totalMs = (maxHour - minHour) * 3600 * 1000;

          // Axis marks (every 2 hours)
          const axisLabels: string[] = [];
          for (let h = minHour; h <= maxHour; h += 2) {
            const markDate = new Date(
              dayData.date.getFullYear(),
              dayData.date.getMonth(),
              dayData.date.getDate(),
              h,
              0,
              0
            );
            axisLabels.push(markDate.toLocaleTimeString([], { hour: 'numeric', hour12: true }));
          }

          const dayEvents = Array.from(dayData.eventsMap.values()).sort(
            (a, b) => new Date(a.start).getTime() - new Date(b.start).getTime()
          );

          return (
            <div key={dayData.date.toDateString()} className="timeline-day-card">
              <div className="timeline-day-header">
                <span className="timeline-day-title">{dayData.formattedDate}</span>
                <span className="timeline-day-conflicts-pill">
                  {dayData.conflicts.length} conflict{dayData.conflicts.length === 1 ? '' : 's'}
                </span>
              </div>

              <div className="timeline-axis">
                {axisLabels.map((lbl, idx) => (
                  <span key={idx} className="timeline-axis-label">
                    {lbl}
                  </span>
                ))}
              </div>

              <div className="timeline-tracks-area">
                {dayEvents.map((ev) => {
                  const evStart = new Date(ev.start);
                  const evEnd = new Date(ev.end);

                  const leftPct = Math.max(
                    0,
                    Math.min(100, ((evStart.getTime() - dayStart.getTime()) / totalMs) * 100)
                  );
                  const rawWidthPct = ((evEnd.getTime() - evStart.getTime()) / totalMs) * 100;
                  const widthPct = Math.max(4, Math.min(100 - leftPct, rawWidthPct));

                  const timeStr = `${evStart.toLocaleTimeString([], {
                    hour: 'numeric',
                    minute: '2-digit',
                    hour12: true,
                  })} – ${evEnd.toLocaleTimeString([], {
                    hour: 'numeric',
                    minute: '2-digit',
                    hour12: true,
                  })}`;

                  return (
                    <div key={ev.id} className="timeline-event-bar-wrapper">
                      <div
                        className="timeline-event-bar conflicted"
                        style={{
                          left: `${leftPct.toFixed(1)}%`,
                          width: `${widthPct.toFixed(1)}%`,
                        }}
                        title={`${ev.title}${ev.accountEmail ? ` (${ev.accountEmail})` : ''} (${timeStr})`}
                      >
                        <span className="timeline-event-bar-title">{ev.title}</span>
                        <span className="timeline-event-bar-time">{timeStr}</span>
                      </div>
                    </div>
                  );
                })}
              </div>

              {dayData.conflicts.map((c) => {
                const oStart = new Date(c.overlapStart).toLocaleTimeString([], {
                  hour: 'numeric',
                  minute: '2-digit',
                  hour12: true,
                });
                const oEnd = new Date(c.overlapEnd).toLocaleTimeString([], {
                  hour: 'numeric',
                  minute: '2-digit',
                  hour12: true,
                });
                return (
                  <div key={c.id} className="timeline-overlap-banner">
                    <svg
                      width="14"
                      height="14"
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth="2.5"
                    >
                      <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2" />
                    </svg>
                    <span>
                      <strong>{c.eventA.title}</strong>
                      {c.isCrossAccount && c.eventA.accountEmail ? ` (${c.eventA.accountEmail})` : ''}{' '}
                      &harr;{' '}
                      <strong>{c.eventB.title}</strong>
                      {c.isCrossAccount && c.eventB.accountEmail ? ` (${c.eventB.accountEmail})` : ''}:{' '}
                      {c.formattedOverlapDuration} overlap ({oStart} – {oEnd})
                    </span>
                  </div>
                );
              })}
            </div>
          );
        })}
      </div>
    </div>
  );
};
