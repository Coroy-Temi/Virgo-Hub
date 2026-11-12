# VirgoHub Production Cloud Deployment

This guide prepares VirgoHub for public testing without requiring the phone and backend to share the same local network.

## Current reality

VirgoHub is not yet production-ready as a public cloud app because the web backend currently stores application state in a local JSON file:

- [VirgoHubService.cs](C:/Users/gamin/OneDrive/Documents/Programming/C#/VirgoHub/VirgoHub.WebAdmin/Services/VirgoHubService.cs:41)

That works on your laptop, but it is not appropriate for Cloud Run because application instances are ephemeral. A container restart can discard local state.

Because of that, public deployment should be done in two stages:

1. Containerize and deploy the web backend/admin publicly.
2. Replace the local JSON state store with managed cloud storage, starting with Cloud Firestore and Firebase Storage.

## Recommended target architecture

- `Cloud Run`
  Hosts the current `VirgoHub.WebAdmin` ASP.NET application over HTTPS.
- `Cloud Firestore`
  Stores users, complaints, events, elections, sports bookings, course materials, enrollments, notifications, sessions, and OTP records.
- `Firebase Storage`
  Stores uploaded files such as course materials, PDFs, flyers, and media.
- `Firebase Cloud Messaging`
  Delivers Android and iOS push notifications later.

## Why this is the right fit

- The current backend is already a .NET web app, so `Cloud Run` is the natural hosting target.
- Firebase documents say dynamic content can be served through `Cloud Run`.
- Firestore supports server client libraries for `C#`.
- FCM is the cleanest notification path for Android and iOS later.

## Prerequisites

Before deployment, set up one Google/Firebase project and attach billing.

You will need:

- a Firebase project
- a Google Cloud billing account
- `gcloud` installed and authenticated
- Firebase CLI if you want Hosting later

## Phase 1: Public hosting for the current backend

The repo now includes a production container:

- [Dockerfile](C:/Users/gamin/OneDrive/Documents/Programming/C#/VirgoHub/Dockerfile)
- [.dockerignore](C:/Users/gamin/OneDrive/Documents/Programming/C#/VirgoHub/.dockerignore)

### Deploy command

From the repo root:

```powershell
gcloud run deploy virgohub-portal `
  --source . `
  --region us-central1 `
  --allow-unauthenticated
```

Notes:

- `us-central1` is only an example. Pick the region closest to your users.
- This command deploys the current portal publicly over HTTPS.
- The current JSON data store is still local to the container, so this phase is useful only as a temporary smoke test.

## Phase 2: Move state to Cloud Firestore

The next code change should replace the in-memory plus JSON persistence in `VirgoHubService` with a repository abstraction.

Recommended split:

- `IAppStateStore`
- `JsonAppStateStore` for local development
- `FirestoreAppStateStore` for production

Suggested collections:

- `users`
- `otpChallenges`
- `sessions`
- `events`
- `facilities`
- `bookings`
- `complaints`
- `elections`
- `votes`
- `materials`
- `enrollments`
- `notifications`
- `activities`

### Firestore setup

1. Create a Firebase project.
2. Create a Firestore database in production mode.
3. Select a permanent region carefully.
4. Grant the Cloud Run service account access to Firestore.

For local development, use Application Default Credentials with `gcloud auth application-default login`.

## Phase 3: Move uploaded files to Firebase Storage

Course materials, test files, and flyers should not live only as links or local files.

Store them in Firebase Storage and save metadata in Firestore:

- file name
- content type
- storage object path
- public or signed URL
- uploader id
- tenant or school id later

## Phase 4: Point the mobile app to the public backend

Once the backend is public, change the mobile app default server URL in:

- [VirgoHubApiClient.cs](C:/Users/gamin/OneDrive/Documents/Programming/C#/VirgoHub/VirgoHub.Mobile/Services/VirgoHubApiClient.cs:18)

Instead of:

- `http://localhost:5128`
- `http://10.0.2.2:5128`

use something like:

- `https://api.virgohub.app`

At that point:

- users can install the APK manually
- they do not need to be on your Wi-Fi or hotspot
- the `Campus server address` field can be hidden or removed

## Phase 5: Push notifications

After the backend is public, add Firebase Cloud Messaging for:

- events
- election updates
- complaint status
- sports booking approvals
- course approval or lecturer notices

## Suggested implementation order in this repo

1. Keep the current UI and APIs stable.
2. Add repository interfaces around the state layer.
3. Keep JSON storage for local development.
4. Add Firestore implementation for production.
5. Add Firebase Storage for uploaded files.
6. Deploy the web backend to Cloud Run.
7. Change the mobile app to use the production base URL.
8. Rebuild the release APK and distribute it directly.

## Immediate outcome after these changes

Once Firestore-backed production deployment is complete:

- the admin portal will open from anywhere
- the APK can be shared directly without Play Store
- users anywhere can sign in over the internet
- the app no longer depends on your laptop IP address

