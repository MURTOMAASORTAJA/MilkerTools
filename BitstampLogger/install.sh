#!/bin/bash

SERVICE_NAME="BitstampLogger"
SERVICE_DESCRIPTION="Logs market data to InfluxDB."
WORKING_DIRECTORY="$(dirname "$(readlink -f "$0")")"
EXEC_START="$WORKING_DIRECTORY/myapp"
USER="$(whoami)"
GROUP="$(id -gn)"
SERVICE_FILE_PATH="/etc/systemd/system/$SERVICE_NAME.service"

# Write the service file
echo "[Unit]
Description=$SERVICE_DESCRIPTION
After=network.target

[Service]
ExecStart=$EXEC_START
WorkingDirectory=$WORKING_DIRECTORY
Restart=always
User=$USER
Group=$GROUP
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target" | sudo tee $SERVICE_FILE_PATH > /dev/null

# Reload systemd manager configuration
sudo systemctl daemon-reload

# Enable the service to start on boot
sudo systemctl enable $SERVICE_NAME.service

# Check the service status
sudo systemctl status $SERVICE_NAME.service
