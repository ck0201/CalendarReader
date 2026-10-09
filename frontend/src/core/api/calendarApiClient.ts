import type {
  AiBriefingResult,
  AuthStatusResponse,
  CalendarEvent,
  CalendarInsightResult,
  CalendarTask,
  UserCalendar,
} from '../types/models';

export class ApiError extends Error {
  status?: number;
  detail?: string;

  constructor(message: string, status?: number, detail?: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.detail = detail;
  }
}

async function request<T>(url: string, options: RequestInit = {}): Promise<T> {
  const res = await fetch(url, {
    credentials: 'include',
    headers: {
      Accept: 'application/json',
      ...options.headers,
    },
    ...options,
  });

  if (res.status === 401) {
    throw new ApiError('Unauthorized or session expired', 401);
  }

  if (!res.ok) {
    const errorData = await res.json().catch(() => ({}));
    const message = errorData.detail || errorData.error || `HTTP error ${res.status}`;
    throw new ApiError(message, res.status, errorData.detail);
  }

  return (await res.json()) as T;
}

export const calendarApiClient = {
  async getAuthStatus(): Promise<AuthStatusResponse> {
    return request<AuthStatusResponse>('/api/auth/status');
  },

  async disconnectAccount(accountId: string): Promise<{ success: boolean }> {
    return request<{ success: boolean }>(`/api/auth/accounts/${encodeURIComponent(accountId)}`, {
      method: 'DELETE',
    });
  },

  async logout(): Promise<{ success: boolean }> {
    return request<{ success: boolean }>('/api/auth/logout', {
      method: 'POST',
    });
  },

  async getCalendars(): Promise<UserCalendar[]> {
    return request<UserCalendar[]>('/api/calendar/calendars');
  },

  async getInsights(start: Date, end: Date, calendarIds?: string[]): Promise<CalendarInsightResult> {
    let url = `/api/calendar/insights?start=${encodeURIComponent(start.toISOString())}&end=${encodeURIComponent(end.toISOString())}`;
    if (calendarIds && calendarIds.length > 0) {
      url += `&calendars=${encodeURIComponent(calendarIds.join(','))}`;
    }
    return request<CalendarInsightResult>(url);
  },

  async getEvents(start: Date, end: Date, filter?: string, calendarIds?: string[]): Promise<CalendarEvent[]> {
    let url = `/api/calendar/events?start=${encodeURIComponent(start.toISOString())}&end=${encodeURIComponent(end.toISOString())}`;
    if (filter) {
      url += `&filter=${encodeURIComponent(filter)}`;
    }
    if (calendarIds && calendarIds.length > 0) {
      url += `&calendars=${encodeURIComponent(calendarIds.join(','))}`;
    }
    return request<CalendarEvent[]>(url);
  },

  async getTasks(start: Date, end: Date): Promise<CalendarTask[]> {
    const url = `/api/calendar/tasks?start=${encodeURIComponent(start.toISOString())}&end=${encodeURIComponent(end.toISOString())}`;
    return request<CalendarTask[]>(url);
  },

  async getBriefing(start: Date, end: Date, calendarIds?: string[], signal?: AbortSignal): Promise<AiBriefingResult> {
    let url = `/api/calendar/briefing?start=${encodeURIComponent(start.toISOString())}&end=${encodeURIComponent(end.toISOString())}`;
    if (calendarIds && calendarIds.length > 0) {
      url += `&calendars=${encodeURIComponent(calendarIds.join(','))}`;
    }
    return request<AiBriefingResult>(url, { signal });
  },
};
