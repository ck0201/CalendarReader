# CalendarReader

> **Google Calendar tells you what is scheduled. CalendarReader tells you what your schedule means.**

CalendarReader is a focused, lightweight web utility designed to read Google Calendar data and present clear, actionable insights that are inconvenient or difficult to calculate in Google Calendar itself.

---

## Current V1 Functionality

V1 delivers the core **Calendar Insights** feature:
* **Google OAuth 2.0 Connection:** Secure authentication using Google's read-only calendar scope (`https://www.googleapis.com/auth/calendar.readonly`).
* **Event Retrieval & Expansion:** Retrieves events from the user's primary calendar, expanding recurring events into individual occurrences.
* **Period Analysis:** Quick switching between **Today** and **This Week**.
* **Key Calendar Metrics:**
  * **Scheduled Time:** Total duration occupied by timed calendar commitments.
  * **Meeting Time:** Collaborative meeting duration (sessions with multiple attendees / video conferences).
  * **Longest Focus Block:** Maximum uninterrupted free block within daytime working hours (9:00 AM – 6:00 PM).
  * **Busiest Day:** Identifies which calendar day carries the highest scheduled workload.
  * **Recurring Commitment Ratio:** Quantifies how much scheduled time is consumed by recurring sessions.
* **Deterministic Observations:** 3 to 5 clear, rule-based human-readable takeaways (no AI/LLM overhead).
* **Train Journey Filter:** Exactly three dedicated event views (`All Events | Train Journeys | Other Events`). The **Train Journeys** view is dedicated to upcoming railway journeys from today through the next 2 months sorted chronologically ascending (excluding past journeys). Identifies railway travel using high-confidence indicators (*IRCTC*, *PNR*, *Railway*, *Train Ticket*, *Train Journey*, train numbers, coach/seat information) while strictly preventing generic false positives (e.g., flight departures, corporate workshops, office arrivals).
* **Calendar Tasks:** Identifies actionable task items scheduled on the calendar using patterns (`[Task]`, `Task:`, `[TODO]`, `TODO:`, `[Action]`, or `#task`/`#todo` tags in descriptions) without modifying OAuth scopes or requiring external tasks APIs. Displays a dedicated tasks card with due dates, completion state, and an empty state when no tasks are scheduled.
* **Schedule Breakdown:** Visual inspection of scheduled events with badges for recurring, all-day, meeting, and train journey tags.

---

## AI Calendar Briefing

CalendarReader includes an executive **AI Calendar Briefing** that synthesizes schedule workload, focus windows, and potential conflicts into an actionable overview.

### Feature Overview
* **Actionable Briefing Structure**: Generates a high-level headline, a 2–3 sentence narrative summary of load and focus patterns, 1–3 preparation priorities, 1–3 actionable scheduling suggestions, and an energy profile classification (e.g., *Balanced*, *Meeting Intensive*, *High Contention*, *Open Focus*).
* **Dynamic Calendar & Date Selection**: The briefing reacts to user selections in the dashboard. Adjusting the date range or toggling specific connected calendars recalculates the schedule snapshot and triggers an updated briefing.
* **Deterministic Fallback**: If an AI API key is not configured, or if the external provider times out or encounters errors, the application automatically produces a structured briefing using deterministic heuristics. Users can distinguish fallback briefings in the UI by a dedicated informational banner stating that the briefing was generated using rule-based heuristics (`isFallback: true`).

### Privacy-Preserving Architecture
CalendarReader enforces strict separation between internal calendar analysis and external AI generation:
* **Internal Processing**: The application accesses authorized calendar events and tasks solely within the authenticated session to calculate aggregate metrics, daytime focus intervals, and schedule conflicts.
* **Minimized Outbound Snapshot**: Before making an external AI call, the backend projects calendar data into a privacy-minimized snapshot and enforces an explicit payload whitelist (`CreateSanitizedSnapshotPayload`).
* **Strictly Excluded from AI Requests**:
  * Event and task titles
  * Event descriptions and task notes
  * Attendee names and email addresses
  * Organizer information
  * Google Calendar IDs and resource URLs
  * User session identifiers, tokens, and account emails
* **Data Transmitted**: Only anonymized temporal metadata (start/end hours, durations, meeting vs. focus category flags) and aggregate numeric metrics (total scheduled hours, conflict counts, busiest day) are included in the AI request.

### Operational Behavior
* **Endpoint Contract**: `GET /api/calendar/briefing` requires an authenticated session and explicit `start` and `end` ISO 8601 query parameters (`end` must be greater than `start`).
* **Maximum Date Range**: The requested range cannot exceed **31 days** (returns HTTP 400 Bad Request if exceeded).
* **Server-Side Caching**: Briefings are cached in memory for **5 minutes** (TTL) using a composite key that partitions by hashed session, normalized date range, sorted calendar set, and schedule snapshot fingerprint.
* **Rate Limiting**: Briefing requests are bounded by a fixed-window rate limiter allowing up to **5 requests per minute** per session partition. Requests exceeding the limit return HTTP 429 (`Too Many Requests`).
* **Single-Instance In-Memory State**: Cache and rate-limiting stores use process memory (`IMemoryCache` and ASP.NET Core rate limiting), intended for single-instance hosting.

---

## Technology Stack

* **Runtime:** [.NET 9](https://dotnet.microsoft.com/) (C#)
* **Backend:** ASP.NET Core Minimal APIs
* **API Integration:** Official `Google.Apis.Calendar.v3` SDK
* **Frontend:** Plain semantic HTML5, modern vanilla CSS, and vanilla JavaScript (served directly from `wwwroot` with zero client-side frameworks or bundlers)
* **Session & State:** Ephemeral, in-memory token store keyed by an HTTP-only session cookie (zero database required)
* **Testing:** xUnit unit test suite for calendar analysis heuristics

---

## Project Structure

```text
CalendarReader/
├── .github/
│   └── workflows/
│       └── build.yml
├── .editorconfig
├── .gitignore
├── CalendarReader.sln
├── README.md
├── src/
│   └── CalendarReader/
│       ├── CalendarReader.csproj
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       ├── Properties/
│       │   └── launchSettings.json
│       ├── Models/
│       │   ├── CalendarEvent.cs
│       │   ├── CalendarInsightResult.cs
│       │   ├── CalendarTask.cs
│       │   ├── ConnectedAccount.cs
│       │   └── GoogleAuthConfig.cs
│       ├── Services/
│       │   ├── IAccountTokenStore.cs
│       │   ├── SqliteAccountTokenStore.cs
│       │   ├── IGoogleCalendarService.cs
│       │   ├── GoogleCalendarService.cs
│       │   ├── ICalendarAnalysisService.cs
│       │   └── CalendarAnalysisService.cs
│       └── wwwroot/
│           ├── index.html
│           ├── css/
│           │   └── style.css
│           └── js/
│               └── app.js
└── tests/
    └── CalendarReader.Tests/
        ├── CalendarReader.Tests.csproj
        ├── CalendarAnalysisServiceTests.cs
        ├── GoogleAuthConfigTests.cs
        ├── MultiAccountCalendarAggregationTests.cs
        └── SqliteAccountTokenStoreTests.cs
```

---

## Google Cloud Console Setup

To enable CalendarReader to interact with Google Calendar:

1. Visit the [Google Cloud Console](https://console.cloud.google.com/).
2. Create a new project (e.g., `CalendarReader-Dev`).
3. Navigate to **APIs & Services > Library**, search for and enable:
   * **Google Calendar API**
   * **Google Tasks API**
4. Configure the **OAuth consent screen**:
   * User Type: **External** (or Internal if inside Google Workspace).
   * App name: `CalendarReader`.
   * User support email & developer contact email.
   * Add Scopes:
     * `https://www.googleapis.com/auth/calendar.readonly`
     * `https://www.googleapis.com/auth/tasks.readonly`
   * In **Test Users**, add the Google email address you plan to test with.
5. Go to **APIs & Services > Credentials**:
   * Click **Create Credentials > OAuth Client ID**.
   * Application type: **Web application**.
   * Name: `CalendarReader Local`.
   * **Authorized redirect URIs**:
     ```text
     http://localhost:5000/api/auth/callback
     ```
   * Click **Create** and copy your **Client ID** and **Client Secret**.

---

## Environment Variables & Configuration

CalendarReader checks environment variables first, falling back to User Secrets and `appsettings.json`. Credentials are never hard-coded.

### Google OAuth Credentials (Required for Calendar Access)

| Variable | Description | Example |
| :--- | :--- | :--- |
| `GOOGLE_CLIENT_ID` | OAuth 2.0 Client ID from Google Cloud Console | `123456789-abc.apps.googleusercontent.com` |
| `GOOGLE_CLIENT_SECRET` | OAuth 2.0 Client Secret | `GOCSPX-xxxxxxxxxxxxx` |
| `GOOGLE_REDIRECT_URI` | Authorized Redirect URI | `http://localhost:5000/api/auth/callback` |

### AI Calendar Briefing Configuration (Optional)

An AI API key is **optional**. If unconfigured, the application runs normally and automatically serves deterministic fallback briefings.

| Variable | Section Key (`Ai:*` in User Secrets, `Ai__*` in Env) | Default | Description |
| :--- | :--- | :--- | :--- |
| `GEMINI_API_KEY` or `AI_API_KEY` | `ApiKey` | *(empty)* | Google Gemini API key. If unset, deterministic fallback is used. |
| `GEMINI_MODEL` | `Model` | `gemini-3.8-flash` | Gemini model ID used for briefing generation. |
| `AI_TIMEOUT_SECONDS` | `TimeoutSeconds` | `15` | Request timeout in seconds before falling back to deterministic generation. |
| `AI_CACHE_TTL_MINUTES` | `CacheTtlMinutes` | `5` | Server-side cache duration in minutes. |

> **Note**: Automated test suites verify the deterministic fallback and provider mock integration. Live external Gemini provider availability has not been verified against live Google endpoints.

Alternatively, you can populate `src/CalendarReader/appsettings.json`:

```json
{
  "Google": {
    "ClientId": "YOUR_CLIENT_ID",
    "ClientSecret": "YOUR_CLIENT_SECRET",
    "RedirectUri": "http://localhost:5000/api/auth/callback"
  },
  "Ai": {
    "ApiKey": "",
    "Model": "gemini-3.8-flash",
    "TimeoutSeconds": 15,
    "CacheTtlMinutes": 5
  }
}
```

---

## Running Locally

### 1. Configure Credentials

#### Option A: .NET User Secrets (Recommended for local development)

Credentials persist across laptop restarts and are never committed to Git:

```powershell
# Required: Google OAuth
dotnet user-secrets set "GOOGLE_CLIENT_ID" "your-client-id" --project src/CalendarReader
dotnet user-secrets set "GOOGLE_CLIENT_SECRET" "your-client-secret" --project src/CalendarReader
dotnet user-secrets set "GOOGLE_REDIRECT_URI" "http://localhost:5000/api/auth/callback" --project src/CalendarReader

# Optional: AI Calendar Briefing (Gemini)
dotnet user-secrets set "Ai:ApiKey" "your-gemini-api-key" --project src/CalendarReader
```

#### Option B: Environment Variables

In PowerShell:
```powershell
# Required: Google OAuth
$env:GOOGLE_CLIENT_ID="your-client-id"
$env:GOOGLE_CLIENT_SECRET="your-client-secret"
$env:GOOGLE_REDIRECT_URI="http://localhost:5000/api/auth/callback"

# Optional: AI Calendar Briefing (Gemini)
$env:GEMINI_API_KEY="your-gemini-api-key"
```

In Command Prompt:
```cmd
rem Required: Google OAuth
set GOOGLE_CLIENT_ID=your-client-id
set GOOGLE_CLIENT_SECRET=your-client-secret
set GOOGLE_REDIRECT_URI=http://localhost:5000/api/auth/callback

rem Optional: AI Calendar Briefing (Gemini)
set GEMINI_API_KEY=your-gemini-api-key
```

### 2. Run the Application

```powershell
dotnet run --project src/CalendarReader
```

Open your browser and navigate to:
```text
http://localhost:5000
```

### 3. Run the Unit Tests

```powershell
dotnet test
```

---

## Current V1 Limitations

* **In-Memory Session Persistence:** User OAuth credentials are kept in memory for the active process session. Restarting the server requires reconnecting Google Calendar.
* **Single-Calendar Scope:** V1 analyzes the user's `primary` calendar. Secondary calendars (e.g. shared team calendars, subscribed holidays) are not included in V1 calculations.
* **Read-Only:** The app does not create, update, or delete calendar events.
* **Deterministic Rules:** Meeting detection uses attendee counts and conference metadata; focus blocks calculate uninterrupted daytime intervals (9:00 AM – 6:00 PM). Machine learning or natural-language scheduling heuristics are deliberately omitted from V1.
