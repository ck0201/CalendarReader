/**
 * CalendarReader Client Application
 */

(function () {
  'use strict';

  // DOM Elements
  const landingView = document.getElementById('landing-view');
  const dashboardView = document.getElementById('dashboard-view');
  const authActions = document.getElementById('auth-actions');
  const accountsList = document.getElementById('accounts-list');
  const reauthNotice = document.getElementById('reauth-notice');
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
  const summaryHealthBadge = document.getElementById('summary-health-badge');
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

  // Schedule Health DOM
  const healthCountBadge = document.getElementById('health-count-badge');
  const statConflictsCount = document.getElementById('stat-conflicts-count');
  const statConflictsDuration = document.getElementById('stat-conflicts-duration');
  const statAffectedEvents = document.getElementById('stat-affected-events');
  const statMostClashingDay = document.getElementById('stat-most-clashing-day');
  const conflictTimelineWrapper = document.getElementById('conflict-timeline-wrapper');
  const conflictTimeline = document.getElementById('conflict-timeline');
  const healthDetailsWrapper = document.getElementById('health-details-wrapper');

  let currentRange = 'week';
  let currentEvents = [];
  let currentEventFilter = 'all';
  let upcomingTrainJourneysCache = null;
  let connectedAccounts = [];
  let conflictingEventIds = new Set();

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

  function renderAccountsList() {
    if (!accountsList) return;
    accountsList.innerHTML = '';

    let hasReauth = false;
    const reauthEmails = [];

    connectedAccounts.forEach((acc) => {
      const chip = document.createElement('div');
      chip.className = `account-chip ${acc.requiresReauth ? 'reauth' : 'connected'}`;

      const dot = document.createElement('span');
      dot.className = `account-dot ${acc.requiresReauth ? 'reauth' : ''}`;

      const emailSpan = document.createElement('span');
      emailSpan.className = 'account-email';
      emailSpan.textContent = acc.email;
      emailSpan.title = acc.displayName ? `${acc.displayName} (${acc.email})` : acc.email;

      const disconnectBtn = document.createElement('button');
      disconnectBtn.className = 'account-disconnect-btn';
      disconnectBtn.innerHTML = '&times;';
      disconnectBtn.title = `Disconnect ${acc.email}`;
      disconnectBtn.setAttribute('aria-label', `Disconnect ${acc.email}`);
      disconnectBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        disconnectAccount(acc.accountId, acc.email);
      });

      chip.appendChild(dot);
      chip.appendChild(emailSpan);
      chip.appendChild(disconnectBtn);
      accountsList.appendChild(chip);

      if (acc.requiresReauth) {
        hasReauth = true;
        reauthEmails.push(acc.email);
      }
    });

    if (reauthNotice) {
      if (hasReauth) {
        reauthNotice.innerHTML = `
          <div><strong>${escapeHtml(reauthEmails.join(', '))}</strong> requires re-authorization. Reconnect to restore all events.</div>
          <a href="/api/auth/login?prompt=select_account" class="btn btn-subtle btn-sm">Reconnect</a>
        `;
        reauthNotice.classList.remove('hidden');
      } else {
        reauthNotice.classList.add('hidden');
        reauthNotice.innerHTML = '';
      }
    }
  }

  async function disconnectAccount(accountId, email) {
    if (!confirm(`Disconnect Google account: ${email}?`)) {
      return;
    }

    try {
      upcomingTrainJourneysCache = null;
      const res = await fetch(`/api/auth/accounts/${encodeURIComponent(accountId)}`, { method: 'DELETE' });
      if (res.ok) {
        showAlert(`Disconnected ${email}.`, 'info');
        await checkAuthStatus();
      } else {
        showAlert(`Failed to disconnect ${email}.`, 'error');
      }
    } catch (err) {
      console.error('Failed to disconnect account:', err);
      showAlert(`Error disconnecting account: ${err.message}`, 'error');
    }
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

      connectedAccounts = data.accounts || [];
      renderAccountsList();

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
        showAlert('Your session has expired or no accounts are connected. Please connect Google Calendar.', 'warning');
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

    // 4. Schedule Health & Conflicts
    renderScheduleHealth(data.scheduleHealth || {});

    // 5. Tasks
    renderTasks(data.tasks || []);

    // 6. Events breakdown list
    currentEvents = data.events || [];
    renderFilteredEvents();
  }

  function formatConflictTime(eventSummary) {
    const start = new Date(eventSummary.start);
    const end = new Date(eventSummary.end);
    const timeOptions = { hour: 'numeric', minute: '2-digit', hour12: true };
    const dateOptions = { month: 'short', day: 'numeric' };
    const sameDay = start.toDateString() === end.toDateString();

    if (sameDay) {
      return `${start.toLocaleDateString([], dateOptions)} · ${start.toLocaleTimeString([], timeOptions)} – ${end.toLocaleTimeString([], timeOptions)}`;
    }
    return `${start.toLocaleDateString([], dateOptions)} ${start.toLocaleTimeString([], timeOptions)} – ${end.toLocaleDateString([], dateOptions)} ${end.toLocaleTimeString([], timeOptions)}`;
  }

  function renderScheduleHealth(health) {
    const totalConflicts = health.totalConflicts || 0;
    const conflicts = health.conflicts || [];
    const suggestions = health.suggestions || [];
    const conflictDurationStr = health.formattedConflictDuration || '0m';
    const affectedEventsCount = health.affectedEventsCount || 0;
    const mostClashingDay = health.mostConflictedDay || 'None';

    // Populate set of conflicting event IDs for inline badges on event rows
    conflictingEventIds.clear();
    conflicts.forEach((c) => {
      if (c.eventA && c.eventA.id) conflictingEventIds.add(c.eventA.id);
      if (c.eventB && c.eventB.id) conflictingEventIds.add(c.eventB.id);
    });

    // 1. Update summary banner badge
    if (summaryHealthBadge) {
      if (totalConflicts > 0) {
        summaryHealthBadge.className = 'health-pill warning';
        summaryHealthBadge.textContent = `⚠️ ${totalConflicts} Conflict${totalConflicts === 1 ? '' : 's'} (${conflictDurationStr})`;
      } else {
        summaryHealthBadge.className = 'health-pill healthy';
        summaryHealthBadge.textContent = '✓ Clash-Free';
      }
    }

    // 2. Update health header badge
    if (healthCountBadge) {
      healthCountBadge.textContent = `${totalConflicts} conflict${totalConflicts === 1 ? '' : 's'}`;
    }

    // 3. Update quick stats ribbon
    if (statConflictsCount) {
      statConflictsCount.textContent = totalConflicts;
      statConflictsCount.className = `health-stat-value ${totalConflicts > 0 ? 'danger' : 'success'}`;
    }
    if (statConflictsDuration) {
      statConflictsDuration.textContent = conflictDurationStr;
    }
    if (statAffectedEvents) {
      statAffectedEvents.textContent = affectedEventsCount;
    }
    if (statMostClashingDay) {
      statMostClashingDay.textContent = mostClashingDay;
    }

    // 4. Render Daily Conflict Timeline
    if (totalConflicts > 0 && conflictTimelineWrapper && conflictTimeline) {
      conflictTimelineWrapper.classList.remove('hidden');
      renderConflictTimeline(conflicts);
    } else if (conflictTimelineWrapper) {
      conflictTimelineWrapper.classList.add('hidden');
      if (conflictTimeline) conflictTimeline.innerHTML = '';
    }

    // 5. Render Suggestions & Conflict Details
    if (healthDetailsWrapper) {
      healthDetailsWrapper.innerHTML = '';

      // Suggestions list
      if (suggestions.length > 0) {
        suggestions.forEach((sug) => {
          const card = document.createElement('div');
          card.className = `suggestion-card ${escapeHtml(sug.severity || 'info')}`;

          let iconSvg = '';
          if (sug.severity === 'warning') {
            iconSvg = '<svg class="suggestion-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#ef4444" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>';
          } else if (sug.severity === 'success') {
            iconSvg = '<svg class="suggestion-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#10b981" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><polyline points="9 11 12 14 22 4"></polyline></svg>';
          } else {
            iconSvg = '<svg class="suggestion-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#6366f1" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>';
          }

          card.innerHTML = `
            ${iconSvg}
            <div class="suggestion-body">
              <span class="suggestion-title">${escapeHtml(sug.title)}</span>
              <span class="suggestion-desc">${escapeHtml(sug.description)}</span>
            </div>
          `;
          healthDetailsWrapper.appendChild(card);
        });
      }

      // Conflict items list
      if (conflicts.length > 0) {
        conflicts.forEach((c) => {
          const card = document.createElement('div');
          card.className = 'conflict-item-card';

          const accountTag = (c.accountEmail && connectedAccounts.length > 1)
            ? `<span class="tag tag-account">${escapeHtml(c.accountEmail)}</span>`
            : '';

          card.innerHTML = `
            <div class="conflict-pair-info">
              <div class="conflict-pair-titles">
                <span>${escapeHtml(c.eventA.title)}</span>
                <span class="conflict-clash-arrow">&harr;</span>
                <span>${escapeHtml(c.eventB.title)}</span>
              </div>
              <div class="conflict-pair-times">
                <span>${escapeHtml(formatConflictTime(c.eventA))}</span> vs <span>${escapeHtml(formatConflictTime(c.eventB))}</span>
              </div>
            </div>
            <div class="conflict-badges-group">
              ${accountTag}
              <span class="conflict-duration-pill">${escapeHtml(c.formattedOverlapDuration)} Overlap</span>
            </div>
          `;
          healthDetailsWrapper.appendChild(card);
        });
      } else if (suggestions.length === 0) {
        healthDetailsWrapper.innerHTML = `
          <div class="health-empty-state">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#10b981" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
              <polyline points="22 4 12 14.01 9 11.01"></polyline>
            </svg>
            <div>
              <div class="health-empty-title">All commitments are clash-free</div>
              <div class="health-empty-subtitle">No overlapping calendar events found in this period.</div>
            </div>
          </div>
        `;
      }
    }
  }

  function renderConflictTimeline(conflicts) {
    if (!conflictTimeline) return;
    conflictTimeline.innerHTML = '';

    // Group conflicts by calendar date of overlap
    const conflictsByDay = new Map();
    conflicts.forEach((c) => {
      const oDate = new Date(c.overlapStart);
      const dateKey = oDate.toDateString();
      if (!conflictsByDay.has(dateKey)) {
        conflictsByDay.set(dateKey, {
          date: oDate,
          conflicts: [],
          eventsMap: new Map()
        });
      }
      const dayData = conflictsByDay.get(dateKey);
      dayData.conflicts.push(c);
      if (c.eventA && c.eventA.id) dayData.eventsMap.set(c.eventA.id, c.eventA);
      if (c.eventB && c.eventB.id) dayData.eventsMap.set(c.eventB.id, c.eventB);
    });

    conflictsByDay.forEach((dayData) => {
      const dayCard = document.createElement('div');
      dayCard.className = 'timeline-day-card';

      const dateOptions = { weekday: 'long', month: 'short', day: 'numeric', year: 'numeric' };
      const formattedDayTitle = dayData.date.toLocaleDateString(undefined, dateOptions);

      // Determine day time boundaries across conflicting events
      let minHour = 8;
      let maxHour = 20;

      dayData.eventsMap.forEach((ev) => {
        const s = new Date(ev.start);
        const e = new Date(ev.end);
        if (s.getHours() < minHour) minHour = Math.max(0, s.getHours() - 1);
        if (e.getHours() >= maxHour) maxHour = Math.min(24, e.getHours() + 1);
      });

      const dayStart = new Date(dayData.date.getFullYear(), dayData.date.getMonth(), dayData.date.getDate(), minHour, 0, 0);
      const totalMs = (maxHour - minHour) * 3600 * 1000;

      // Axis labels (every 2 hours)
      let axisLabelsHtml = '';
      for (let h = minHour; h <= maxHour; h += 2) {
        const markDate = new Date(dayData.date.getFullYear(), dayData.date.getMonth(), dayData.date.getDate(), h, 0, 0);
        const label = markDate.toLocaleTimeString([], { hour: 'numeric', hour12: true });
        axisLabelsHtml += `<span class="timeline-axis-label">${escapeHtml(label)}</span>`;
      }

      // Event track bars
      let eventBarsHtml = '';
      const dayEvents = Array.from(dayData.eventsMap.values()).sort((a, b) => new Date(a.start) - new Date(b.start));

      dayEvents.forEach((ev) => {
        const evStart = new Date(ev.start);
        const evEnd = new Date(ev.end);

        const leftPct = Math.max(0, Math.min(100, ((evStart.getTime() - dayStart.getTime()) / totalMs) * 100));
        const rawWidthPct = ((evEnd.getTime() - evStart.getTime()) / totalMs) * 100;
        const widthPct = Math.max(4, Math.min(100 - leftPct, rawWidthPct));

        const timeStr = `${evStart.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit', hour12: true })} – ${evEnd.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit', hour12: true })}`;

        eventBarsHtml += `
          <div class="timeline-event-bar-wrapper">
            <div class="timeline-event-bar conflicted" style="left: ${leftPct.toFixed(1)}%; width: ${widthPct.toFixed(1)}%;" title="${escapeHtml(ev.title)} (${timeStr})">
              <span class="timeline-event-bar-title">${escapeHtml(ev.title)}</span>
              <span class="timeline-event-bar-time">${timeStr}</span>
            </div>
          </div>
        `;
      });

      // Overlap banners for each conflict on this day
      let overlapBannersHtml = '';
      dayData.conflicts.forEach((c) => {
        const oStart = new Date(c.overlapStart).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit', hour12: true });
        const oEnd = new Date(c.overlapEnd).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit', hour12: true });
        overlapBannersHtml += `
          <div class="timeline-overlap-banner">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"></polygon></svg>
            <span><strong>${escapeHtml(c.eventA.title)}</strong> &harr; <strong>${escapeHtml(c.eventB.title)}</strong>: ${escapeHtml(c.formattedOverlapDuration)} overlap (${oStart} – ${oEnd})</span>
          </div>
        `;
      });

      dayCard.innerHTML = `
        <div class="timeline-day-header">
          <span class="timeline-day-title">${escapeHtml(formattedDayTitle)}</span>
          <span class="timeline-day-conflicts-pill">${dayData.conflicts.length} conflict${dayData.conflicts.length === 1 ? '' : 's'}</span>
        </div>
        <div class="timeline-axis">
          ${axisLabelsHtml}
        </div>
        <div class="timeline-tracks-area">
          ${eventBarsHtml}
        </div>
        ${overlapBannersHtml}
      `;

      conflictTimeline.appendChild(dayCard);
    });
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

      const accountHtml = (task.accountEmail && connectedAccounts.length > 1)
        ? `<span class="task-account">${escapeHtml(task.accountEmail)}</span>`
        : '';

      row.innerHTML = `
        <div class="task-info">
          <div class="task-checkbox${task.isCompleted ? ' completed' : ''}" title="${task.isCompleted ? 'Completed' : 'Pending'}">
            ${checkmark}
          </div>
          <span class="task-title${task.isCompleted ? ' completed' : ''}">${escapeHtml(task.title)}</span>
          ${accountHtml}
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
      if (ev.accountEmail && connectedAccounts.length > 1) {
        tagsHtml += `<span class="tag tag-account" title="Account: ${escapeHtml(ev.accountEmail)}">${escapeHtml(ev.accountEmail)}</span>`;
      }
      if (conflictingEventIds.has(ev.id)) {
        tagsHtml += '<span class="tag tag-conflict" title="This event conflicts with another commitment">Conflict</span>';
      }
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
    if (connectedAccounts.length > 0 && !confirm('Are you sure you want to disconnect all Google accounts?')) {
      return;
    }
    upcomingTrainJourneysCache = null;
    try {
      await fetch('/api/auth/logout', { method: 'POST' });
    } catch (err) {
      console.error('Logout error:', err);
    }
    connectedAccounts = [];
    renderAccountsList();
    showLanding();
    hideAlert();
  });

  // Init
  checkAuthStatus();
})();
