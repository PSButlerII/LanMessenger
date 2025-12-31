# Vendor Manifest (Offline Assets)

This project is designed to run fully offline. All third-party frontend assets are stored under `wwwroot/vendor/`
and referenced by local paths (no CDNs).

## Assets

- microsoft-signalr
  - Version: 8.0.7
  - File: `wwwroot/vendor/microsoft-signalr/8.0.7/signalr.min.js`
  - Source (npm dist): `@microsoft/signalr@8.0.7/dist/browser/signalr.min.js`
  - Purpose: SignalR client used by the LAN Messenger UI for realtime updates.
