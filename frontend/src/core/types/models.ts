export interface ConnectedAccount {
  accountId: string;
  email: string;
  displayName: string;
  avatarUrl?: string | null;
  connectedAt: string;
  requiresReauth: boolean;
}

export interface AuthStatusResponse {
  isAuthenticated: boolean;
  isConfigured: boolean;
  missingConfigMessage?: string | null;
  accounts: ConnectedAccount[];
}

export interface CalendarEvent {
  id: string;
  title: string;
  start: string;
  end: string;
  isAllDay: boolean;
  isRecurring: boolean;
  attendeeCount: number;
  description?: string | null;
  isTrainJourney: boolean;
  status?: string | null;
  htmlLink?: string | null;
  accountEmail: string;
}

export interface CalendarTask {
  id: string;
  title: string;
  dueDate: string;
  isAllDay: boolean;
  isCompleted: boolean;
  description?: string | null;
  htmlLink?: string | null;
  accountEmail: string;
}

export interface FocusBlockInfo {
  duration: string;
  formattedDuration: string;
  dayOfWeek: string;
  start?: string | null;
  end?: string | null;
}

export interface BusiestDayInfo {
  dayOfWeek: string;
  dateFormatted: string;
  scheduledTime: string;
  formattedScheduledTime: string;
  eventCount: number;
}

export interface RecurringTimeInfo {
  duration: string;
  formattedDuration: string;
  percentageOfScheduledTime: number;
  recurringEventCount: number;
}

export interface ConflictEventSummary {
  id: string;
  title: string;
  start: string;
  end: string;
  htmlLink?: string | null;
}

export interface ScheduleConflict {
  id: string;
  eventA: ConflictEventSummary;
  eventB: ConflictEventSummary;
  overlapStart: string;
  overlapEnd: string;
  overlapDuration: string;
  formattedOverlapDuration: string;
  accountEmail: string;
}

export interface ScheduleSuggestion {
  type: string;
  title: string;
  description: string;
  severity: 'warning' | 'info' | 'success' | string;
}

export interface ScheduleHealthInfo {
  totalConflicts: number;
  totalConflictDuration: string;
  formattedConflictDuration: string;
  affectedEventsCount: number;
  conflictFreeDaysCount: number;
  mostConflictedDay: string;
  hasTodayConflict: boolean;
  conflicts: ScheduleConflict[];
  suggestions: ScheduleSuggestion[];
}

export interface CalendarInsightResult {
  startDate: string;
  endDate: string;
  totalEvents: number;
  timedEventsCount: number;
  allDayEventsCount: number;
  totalScheduledTime: string;
  formattedScheduledTime: string;
  totalMeetingTime: string;
  formattedMeetingTime: string;
  longestFocusBlock: FocusBlockInfo;
  busiestDay: BusiestDayInfo;
  recurringTime: RecurringTimeInfo;
  observations: string[];
  events: CalendarEvent[];
  tasks: CalendarTask[];
  scheduleHealth: ScheduleHealthInfo;
}

export type RangeType = 'today' | 'week';
export type EventFilterType = 'all' | 'train' | 'other';
