/**
 * CalendarReader Client Application
 */

(function () {
  'use strict';

  // DOM Elements
  const landingView = document.getElementById('landing-view');
  const dashboardView = document.getElementById('dashboard-view');
  const authActions = document.getElementById('auth-actions');
  const logoutBtn = document.getElementById('logout-btn');
  const alertBanner = document.getElementById('alert-banner');
  const connectBtn = document.getElementById('connect-btn');
  const loadingState = document.getElementById('loading-state');
  const insightsContent = document.getElementById('insights-content');
  const rangeSubtitle = document.getElementById('range-subtitle');
  const rangeBtns = document.querySelectorAll('.range-btn');

  // Metrics DOM
  const summaryHeadline = document.getElementById('summary-headline');
  const summaryMeta = document.getElementById('summary-meta');
  const metricScheduled = document.getElementById('metric-scheduled');
  const metricMeeting = document.getElementById('metric-meeting');
  const metricMeetingSub = document.getElementById('metric-meeting-sub');
  const metricFocus = document.getElementById('metric-focus');
  const metricFocusSub = document.getElementById('metric-focus-sub');
  const metricBusiest = document.getElementById('metric-busiest');
  const metricBusiestSub = document.getElementById('metric-busiest-sub');
  const observationsList = document.getElementById('observations-list');
  const tasksList = document.getElementById('tasks-list');
  const tasksCountBadge = document.getElementById('tasks-count-badge');
  const eventsList = document.getElementById('events-list');
  const eventsCountBadge = document.getElementById('events-count-badge');
  const eventFilterBtns = document.querySelectorAll('.event-filter-btn');

  let currentRange = 'week';
  let currentEvents = [];
  let currentEventFilter = 'all';
  let upcomingTrainJourneysCache = null;

  // -------------------------------------------------------------
  // Helpers
  // -------------------------------------------------------------

  function showAlert(message, type = 'info') {
    alertBanner.textContent = message;
    alertBanner.className = `alert-banner ${type}`;
    alertBanner.classList.remove('hidden');
  }

  function hideAlert() {
    alertBanner.classList.add('hidden');
    alertBanner.textContent = '';
  }

  function getDateRange(rangeType) {
    const now = new Date();
    let start, end;

    if (rangeType === 'today') {
      start = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 0, 0, 0);
      end = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 23, 59, 59, 999);
    } else {
      // 'week': Monday of current week to Sunday end of day
      const day = now.getDay();
      // In JS, 0 is Sunday, 1 is Monday. Calculate difference to Monday:
      const diffToMonday = day === 0 ? -6 : 1 - day;
      start = new Date(now.getFullYear(), now.getMonth(), now.getDate() + diffToMonday, 0, 0, 0);
      end = new Date(start.getFullYear(), start.getMonth(), start.getDate() + 6, 23, 59, 59, 999);
    }

    return { start, end };
  }

  function formatRangeLabel(start, end, rangeType) {
    const options = { month: 'short', day: 'numeric' };
    if (rangeType === 'today') {
      return `Today, ${start.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric', year: 'numeric' })}`;
    }
    return `${start.toLocaleDateString(undefined, options)} – ${end.toLocaleDateString(undefined, { ...options, year: 'numeric' })}`;
  }

  function formatEventTime(event) {
    if (event.isAllDay) {
      return 'All Day';
    }
    const start = new Date(event.start);
    const end = new Date(event.end);
    const timeOptions = { hour: 'numeric', minute: '2-digit', hour12: true };
    const dateOptions = { month: 'short', day: 'numeric' };

    const sameDay = start.toDateString() === end.toDateString();
    if (sameDay) {
      return `${start.toLocaleTimeString([], timeOptions)} – ${end.toLocaleTimeString([], timeOptions)}`;
    }
    return `${start.toLocaleDateString([], dateOptions)} ${start.toLocaleTimeString([], timeOptions)} – ${end.toLocaleTimeString([], timeOptions)}`;
  }

  function formatTaskDue(task) {
    if (!task.dueDate) return '';
    const date = new Date(task.dueDate);
    const dateOptions = { month: 'short', day: 'numeric' };
    const timeOptions = { hour: 'numeric', minute: '2-digit', hour12: true };

    if (task.isAllDay) {
      return date.toLocaleDateString([], dateOptions);
    }
    return `${date.toLocaleDateString([], dateOptions)}, ${date.toLocaleTimeString([], timeOptions)}`;
  }

  // -------------------------------------------------------------
  // Data Fetching & Rendering
  // -------------------------------------------------------------

  async function checkAuthStatus() {
    try {
      // Check query parameter errors first
      const params = new URLSearchParams(window.location.search);
      if (params.has('error')) {
        const err = params.get('error');
        if (err === 'access_denied') {
          showAlert('Google authorization was cancelled or access was denied.', 'warning');
        } else {
          showAlert(`Authentication error: ${err}`, 'error');
        }
        window.history.replaceState({}, document.title, window.location.pathname);
      }

      const res = await fetch('/api/auth/status');
      const data = await res.json();

      if (!data.isConfigured) {
        showAlert(
          'Google OAuth is not configured on the server. Please set GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET in your environment.',
          'warning'
        );
        connectBtn.classList.add('disabled');
        connectBtn.onclick = function (e) {
          e.preventDefault();
          showAlert('Google OAuth credentials must be configured before connecting.', 'error');
        };
      }

      if (data.isAuthenticated) {
        showDashboard();
      } else {
        showLanding();
      }
    } catch (err) {
      console.error('Failed to check auth status:', err);
      showLanding();
    }
  }

  function showLanding() {
    landingView.classList.remove('hidden');
    dashboardView.classList.add('hidden');
    authActions.classList.add('hidden');
  }

  function showDashboard() {
    landingView.classList.add('hidden');
    dashboardView.classList.remove('hidden');
    authActions.classList.remove('hidden');
    loadInsights(currentRange);
  }

  async function loadInsights(rangeType) {
    const { start, end } = getDateRange(rangeType);
    rangeSubtitle.textContent = formatRangeLabel(start, end, rangeType);

    loadingState.classList.remove('hidden');
    insightsContent.classList.add('hidden');

    try {
      const url = `/api/calendar/insights?start=${encodeURIComponent(start.toISOString())}&end=${encodeURIComponent(end.toISOString())}`;
      const res = await fetch(url);

      if (res.status === 401) {
        showLanding();
        showAlert('Your session has expired. Please reconnect your Google Calendar.', 'warning');
        return;
      }

      if (!res.ok) {
        const errorData = await res.json().catch(() => ({}));
        throw new Error(errorData.detail || errorData.error || 'Failed to fetch insights');
      }

      const data = await res.json();
      renderInsights(data);
    } catch (err) {
      console.error('Error fetching insights:', err);
      showAlert(`Unable to load insights: ${err.message}`, 'error');
    } finally {
      loadingState.classList.add('hidden');
      insightsContent.classList.remove('hidden');
    }
  }

  function renderInsights(data) {
    // 1. Headline summary
    summaryHeadline.textContent = `${data.totalEvents} event${data.totalEvents === 1 ? '' : 's'} · ${data.formattedScheduledTime} scheduled`;

    let metaParts = [];
    if (data.allDayEventsCount > 0) {
      metaParts.push(`${data.allDayEventsCount} all-day event${data.allDayEventsCount === 1 ? '' : 's'}`);
    }
    if (data.recurringTime && data.recurringTime.recurringEventCount > 0) {
      metaParts.push(`${data.recurringTime.percentageOfScheduledTime}% recurring`);
    }
    summaryMeta.textContent = metaParts.join(' · ');

    // 2. Metric Cards
    // Metric 1: Scheduled Time
    metricScheduled.textContent = data.formattedScheduledTime || '0m';

    // Metric 2: Meeting Time
    metricMeeting.textContent = data.formattedMeetingTime || '0m';
    if (data.recurringTime && data.recurringTime.percentageOfScheduledTime > 0) {
      metricMeetingSub.textContent = `${data.recurringTime.percentageOfScheduledTime}% recurring time`;
    } else {
      metricMeetingSub.textContent = 'Collaborative sessions';
    }

    // Metric 3: Longest Focus Block
    if (data.longestFocusBlock && data.longestFocusBlock.duration !== '00:00:00' && data.longestFocusBlock.formattedDuration !== '0m') {
      metricFocus.textContent = data.longestFocusBlock.formattedDuration;
      let focusSub = data.longestFocusBlock.dayOfWeek;
      if (data.longestFocusBlock.start && data.longestFocusBlock.end) {
        const sTime = new Date(data.longestFocusBlock.start).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit', hour12: true });
        const eTime = new Date(data.longestFocusBlock.end).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit', hour12: true });
        focusSub += ` (${sTime} – ${eTime})`;
      }
      metricFocusSub.textContent = focusSub;
    } else {
      metricFocus.textContent = '0m';
      metricFocusSub.textContent = 'No daytime focus block';
    }

    // Metric 4: Busiest Day
    if (data.busiestDay && data.busiestDay.dayOfWeek !== 'None') {
      metricBusiest.textContent = data.busiestDay.dayOfWeek;
      metricBusiestSub.textContent = `${data.busiestDay.formattedScheduledTime} scheduled (${data.busiestDay.eventCount} event${data.busiestDay.eventCount === 1 ? '' : 's'})`;
    } else {
      metricBusiest.textContent = '-';
      metricBusiestSub.textContent = 'No events';
    }

    // 3. Observations
    observationsList.innerHTML = '';
    if (data.observations && data.observations.length > 0) {
      data.observations.forEach((obs) => {
        const item = document.createElement('div');
        item.className = 'observation-item';
        item.innerHTML = `
          <div class="observation-dot"></div>
          <div>${escapeHtml(obs)}</div>
        `;
        observationsList.appendChild(item);
      });
    } else {
      observationsList.innerHTML = '<div class="empty-state">No observations available.</div>';
    }

    // 4. Tasks
    renderTasks(data.tasks || []);

    // 5. Events breakdown list
    currentEvents = data.events || [];
    renderFilteredEvents();
  }

  function renderTasks(tasks) {
    tasksList.innerHTML = '';
    tasksCountBadge.textContent = `${tasks.length} task${tasks.length === 1 ? '' : 's'}`;

    if (tasks.length === 0) {
      tasksList.innerHTML = '<div class="empty-state">No tasks scheduled in this period.</div>';
      return;
    }

    tasks.forEach((task) => {
      const row = document.createElement('div');
      row.className = 'task-row';

      const checkmark = task.isCompleted
        ? '<svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="#ffffff" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg>'
        : '';

      const dueHtml = task.dueDate
        ? `<div class="task-meta"><span class="task-due">${escapeHtml(formatTaskDue(task))}</span></div>`
        : '';

      row.innerHTML = `
        <div class="task-info">
          <div class="task-checkbox${task.isCompleted ? ' completed' : ''}" title="${task.isCompleted ? 'Completed' : 'Pending'}">
            ${checkmark}
          </div>
          <span class="task-title${task.isCompleted ? ' completed' : ''}">${escapeHtml(task.title)}</span>
        </div>
        ${dueHtml}
      `;
      tasksList.appendChild(row);
    });
  }

  async function getUpcomingTrainJourneys() {
    const now = new Date();
    const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 0, 0, 0);
    const twoMonthsLater = new Date(todayStart);
    twoMonthsLater.setMonth(twoMonthsLater.getMonth() + 2);
    twoMonthsLater.setHours(23, 59, 59, 999);

    // If currentEvents already covers through 2 months, filter in-memory with zero network calls
    const coversTwoMonths = currentEvents.some((ev) => new Date(ev.start) > new Date(todayStart.getTime() + 50 * 24 * 60 * 60 * 1000));
    if (coversTwoMonths) {
      return currentEvents
        .filter((ev) => ev.isTrainJourney && new Date(ev.end || ev.start) >= now && new Date(ev.start) <= twoMonthsLater)
        .sort((a, b) => new Date(a.start) - new Date(b.start));
    }

    // Use session in-memory cache if available
    if (upcomingTrainJourneysCache !== null) {
      return upcomingTrainJourneysCache
        .filter((ev) => new Date(ev.end || ev.start) >= now)
        .sort((a, b) => new Date(a.start) - new Date(b.start));
    }

    try {
      const url = `/api/calendar/events?start=${encodeURIComponent(todayStart.toISOString())}&end=${encodeURIComponent(twoMonthsLater.toISOString())}&filter=train`;
      const res = await fetch(url);
      if (res.ok) {
        const events = await res.json();
        upcomingTrainJourneysCache = events
          .filter((ev) => new Date(ev.end || ev.start) >= now)
          .sort((a, b) => new Date(a.start) - new Date(b.start));
        return upcomingTrainJourneysCache;
      }
    } catch (err) {
      console.error('Failed to fetch upcoming train journeys:', err);
    }

    // Fallback: filter from current events excluding past
    return currentEvents
      .filter((ev) => ev.isTrainJourney && new Date(ev.end || ev.start) >= now)
      .sort((a, b) => new Date(a.start) - new Date(b.start));
  }

  async function renderFilteredEvents() {
    let filtered = [];

    if (currentEventFilter === 'train') {
      eventsList.innerHTML = '<div class="empty-state">Loading train journeys...</div>';
      filtered = await getUpcomingTrainJourneys();
    } else if (currentEventFilter === 'other') {
      filtered = currentEvents.filter((ev) => !ev.isTrainJourney);
    } else {
      filtered = currentEvents;
    }

    eventsList.innerHTML = '';

    if (currentEventFilter === 'train') {
      eventsCountBadge.textContent = `${filtered.length} train journey${filtered.length === 1 ? '' : 's'}`;
    } else {
      eventsCountBadge.textContent = `${filtered.length} event${filtered.length === 1 ? '' : 's'}`;
    }

    if (filtered.length === 0) {
      if (currentEventFilter === 'train') {
        eventsList.innerHTML = '<div class="empty-state">No upcoming train journeys scheduled in the next 2 months.</div>';
      } else if (currentEventFilter === 'other') {
        eventsList.innerHTML = '<div class="empty-state">No other events scheduled in this period.</div>';
      } else {
        eventsList.innerHTML = '<div class="empty-state">No events scheduled in this period.</div>';
      }
      return;
    }

    // Sort ascending by departure/start time
    const listToRender = currentEventFilter === 'train'
      ? [...filtered].sort((a, b) => new Date(a.start) - new Date(b.start))
      : filtered;

    listToRender.forEach((ev) => {
      const row = document.createElement('div');
      row.className = 'event-row';

      let tagsHtml = '';
      if (ev.isTrainJourney) {
        tagsHtml += '<span class="tag tag-train">Train Journey</span>';
      }
      if (ev.isAllDay) {
        tagsHtml += '<span class="tag tag-allday">All Day</span>';
      }
      if (ev.isRecurring) {
        tagsHtml += '<span class="tag tag-recurring">Recurring</span>';
      }
      if (ev.attendeeCount > 1) {
        tagsHtml += `<span class="tag tag-meeting">${ev.attendeeCount} Guests</span>`;
      }

      row.innerHTML = `
        <div class="event-info">
          <span class="event-time">${escapeHtml(formatEventTime(ev))}</span>
          <span class="event-title">${escapeHtml(ev.title)}</span>
        </div>
        <div class="event-tags">
          ${tagsHtml}
        </div>
      `;
      eventsList.appendChild(row);
    });
  }

  function escapeHtml(str) {
    if (!str) return '';
    return str
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  // -------------------------------------------------------------
  // Event Listeners
  // -------------------------------------------------------------

  rangeBtns.forEach((btn) => {
    btn.addEventListener('click', () => {
      rangeBtns.forEach((b) => b.classList.remove('active'));
      btn.classList.add('active');
      currentRange = btn.dataset.range;
      loadInsights(currentRange);
    });
  });

  eventFilterBtns.forEach((btn) => {
    btn.addEventListener('click', () => {
      eventFilterBtns.forEach((b) => b.classList.remove('active'));
      btn.classList.add('active');
      currentEventFilter = btn.dataset.eventFilter;
      renderFilteredEvents();
    });
  });

  logoutBtn.addEventListener('click', async () => {
    upcomingTrainJourneysCache = null;
    try {
      await fetch('/api/auth/logout', { method: 'POST' });
    } catch (err) {
      console.error('Logout error:', err);
    }
    showLanding();
    hideAlert();
  });

  // Init
  checkAuthStatus();
})();
