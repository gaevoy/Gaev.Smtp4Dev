#!/bin/bash
ssh root@app.gaevoy.com 'bash -s' <<'ENDSSH'
printf "Stopping service...\n"
systemctl stop GaevSmtp4Dev
printf "Service is "
systemctl is-active GaevSmtp4Dev
mkdir -p /apps/GaevSmtp4Dev
ENDSSH

printf "Uploading new version of service...\n"
rsync -v -a ./bin/Release/netcoreapp3.0/ubuntu.18.04-x64/publish/ root@app.gaevoy.com:/apps/GaevSmtp4Dev/

ssh root@app.gaevoy.com 'bash -s' <<'ENDSSH'
chmod 777 /apps/GaevSmtp4Dev/Gaev.Smtp4Dev
if [[ ! -e /etc/systemd/system/GaevSmtp4Dev.service ]]; then
    printf "Installing service...\n"
    cat > /etc/systemd/system/GaevSmtp4Dev.service <<'EOF'
    [Unit]
    Description=GaevSmtp4Dev
    After=network.target
    
    [Service]
    WorkingDirectory=/apps/GaevSmtp4Dev
    ExecStart=/apps/GaevSmtp4Dev/Gaev.Smtp4Dev
    Restart=always
    KillSignal=SIGINT
    
    [Install]
    WantedBy=multi-user.target
EOF
    systemctl daemon-reload
    systemctl enable GaevSmtp4Dev
fi
printf "Starting service...\n"
systemctl start GaevSmtp4Dev
printf "Service is "
systemctl is-active GaevSmtp4Dev
ENDSSH