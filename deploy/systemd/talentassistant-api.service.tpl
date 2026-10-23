[Unit]
Description=Talent Assistant API (own systemd service; SPEC-015 template)
After=network.target

[Service]
Type=exec
WorkingDirectory=/opt/talentassistant/current/api
EnvironmentFile=/opt/talentassistant/.env.production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5101
ExecStart=/usr/bin/dotnet /opt/talentassistant/current/api/TalentAssistant.Api.dll
Restart=on-failure
RestartSec=5
KillSignal=SIGINT
KillMode=mixed
TimeoutStopSec=20

User=talentassistant
Group=talentassistant
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=
RestrictSUIDSGID=true
CapabilityBoundingSet=
AmbientCapabilities=

[Install]
WantedBy=multi-user.target
