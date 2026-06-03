# Optional: machine-restart survival via launchd

The nohup daemons launched by `start.sh` survive **Claude-session suspend/resume** and shell exit
(they reparent to launchd). They do NOT survive a full **machine reboot / logout**. If you want the
wave to resume automatically after a reboot, register a `launchd` LaunchAgent.

## Hard requirement: keep everything OFF the TCC-blocked Desktop

A LaunchAgent started by `launchd` cannot read files under `~/Desktop` (and other TCC-protected
locations) without a granted exception — this is why the scripts + control dir live under `$HOME`
(`~/.rimv-delivery/<wave>/`) and `~/agent-skills` rather than the iCloud Desktop. The repo `.git`
may stay on the Desktop, but the **scripts, wave.conf, control dir, and worktrees must be off it**
for launchd to work. (The nohup path tolerates the Desktop; the launchd path does not.)

## What to register

You only need to keep the **driver-loop** alive across reboot — it re-launches `driver.sh` (which
re-acquires its lock and is idempotent), and `driver-loop.sh` itself re-launches the sentinel is
NOT automatic, so register the **sentinel** too if you want self-heal after reboot. Two agents:

- `com.rimv.<wave>.driver`   → KeepAlive(SuccessfulExit=false), RunAtLoad
- `com.rimv.<wave>.sentinel`  → StartInterval, RunAtLoad

Both must export `RIMV_WAVE_CONF` so the scripts find the wave config, plus a sane `PATH` (Codex,
gh, git, node, dotnet live in `/opt/homebrew/bin` etc.) and `HOME`.

## Sample plist (driver). Save off-Desktop, e.g. `~/.rimv-delivery/<wave>/launchd/`

Replace `<WAVE>`, `<USER>`, `<SKILL_DIR>`, `<CONTROL_DIR>`, `<WAVE_CONF>` before loading.

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key><string>com.rimv.<WAVE>.driver</string>
  <key>ProgramArguments</key>
  <array>
    <string>/bin/bash</string>
    <string><SKILL_DIR>/scripts/driver-loop.sh</string>
  </array>
  <key>WorkingDirectory</key><string><CONTROL_DIR></string>
  <key>RunAtLoad</key><true/>
  <key>KeepAlive</key>
  <dict><key>SuccessfulExit</key><false/></dict>
  <key>ThrottleInterval</key><integer>30</integer>
  <key>ProcessType</key><string>Background</string>
  <key>StandardOutPath</key><string><CONTROL_DIR>/launchd-driver.out</string>
  <key>StandardErrorPath</key><string><CONTROL_DIR>/launchd-driver.err</string>
  <key>EnvironmentVariables</key>
  <dict>
    <key>HOME</key><string>/Users/<USER></string>
    <key>LANG</key><string>en_US.UTF-8</string>
    <key>PATH</key><string>/opt/homebrew/bin:/opt/homebrew/sbin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin</string>
    <key>RIMV_WAVE_CONF</key><string><WAVE_CONF></string>
  </dict>
</dict>
</plist>
```

The **sentinel** plist is identical except: `Label` = `com.rimv.<WAVE>.sentinel`, the script is
`scripts/sentinel.sh`, drop the `KeepAlive` dict, and add `<key>StartInterval</key><integer>600</integer>`.

## Load / unload

```bash
# load (per-user LaunchAgent)
launchctl bootstrap gui/$(id -u) ~/.rimv-delivery/<wave>/launchd/com.rimv.<wave>.driver.plist
launchctl bootstrap gui/$(id -u) ~/.rimv-delivery/<wave>/launchd/com.rimv.<wave>.sentinel.plist

# stop the wave (wave-local STOP — the loops exit cleanly; KeepAlive won't relaunch on a clean exit)
touch <CONTROL_DIR>/STOP

# fully unload the agents
launchctl bootout gui/$(id -u)/com.rimv.<wave>.driver
launchctl bootout gui/$(id -u)/com.rimv.<wave>.sentinel
```

Note: with `KeepAlive(SuccessfulExit=false)`, a **clean** driver exit (WAVE_DONE / STOP / idle =
exit 0) is NOT relaunched; only a crash (non-zero) is. That matches the nohup behavior. For most
runs the nohup daemons from `start.sh` are enough — only add launchd if you specifically need to
survive a reboot mid-wave.
