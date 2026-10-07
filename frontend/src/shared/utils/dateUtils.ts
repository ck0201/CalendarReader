import type { CalendarEvent, CalendarTask, ConflictEventSummary, RangeType } from '../../core/types/models';

export function getDateRange(rangeType: RangeType): { start: Date; end: Date } {
  const now = new Date();
  let start: Date;
  let end: Date;

  if (rangeType === 'today') {
    start = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 0, 0, 0);
    end = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 23, 59, 59, 999);
  } else {
    // 'week': Monday of current week to Sunday end of day
    const day = now.getDay();
    // In JS, 0 is Sunday, 1 is Monday.
    const diffToMonday = day === 0 ? -6 : 1 - day;
    start = new Date(now.getFullYear(), now.getMonth(), now.getDate() + diffToMonday, 0, 0, 0);
    end = new Date(start.getFullYear(), start.getMonth(), start.getDate() + 6, 23, 59, 59, 999);
  }

  return { start, end };
}

export function formatRangeLabel(start: Date, end: Date, rangeType: RangeType): string {
  const options: Intl.DateTimeFormatOptions = { month: 'short', day: 'numeric' };
  if (rangeType === 'today') {
    return `Today, ${start.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric', year: 'numeric' })}`;
  }
  return `${start.toLocaleDateString(undefined, options)} – ${end.toLocaleDateString(undefined, { ...options, year: 'numeric' })}`;
}

export function formatEventTime(event: CalendarEvent): string {
  if (event.isAllDay) {
    return 'All Day';
  }
  const start = new Date(event.start);
  const end = new Date(event.end);
  const timeOptions: Intl.DateTimeFormatOptions = { hour: 'numeric', minute: '2-digit', hour12: true };
  const dateOptions: Intl.DateTimeFormatOptions = { month: 'short', day: 'numeric' };

  const sameDay = start.toDateString() === end.toDateString();
  if (sameDay) {
    return `${start.toLocaleTimeString([], timeOptions)} – ${end.toLocaleTimeString([], timeOptions)}`;
  }
  return `${start.toLocaleDateString([], dateOptions)} ${start.toLocaleTimeString([], timeOptions)} – ${end.toLocaleTimeString([], timeOptions)}`;
}

export function formatTaskDue(task: CalendarTask): string {
  if (!task.dueDate) return '';
  const date = new Date(task.dueDate);
  const dateOptions: Intl.DateTimeFormatOptions = { month: 'short', day: 'numeric' };
  const timeOptions: Intl.DateTimeFormatOptions = { hour: 'numeric', minute: '2-digit', hour12: true };

  if (task.isAllDay) {
    return date.toLocaleDateString([], dateOptions);
  }
  return `${date.toLocaleDateString([], dateOptions)}, ${date.toLocaleTimeString([], timeOptions)}`;
}

export function formatConflictTime(eventSummary: ConflictEventSummary): string {
  const start = new Date(eventSummary.start);
  const end = new Date(eventSummary.end);
  const timeOptions: Intl.DateTimeFormatOptions = { hour: 'numeric', minute: '2-digit', hour12: true };
  const dateOptions: Intl.DateTimeFormatOptions = { month: 'short', day: 'numeric' };
  const sameDay = start.toDateString() === end.toDateString();

  if (sameDay) {
    return `${start.toLocaleDateString([], dateOptions)} · ${start.toLocaleTimeString([], timeOptions)} – ${end.toLocaleTimeString([], timeOptions)}`;
  }
  return `${start.toLocaleDateString([], dateOptions)} ${start.toLocaleTimeString([], timeOptions)} – ${end.toLocaleDateString([], dateOptions)} ${end.toLocaleTimeString([], timeOptions)}`;
}
