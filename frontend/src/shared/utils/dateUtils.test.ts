import { describe, it, expect } from 'vitest';
import {
  getDateRange,
  formatRangeLabel,
  formatEventTime,
  formatTaskDue,
  formatConflictTime,
} from './dateUtils';
import type { CalendarEvent, CalendarTask, ConflictEventSummary } from '../../core/types/models';

describe('dateUtils', () => {
  describe('getDateRange', () => {
    it('returns start and end of today', () => {
      const { start, end } = getDateRange('today');
      expect(start).toBeInstanceOf(Date);
      expect(end).toBeInstanceOf(Date);
      expect(start.getHours()).toBe(0);
      expect(start.getMinutes()).toBe(0);
      expect(end.getHours()).toBe(23);
      expect(end.getMinutes()).toBe(59);
      expect(end.getTime()).toBeGreaterThan(start.getTime());
    });

    it('returns start and end of week', () => {
      const { start, end } = getDateRange('week');
      expect(start).toBeInstanceOf(Date);
      expect(end).toBeInstanceOf(Date);
      const days = (end.getTime() - start.getTime()) / (1000 * 60 * 60 * 24);
      expect(days).toBeCloseTo(7, 0);
    });
  });

  describe('formatRangeLabel', () => {
    it('formats today range label', () => {
      const { start, end } = getDateRange('today');
      const label = formatRangeLabel(start, end, 'today');
      expect(label).toContain('Today,');
    });

    it('formats week range label', () => {
      const { start, end } = getDateRange('week');
      const label = formatRangeLabel(start, end, 'week');
      expect(label).toContain('–');
    });
  });

  describe('formatEventTime', () => {
    it('returns All Day for all-day events', () => {
      const ev: CalendarEvent = {
        id: '1',
        title: 'Holiday',
        start: '2026-06-01',
        end: '2026-06-02',
        isAllDay: true,
        isRecurring: false,
        attendeeCount: 1,
        isTrainJourney: false,
        accountEmail: 'test@example.com',
      };
      expect(formatEventTime(ev)).toBe('All Day');
    });

    it('formats single day event with start and end times', () => {
      const ev: CalendarEvent = {
        id: '2',
        title: 'Standup',
        start: '2026-06-01T10:00:00Z',
        end: '2026-06-01T10:30:00Z',
        isAllDay: false,
        isRecurring: false,
        attendeeCount: 1,
        isTrainJourney: false,
        accountEmail: 'test@example.com',
      };
      const formatted = formatEventTime(ev);
      expect(formatted).toContain('–');
    });

    it('formats multi-day event with both dates', () => {
      const ev: CalendarEvent = {
        id: '3',
        title: 'Hackathon',
        start: '2026-06-01T10:00:00Z',
        end: '2026-06-03T18:00:00Z',
        isAllDay: false,
        isRecurring: false,
        attendeeCount: 1,
        isTrainJourney: false,
        accountEmail: 'test@example.com',
      };
      const formatted = formatEventTime(ev);
      expect(formatted).toContain('–');
    });
  });

  describe('formatTaskDue', () => {
    it('returns empty string for missing dueDate', () => {
      const task: CalendarTask = {
        id: '1',
        title: 'Task without due',
        dueDate: '',
        isAllDay: false,
        isCompleted: false,
        accountEmail: 'test@example.com',
      };
      expect(formatTaskDue(task)).toBe('');
    });

    it('formats all-day task due date', () => {
      const task: CalendarTask = {
        id: '2',
        title: 'All-day task',
        dueDate: '2026-06-01T00:00:00Z',
        isAllDay: true,
        isCompleted: false,
        accountEmail: 'test@example.com',
      };
      expect(formatTaskDue(task)).toContain('Jun');
    });

    it('formats timed task due date', () => {
      const task: CalendarTask = {
        id: '3',
        title: 'Timed task',
        dueDate: '2026-06-01T15:30:00Z',
        isAllDay: false,
        isCompleted: false,
        accountEmail: 'test@example.com',
      };
      expect(formatTaskDue(task)).toContain('Jun');
    });
  });

  describe('formatConflictTime', () => {
    it('formats conflict summary interval', () => {
      const summary: ConflictEventSummary = {
        id: 'ev-1',
        title: 'Design Review',
        start: '2026-06-01T10:00:00Z',
        end: '2026-06-01T11:00:00Z',
        htmlLink: null,
      };
      const result = formatConflictTime(summary);
      expect(result).toContain('·');
      expect(result).toContain('–');
    });
  });
});
