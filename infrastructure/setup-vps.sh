#!/bin/bash
# =============================================================
# VPS Hardening Script - Cap Theorem Cell
# Run this ONCE on a fresh Ubuntu/Debian VPS as root.
# =============================================================
set -euo pipefail

echo "=== Updating system packages ==="
apt update && apt upgrade -y

echo "=== Installing required packages ==="
apt install -y ufw nginx fail2ban docker.io docker-compose-plugin certbot python3-certbot-nginx

echo "=== Configuring UFW (only 22, 80, 443) ==="
ufw --force reset
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp comment 'SSH'
ufw allow 80/tcp comment 'HTTP'
ufw allow 443/tcp comment 'HTTPS'
ufw --force enable

echo "=== Hardening SSH ==="
sed -i 's/^#PasswordAuthentication yes/PasswordAuthentication no/' /etc/ssh/sshd_config
sed -i 's/^PasswordAuthentication yes/PasswordAuthentication no/' /etc/ssh/sshd_config
sed -i 's/^#PermitRootLogin prohibit-password/PermitRootLogin prohibit-password/' /etc/ssh/sshd_config
sed -i 's/^PermitRootLogin yes/PermitRootLogin prohibit-password/' /etc/ssh/sshd_config
systemctl restart sshd

echo "=== Configuring Fail2Ban ==="
cp jail.local /etc/fail2ban/jail.d/custom.conf 2>/dev/null || cat > /etc/fail2ban/jail.d/custom.conf << 'F2BCONF'
[DEFAULT]
bantime = 3600
findtime = 600
maxretry = 5
ignoreip = 127.0.0.1/8 ::1

[sshd]
enabled = true
port = 22
maxretry = 5
bantime = 86400

[nginx-http-auth]
enabled = true
port = http,https

[nginx-botsearch]
enabled = true
port = http,https
F2BCONF

# Create custom nginx filter for rate limiting
cat > /etc/fail2ban/filter.d/nginx-limit-req.conf << 'FILTER'
[Definition]
failregex = ^\s*\[error\] .* \#\d+: \*\d+ limiting requests, excess: .* by zone "req_per_ip", client: <HOST>
ignoreregex =
FILTER

systemctl restart fail2ban

echo "=== Creating Docker network ==="
docker network create proxy 2>/dev/null || true

echo "=== Setting up swap (2GB) ==="
if ! swapon --show | grep -q /swapfile; then
    fallocate -l 2G /swapfile
    chmod 600 /swapfile
    mkswap /swapfile
    swapon /swapfile
    echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

echo "=== Final status ==="
echo "--- UFW ---"
ufw status verbose
echo "--- Fail2Ban ---"
fail2ban-client status
echo "--- Docker ---"
docker info --format '{{.ServerVersion}}'
echo "--- Swap ---"
swapon --show

echo ""
echo "=========================================="
echo "  VPS hardening complete!"
echo "  Next steps:"
echo "  1. Set up SSL certs: certbot --nginx -d alpha.celula-cap-theorem.andrescortes.dev -d api.celula-cap-theorem.andrescortes.dev"
echo "  2. Copy nginx.conf to /etc/nginx/sites-available/ and enable them"
echo "  3. Set up environment variables in .env file"
echo "  4. Run: docker compose up -d"
echo "=========================================="
