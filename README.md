# VirgoHub

VirgoHub is a .NET MAUI app with an ASP.NET Core and Blazor portal. School workspaces support events, sports bookings, voting, hostel reports, and course materials. Personal workspaces support teaching groups, shared materials, assignments, and graded assessments.

## Run locally

Install the .NET 9 SDK. For mobile development, install the matching .NET MAUI workload and platform tools.

```powershell
.\Run-WebAdmin.ps1
```

Open `http://localhost:5128` for the web portal. Run the mobile project from Visual Studio. Use `http://10.0.2.2:5128` for the Android emulator or enter the server address reachable from your device.

## Configuration

Set SMTP credentials through environment variables such as `Mail__FromAddress`, `Mail__Username`, and `Mail__Password`. The checked-in mail credentials are blank.

Local state is generated under `VirgoHub.WebAdmin/App_Data`. The app includes demo accounts and uses local file storage, so configure authentication and persistent storage before exposing it publicly.

See [deployment notes](docs/production-cloud-deployment.md) for the hosting setup.
